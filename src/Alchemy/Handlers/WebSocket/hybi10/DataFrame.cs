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

namespace Alchemy.Handlers.WebSocket.hybi10
{
    /// <summary>
    /// Simple WebSocket data Frame implementation. 
    /// Automatically manages adding received data to an existing frame and checking whether or not we've received the entire frame yet.
    /// See http://www.whatwg.org/specs/web-socket-protocol/ for more details on the WebSocket Protocol.
    /// </summary>
    public class DataFrame : WebSocket.DataFrame
    {
        #region OpCode enum

        public enum OpCode
        {
            Continue = 0x0,
            Text = 0x1,
            Binary = 0x2,
            Close = 0x8,
            Ping = 0x9,
            Pong = 0xA
        }

        #endregion

        private readonly FrameHeader _header = new FrameHeader();

        public override WebSocket.DataFrame CreateInstance()
        {
            return new DataFrame();
        }

        /// <summary>
        /// Wraps the specified data in WebSocket Start/End Bytes.
        /// Accepts a byte array.
        /// </summary>
        /// <returns>The data array wrapped in WebSocket DataFrame Start/End qualifiers.</returns>
        public override List<ArraySegment<Byte>> AsFrame()
        {
            if (Format == DataFormat.Raw)
            {
                _header.PayloadSize = Length;
                byte[] headerBytes = _header.ToBytes();
                Mask();
                Payload.Insert(0, new ArraySegment<byte>(headerBytes));
                Format = DataFormat.Frame;
            }
            return Payload;
        }

        /// <summary>
        /// Returns the data as raw
        /// </summary>
        public override List<ArraySegment<Byte>> AsRaw()
        {
            if (Format == DataFormat.Frame)
            {
                Payload.RemoveAt(0);
                Mask();
                Format = DataFormat.Raw;
            }
            return Payload;
        }

        private void Mask()
        {
            foreach (var t in Payload)
            {
                Mask(t.Array);
            }
            _header.CurrentMaskIndex = 0;
        }


        private void Mask(byte[] someBytes)
        {
            byte[] byteKeys = BitConverter.GetBytes(_header.Mask);
            for (int index = 0; index < someBytes.Length; index++)
            {
                someBytes[index] = (byte) (someBytes[index] ^ byteKeys[_header.CurrentMaskIndex]);
                if (_header.CurrentMaskIndex == 3)
                {
                    _header.CurrentMaskIndex = 0;
                }
                else
                {
                    _header.CurrentMaskIndex++;
                }
            }
        }

        public override void Append(byte[] someBytes, bool asFrame = false)
        {
            byte[] data = someBytes;
            if (asFrame)
            {
                UInt64 dataLength;
                if (InternalState == DataState.Empty)
                {
                    var headerBytes = _header.FromBytes(someBytes);
                    Payload.Add(new ArraySegment<byte>(headerBytes));
                    int dataStart = headerBytes.Length;
                    data = new byte[Math.Min(Convert.ToInt32(Math.Min(_header.PayloadSizeRemaining, int.MaxValue)), someBytes.Length)];
                    dataLength = Math.Min(_header.PayloadSizeRemaining, Convert.ToUInt64(someBytes.Length - dataStart));
                    Array.Copy(someBytes, dataStart, data, 0, Convert.ToInt32(dataLength));
                    Format = DataFormat.Frame;
                }
                else
                {
                    dataLength = Math.Min(Convert.ToUInt64(data.Length), _header.PayloadSizeRemaining);
                    if (dataLength < Convert.ToUInt64(data.Length))
                    {
                        data = new byte[dataLength];
                        Array.Copy(someBytes, 0, data, 0, Convert.ToInt32(dataLength));
                    }
                }

                _header.PayloadSizeRemaining -= dataLength;
                switch (_header.OpCode)
                {
                    case OpCode.Close:
                        InternalState = DataState.Closed;
                        break;
                    case OpCode.Ping:
                        InternalState = DataState.Ping;
                        break;
                    case OpCode.Pong:
                        InternalState = DataState.Pong;
                        break;
                    default:
                        InternalState = _header.PayloadSizeRemaining == 0 ? DataState.Complete : DataState.Receiving;
                        break;
                }
            }
            else
            {
                Format = DataFormat.Raw;
            }
            Payload.Add(new ArraySegment<byte>(data));
        }
    }
}