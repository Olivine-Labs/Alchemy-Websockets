﻿/*
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
using System.Text;


namespace Alchemy.Server.Handlers.WebSocket.hybi10
{
    /// <summary>
    /// Simple WebSocket data Frame implementation. 
    /// Automatically manages adding received data to an existing frame and checking whether or not we've received the entire frame yet.
    /// See http://www.whatwg.org/specs/web-socket-protocol/ for more details on the WebSocket Protocol.
    /// </summary>
    public class DataFrame : Alchemy.Server.Classes.DataFrame
    {
        public enum OpCode
        {
            Continue    = 0x0,
            Text        = 0x1,
            Binary      = 0x2,
            Close       = 0x8,
            Ping        = 0x9,
            Pong        = 0xA
        }

        private const byte _continueBit  = 0x0;
        private const byte _endBit       = 0x80;

        /// <summary>
        /// Wraps the specified data in WebSocket Start/End Bytes.
        /// Accepts a byte array.
        /// </summary>
        /// <param name="data">The data.</param>
        /// <returns>The data array wrapped in WebSocket DataFrame Start/End qualifiers.</returns>
        public override byte[] Wrap(byte[] data)
        {
            byte[] wrappedBytes = null;

            if (data.Length > 0)
            {
                // wrap the array with the wrapper bytes
                int startIndex = 2;
                byte[] headerBytes = new byte[14];
                headerBytes[0] = 0x81;
                if (data.Length <= 125)
                {
                    headerBytes[1] = (byte)data.Length;
                }
                else
                {
                    if (data.Length <= ushort.MaxValue)
                    {
                        headerBytes[1] = 126;
                        byte[] extendedLength = BitConverter.GetBytes((UInt16)data.Length);
                        headerBytes[2] = extendedLength[1];
                        headerBytes[3] = extendedLength[0];
                        startIndex = 4;
                    }
                    else
                    {
                        headerBytes[1] = 127;
                        byte[] extendedLength = BitConverter.GetBytes((UInt64)data.Length);
                        headerBytes[2] = extendedLength[7];
                        headerBytes[3] = extendedLength[6];
                        headerBytes[4] = extendedLength[5];
                        headerBytes[5] = extendedLength[4];
                        headerBytes[6] = extendedLength[3];
                        headerBytes[7] = extendedLength[2];
                        headerBytes[8] = extendedLength[1];
                        headerBytes[9] = extendedLength[0];
                        startIndex = 10;
                    }
                }
                headerBytes[1] = (byte)(headerBytes[1] | 0x80);

                Random random = new Random();
                int key = random.Next(Int32.MaxValue);
                Array.Copy(BitConverter.GetBytes(key), 0, headerBytes, startIndex, 4);
                startIndex += 4;

                byte[] maskedData=Mask(data, key);

                wrappedBytes = new byte[data.Length + startIndex];
                Array.Copy(headerBytes, 0, wrappedBytes, 0, startIndex);
                Array.Copy(maskedData, 0, wrappedBytes, startIndex, maskedData.Length);
            }
            else
            {
                wrappedBytes = new byte[1];
                wrappedBytes[0] = 0x0;
            }
            return wrappedBytes;
        }

        /// <summary>
        /// Appends the specified data to the internal byte buffer.
        /// </summary>
        /// <param name="data">The data.</param>
        public override void Append(byte[] data)
        {
            if (data.Length > 0)
            {
                byte nibble2 = (byte)(data[0] & 0x0F);
                byte nibble1 = (byte)(data[0] & 0xF0);

                if ((nibble1 & _endBit) == _endBit)
                    _state = DataState.Complete;


                //Combine bytes to form one large number
                int startIndex = 2;
                Int64 dataLength = (byte)(data[1] & 0x7F);
                if (dataLength == 126)
                {
                    dataLength = data[startIndex] * 256; 
                    dataLength += data[startIndex + 1]; 
                    startIndex = 4;
                }
                else if (dataLength == 127)
                {
                    dataLength = data[startIndex] * 16777216; 
                    dataLength += data[startIndex + 1] * 65536; 
                    dataLength += data[startIndex + 2] * 256; 
                    dataLength += data[startIndex + 3]; 
                    startIndex = 10;
                }

                bool masked = Convert.ToBoolean((data[1] & 0x80) >> 7);
                int maskingKey = 0;
                if (masked)
                {
                    maskingKey = BitConverter.ToInt32(data, startIndex);
                    startIndex = startIndex + 4;
                }

                byte[] payload = new byte[dataLength];
                Array.Copy(data, (int)startIndex, payload, 0, (int)dataLength);
                if(masked)
                    payload = Mask(payload, maskingKey);

                OpCode currentFrameOpcode = (OpCode)nibble2;
                switch (currentFrameOpcode)
                {
                    case OpCode.Continue:
                    case OpCode.Binary:
                    case OpCode.Text:
                        AppendDataToFrame(payload);
                        break;
                    case OpCode.Close:
                        _state = DataState.Closed;
                        break;
                    case OpCode.Ping:
                        _state = DataState.Ping;
                        break;
                    case OpCode.Pong:
                        _state = DataState.Pong;
                        break;
                }
            }
        }

        /// <summary>
        /// Appends the data to frame. Manages recreating the byte array and such.
        /// </summary>
        /// <param name="SomeBytes">Some bytes.</param>
        /// <param name="Start">The start index.</param>
        /// <param name="End">The end index.</param>
        private void AppendDataToFrame(byte[] someBytes)
        {
            int currentFrameLength = 0;
            if (_rawFrame != null)
                currentFrameLength = _rawFrame.Length;
            byte[] newFrame = new byte[currentFrameLength + someBytes.Length];
            if(currentFrameLength > 0)
                Array.Copy(_rawFrame, 0, newFrame, 0, currentFrameLength);
            Array.Copy(someBytes, 0, newFrame, currentFrameLength, someBytes.Length);
            _rawFrame = newFrame;
        }

        private static byte[] Mask(byte[] someBytes, Int32 key)
        {
            byte[] newBytes = new byte[someBytes.Length];
            byte[] byteKeys = BitConverter.GetBytes(key);
            for(int index = 0; index < someBytes.Length; index++)
            {
                int KeyIndex = index % 4;
                newBytes[index] = (byte)(someBytes[index]^byteKeys[KeyIndex]);
            }
            return newBytes;
        }
    }
}