using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Alchemy.Classes;
using Alchemy.Handlers.WebSocket.hybi10;

namespace Alchemy
{
    public class WebSocketClient
    {
        public TimeSpan ConnectTimeout = new TimeSpan(0, 0, 0, 10);
        public string Host = "localhost";

        public bool IsAuthenticated;

        public OnEventDelegate OnConnect = x => { };
        public OnEventDelegate OnConnected = x => { };
        public OnEventDelegate OnDisconnect = x => { };
        public OnEventDelegate OnReceive = x => { };
        public OnEventDelegate OnSend = x => { };
        public String Origin = "localhost";
        public String Path = "/";
        public int Port = 81;
        private TcpClient _client;

        private bool _connecting;
        private Context _context;
        private ClientHandshake _handshake;

        public Boolean Connected
        {
            get
            {
                if (_client != null)
                {
                    return _client.Connected;
                }
                return false;
            }
        }

        public void Connect()
        {
            if (_client == null)
            {
                try
                {
                    _client = new TcpClient();
                    _connecting = true;
                    _client.BeginConnect(Host, Port, OnRunClient, null);
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
        }

        /// <summary>
        /// Fires when a client connects.
        /// </summary>
        /// <param name="result">null</param>
        protected void OnRunClient(IAsyncResult result)
        {
            try
            {
                _client.EndConnect(result);
            }
            catch (Exception)
            {
                Disconnect();
            }

            using (_context = new Context())
            {
                _context.Connection = _client;
                _context.BufferSize = 512;
                _context.UserContext.ClientAddress = _context.Connection.Client.RemoteEndPoint;
                _context.UserContext.DataFrame = new DataFrame();
                _context.UserContext.SetOnConnect(OnConnect);
                _context.UserContext.SetOnConnected(OnConnected);
                _context.UserContext.SetOnDisconnect(OnDisconnect);
                _context.UserContext.SetOnSend(OnSend);
                _context.UserContext.SetOnReceive(OnReceive);
                _context.UserContext.OnConnect();

                try
                {
                    while (_context.Connection.Connected)
                    {
                        _context.ReceiveReady.Wait();
                        _context.Connection.Client.BeginReceive(_context.Buffer, 0, _context.Buffer.Length,
                                                                SocketFlags.None, DoReceive, _context);
                        if (!IsAuthenticated)
                        {
                            Authenticate();
                        }
                    }
                }
                catch (Exception)
                {
                    Disconnect();
                }
            }
        }

        private void Authenticate()
        {
            _handshake = new ClientHandshake
            {Version = "8", Origin = Origin, Host = Host, Key = GenerateKey(), ResourcePath = Path};
            _client.Client.Send(Encoding.UTF8.GetBytes(_handshake.ToString()));
        }

        private void CheckAuthenticationResponse(Context context)
        {
            String receivedData = context.UserContext.DataFrame.ToString();
            var header = new Header(receivedData);
            var handshake = new ServerHandshake(header);
            if (Authentication.GenerateAccept(_handshake.Key) == handshake.Accept)
            {
                IsAuthenticated = true;
                _connecting = false;
                context.UserContext.OnConnected();
            }
        }

        private void ReceiveData(Context context)
        {
            if (!IsAuthenticated)
            {
                var someBytes = new byte[context.ReceivedByteCount];
                Array.Copy(context.Buffer, 0, someBytes, 0, context.ReceivedByteCount);
                context.UserContext.DataFrame.Append(someBytes);
                CheckAuthenticationResponse(context);
                context.UserContext.DataFrame.Reset();
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

        private void DoReceive(IAsyncResult result)
        {
            var context = (Context) result.AsyncState;
            context.Reset();
            try
            {
                context.ReceivedByteCount = context.Connection.Client.EndReceive(result);
            }
            catch (Exception)
            {
                Disconnect();
            }

            if (context.ReceivedByteCount > 0)
            {
                ReceiveData(context);
                context.ReceiveReady.Release();
            }
            else
            {
                context.Dispose();
                context.ReceiveReady.Release();
            }
        }

        private String GenerateKey()
        {
            var bytes = new byte[16];
            var random = new Random();
            for (int index = 0; index < bytes.Length; index++)
            {
                bytes[index] = (byte) random.Next(0, 255);
            }
            return Convert.ToBase64String(bytes);
        }

        public void Disconnect()
        {
            _connecting = false;
            if (_client != null)
            {
                var dataFrame = new DataFrame();
                dataFrame.Append(new byte[0]);

                byte[] bytes = dataFrame.AsFrame()[0].Array;
                bytes[0] = 0x88;
                _context.UserContext.Send(bytes);
                _client.Close();
                _client = null;
            }
        }

        public void Send(String data)
        {
            _context.UserContext.Send(data);
        }

        public void Send(byte[] data)
        {
            _context.UserContext.Send(data);
        }
    }
}