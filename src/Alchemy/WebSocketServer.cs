using System;
using System.Net;
using System.Net.Sockets;
using Alchemy.Classes;
using Alchemy.Handlers;

namespace Alchemy
{
    public delegate void OnEventDelegate(UserContext context);

    /// <summary>
    /// The Main WebSocket Server
    /// </summary>
    public class WebSocketServer : TcpServer, IDisposable
    {
        /// <summary>
        /// This is the Flash Access Policy Server. It allows us to facilitate flash socket connections much more quickly in most cases.
        /// Don't mess with it through here. It's only public so we can access it later from all the IOCPs.
        /// </summary>
        internal AccessPolicyServer AccessPolicyServer;

        /// <summary>
        /// These are the default OnEvent delegates for the server. By default, all new UserContexts will use these events.
        /// It is up to you whether you want to replace them at runtime or even manually set the events differently per connection in OnReceive.
        /// </summary>
        public OnEventDelegate OnConnect = x => { };

        public OnEventDelegate OnConnected = x => { };
        public OnEventDelegate OnDisconnect = x => { };
        public OnEventDelegate OnReceive = x => { };
        public OnEventDelegate OnSend = x => { };

        /// <summary>
        /// Enables or disables the Flash Access Policy Server(APServer).
        /// This is used when you would like your app to only listen on a single port rather than 2.
        /// Warning, any flash socket connections will have an added delay on connection due to the client looking to port 843 first for the connection restrictions.
        /// </summary>
        public bool FlashAccessPolicyEnabled = true;

        /// <summary>
        /// Configuration for the above heartbeat setup.
        /// TimeOut : How long until a connection drops when it doesn't receive anything.
        /// MaxPingsInSequence : A multiple of TimeOut for how long a connection can remain idle(only pings received) before we kill it.
        /// </summary>
        public TimeSpan TimeOut = TimeSpan.FromMinutes(1);

        private string _destination = String.Empty;
        private string _origin = String.Empty;

        /// <summary>
        /// Initializes a new instance of the <see cref="WebSocketServer"/> class.
        /// </summary>
        public WebSocketServer(int listenPort = 0, IPAddress listenAddress = null) : base(listenPort, listenAddress) {}

        /// <summary>
        /// Gets or sets the origin host.
        /// </summary>
        /// <value>
        /// The origin host.
        /// </value>
        public string Origin
        {
            get { return _origin; }
            set
            {
                _origin = value;
                Authentication.Origin = value;
            }
        }

        /// <summary>
        /// Gets or sets the destination host.
        /// </summary>
        /// <value>
        /// The destination host.
        /// </value>
        public string Destination
        {
            get { return _destination; }
            set
            {
                _destination = value;
                Authentication.Destination = value;
            }
        }

        /// <summary>
        /// Starts this instance.
        /// </summary>
        public override void Start()
        {
            base.Start();
            if (AccessPolicyServer == null)
            {
                AccessPolicyServer = new AccessPolicyServer(ListenAddress, Origin, Port);

                if (FlashAccessPolicyEnabled)
                {
                    AccessPolicyServer.Start();
                }
            }
        }

        /// <summary>
        /// Stops this instance.
        /// </summary>
        public override void Stop()
        {
            if ((AccessPolicyServer != null) && (FlashAccessPolicyEnabled))
            {
                AccessPolicyServer.Stop();
                AccessPolicyServer = null;
            }
        }

        /// <summary>
        /// Fires when a client connects.
        /// </summary>
        /// <param name="data">The TCP Connection.</param>
        protected override void OnRunClient(object data)
        {
            var connection = (TcpClient)data;
            using (var context = new Context(this, connection))
            {
                context.UserContext.ClientAddress = context.Connection.Client.RemoteEndPoint;
                context.UserContext.SetOnConnect(OnConnect);
                context.UserContext.SetOnConnected(OnConnected);
                context.UserContext.SetOnDisconnect(OnDisconnect);
                context.UserContext.SetOnSend(OnSend);
                context.UserContext.SetOnReceive(OnReceive);
                context.BufferSize = BufferSize;
                context.UserContext.OnConnect();
                while (context.Connected)
                {
                    if (context.ReceiveReady.Wait(TimeOut))
                    {
                        try
                        {
                            context.Connection.Client.BeginReceive(context.Buffer, 0, context.Buffer.Length,
                                                                   SocketFlags.None, DoReceive, context);
                        }
                        catch (SocketException)
                        {
                            break;
                        }
                    }
                    else
                    {
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// The root receive event for each client. Executes in it's own thread.
        /// </summary>
        /// <param name="result">The Async result.</param>
        private void DoReceive(IAsyncResult result)
        {
            var context = (Context) result.AsyncState;
            context.Reset();
            try
            {
                context.ReceivedByteCount = context.Connection.Client.EndReceive(result);
            }
            catch
            {
                context.ReceivedByteCount = 0;
            }

            if (context.ReceivedByteCount > 0)
            {
                context.Handler.HandleRequest(context);
                context.ReceiveReady.Release();
            }
            else
            {
                context.Disconnect();
                context.ReceiveReady.Release();
            }
        }
    }
}
