using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Alchemy
{
    public abstract class TcpServer : IDisposable
    {
        protected int BufferSize = 512;

        private IPAddress _listenAddress = IPAddress.Any;

        private TcpListener _listener;

        private int _port = 80;

        protected TcpServer(int listenPort, IPAddress listenAddress)
        {
            if (listenPort > 0)
            {
                _port = listenPort;
            }
            if (listenAddress != null)
            {
                _listenAddress = listenAddress;
            }
        }

        /// <summary>
        /// Gets or sets the port.
        /// </summary>
        public int Port
        {
            get { return _port; }
            set { _port = value; }
        }

        /// <summary>
        /// Gets or sets the listener address.
        /// </summary>
        public IPAddress ListenAddress
        {
            get { return _listenAddress; }
            set { _listenAddress = value; }
        }

        /// <summary>
        /// Starts this instance.
        /// </summary>
        public virtual void Start()
        {
            if (_listener == null)
            {
                _listener = new TcpListener(_listenAddress, _port);
                _listener.Start(10);
                _listener.BeginAcceptTcpClient(RunClient, null);
            }
        }

        /// <summary>
        /// Stops this instance.
        /// </summary>
        public virtual void Stop()
        {
            if (_listener != null)
            {
                _listener.Stop();
            }
            _listener = null;
        }

        /// <summary>
        /// Restarts this instance.
        /// </summary>
        public virtual void Restart()
        {
            Stop();
            Start();
        }

        /// <summary>
        /// Runs the client.
        /// Sets up the UserContext.
        /// Executes in it's own thread.
        /// Utilizes a semaphore(ReceiveReady) to limit the number of receive events active for this client to 1 at a time.
        /// </summary>
        /// <param name="result">The A result.</param>
        private void RunClient(IAsyncResult result)
        {
            TcpClient connection = null;
            if (_listener != null)
            {
                try
                {
                    connection = _listener.EndAcceptTcpClient(result);
                }
                catch (Exception)
                {
                    connection = null;
                }
            }

            if (connection != null)
            {
                ThreadPool.QueueUserWorkItem(OnRunClient, connection);
                _listener.BeginAcceptTcpClient(RunClient, null);
            }
        }

        protected abstract void OnRunClient(object connection);

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            Stop();
        }
    }
}
