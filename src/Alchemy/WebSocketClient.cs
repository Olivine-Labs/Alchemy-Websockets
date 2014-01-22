using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Alchemy.Classes;
using Alchemy.Handlers.WebSocket.rfc6455;

namespace Alchemy
{
    public class WebSocketClient : IDisposable
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
        private static CancellationTokenSource cancellation = new CancellationTokenSource();
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
                ClientThreads[i].Start();
            }
        }

        private static void HandleClientThread()
        {
            while (!cancellation.IsCancellationRequested)
            {
                Context context = null;

                while (NewClients.Count == 0)
                {
                    Thread.Sleep(10);
                    if (cancellation.IsCancellationRequested) return;
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
            if (_client != null) return;
            
            try
            {
                ReadyState = ReadyStates.CONNECTING;

                _client = new TcpClient();
                _connecting = true;
                _client.BeginConnect(_host, _port, OnRunClient, null);

                var waiting = new TimeSpan();
                while (_connecting && waiting < ConnectTimeout)
                {
                    var timeSpan = new TimeSpan(0, 0, 0, 0, 100);
                    waiting = waiting.Add(timeSpan);
                    Thread.Sleep(timeSpan.Milliseconds);
                }
            }
            catch (Exception)
            {
                Disconnect();
            }
        }

        /// <summary>
        /// Fires when a client connects.
        /// </summary>
        /// <param name="result">null</param>
        #pragma warning disable 168 // warning CS0168: The variable 'ex' is declared but never used
        protected void OnRunClient(IAsyncResult result)
        {
            bool connectError = false;
            try
            {
                _client.EndConnect(result);
            }
            catch (Exception ex)
            {
                Disconnect();
                connectError = true;
            }

            _context = new Context(null, _client);
            _context.BufferSize = 512;
            _context.UserContext.DataFrame = new DataFrame();
            _context.UserContext.SetOnConnect(OnConnect);
            _context.UserContext.SetOnConnected(OnConnected);
            _context.UserContext.SetOnDisconnect(OnDisconnect);
            _context.UserContext.SetOnSend(OnSend);
            _context.UserContext.SetOnReceive(OnReceive);
            _context.UserContext.OnConnect();

            if (connectError)
            {
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
        #pragma warning restore 168

        private void SetupContext(Context context)
        {
            _context.ReceiveEventArgs.UserToken = _context;
            _context.ReceiveEventArgs.Completed += ReceiveEventArgs_Completed;
            _context.ReceiveEventArgs.SetBuffer(_context.Buffer, 0, _context.Buffer.Length);

            if (_context.Connection != null && _context.Connection.Connected)
            {
                _context.ReceiveReady.Wait();

                if (!_context.Connection.Client.ReceiveAsync(_context.ReceiveEventArgs))
                {
                    ReceiveEventArgs_Completed(_context.Connection.Client, _context.ReceiveEventArgs);
                }
 

                if (!IsAuthenticated)
                {

                    Authenticate();
                }
            }
        }

        void ReceiveEventArgs_Completed(object sender, SocketAsyncEventArgs e)
        {
            var context = (Context)e.UserToken;
            context.Reset();

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
                ReceiveData(context);
                context.ReceiveReady.Release();
            }
            else
            {
                context.Disconnect();
            }

            _context.ReceiveReady.Wait();

            if (!_context.Connection.Client.ReceiveAsync(_context.ReceiveEventArgs))
            {
                ReceiveEventArgs_Completed(_context.Connection.Client, _context.ReceiveEventArgs);
            }
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
                _context.Connected = false;
                while (_context.SendReady.CurrentCount < 1)
                {
                    _context.SendReady.Release(); // release all blocked send threads for this context only
                    Thread.Sleep(1);
                }

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
                    _context.UserContext.Send(bytes, raw: true, close: true);
                    Thread.Sleep(30); // let the send thread do its work
                }
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
        
        public void Dispose()
        {
            cancellation.Cancel();
            Handler.Instance.Dispose();
        }
        
    }
}
