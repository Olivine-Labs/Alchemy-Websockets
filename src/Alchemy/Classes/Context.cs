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
        public UInt64 MaxFrameSize = 102400; //100kb

        /// <summary>
        /// Semaphores that limit sends and receives to 1 and a time.
        /// </summary>
        public SemaphoreSlim ReceiveReady = new SemaphoreSlim(1);

        /// <summary>
        /// How many bytes we received this tick.
        /// </summary>
        public int ReceivedByteCount;

        public SemaphoreSlim SendReady = new SemaphoreSlim(1);

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

        #region IDisposable Members

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
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
