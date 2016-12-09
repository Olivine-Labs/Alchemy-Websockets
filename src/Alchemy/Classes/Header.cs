using System;
using System.Collections.Specialized;
using System.Text.RegularExpressions;
using System.Net;

namespace Alchemy.Classes
{
    /// <summary>
    /// What protocols we support
    /// </summary>
    public enum Protocol
    {
        None = -1,
        WebSocketRFC6455 = 0,
        WebSocketHybi00 = 1
    }

    /// <summary>
    /// This class implements a rudimentary HTTP header reading interface.
    /// </summary>
    public class Header
    {
        /// <summary>
        /// Regular expression to parse http header
        /// </summary>
        internal static string Pattern =
            @"^(?<connect>[^\s]+)?\s?(?<path>[^\s]+)?\s?HTTP\/1\.1(.*?)?\r\n" + // HTTP Request
            @"((?<field_name>[^:\r\n]+):(?<field_value>[^\r\n]+)\r\n)+";

        // HTTP Header Fields (<Field_Name>: <Field_Value> CR LF)

        /// <summary>
        /// A collection of fields attached to the header.
        /// </summary>
        private readonly NameValueCollection _fields = new NameValueCollection();

        /// <summary>
        /// Any cookies sent with the header.
        /// </summary>
        public CookieCollection Cookies {get; internal set;}

        /// <summary>
        /// The HTTP Method (GET/POST/PUT, etc.)
        /// </summary>
        public String Method {get; internal set;}

        /// <summary>
        /// What protocol this header represents, if any.
        /// </summary>
        public Protocol Protocol {get; internal set;}


        /// <summary>
        /// The path requested by the header.
        /// </summary>
        public string RequestPath {get; internal set;}

        /// <summary>
        /// The subprotocols specified by the header.
        /// </summary>
        public string[] SubProtocols {get; internal set;}

        /// <summary>
        /// Initializes a new instance of the <see cref="Header"/> class.
        /// Accepts a string that represents an HTTP header.
        /// </summary>
        /// <param name="data">The data.</param>
        public Header(string data)
        {
            Cookies = new CookieCollection();
            Method = String.Empty;
            Protocol = Protocol.None;
            RequestPath = string.Empty;

            // Parse HTTP Header
            var regex = new Regex(Pattern, RegexOptions.IgnoreCase);
            var match = regex.Match(data);
            var matchGroups = match.Groups;
            var fieldNameCollection = matchGroups["field_name"];
            var fieldValueCollection = matchGroups["field_value"];
            
            if (fieldNameCollection != null && fieldValueCollection != null)
            {
                // run through every match and save them in the handshake object
                for (var i = 0; i < fieldNameCollection.Captures.Count; i++)
                {
                    var name = fieldNameCollection.Captures[i].ToString().ToLower();
                    var value = fieldValueCollection.Captures[i].ToString().Trim();

                    switch (name)
                    {
                        case "cookie":
                            var cookieArray = value.Split(';');
                            foreach (var cookie in cookieArray)
                            {
                                var cookieIndex = cookie.IndexOf('=');
                                
                                if (cookieIndex < 0) continue;
                                
                                var cookieName = cookie.Remove(cookieIndex).TrimStart();
                                
                                var cookieValue = cookie.Substring(cookieIndex + 1);
                                if (cookieName != string.Empty)
                                {
                                    Cookies.Add(new Cookie(cookieName, cookieValue));
                                }
                            }
                            break;
                        default:
                            _fields.Add(name, value);
                            break;
                    }
                }
            }

            var pathCollection = matchGroups["path"];
            var methodCollection = matchGroups["connect"];

            if (pathCollection != null)
            {
                if (pathCollection.Captures.Count > 0)
                {
                    RequestPath = pathCollection.Captures[0].Value.Trim();
                }
            }

            if (methodCollection != null)
            {
                if (methodCollection.Captures.Count > 0)
                {
                    Method = methodCollection.Captures[0].Value.Trim();
                }
            }

            int version;
            Int32.TryParse(_fields["sec-websocket-version"], out version);

            if(!String.IsNullOrEmpty(_fields["sec-websocket-protocol"]))
            {
                SubProtocols = _fields["sec-websocket-protocol"].Split(',');
            }

            Protocol = version < 8 ? Protocol.WebSocketHybi00 : Protocol.WebSocketRFC6455;
        }

        /// <summary>
        /// Gets or sets the Fields object with the specified key.
        /// </summary>
        public string this[string key]
        {
            get { return _fields[key]; }
            set { _fields[key] = value; }
        }
    }
}