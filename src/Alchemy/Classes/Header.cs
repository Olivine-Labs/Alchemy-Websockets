using System;
using System.Collections.Specialized;
using System.Text.RegularExpressions;
using System.Web;

namespace Alchemy.Classes
{
    /// <summary>
    /// What protocols we support
    /// </summary>
    public enum Protocol
    {
        None = -1,
        WebSocketHybi10 = 0,
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
        public static string Pattern =
            @"^(?<connect>[^\s]+)\s(?<path>[^\s]+)\sHTTP\/1\.1\r\n" + // HTTP Request
            @"((?<field_name>[^:\r\n]+):(?<field_value>[^\r\n]+)\r\n)+";

        // HTTP Header Fields (<Field_Name>: <Field_Value> CR LF)

        /// <summary>
        /// A collection of fields attached to the header.
        /// </summary>
        private readonly NameValueCollection _fields = new NameValueCollection();

        /// <summary>
        /// Any cookies sent with the header.
        /// </summary>
        public HttpCookieCollection Cookies = new HttpCookieCollection();

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
        /// Initializes a new instance of the <see cref="Header"/> class.
        /// Accepts a string that represents an HTTP header.
        /// </summary>
        /// <param name="data">The data.</param>
        public Header(string data)
        {
            try
            {
                // Parse HTTP Header
                var regex = new Regex(Pattern, RegexOptions.IgnoreCase);
                Match match = regex.Match(data);
                GroupCollection someFields = match.Groups;
                // run through every match and save them in the handshake object
                for (int i = 0; i < someFields["field_name"].Captures.Count; i++)
                {
                    string name = someFields["field_name"].Captures[i].ToString().ToLower();
                    string value = someFields["field_value"].Captures[i].ToString().Trim();
                    switch (name)
                    {
                        case "cookie":
                            string[] cookieArray = value.Split(';');
                            foreach (string cookie in cookieArray)
                            {
                                try
                                {
                                    string cookieName = cookie.Remove(cookie.IndexOf('='));
                                    string cookieValue = cookie.Substring(cookie.IndexOf('=') + 1);
                                    Cookies.Add(new HttpCookie(cookieName.TrimStart(), cookieValue));
                                }
                                    // ReSharper disable EmptyGeneralCatchClause
                                catch
                                    // ReSharper restore EmptyGeneralCatchClause
                                {
                                    /* Ignore bad cookie */
                                }
                            }
                            break;
                        default:
                            _fields.Add(name, value);
                            break;
                    }
                }

                RequestPath = someFields["path"].Captures[0].Value.Trim();
                Method = someFields["connect"].Captures[0].Value.Trim();

                int version;
                Int32.TryParse(_fields["sec-websocket-version"], out version);

                // ReSharper restore EmptyGeneralCatchClause

                Protocol = version < 8 ? Protocol.WebSocketHybi00 : Protocol.WebSocketHybi10;
            }
                // ReSharper disable EmptyGeneralCatchClause
            catch
                // ReSharper restore EmptyGeneralCatchClause
            {
                /* Ignore bad header */
            }
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