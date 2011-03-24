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
using System.Security.Cryptography;
using System.Web;
using Alchemy.Server.Classes;

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
                ClientHandshake AHandshake = new ClientHandshake(new ArraySegment<byte>(AContext.Buffer, AContext.ReceivedByteCount-8, 8), AContext.Header);
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
                    ServerHandshake ServerShake = GenerateResponseHandshake(AHandshake);
                    // Send the response handshake
                    SendServerHandshake(ServerShake, AContext);
                    return true;
                }
            }
            return false;
        }

        private static ServerHandshake GenerateResponseHandshake(ClientHandshake AHandshake)
        {
            ServerHandshake AResponseHandshake = new ServerHandshake();
            AResponseHandshake.Location = "ws://" + AHandshake.Host + AHandshake.ResourcePath;
            AResponseHandshake.Origin = AHandshake.Origin;
            AResponseHandshake.SubProtocol = AHandshake.SubProtocol;
            AResponseHandshake.AnswerBytes = GenerateAnswerBytes(AHandshake.Key1, AHandshake.Key2, AHandshake.ChallengeBytes);

            return AResponseHandshake;
        }
        
        private static void SendServerHandshake(ServerHandshake AHandshake, Context AContext)
        {
            // generate a byte array representation of the handshake including the answer to the challenge
            byte[] HandshakeBytes = AContext.UserContext.Encoding.GetBytes(AHandshake.ToString());
            Array.Copy(AHandshake.AnswerBytes, 0, HandshakeBytes, HandshakeBytes.Length-16, 16);

            AContext.UserContext.SendRaw(HandshakeBytes);
        }

        private static byte[] TranslateKey(string AKey)
        {
            //  Count total spaces in the keys
            int KeySpaceCount = AKey.Count(x => x == ' ');

            // Get a number which is a concatenation of all digits in the keys.
            string KeyNumberString = new String(AKey.Where(x => Char.IsDigit(x)).ToArray());

            // Divide the number with the number of spaces
            Int32 KeyResult = (Int32)(Int64.Parse(KeyNumberString) / KeySpaceCount);

            // convert the results to 32 bit big endian byte arrays
            byte[] KeyResultBytes = BitConverter.GetBytes(KeyResult);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(KeyResultBytes);
            }
            return KeyResultBytes;
        }
        
        private static byte[] GenerateAnswerBytes(string Key1, string Key2, ArraySegment<byte> AChallenge)
        {
            // Translate the two keys, concatenate them and the 8 challenge bytes from the client
            byte[] RawAnswer = new byte[16];
            Array.Copy(TranslateKey(Key1), 0, RawAnswer, 0, 4);
            Array.Copy(TranslateKey(Key2), 0, RawAnswer, 4, 4);
            Array.Copy(AChallenge.Array, AChallenge.Offset, RawAnswer, 8, 8);

            // Create a hash of the RawAnswer and return it
            MD5 Hasher = MD5.Create();
            return Hasher.ComputeHash(RawAnswer);
        }
    }
}
