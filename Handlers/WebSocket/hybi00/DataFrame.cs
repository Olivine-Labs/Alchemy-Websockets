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

﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;


namespace Alchemy.Server.Handlers.WebSocket.hybi00
{
    /// <summary>
    /// Simple WebSocket Data Frame implementation. 
    /// Automatically manages adding received data to an existing frame and checking whether or not we've received the entire frame yet.
    /// See http://www.whatwg.org/specs/web-socket-protocol/ for more details on the WebSocket Protocol.
    /// </summary>
    public class DataFrame : Alchemy.Server.Classes.DataFrame
    {
        public const byte StartByte = 0;
        public const byte EndByte = 255;

        /// <summary>
        /// Wraps the specified data in WebSocket Start/End Bytes.
        /// Accepts a byte array.
        /// </summary>
        /// <param name="Data">The data.</param>
        /// <returns>The Data array wrapped in WebSocket DataFrame Start/End qualifiers.</returns>
        public override byte[] Wrap(byte[] Data)
        {
            // wrap the array with the wrapper bytes
            byte[] WrappedBytes = new byte[Data.Length + 2];
            WrappedBytes[0] = StartByte;
            WrappedBytes[WrappedBytes.Length - 1] = EndByte;
            Array.Copy(Data, 0, WrappedBytes, 1, Data.Length);
            return WrappedBytes;
        }

        /// <summary>
        /// Appends the specified data to the internal byte buffer.
        /// </summary>
        /// <param name="Data">The data.</param>
        public override void Append(byte[] Data)
        {
            if (Data.Length > 0)
            {
                int End = Array.IndexOf(Data, EndByte);
                if (End != -1)
                {
                    _State = DataState.Complete;
                }
                else //If no match found, default.
                {
                    End = Data.Length;
                    _State = DataState.Receiving;
                }

                int Start = Array.IndexOf(Data, StartByte);
                if ((Start != -1) && (Start < End)) // Make sure the start is before the end and that we actually found a match.
                {
                    Start++; // Do not include the Start Byte
                }
                else //If no match found, default.
                {
                    Start = 0;
                }

                AppendDataToFrame(Data, Start, End);
            }
        }

        /// <summary>
        /// Appends the data to frame. Manages recreating the byte array and such.
        /// </summary>
        /// <param name="SomeBytes">Some bytes.</param>
        /// <param name="Start">The start index.</param>
        /// <param name="End">The end index.</param>
        private void AppendDataToFrame(byte[] SomeBytes, int Start, int End)
        {
            int CurrentFrameLength = 0;
            if (RawFrame != null)
                CurrentFrameLength = RawFrame.Length;
            byte[] NewFrame = new byte[CurrentFrameLength + (End - Start)];
            if (CurrentFrameLength > 0)
                Array.Copy(RawFrame, 0, NewFrame, 0, CurrentFrameLength);
            Array.Copy(SomeBytes, Start, NewFrame, CurrentFrameLength, End - Start);
            RawFrame = NewFrame;
        }
    }
}