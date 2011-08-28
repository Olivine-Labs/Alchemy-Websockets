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
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web;
using Alchemy.Server.Classes;
using System.Security.Cryptography;

namespace Alchemy.Server.Handlers.WebSocket
{
    /// <summary>
    /// Handles the handshaking between the client and the host, when a new connection is created
    /// </summary>
    static class WebSocketAuthentication
    {
        public static string Origin = string.Empty;
        public static string Location = string.Empty;

        public static bool CheckHandshake(Context AContext)
        {
            if(AContext.ReceivedByteCount > 8)
            {
                ClientHandshake AHandshake = new ClientHandshake(AContext.Header);
                // See if our header had the required information
                if (AHandshake.IsValid())
                {
                    // Optionally check Origin and Location if they're set.
                    if (Origin != string.Empty)
                        if (AHandshake.Origin != "http://" + Origin)
                            return false;
                    if (Location != string.Empty)
                        if (AHandshake.Host != Location + ":" + AContext.Server.Port.ToString())
                            return false;
                    // Generate response handshake for the client
                    ServerHandshake ServerShake = GenerateResponseHandshake(AHandshake, AContext);
                    ServerShake.SubProtocol = AHandshake.SubProtocol;
                    // Send the response handshake
                    SendServerHandshake(ServerShake, AContext);
                    return true;
                }
            }
            return false;
        }

        private static ServerHandshake GenerateResponseHandshake(ClientHandshake AHandshake, Context AContext)
        {
            ServerHandshake AResponseHandshake = new ServerHandshake();
            AResponseHandshake.Accept = GenerateAccept(AHandshake.Key, AContext);
            return AResponseHandshake;
        }
        
        private static void SendServerHandshake(ServerHandshake AHandshake, Context AContext)
        {
            // generate a byte array representation of the handshake including the answer to the challenge
            string temp = AHandshake.ToString();
            byte[] HandshakeBytes = AContext.UserContext.Encoding.GetBytes(temp);
            AContext.UserContext.SendRaw(HandshakeBytes);
        }

        private static string GenerateAccept(string Key, Context AContext)
        {
            string RawAnswer = Key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

            // Create a hash of the RawAnswer and return it
            SHA1 Hasher = SHA1.Create();
            return Convert.ToBase64String(Hasher.ComputeHash(AContext.UserContext.Encoding.GetBytes(RawAnswer)));
        }
    }
}
