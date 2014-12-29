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
    public class Context : IDisposable
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
        /// True indicates an underlying TCP connection that is ready to communicate. 
        /// False indicates a connection that is closed or is about to be closed.
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

        private int _bufferSize = 1500; // default: one ethernet frame

        /// <summary>
        /// Gets or sets the size of the buffer. The header must fit into one buffer. 
        /// A webs socket message (frame) may consist of many buffer contents.
        /// </summary>
        /// <value>The size of the buffer.</value>
        public int BufferSize
        {
            get { return _bufferSize; }
            set
            {
                _bufferSize = value;
                Buffer = new byte[_bufferSize];
            }
        }

        /// <summary>
        /// The maximum web socket message size (frame).
        /// </summary>
        public long MaxFrameSize = 1024*1024; // default 1 MB


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

        private bool _disposed;

        /// <summary>
        /// Called from context cleanup thread (on the server) or when Disconnecting (on the client).
        /// </summary>
        public void Close()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            UserContext.OnDisconnect(); // sets Connected=false
            
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
                Connection = null;
            }
            SendReady.Release();
            ReceiveReady.Release();            
            //Cancellation.Dispose();
            //ReceiveReady.Dispose();
            //SendReady.Dispose(); delay disposing until GC does it. Send thread will use the semaphore.
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                Close();
            }
        }

        /// <summary>
        /// Closes the connection.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
        }

        /// <summary>
        /// Disconnects the client.
        /// </summary>
        public void Disconnect()
        {
            //logger.Debug("Disconnected in " + Environment.StackTrace);
            if (Server != null)
            {
                // On the server, this stops communication and the context cleanup thread will close the connection.
                Connected = false;
            }
            else
            {
                // on the client we close the connection directly and notify the application.
                Close();
            }
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
        #endregion
    }
}
