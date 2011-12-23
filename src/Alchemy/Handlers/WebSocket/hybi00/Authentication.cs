using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Alchemy.Classes;

namespace Alchemy.Handlers.WebSocket.hybi00
{
    /// <summary>
    /// Handles the handshaking between the client and the host, when a new connection is created
    /// </summary>
    internal class Authentication : Handlers.Authentication
    {
        protected override bool CheckAuthentication(Context context)
        {
            if (context.ReceivedByteCount > 8)
            {
                var handshake =
                    new ClientHandshake(new ArraySegment<byte>(context.Buffer, context.ReceivedByteCount - 8, 8),
                                        context.Header);
                // See if our header had the required information
                if (handshake.IsValid())
                {
                    // Optionally check Origin and Location if they're set.
                    if (Origin != string.Empty)
                    {
                        if (handshake.Origin != "http://" + Origin)
                        {
                            return false;
                        }
                    }
                    if (Destination != string.Empty)
                    {
                        if (handshake.Host != Destination + ":" + context.Server.Port.ToString())
                        {
                            return false;
                        }
                    }
                    // Generate response handshake for the client
                    ServerHandshake serverShake = GenerateResponseHandshake(handshake);
                    // Send the response handshake
                    SendServerHandshake(serverShake, context);
                    return true;
                }
            }
            return false;
        }

        private static ServerHandshake GenerateResponseHandshake(ClientHandshake handshake)
        {
            var responseHandshake = new ServerHandshake
            {
                Location = "ws://" + handshake.Host + handshake.ResourcePath,
                Origin = handshake.Origin,
                SubProtocol = handshake.SubProtocol,
                AnswerBytes = GenerateAnswerBytes(handshake.Key1, handshake.Key2, handshake.ChallengeBytes)
            };

            return responseHandshake;
        }

        private static void SendServerHandshake(ServerHandshake handshake, Context context)
        {
            // generate a byte array representation of the handshake including the answer to the challenge
            byte[] handshakeBytes = Encoding.UTF8.GetBytes(handshake.ToString());
            Array.Copy(handshake.AnswerBytes, 0, handshakeBytes, handshakeBytes.Length - 16, 16);

            context.UserContext.Send(handshakeBytes, true);
        }

        private static byte[] TranslateKey(string key)
        {
            //  Count total spaces in the keys
            int keySpaceCount = key.Count(x => x == ' ');

            // Get a number which is a concatenation of all digits in the keys.
            var keyNumberString = new String(key.Where(Char.IsDigit).ToArray());

            // Divide the number with the number of spaces
            var keyResult = (Int32) (Int64.Parse(keyNumberString)/keySpaceCount);

            // convert the results to 32 bit big endian byte arrays
            byte[] keyResultBytes = BitConverter.GetBytes(keyResult);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(keyResultBytes);
            }
            return keyResultBytes;
        }

        private static byte[] GenerateAnswerBytes(string key1, string key2, ArraySegment<byte> challenge)
        {
            // Translate the two keys, concatenate them and the 8 challenge bytes from the client
            var rawAnswer = new byte[16];
            Array.Copy(TranslateKey(key1), 0, rawAnswer, 0, 4);
            Array.Copy(TranslateKey(key2), 0, rawAnswer, 4, 4);
            Array.Copy(challenge.Array, challenge.Offset, rawAnswer, 8, 8);

            // Create a hash of the rawAnswer and return it
            MD5 hasher = MD5.Create();
            return hasher.ComputeHash(rawAnswer);
        }
    }
}