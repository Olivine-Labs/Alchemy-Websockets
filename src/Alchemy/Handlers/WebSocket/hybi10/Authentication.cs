using System;
using System.Security.Cryptography;
using System.Text;
using Alchemy.Classes;

namespace Alchemy.Handlers.WebSocket.hybi10
{
    /// <summary>
    /// Handles the handshaking between the client and the host, when a new connection is created
    /// </summary>
    internal class Authentication : Handlers.Authentication, IWebSocketAuthentication
    {
        public static string Origin = string.Empty;
        public static string Location = string.Empty;

        #region IWebSocketAuthentication Members

        public void SetOrigin(string origin)
        {
            Origin = origin;
        }

        public void SetLocation(string location)
        {
            Location = location;
        }

        #endregion

        protected override bool CheckAuthentication(Context context)
        {
            if (context.ReceivedByteCount > 8)
            {
                var handshake = new ClientHandshake(context.Header);
                // See if our header had the required information
                if (handshake.IsValid())
                {
                    // Optionally check Origin and Location if they're set.
                    if (!String.IsNullOrEmpty(Origin))
                    {
                        if (handshake.Origin != "http://" + Origin)
                        {
                            return false;
                        }
                    }
                    if (!String.IsNullOrEmpty(Location))
                    {
                        if (handshake.Host != Location + ":" + context.Server.Port.ToString())
                        {
                            return false;
                        }
                    }
                    // Generate response handshake for the client
                    ServerHandshake serverShake = GenerateResponseHandshake(handshake, context);
                    serverShake.SubProtocol = handshake.SubProtocol;
                    // Send the response handshake
                    SendServerHandshake(serverShake, context);
                    return true;
                }
            }
            return false;
        }

        private static ServerHandshake GenerateResponseHandshake(ClientHandshake handshake, Context context)
        {
            var responseHandshake = new ServerHandshake {Accept = GenerateAccept(handshake.Key, context)};
            return responseHandshake;
        }

        private static void SendServerHandshake(ServerHandshake handshake, Context context)
        {
            // generate a byte array representation of the handshake including the answer to the challenge
            string temp = handshake.ToString();
            byte[] handshakeBytes = Encoding.UTF8.GetBytes(temp);
            context.UserContext.Send(handshakeBytes, true);
        }

        private static string GenerateAccept(string key, Context context)
        {
            string rawAnswer = key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

            // Create a hash of the rawAnswer and return it
            SHA1 hasher = SHA1.Create();
            return Convert.ToBase64String(hasher.ComputeHash(Encoding.UTF8.GetBytes(rawAnswer)));
        }
    }
}