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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Alchemy.Server.Classes;
using log4net;
using System.IO;

namespace Alchemy.Server
{
    /// <summary>
    /// This is the Flash Access Policy Server
    /// It manages sending the XML cross domain policy to flash socket clients over port 843.
    /// See http://www.adobe.com/devnet/articles/crossdomain_policy_file_spec.html for details.
    /// </summary>
    public class APServer : IDisposable
    {
        private int _Port = 843;
        private IPAddress _ListenerAddress = IPAddress.Any;
        private string _AllowedHost = "localhost";
        private int _AllowedPort = 81;

        private TcpListener Listener = null;

        /// <summary>
        /// Limits how many active connect events we have.
        /// </summary>
        private SemaphoreSlim ConnectReady = new SemaphoreSlim(10);

        /// <summary>
        /// The pre-formatted XML response.
        /// </summary>
        private const string Response = 
            "<cross-domain-policy>\r\n" +
                "\t<allow-access-from domain=\"{0}\" to-ports=\"{1}\" />\r\n" +
            "</cross-domain-policy>\r\n\0";

        /// <summary>
        /// Gets or sets the listener address.
        /// </summary>
        /// <value>
        /// The listener address.
        /// </value>
        public IPAddress ListenerAddress
        {
            get
            {
                return _ListenerAddress;
            }
            set
            {
                _ListenerAddress = value;
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="APServer"/> class.
        /// </summary>
        /// <param name="ListenAddress">The listen address.</param>
        /// <param name="OriginDomain">The origin domain.</param>
        /// <param name="AllowedPort">The allowed port.</param>
        public APServer(IPAddress ListenAddress, string OriginDomain, int AllowedPort)
        {
            string OriginLockdown = "*";
            if (OriginDomain != String.Empty)
                OriginLockdown = OriginDomain;

            _ListenerAddress = ListenAddress;
            _AllowedHost = OriginLockdown;
            _AllowedPort = AllowedPort;
        }

        /// <summary>
        /// Starts this instance.
        /// </summary>
        public void Start()
        {
            if (Listener == null)
            {
                try
                {
                    Listener = new TcpListener(ListenerAddress, _Port);
                    ThreadPool.QueueUserWorkItem(Listen, null);
                }
                catch { /* Ignore */ }
            }
        }

        /// <summary>
        /// Stops this instance.
        /// </summary>
        public void Stop()
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
        public void Restart()
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
        /// </summary>
        /// <param name="AResult">The Async result.</param>
        private void RunClient(IAsyncResult AResult)
        {
            TcpClient AConnection = null;
            try
            {
                if (Listener != null)
                    AConnection = Listener.EndAcceptTcpClient(AResult);
            }
            catch { /* Ignore */ }

            ConnectReady.Release();
            if (AConnection != null)
            {
                try
                {
                    AConnection.Client.Receive(new byte[32]);
                    SendResponse(AConnection);
                    AConnection.Client.Close();
                }
                catch { /* Ignore */ }
            }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            Stop();
        }

        /// <summary>
        /// Sends the response.
        /// </summary>
        /// <param name="AConnection">The TCP Connection.</param>
        public void SendResponse(TcpClient AConnection)
        {
            AConnection.Client.Send(UTF8Encoding.UTF8.GetBytes(String.Format(Response, _AllowedHost, _AllowedPort.ToString())));
        }
    }
}