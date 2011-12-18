/*
Copyright 2011 Olivine Labs, LLC.
http://www.olivinelabs.com
*/

/*
This file is part of Alchemy Websockets.

Alchemy Websockets is free software: you can redistribute it and/or modify
it under the terms of the GNU Lesser General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

Alchemy Websockets is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU Lesser General Public License for more details.

You should have received a copy of the GNU Lesser General Public License
along with Alchemy Websockets.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Alchemy
{
    public abstract class TcpServer
    {
        /// <summary>
        /// This Semaphore protects our clients variable on increment/decrement when a user connects/disconnects.
        /// </summary>
        private readonly SemaphoreSlim _clientLock = new SemaphoreSlim(1);

        /// <summary>
        /// Limits how many active connect events we have.
        /// </summary>
        private readonly SemaphoreSlim _connectReady = new SemaphoreSlim(10);

        protected int DefaultBufferSize = 512;

        /// <summary>
        /// The number of connected clients.
        /// </summary>
        /// 
        private int _clients;

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
        /// <value>
        /// The port.
        /// </value>
        public int Port
        {
            get { return _port; }
            set { _port = value; }
        }

        /// <summary>
        /// Gets the client count.
        /// </summary>
        public int ClientCount
        {
            get { return _clients; }
        }

        /// <summary>
        /// Gets or sets the listener address.
        /// </summary>
        /// <value>
        /// The listener address.
        /// </value>
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
                try
                {
                    _listener = new TcpListener(_listenAddress, _port);
                    ThreadPool.QueueUserWorkItem(Listen, null);
                }
                    // ReSharper disable EmptyGeneralCatchClause
                catch
                    // ReSharper restore EmptyGeneralCatchClause
                {
                    /* Ignore */
                }
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
                    // ReSharper disable EmptyGeneralCatchClause
                catch
                    // ReSharper restore EmptyGeneralCatchClause
                {
                    /* Ignore */
                }
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
        /// <param name="state">The state.</param>
        private void Listen(object state)
        {
            _listener.Start();
            while (_listener != null)
            {
                try
                {
                    _listener.BeginAcceptTcpClient(RunClient, null);
                }
                    // ReSharper disable EmptyGeneralCatchClause
                catch
                    // ReSharper restore EmptyGeneralCatchClause
                {
                    /* Ignore */
                }
                _connectReady.Wait();
            }
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
            try
            {
                if (_listener != null)
                {
                    connection = _listener.EndAcceptTcpClient(result);
                }
            }
                // ReSharper disable EmptyGeneralCatchClause
            catch
                // ReSharper restore EmptyGeneralCatchClause
            {
                /* Ignore*/
            }

            _connectReady.Release();
            if (connection != null)
            {
                _clientLock.Wait();
                _clients++;
                _clientLock.Release();

                OnRunClient(connection);

                _clientLock.Wait();
                _clients--;
                _clientLock.Release();
            }
        }

        protected abstract void OnRunClient(TcpClient connection);

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            Stop();
        }
    }
}