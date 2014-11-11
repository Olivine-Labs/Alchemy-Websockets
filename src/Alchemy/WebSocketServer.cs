using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Alchemy.Classes;
using Alchemy.Handlers;

namespace Alchemy
{
    public delegate void OnEventDelegate(UserContext context);

    /// <summary>
    /// The Main WebSocket Server
    /// </summary>
    public class WebSocketServer : TcpServer
    {

        private static Thread[] ClientThreads = new Thread[Environment.ProcessorCount];
        private static Thread CleanupThread;

        private static ConcurrentQueue<Context> ContextQueue { get; set; }
        private static Dictionary<Context, WebSocketServer> ContextMapping { get; set; }

        private static List<Context> CurrentConnections { get; set; }

        // static constructor
        static WebSocketServer()
        {
            ContextQueue = new ConcurrentQueue<Context>();
            ContextMapping = new Dictionary<Context, WebSocketServer>();
            CurrentConnections = new List<Context>();

            CleanupThread = new Thread(HandleContextCleanupThread);
            CleanupThread.Name = "WebSocketServer Cleanup Thread";
            CleanupThread.Start();

            for(int i = 0; i < ClientThreads.Length; i++){
                ClientThreads[i] = new Thread(HandleClientThread);
                ClientThreads[i].Name = "WebSocketServer Client Thread #" + (i + 1);
                ClientThreads[i].Start();
            }
        }

        private static void HandleClientThread()
        {
            while (!Handler.Shutdown.Token.IsCancellationRequested)
            {
                Context context;

                while (ContextQueue.Count == 0)
                {
                    Thread.Sleep(10);
                    if (Handler.Shutdown.IsCancellationRequested) return;
                }

                if (!ContextQueue.TryDequeue(out context))
                {
                    continue;
                }

                lock (ContextMapping)
                {
                    WebSocketServer server = ContextMapping[context];
                    server.SetupContext(context);
                }

                lock(CurrentConnections){
                    CurrentConnections.Add(context);
                }
            }
        }

        private static void HandleContextCleanupThread()
        {
            while (!Handler.Shutdown.IsCancellationRequested)
            {
                Thread.Sleep(100);

                List<Context> currentConnections = new List<Context>();

                lock (CurrentConnections)
                {
                    currentConnections.AddRange(CurrentConnections);
                }

                foreach (var connection in currentConnections)
                {
                    if (Handler.Shutdown.IsCancellationRequested) break;

                    if (!connection.Connected)
                    {
                        lock (CurrentConnections)
                        {
                            CurrentConnections.Remove(connection);
                        }

                        lock (ContextMapping)
                        {
                            ContextMapping.Remove(connection);
                        }

                        connection.Handler.UnregisterContext(connection);

                        connection.Close();
                    }
                }
            }
        }

        /// <summary>
        /// Gets the client count of all servers.
        /// </summary>
        public static int ClientCount
        {
            get { return CurrentConnections.Count; }
        }

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
        /// When true, an additional TCPServer on port 843 is started. 
        /// Make sure only one server is active on this port.
        /// Set to false, when you would like your app to only listen on a single port rather than 2.
        /// Warning, any flash socket connections will have an added delay on connection due to the client looking to port 843 first for the connection restrictions.
        /// </summary>
        public bool FlashAccessPolicyEnabled = true;

        /// <summary>
        /// Configuration for the above heartbeat setup.
        /// TimeOut : How long until a connection drops when it doesn't receive anything.
        /// MaxPingsInSequence : A multiple of TimeOut for how long a connection can remain idle(only pings received) before we kill it.
        /// </summary>
        public TimeSpan TimeOut = TimeSpan.FromMinutes(1);

        /// <summary>
        /// A list of acceptable subprotocols that this server supports.
        /// See http://tools.ietf.org/html/rfc6455#section-1.9
        /// </summary>
        public string[] SubProtocols;

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
        /// Starts this WebSocketServer on the given listenPort.
        /// When enabled, starts also the FlashAccessPolicy-server on port 843.
        /// </summary>
        public override void Start()
        {
            base.Start();
            if (AccessPolicyServer == null)
            {
                // every server needs an instance for its clients to send AccessPolicy requests to a remote server
                AccessPolicyServer = new AccessPolicyServer(ListenAddress, Origin, Port);
                
                // at most one instance may be enabled
                if (FlashAccessPolicyEnabled)
                {
                    try
                    {
                    	AccessPolicyServer.Start();
                    }
                    catch (Exception ex)
                    {
                    	throw new Exception("cannot start the AccessPolicyServer", ex);
                    }
                }
            }
        }

        /// <summary>
        /// Stops this WebSocketServer.
        /// When enabled, stops also the FlashAccessPolicy-server.
        /// </summary>
        public override void Stop()
        {
            base.Stop();
            if (AccessPolicyServer != null && FlashAccessPolicyEnabled)
            {
                try
                {
                	AccessPolicyServer.Stop();
               		AccessPolicyServer = null;
                }
                catch (Exception ex)
                {
                	AccessPolicyServer = null;
                	throw new Exception("cannot stop the AccessPolicyServer", ex);
                }
            }
        }

        /// <summary>
        /// Fires when a client connects.
        /// </summary>
        /// <param name="data">The TCP Connection.</param>
        protected override void OnRunClient(object data)
        {
            var connection = (TcpClient)data;
            var context = new Context(this, connection);

            context.UserContext.ClientAddress = context.Connection.Client.RemoteEndPoint;
            context.UserContext.SetOnConnect(OnConnect);
            context.UserContext.SetOnConnected(OnConnected);
            context.UserContext.SetOnDisconnect(OnDisconnect);
            context.UserContext.SetOnSend(OnSend);
            context.UserContext.SetOnReceive(OnReceive);
            context.BufferSize = BufferSize;
            context.UserContext.OnConnect();

            if (context.Connected)
            {
                lock (ContextMapping)
                {
                    ContextMapping[context] = this;
                }

                ContextQueue.Enqueue(context);
            }
        }

        private void SetupContext(Context _context)
        {
            _context.ReceiveEventArgs.UserToken = _context;
            _context.ReceiveEventArgs.Completed += ReceiveEventArgs_Completed;
            _context.ReceiveEventArgs.SetBuffer(_context.Buffer, 0, _context.Buffer.Length);

            StartReceive(_context);
        }

        private void StartReceive(Context _context)
        {
            try
            {
                if (!_context.ReceiveEventArgs_StartAsync())
                {
                    ReceiveEventArgs_Completed(_context.Connection.Client, _context.ReceiveEventArgs);
                }
            }
            catch (OperationCanceledException) { }
            catch (SocketException ex)
            {
                //logger.Error("SocketException in StartReceive", ex);
                _context.UserContext.LatestException = ex;
                _context.Disconnect();
            }
        }

        void ReceiveEventArgs_Completed(object sender, SocketAsyncEventArgs e)
        {
            var context = (Context)e.UserToken;
            context.Reset();
            if (e.SocketError != SocketError.Success)
            {
                //logger.Error("Socket Error: " + e.SocketError.ToString());
                context.ReceivedByteCount = 0;
            } else {
                context.ReceivedByteCount = e.BytesTransferred;
            }

            if (context.ReceivedByteCount > 0)
            {
                context.Handler.HandleRequest(context);
                StartReceive(context);
            } else {
                context.Disconnect();
            }
        }

        /// <summary>
        /// Shutdown stops all static allocated receive- and send threads.
        /// Therefore, Shutdown may only be called, when the application shuts down.
        /// Use 'Stop' to just end one WebSocketServer instance.
        /// </summary>
        public static void Shutdown()
        {
            Handler.Shutdown.Cancel();
        }
    }
}
