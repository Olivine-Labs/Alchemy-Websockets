using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Diagnostics;
using Alchemy.Classes;
using Alchemy.Handlers.WebSocket.rfc6455;

namespace Alchemy
{
    public class WebSocketClient
    {
        public TimeSpan ConnectTimeout = new TimeSpan(0, 0, 0, 10);
        public bool IsAuthenticated;
        public ReadyStates ReadyState = ReadyStates.CLOSED;
        public string Origin;
        public string[] SubProtocols;
        public string CurrentProtocol { get; private set; }

        public OnEventDelegate OnConnect = x => { };
        public OnEventDelegate OnConnected = x => { };
        public OnEventDelegate OnDisconnect = x => { };
        public OnEventDelegate OnReceive = x => { };
        public OnEventDelegate OnSend = x => { };

        private TcpClient _client;
        private bool _connecting;
        private Context _context;
        private ClientHandshake _handshake;

        private readonly string _path;
        private readonly int _port;
        private readonly string _host;

        private static Thread[] ClientThreads = new Thread[Environment.ProcessorCount];
        private static Queue<Context> NewClients { get; set; }
        private static Dictionary<Context, WebSocketClient> ContextMapping { get; set; }

        public enum ReadyStates
        {
            CONNECTING,
            OPEN,
            CLOSING,
            CLOSED
        }

        public Boolean Connected
        {
            get
            {
                return _client != null && _client.Connected;
            }
        }

        static WebSocketClient()
        {
            NewClients = new Queue<Context>();
            ContextMapping = new Dictionary<Context, WebSocketClient>();

            for(int i = 0; i < ClientThreads.Length; i++){
                ClientThreads[i] = new Thread(HandleClientThread);
                ClientThreads[i].Name = "WebSocketClient Setup Thread #" + (i + 1);
                ClientThreads[i].Start();
            }
        }

        public static void Log (string text)
        {
            Trace.Write(string.Format ("--{0:HH:mm:ss.fff}, {1}\r\n", DateTime.Now, text));
        }

        private static void HandleClientThread()
        {
            while (!Handler.Shutdown.IsCancellationRequested)
            {
                try
                {
                    Context context = null;

                    while (NewClients.Count == 0)
                    {
                        Thread.Sleep(10);
                        if (Handler.Shutdown.IsCancellationRequested) return;
                    }

                    lock (NewClients)
                    {
                        if (NewClients.Count == 0)
                        {
                            continue;
                        }

                        context = NewClients.Dequeue();
                    }

                    lock (ContextMapping)
                    {
                        WebSocketClient client = ContextMapping[context];
                        client.SetupContext(context);
                    }
                }
                catch (OperationCanceledException) {}
            }
        }

        public WebSocketClient(string path)
        {
            var r = new Regex("^(wss?)://(.*)\\:([0-9]*)/(.*)$");
            var matches = r.Match(path);

            _host = matches.Groups[2].Value;
            _port = Int32.Parse(matches.Groups[3].Value);
            _path = matches.Groups[4].Value;
        }

        public void Connect()
        {
            BeginConnect(null);

            var waiting = new TimeSpan();
            while (_connecting && waiting < ConnectTimeout)
            {
                var timeSpan = new TimeSpan(0, 0, 0, 0, 100);
                waiting = waiting.Add(timeSpan);
                Thread.Sleep(timeSpan.Milliseconds);
            }
        }

        ///<summary>
        /// Starts the asynchronous connection process.
        /// User callbacks are: OnConnected() when successful - or OnDisconnect() in case of failure.
        /// UserContext.Data will contain the data object passed here.
        /// Timeout is defined by the TCP stack.
        ///</summary>
        public void BeginConnect(object data)
        {
            if (_client != null) return;

            _context = new Context(null, null);
            _context.BufferSize = 512;
            _context.UserContext.DataFrame = new DataFrame();
            _context.UserContext.SetOnConnect(OnConnect);
            _context.UserContext.SetOnConnected(OnConnected);
            _context.UserContext.SetOnDisconnect(OnDisconnect);
            _context.UserContext.SetOnSend(OnSend);
            _context.UserContext.SetOnReceive(OnReceive);
            _context.UserContext.Data = data;

            try
            {
                ReadyState = ReadyStates.CONNECTING;
                _context.UserContext.OnConnect();

                _client = new TcpClient();
                _context.Connection = _client;
                _connecting = true;
                _client.BeginConnect(_host, _port, OnClientConnected, null);
            }
            catch (Exception ex)
            {
                Disconnect();
                _context.UserContext.LatestException = ex;
                _context.UserContext.OnDisconnect();
            }
        }

        /// <summary>
        /// Fires when a client connects.
        /// </summary>
        protected void OnClientConnected(IAsyncResult result)
        {
            if (_client == null) return;
            try
            {
                _client.EndConnect(result);
            }
            catch (Exception ex)
            {
                Disconnect();
                _context.UserContext.LatestException = ex;
                _context.UserContext.OnDisconnect();
                return;
            }

            lock (ContextMapping)
            {
                ContextMapping[_context] = this;
            }

            lock (NewClients)
            {
                NewClients.Enqueue(_context);
            }
        }

        private void SetupContext(Context context)
        {
            _context.ReceiveEventArgs.UserToken = _context;
            _context.ReceiveEventArgs.Completed += ReceiveEventArgs_Completed;
            _context.ReceiveEventArgs.SetBuffer(_context.Buffer, 0, _context.Buffer.Length);

            if (_context.Connection != null && _context.Connection.Connected)
            {
                if (!ReceiveEventArgs_StartAsync())
                {
                    ReceiveEventArgs_Completed(_context.Connection.Client, _context.ReceiveEventArgs);
                }

                if (!IsAuthenticated)
                {
                    Authenticate();
                }
            }
        }

        // Starts the ReceiveEventArgs for next incoming data. Does not block.
        // Returns false, when finished synchronous and data is already available
        // Examples: http://msdn.microsoft.com/en-us/library/system.net.sockets.socketasynceventargs%28v=vs.110%29.aspx
        //           http://www.codeproject.com/Articles/22918/How-To-Use-the-SocketAsyncEventArgs-Class
        //           http://netrsc.blogspot.ch/2010/05/async-socket-server-sample-in-c.html
        // Only one ReceiveEventArgs exist for one context. Therefore, no concurrency while receiving.
        // Receiving is on a threadpool thread. Sending is on another thread.
        // At least under Mono 2.10.8 there is a threading issue (multi core ?) that can be prevented, 
        // when we thread-lock access to the ReceiveAsync method.
        private bool ReceiveEventArgs_StartAsync()
        {
            bool started;
            try
            {
                _context.ReceiveReady.Wait(_context.Cancellation.Token);
                started = _context.Connection.Client.ReceiveAsync(_context.ReceiveEventArgs);
            }
            finally
            {
                _context.ReceiveReady.Release();
            }

            return started;
        }


        void ReceiveEventArgs_Completed(object sender, SocketAsyncEventArgs e)
        {
            try
            {
                var context = (Context)e.UserToken;
                context.Reset(); // only one ReceiveEventArgs exist for this context. Therefore, no concurrency.

                if (e.SocketError != SocketError.Success)
                {
                    context.ReceivedByteCount = 0;
                }
                else
                {
                    context.ReceivedByteCount = e.BytesTransferred;
                }

                if (context.ReceivedByteCount > 0)
                {
                    ReceiveData(context); // process data
                    ReceiveEventArgs_StartAsync();
                }
                else
                {
                    context.Disconnect();
                }
            }
            catch (OperationCanceledException) {}
        }

        private void Authenticate()
        {
            _handshake = new ClientHandshake { Version = "8", Origin = Origin, Host = _host, Key = GenerateKey(), ResourcePath = _path, SubProtocols = SubProtocols};

            _client.Client.Send(Encoding.UTF8.GetBytes(_handshake.ToString()));
        }

        private bool CheckAuthenticationResponse(Context context)
        {
            var receivedData = context.UserContext.DataFrame.ToString();
            var header = new Header(receivedData);
            var handshake = new ServerHandshake(header);

            if (Authentication.GenerateAccept(_handshake.Key) != handshake.Accept) return false;

            if (SubProtocols != null)
            {
                if (header.SubProtocols == null)
                {
                    return false;
                }

                foreach (var s in SubProtocols)
                {
                    if (header.SubProtocols.Contains(s) && String.IsNullOrEmpty(CurrentProtocol))
                    {
                        CurrentProtocol = s;
                    }

                }
                if(String.IsNullOrEmpty(CurrentProtocol))
                {
                    return false;
                }
            }

            ReadyState = ReadyStates.OPEN;
            IsAuthenticated = true;
            _connecting = false;
            context.UserContext.OnConnected();
            return true;
        }

        private void ReceiveData(Context context)
        {
            if (!IsAuthenticated)
            {
                var someBytes = new byte[context.ReceivedByteCount];
                Array.Copy(context.Buffer, 0, someBytes, 0, context.ReceivedByteCount);
                context.UserContext.DataFrame.Append(someBytes);
                var authenticated = CheckAuthenticationResponse(context);
                context.UserContext.DataFrame.Reset();

                if (!authenticated)
                {
                    Disconnect();
                    _context.UserContext.LatestException = new Exception("could not authenticate web socket server");
                    _context.UserContext.OnDisconnect();
                }
            }
            else
            {
                context.UserContext.DataFrame.Append(context.Buffer, true);
                if (context.UserContext.DataFrame.State == Handlers.WebSocket.DataFrame.DataState.Complete)
                {
                    context.UserContext.OnReceive();
                    context.UserContext.DataFrame.Reset();
                }
            }
        }

        private static String GenerateKey()
        {
            var bytes = new byte[16];
            var random = new Random();

            for (var index = 0; index < bytes.Length; index++)
            {
                bytes[index] = (byte) random.Next(0, 255);
            }

            return Convert.ToBase64String(bytes);
        }

        public void Disconnect()
        {
            _connecting = false;

            if (_client == null) return;

            if (_context != null)
            {
                if (ReadyState == ReadyStates.OPEN)
                {
                    ReadyState = ReadyStates.CLOSING;
                    // see http://stackoverflow.com/questions/17176827/websocket-close-packet
                    var bytes = new byte[6];
                    bytes[0] = 0x88; // Fin + Close
                    bytes[1] = 0x80; // Mask = 1, Len = 0
                    bytes[2] = 0;
                    bytes[3] = 0;
                    bytes[4] = 0; // Mask = 0
                    bytes[5] = 0; // Mask = 0
                    _context.UserContext.Send(bytes, raw: true);
                    Thread.Sleep(30); // let the send thread do its work
                }

                _context.Connected = false;
                ReadyState = ReadyStates.CLOSING;
                _context.Cancellation.Cancel();
            }

            _client.Close();
            _client = null;
            ReadyState = ReadyStates.CLOSED;
        }

        public void Send(String data)
        {
            _context.UserContext.Send(data);
        }

        public void Send(byte[] data)
        {
            _context.UserContext.Send(data);
        }

        /// <summary>
        /// Stops all static allocated receive- and send threads.
        /// Therefore, Shutdown may only be called, when the application shuts down.
        /// </summary>
        public static void Shutdown()
        {
            Handler.Shutdown.Cancel();
        }
    }
}
