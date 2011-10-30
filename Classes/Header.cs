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
using System.Collections.Specialized;
using System.Text.RegularExpressions;
using System.Web;

namespace Alchemy.Server.Classes
{
    /// <summary>
    /// What protocols we support
    /// </summary>
    public enum Protocol
    {
        None = -1,
        WebSocketHybi10 = 0,
        WebSocketHybi00 = 1,
        FlashSocket = 2
    }

    /// <summary>
    /// This class implements a rudimentary HTTP header reading interface.
    /// </summary>
    public class Header
    {
        /// <summary>
        /// Regular expression to parse http header
        /// </summary>
        public static string Pattern = 
            @"^(?<connect>[^\s]+)\s(?<path>[^\s]+)\sHTTP\/1\.1\r\n" +       // HTTP Request
            @"((?<field_name>[^:\r\n]+):(?<field_value>[^\r\n]+)\r\n)+";    // HTTP Header Fields (<Field_Name>: <Field_Value> CR LF)

        /// <summary>
        /// The HTTP Method (GET/POST/PUT, etc.)
        /// </summary>
        public String Method = String.Empty;

        /// <summary>
        /// What protocol this header represents, if any.
        /// </summary>
        public Protocol Protocol = Protocol.None;


        /// <summary>
        /// The path requested by the header.
        /// </summary>
        public string RequestPath = string.Empty;

        /// <summary>
        /// Any cookies sent with the header.
        /// </summary>
        public HttpCookieCollection Cookies = new HttpCookieCollection();

        /// <summary>
        /// A collection of fields attached to the header.
        /// </summary>
        private NameValueCollection Fields = new NameValueCollection();

        /// <summary>
        /// Gets or sets the Fields object with the specified key.
        /// </summary>
        public string this[string Key]
        {
            get
            {
                return Fields[Key];
            }
            set
            {
                Fields[Key] = value;
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Header"/> class.
        /// Accepts a string that represents an HTTP header.
        /// </summary>
        /// <param name="data">The data.</param>
        public Header(string Data)
        {
            try
            {
                // Parse HTTP Header
                Regex regex = new Regex(Pattern, RegexOptions.IgnoreCase);
                Match match = regex.Match(Data);
                GroupCollection SomeFields = match.Groups;
                // run through every match and save them in the handshake object
                for (int i = 0; i < SomeFields["field_name"].Captures.Count; i++)
                {
                    string Name = SomeFields["field_name"].Captures[i].ToString().ToLower();
                    string Value = SomeFields["field_value"].Captures[i].ToString().Trim();
                    switch (Name)
                    {
                        case "cookie":
                            string[] CookieArray = Value.Split(';');
                            foreach (string ACookie in CookieArray)
                            {
                                try
                                {
                                    string CookieName = ACookie.Remove(ACookie.IndexOf('='));
                                    string CookieValue = ACookie.Substring(ACookie.IndexOf('=') + 1);
                                    Cookies.Add(new HttpCookie(CookieName.TrimStart(), CookieValue));
                                }
                                catch { /* Ignore bad cookie */ }
                            }
                            break;
                        default:
                            Fields.Add(Name, Value);
                            break;
                    }
                }

                RequestPath = SomeFields["path"].Captures[0].Value.Trim();
                Method = SomeFields["connect"].Captures[0].Value.Trim();

                string Version = string.Empty;
                try
                {
                    Version = Fields["sec-websocket-version"];
                }
                catch (Exception){}
                if (Version != "8")
                {
                    string[] PathExplode = RequestPath.Split('/');
                    string ProtocolString = string.Empty;
                    if (PathExplode.Length > 0)
                        ProtocolString = PathExplode[PathExplode.Length - 1].ToLower().Trim();
                    switch (ProtocolString)
                    {
                        case "websocket":
                            this.Protocol = Protocol.WebSocketHybi00;
                            break;
                        case "flashsocket":
                            this.Protocol = Protocol.FlashSocket;
                            break;
                        default:
                            this.Protocol = Protocol.None;
                            break;
                    }
                }
                else
                {
                    this.Protocol = Protocol.WebSocketHybi10;
                }
            }
            catch{ /* Ignore bad header */ }
        }
    }
}
