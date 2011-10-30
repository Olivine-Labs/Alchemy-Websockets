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
        protected TcpListener Listener = null;

        protected int _port = 80;
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
        private int _Clients = 0;

        /// <summary>
        /// Gets the client count.
        /// </summary>
        public int ClientCount
        { get { return _Clients; } }

        /// <summary>
        /// This Semaphore protects our clients variable on increment/decrement when a user connects/disconnects.
        /// </summary>
        private SemaphoreSlim ClientLock = new SemaphoreSlim(1);

        /// <summary>
        /// Limits how many active connect events we have.
        /// </summary>
        private SemaphoreSlim ConnectReady = new SemaphoreSlim(10);

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
            if (Listener == null)
            {
                try
                {
                    Listener = new TcpListener(_listenAddress, _port);
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
            if (Listener != null)
            {
                try
                {
                    Listener.Stop();
                }
                catch { /* Ignore */ }
            }
            Listener = null;
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
            Listener.Start();
            while (Listener != null)
            {
                try
                {
                    Listener.BeginAcceptTcpClient(RunClient, null);
                }
                catch { /* Ignore */ }
                ConnectReady.Wait();
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
                if (Listener != null)
                    AConnection = Listener.EndAcceptTcpClient(AResult);
            }
            catch (Exception) { /* Ignore*/ }

            ConnectReady.Release();
            if (AConnection != null)
            {
                ClientLock.Wait();
                _Clients++;
                ClientLock.Release();

                OnRunClient(AConnection);

                ClientLock.Wait();
                _Clients--;
                ClientLock.Release();
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
