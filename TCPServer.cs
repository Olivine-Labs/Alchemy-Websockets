using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.Net;
using System.Threading;

namespace Alchemy.Server
{
    public abstract class TCPServer
    {
        private TcpListener _listener = null;

        private int _port = 80;
        /// <summary>
        /// Gets or sets the port.
        /// </summary>
        /// <value>
        /// The port.
        /// </value>
        public int Port
        {
            get
            {
                return _port;
            }
            set
            {
                _port = value;
            }
        }

        private IPAddress _listenAddress = IPAddress.Any;
        protected int _defaultBufferSize = 512;

        /// <summary>
        /// The number of connected clients.
        /// </summary>
        /// 
        private int _clients = 0;

        /// <summary>
        /// Gets the client count.
        /// </summary>
        public int ClientCount
        { get { return _clients; } }

        /// <summary>
        /// This Semaphore protects our clients variable on increment/decrement when a user connects/disconnects.
        /// </summary>
        private SemaphoreSlim _clientLock = new SemaphoreSlim(1);

        /// <summary>
        /// Limits how many active connect events we have.
        /// </summary>
        private SemaphoreSlim _connectReady = new SemaphoreSlim(10);

        /// <summary>
        /// Gets or sets the listener address.
        /// </summary>
        /// <value>
        /// The listener address.
        /// </value>
        public IPAddress ListenAddress
        {
            get
            {
                return _listenAddress;
            }
            set
            {
                _listenAddress = value;
            }
        }

        public TCPServer(int listenPort, IPAddress listenAddress)
        {
            if (listenPort > 0)
                _port = listenPort;
            if (listenAddress != null)
                _listenAddress = listenAddress;
        }

        /// <summary>
        /// Starts this instance.
        /// </summary>
        public virtual void Start()
        {
            if (_listener == null)
            {
                try
                {
                    _listener = new TcpListener(_listenAddress, _port);
                    ThreadPool.QueueUserWorkItem(Listen, null);
                }
                catch { /* Ignore */ }
            }
        }

        /// <summary>
        /// Stops this instance.
        /// </summary>
        public virtual void Stop()
        {
            if (_listener != null)
            {
                try
                {
                    _listener.Stop();
                }
                catch { /* Ignore */ }
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
        /// Listens on the ip and port specified.
        /// </summary>
        /// <param name="State">The state.</param>
        private void Listen(object State)
        {
            _listener.Start();
            while (_listener != null)
            {
                try
                {
                    _listener.BeginAcceptTcpClient(RunClient, null);
                }
                catch { /* Ignore */ }
                _connectReady.Wait();
            }
        }

        /// <summary>
        /// Runs the client.
        /// Sets up the UserContext.
        /// Executes in it's own thread.
        /// Utilizes a semaphore(ReceiveReady) to limit the number of receive events active for this client to 1 at a time.
        /// </summary>
        /// <param name="AResult">The A result.</param>
        private void RunClient(IAsyncResult AResult)
        {
            TcpClient AConnection = null;
            try
            {
                if (_listener != null)
                    AConnection = _listener.EndAcceptTcpClient(AResult);
            }
            catch (Exception) { /* Ignore*/ }

            _connectReady.Release();
            if (AConnection != null)
            {
                _clientLock.Wait();
                _clients++;
                _clientLock.Release();

                OnRunClient(AConnection);

                _clientLock.Wait();
                _clients--;
                _clientLock.Release();
            }
        }

        protected abstract void OnRunClient(TcpClient AConnection);

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            Stop();
        }
    }
}
