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

namespace Alchemy.Server.Handlers.WebSocket.hybi00
{
    /// <summary>
    /// Simple WebSocket data Frame implementation. 
    /// Automatically manages adding received data to an existing frame and checking whether or not we've received the entire frame yet.
    /// See http://www.whatwg.org/specs/web-socket-protocol/ for more details on the WebSocket Protocol.
    /// </summary>
    public class DataFrame : WebSocket.DataFrame
    {
        public const byte StartByte = 0;
        public const byte EndByte = 255;

        public override WebSocket.DataFrame CreateInstance()
        {
            return new DataFrame();
        }

        /// <summary>
        /// Wraps the specified data in WebSocket Start/End Bytes.
        /// </summary>
        public override void Wrap()
        {
            // wrap the array with the wrapper bytes
            var startBytes = new byte[1];
            var endBytes = new byte[1];
            RawFrame.Insert(0, new ArraySegment<byte>(startBytes));
            RawFrame.Add(new ArraySegment<byte>(endBytes));
        }

        /// <summary>
        /// Appends the specified data to the internal byte buffer.
        /// </summary>
        /// <param name="data">The data.</param>
        public override void Append(byte[] data)
        {
            if (data.Length > 0)
            {
                int end = Array.IndexOf(data, EndByte);
                if (end != -1)
                {
                    InternalState = DataState.Complete;
                }
                else //If no match found, default.
                {
                    end = data.Length;
                    InternalState = DataState.Receiving;
                }

                int start = Array.IndexOf(data, StartByte);
                if ((start != -1) && (start < end))
                    // Make sure the start is before the end and that we actually found a match.
                {
                    start++; // Do not include the Start Byte
                }
                else //If no match found, default.
                {
                    start = 0;
                }

                var temp = new byte[end - start];
                Array.Copy(data, start, temp, 0, end - start);
                AppendDataToFrame(temp);
            }
        }
    }
}