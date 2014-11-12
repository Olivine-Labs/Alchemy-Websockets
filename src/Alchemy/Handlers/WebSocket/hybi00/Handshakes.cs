using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web;
using Alchemy.Classes;

namespace Alchemy.Handlers.WebSocket.hybi00
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
            "Upgrade: WebSocket\r\n" +
            "Connection: Upgrade\r\n" +
            "Origin: {1}\r\n" +
            "Host: {2}\r\n" +
            "Sec-Websocket-Key1: {3}\r\n" +
            "Sec-Websocket-Key2: {4}\r\n" +
            "{5}";

        public string Host = String.Empty;
        public string Key1 = String.Empty;
        public string Key2 = String.Empty;
        public string Origin = String.Empty;
        public string ResourcePath = String.Empty;

        public ClientHandshake() {}

        /// <summary>
        /// Initializes a new instance of the <see cref="ClientHandshake"/> class.
        /// </summary>
        /// <param name="challengeBytes">The challenge bytes.</param>
        /// <param name="header">The header.</param>
        public ClientHandshake(ArraySegment<byte> challengeBytes, Header header)
        {
            ChallengeBytes = challengeBytes;
            ResourcePath = header.RequestPath;
            Key1 = header["sec-websocket-key1"];
            Key2 = header["sec-websocket-key2"];
            SubProtocols = header.SubProtocols;
            Origin = header["origin"];
            Host = header["host"];
            Cookies = header.Cookies;
        }

        public ArraySegment<byte> ChallengeBytes { get; set; }
        public HttpCookieCollection Cookies { get; set; }
        public string[] SubProtocols { get; set; }
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
            var additionalFields = String.Empty;

            if (Cookies != null)
            {
                additionalFields += "Cookie: " + Cookies + "\r\n";
            }

            if (SubProtocols != null && SubProtocols.Length > 0)
            {
                additionalFields += "Sec-Websocket-Protocol: " + String.Join(",", SubProtocols) + "\r\n";
            }

            if (additionalFields != String.Empty)
            {
                additionalFields = AdditionalFields.Aggregate(additionalFields,
                                                              (current, field) =>
                                                              current + (field.Key + ": " + field.Value + "\r\n"));
            }
            additionalFields += "\r\n";

            return String.Format(Handshake, ResourcePath, Origin, Host, Key1, Key2, additionalFields).ToString(CultureInfo.InvariantCulture);
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
            "HTTP/1.1 101 Web Socket Protocol Handshake\r\n" +
            "Upgrade: WebSocket\r\n" +
            "Connection: Upgrade\r\n" +
            "Sec-WebSocket-Origin: {0}\r\n" +
            "Sec-WebSocket-Location: {1}\r\n" +
            "{2}" +
            "                "; //Empty space for challenge answer

        public string Location = String.Empty;
        public string Origin = String.Empty;
        public byte[] AnswerBytes { get; set; }
        public string[] RequestProtocols { get; set; }

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
                foreach (var s in RequestProtocols)
                {
                    if (Server.SubProtocols.Contains(s))
                    {
                        additionalFields += "Sec-WebSocket-Protocol: " + s + "\r\n";
                    }

                    break;
                }
            }

            additionalFields += "\r\n";

            return String.Format(Handshake, Origin, Location, additionalFields).ToString(CultureInfo.InvariantCulture);
        }
    }
}