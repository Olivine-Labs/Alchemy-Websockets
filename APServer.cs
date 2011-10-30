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
using System.Text;
using System.Net;
using System.Net.Sockets;

namespace Alchemy.Server
{
    /// <summary>
    /// This is the Flash Access Policy Server
    /// It manages sending the XML cross domain policy to flash socket clients over port 843.
    /// See http://www.adobe.com/devnet/articles/crossdomain_policy_file_spec.html for details.
    /// </summary>
    public class APServer : TCPServer, IDisposable
    {
        private string _allowedHost = "localhost";
        private int _allowedPort = 80;

        /// <summary>
        /// The pre-formatted XML response.
        /// </summary>
        private const string _response =
            "<cross-domain-policy>\r\n" +
                "\t<allow-access-from domain=\"{0}\" to-ports=\"{1}\" />\r\n" +
            "</cross-domain-policy>\r\n\0";

        /// <summary>
        /// Initializes a new instance of the <see cref="APServer"/> class.
        /// </summary>
        /// <param name="ListenAddress">The listen address.</param>
        /// <param name="OriginDomain">The origin domain.</param>
        /// <param name="AllowedPort">The allowed port.</param>
        public APServer(IPAddress listenAddress, string originDomain, int allowedPort)
            : base(843, listenAddress)
        {
            _allowedHost = "*";
            if (originDomain != String.Empty)
                _allowedHost = originDomain;

            _allowedPort = allowedPort;
        }

        /// <summary>
        /// Fires when a client connects.
        /// </summary>
        /// <param name="AConnection">The TCP Connection.</param>
        protected override void OnRunClient(TcpClient connection)
        {
            try
            {
                connection.Client.Receive(new byte[32]);
                SendResponse(connection);
                connection.Client.Close();
            }
            catch { /* Ignore */ }
        }

        /// <summary>
        /// Sends the response.
        /// </summary>
        /// <param name="AConnection">The TCP Connection.</param>
        public void SendResponse(TcpClient connection)
        {
            connection.Client.Send(UTF8Encoding.UTF8.GetBytes(String.Format(_response, _allowedHost, _allowedPort.ToString())));
        }
    }
}