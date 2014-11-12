using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web;
using Alchemy.Classes;

namespace Alchemy.Handlers.WebSocket.rfc6455
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
            "Origin: {1}\r\n" +
            "Upgrade: websocket\r\n" +
            "Connection: Upgrade\r\n" +
            "Sec-WebSocket-Key: {3}\r\n" +
            "Sec-WebSocket-Version: 8\r\n" +
            "{4}";

        public string Host = String.Empty;
        public string Key = String.Empty;
        public string Origin = String.Empty;
        public string ResourcePath = String.Empty;

        public ClientHandshake() {}

        /// <summary>
        /// Initializes a new instance of the <see cref="ClientHandshake"/> class.
        /// </summary>
        /// <param name="header">The header.</param>
        public ClientHandshake(Header header)
        {
            ResourcePath = header.RequestPath;
            Key = header["sec-websocket-key"];
            SubProtocols = header.SubProtocols;
            Origin = header["origin"];
            if (String.IsNullOrEmpty(Origin))
            {
                Origin = header["sec-websocket-origin"];
            }
            Host = header["host"];
            Version = header["sec-websocket-version"];
            Cookies = header.Cookies;
        }

        public HttpCookieCollection Cookies { get; set; }
        public string[] SubProtocols { get; set; }
        public string Version { get; set; }
        public Dictionary<string, string> AdditionalFields { get; set; }

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
                       (Int32.Parse(Version) >= 8)
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
            var additionalFields = String.Empty;

            if (Cookies != null)
            {
                additionalFields += "Cookie: " + Cookies + "\r\n";
            }

            if (additionalFields != String.Empty)
            {
                additionalFields = AdditionalFields.Aggregate(additionalFields,
                                                              (current, field) =>
                                                              current + (field.Key + ": " + field.Value + "\r\n"));
            }

            if (SubProtocols != null && SubProtocols.Length > 0)
            {
                additionalFields += "Sec-Websocket-Protocol: " + String.Join(",", SubProtocols) + "\r\n";
            }

            additionalFields += "\r\n";

            return String.Format(Handshake, ResourcePath, Origin, Host, Key, additionalFields).ToString(CultureInfo.InvariantCulture);
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

        public ServerHandshake() {}

        /// <summary>
        /// Initializes a new instance of the <see cref="ServerHandshake"/> class.
        /// </summary>
        /// <param name="header">The header.</param>
        public ServerHandshake(Header header)
        {
            Accept = header["Sec-WebSocket-Accept"];
        }

        public string[] RequestProtocols { get; set; }

        public string Accept { get; set; }

        public WebSocketServer Server { get; set; }
         
        /// <summary>
        /// Returns a <see cref="System.String"/> that represents this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="System.String"/> that represents this instance.
        /// </returns>
        public override string ToString()
        {
            var additionalFields = String.Empty;

            if (Server.SubProtocols != null && RequestProtocols != null)
            {
                string subprotocol = "";

                foreach (var s in RequestProtocols)
                {
                    if (!Server.SubProtocols.Contains(s) || !String.IsNullOrEmpty(subprotocol)) continue;
                    subprotocol = s;
                }

                if(!String.IsNullOrEmpty(subprotocol))
                {
                    additionalFields += "Sec-WebSocket-Protocol: " + subprotocol + "\r\n";
                }
            }

            additionalFields += "\r\n";
            return String.Format(Handshake, Accept, additionalFields).ToString(CultureInfo.InvariantCulture);
        }
    }
}