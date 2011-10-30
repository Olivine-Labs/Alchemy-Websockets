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

namespace Alchemy.Server.Handlers.WebSocket.hybi00
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
        private const String _handshake =
            "GET {0} HTTP/1.1\r\n" +
            "Upgrade: WebSocket\r\n" +
            "Connection: Upgrade\r\n" +
            "Origin: {1}\r\n" +
            "Host: {2}\r\n" +
            "Sec-Websocket-Key1: {3}\r\n" +
            "Sec-Websocket-Key2: {4}\r\n" +
            "{5}";

        public string Origin = String.Empty;
        public string Host = String.Empty;
        public string ResourcePath = String.Empty;
        public string Key1 = String.Empty;
        public string Key2 = String.Empty;
        public ArraySegment<byte> ChallengeBytes { get; set; }
        public HttpCookieCollection Cookies { get; set; }
        public string SubProtocol { get; set; }
        public Dictionary<string, string> AdditionalFields { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ClientHandshake"/> class.
        /// </summary>
        /// <param name="ChallengeBytes">The challenge bytes.</param>
        /// <param name="header">The header.</param>
        public ClientHandshake(ArraySegment<byte> challengeBytes, Header header)
        {
            ChallengeBytes = challengeBytes;
            ResourcePath = header.RequestPath;
            Key1 = header["sec-websocket-key1"];
            Key2 = header["sec-websocket-key2"];
            SubProtocol = header["sec-websocket-protocol"];
            Origin = header["origin"];
            Host = header["host"];
            Cookies = header.Cookies;
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
                (ChallengeBytes != null) &&
                (Host != null) &&
                (Key1 != null) &&
                (Key2 != null) &&
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
            string additionalFields = String.Empty;

            if (Cookies != null)
            {
                additionalFields += "Cookie: " + Cookies.ToString() + "\r\n";
            }
            if (SubProtocol != null)
                additionalFields += "Sec-Websocket-Protocol: " + SubProtocol + "\r\n";

            if (additionalFields != null)
            {
                foreach (KeyValuePair<string, string> field in this.AdditionalFields)
                {
                    additionalFields += field.Key + ": " + field.Value + "\r\n";
                }
            }
            additionalFields += "\r\n";

            return String.Format(_handshake, ResourcePath, Origin, Host, Key1, Key2, AdditionalFields);
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
        private const string _handshake =
            "HTTP/1.1 101 Web Socket Protocol Handshake\r\n" +
                "Upgrade: WebSocket\r\n" +
                "Connection: Upgrade\r\n" +
                "Sec-WebSocket-Origin: {0}\r\n" +
                "Sec-WebSocket-Location: {1}\r\n" +
                "{2}" +
                "                ";//Empty space for challenge answer

        public string Origin = String.Empty;
        public string Location = String.Empty;
        public byte[] AnswerBytes { get; set; }
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
            string additionalFields = String.Empty;
            if (SubProtocol != null)
            {
                additionalFields += "Sec-WebSocket-Protocol: " + SubProtocol + "\r\n";
            }
            additionalFields += "\r\n";

            return String.Format(_handshake, Origin, Location, AdditionalFields);
        }
    }
}