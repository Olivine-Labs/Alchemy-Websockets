using System;
using System.Net.Sockets;
using System.Threading;
using Alchemy.Handlers;
using Alchemy.Handlers.WebSocket;

namespace Alchemy.Classes
{
/// <summary>
/// This class contains the required data for each connection to the server.
/// </summary>
    public class Context// : IDisposable
    {
        //private static readonly ILog logger = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// The exported version of this context.
        /// </summary>
        public readonly UserContext UserContext;

        /// <summary>
        /// The buffer used for accepting raw data from the socket.
        /// </summary>
        public byte[] Buffer;

        /// <summary>
        /// Whether or not the TCPClient is still connected.
        /// </summary>
        public bool Connected = true;

        /// <summary>
        /// The raw client connection.
        /// </summary>
        public TcpClient Connection;

        /// <summary>
        /// The current connection handler.
        /// </summary>
        public Handler Handler = Handler.Instance;

        /// <summary>
        /// The Header
        /// </summary>
        public Header Header;

        /// <summary>
        /// Whether or not this client has passed all the setup routines for the current handler(authentication, etc)
        /// </summary>
        public Boolean IsSetup;

        /// <summary>
        /// The max frame that we will accept from the client
        /// </summary>
        public long MaxFrameSize = 102400; //100kb

        /// <summary>
        /// Semaphore that limits receive operations to 1 at a time.
        /// </summary>
        public SemaphoreSlim ReceiveReady = new SemaphoreSlim(1);

        /// <summary>
        /// How many bytes we received this tick.
        /// </summary>
        public int ReceivedByteCount;

        /// <summary>
        /// Semaphore that limits sends operations to 1 at a time.
        /// </summary>
        public SemaphoreSlim SendReady = new SemaphoreSlim(1);

        /// <summary>
        /// Throws OperationCanceledException on threads waiting at a semaphore.
        /// Is used when a client is disconnected.
        /// </summary>
        public CancellationTokenSource Cancellation;

        /// <summary>
        /// A link to the server listener instance this client is currently hosted on.
        /// </summary>
        public WebSocketServer Server;

        private int _bufferSize = 512;


        public SocketAsyncEventArgs ReceiveEventArgs { get; set; }
        public SocketAsyncEventArgs SendEventArgs { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Context"/> class.
        /// </summary>
        public Context(WebSocketServer server, TcpClient connection)
        {
            Server = server;
            Connection = connection;
            Buffer = new byte[_bufferSize];
            UserContext = new UserContext(this);
            Cancellation = new CancellationTokenSource();
            Cancellation = CancellationTokenSource.CreateLinkedTokenSource
                          (Handler.Shutdown.Token, this.Cancellation.Token);
            ReceiveEventArgs = new SocketAsyncEventArgs();
            SendEventArgs = new SocketAsyncEventArgs();

            Handler.RegisterContext(this);

            if (connection != null)
            {
                UserContext.ClientAddress = connection.Client.RemoteEndPoint;
            }
        }

        /// <summary>
        /// Gets or sets the size of the buffer.
        /// </summary>
        /// <value>
        /// The size of the buffer.
        /// </value>
        public int BufferSize
        {
            get { return _bufferSize; }
            set
            {
                _bufferSize = value;
                Buffer = new byte[_bufferSize];
            }
        }

        // Starts the ReceiveEventArgs for next incoming data on server- or client side. Does not block.
        // Returns false, when finished synchronous and data is already available
        // Examples: http://msdn.microsoft.com/en-us/library/system.net.sockets.socketasynceventargs%28v=vs.110%29.aspx
        //           http://www.codeproject.com/Articles/22918/How-To-Use-the-SocketAsyncEventArgs-Class
        //           http://netrsc.blogspot.ch/2010/05/async-socket-server-sample-in-c.html
        // Only one ReceiveEventArgs exist for one context. Therefore, no concurrency while receiving.
        // Receiving is on a threadpool thread. Sending is on another thread.
        // At least under Mono 2.10.8 there is a threading issue (multi core ?) that can be prevented,
        // when we thread-lock access to the ReceiveAsync method.
        internal bool ReceiveEventArgs_StartAsync()
        {
            bool started;
            try
            {
                ReceiveReady.Wait(Cancellation.Token);
                started = Connection.Client.ReceiveAsync(ReceiveEventArgs);
            }
            finally
            {
                ReceiveReady.Release();
            }

            return started;
        }

        #region IDisposable Members

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Close()
        {
            Connected = false;
            UserContext.OnDisconnect();
            
            // close client connection
            if (Connection != null)
            {
                try
                {
                    Connection.Close();
                }
                catch (Exception)
                {
                    // skip
                }
            }
            SendReady.Release();
            ReceiveReady.Release();            
        }

        #endregion

        /// <summary>
        /// Disconnects the client
        /// </summary>
        public void Disconnect()
        {
            //logger.Debug("Disconnected in " + Environment.StackTrace);
            Connected = false;
            UserContext.OnDisconnect(); // 2014-01-16 added for Remact.Net
        }

        /// <summary>
        /// Resets this instance.
        /// Clears the dataframe if necessary. Resets Received byte count.
        /// </summary>
        public void Reset()
        {
            if (UserContext.DataFrame != null)
            {
                if (UserContext.DataFrame.State == DataFrame.DataState.Complete)
                {
                    UserContext.DataFrame.Reset();
                }
            }
            ReceivedByteCount = 0;
        }
    }
}
