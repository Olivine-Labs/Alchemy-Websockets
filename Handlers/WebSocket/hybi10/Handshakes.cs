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
using System.Web;
using Alchemy.Server.Classes;

namespace Alchemy.Server.Handlers.WebSocket.hybi10
{
    /// <summary>
    /// An easy wrapper for the header to access client handshake data.
    /// See http://www.whatwg.org/specs/web-socket-protocol/ for more details on the WebSocket Protocol.
    /// </summary>
    public class ClientHandshake
    {
        /// <summary>
        /// The preformatted handshake as a string.
        /// </summary>
        private const String Handshake = 
            "GET {0} HTTP/1.1\r\n" +
            "Host: {2}\r\n" +
            "Upgrade: websocket\r\n" +
            "Connection: Upgrade\r\n" +
            "Sec-WebSocket-Key: {4}\r\n" +
            "Sec-WebSocket-Origin: {1}\r\n" +
            "Sec-WebSocket-Protocol: {3}\r\n" +
            "Sec-WebSocket-Version: 8\r\n" + 
            "{5}";

        public string Origin = String.Empty;
        public string Host = String.Empty;
        public string ResourcePath = String.Empty;
        public string Key = String.Empty;
        public HttpCookieCollection Cookies { get; set; }
        public string SubProtocol { get; set; }
        public string Version { get; set; }
        public Dictionary<string,string> AdditionalFields { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ClientHandshake"/> class.
        /// </summary>
        /// <param name="ChallengeBytes">The challenge bytes.</param>
        /// <param name="AHeader">The header.</param>
        public ClientHandshake(Header AHeader)
        {
            ResourcePath= AHeader.RequestPath;
            Key         = AHeader["sec-websocket-key"];
            SubProtocol = AHeader["sec-websocket-protocol"];
            Origin      = AHeader["sec-websocket-origin"];
            Host        = AHeader["host"];
            Version     = AHeader["sec-websocket-version"];
            Cookies     = AHeader.Cookies;
        }

        /// <summary>
        /// Determines whether this instance is valid.
        /// </summary>
        /// <returns>
        ///   <c>true</c> if this instance is valid; otherwise, <c>false</c>.
        /// </returns>
        public bool IsValid()
        {
            return (
                (Host != null) &&
                (Key != null) &&
                (Version == "8") &&
                (Origin != null) &&
                (ResourcePath != null)
            );
        }

        /// <summary>
        /// Returns a <see cref="System.String"/> that represents this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="System.String"/> that represents this instance.
        /// </returns>
        public override string ToString()
        {
            string AdditionalFields = String.Empty;

            if (Cookies != null)
            {
                AdditionalFields += "Cookie: " + Cookies.ToString() + "\r\n";
            }

            if (AdditionalFields != null)
            {
                foreach (KeyValuePair<string, string> field in this.AdditionalFields)
                {
                    AdditionalFields += field.Key + ": " + field.Value + "\r\n";
                }
            }
            AdditionalFields += "\r\n";

            return String.Format(Handshake, ResourcePath, Origin, Host, SubProtocol, Key, AdditionalFields);
        }
    }

    /// <summary>
    /// Implements a server handshake
    /// See http://www.whatwg.org/specs/web-socket-protocol/ for more details on the WebSocket Protocol.
    /// </summary>
    public class ServerHandshake
    {
        /// <summary>
        /// The preformatted handshake string.
        /// </summary>
        private const string Handshake =
            "HTTP/1.1 101 Switching Protocols\r\n" +
            "Upgrade: websocket\r\n" +
            "Connection: Upgrade\r\n" +
            "Sec-WebSocket-Accept: {0}\r\n" +
            "{1}";

        public string Accept { get; set; }
        public string SubProtocol { get; set; }

        public Dictionary<string, string> AdditionalFields { get; set; }

        /// <summary>
        /// Returns a <see cref="System.String"/> that represents this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="System.String"/> that represents this instance.
        /// </returns>
        public override string ToString()
        {
            string AdditionalFields = String.Empty;
            if (SubProtocol != null)
            {
                AdditionalFields += "Sec-WebSocket-Protocol: " + SubProtocol + "\r\n";
            }
            AdditionalFields += "\r\n";
            return String.Format(Handshake, Accept, AdditionalFields);
        }
    }
}
