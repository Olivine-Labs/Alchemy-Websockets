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

namespace Alchemy.Server.Handlers.WebSocket.hybi10
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

        private const byte EndBit = 0x80;
        private OpCode _currentFrameOpcode = OpCode.Close;

        private int _currentMask;
        private byte _currentMaskIndex;
        private UInt64 _dataLength;
        private bool _isEnd;
        private bool _masked;
        private UInt64 _remainingDataLength;

        public override WebSocket.DataFrame CreateInstance()
        {
            return new DataFrame();
        }

        /// <summary>
        /// Wraps the specified data in WebSocket Start/End Bytes.
        /// Accepts a byte array.
        /// </summary>
        /// <returns>The data array wrapped in WebSocket DataFrame Start/End qualifiers.</returns>
        public override void Wrap()
        {
            UInt64 dataLength = Length;
            // wrap the array with the wrapper bytes
            int startIndex = 2;
            var headerBytes = new byte[14];
            headerBytes[0] = 0x81;
            if (Length <= 125)
            {
                headerBytes[1] = (byte) dataLength;
            }
            else
            {
                if (Length <= ushort.MaxValue)
                {
                    headerBytes[1] = 126;
                    byte[] extendedLength = BitConverter.GetBytes(dataLength);
                    headerBytes[2] = extendedLength[1];
                    headerBytes[3] = extendedLength[0];
                    startIndex = 4;
                }
                else
                {
                    headerBytes[1] = 127;
                    byte[] extendedLength = BitConverter.GetBytes(dataLength);
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
            headerBytes[1] = (byte) (headerBytes[1] | 0x80);

            var random = new Random();
            _currentMask = random.Next(Int32.MaxValue);
            Array.Copy(BitConverter.GetBytes(_currentMask), 0, headerBytes, startIndex, 4);
            startIndex += 4;
            var newHeaderBytes = new byte[startIndex];
            Array.Copy(headerBytes, 0, newHeaderBytes, 0, startIndex);
            _currentMaskIndex = 0;

            for (int index = 0; index < RawFrame.Count; index++)
            {
                RawFrame[index] = new ArraySegment<byte>(Mask(RawFrame[index].Array));
            }

            RawFrame.Insert(0, new ArraySegment<byte>(headerBytes));
        }

        private int ProcessFrameHeader(byte[] data)
        {
            int startIndex = 2;
            var nibble2 = (byte) (data[0] & 0x0F);
            var nibble1 = (byte) (data[0] & 0xF0);

            if ((nibble1 & EndBit) == EndBit)
            {
                _isEnd = true;
            }


            //Combine bytes to form one large number
            _dataLength = (byte) (data[1] & 0x7F);
            if (_dataLength == 126)
            {
                Array.Reverse(data, startIndex, 2);
                _dataLength = BitConverter.ToUInt16(data, startIndex);
                startIndex = 4;
            }
            else if (_dataLength == 127)
            {
                Array.Reverse(data, startIndex, 8);
                _dataLength = BitConverter.ToUInt64(data, startIndex);
                startIndex = 10;
            }
            _remainingDataLength = _dataLength;
            _masked = Convert.ToBoolean((data[1] & 0x80) >> 7);
            _currentMask = 0;
            _currentMaskIndex = 0;
            if (_masked)
            {
                _currentMask = BitConverter.ToInt32(data, startIndex);
                startIndex = startIndex + 4;
            }

            _currentFrameOpcode = (OpCode) nibble2;
            return startIndex;
        }

        /// <summary>
        /// Appends the specified data to the internal byte buffer.
        /// </summary>
        /// <param name="data">The data.</param>
        public override void Append(byte[] data)
        {
            if (data.Length > 0)
            {
                int startIndex = 0;
                if (State == DataState.Empty || State == DataState.Waiting)
                {
                    startIndex = ProcessFrameHeader(data);
                    InternalState = DataState.Receiving;
                }

                int temp = data.Length;
                if (_remainingDataLength < Convert.ToUInt64(temp))
                {
                    temp = Convert.ToInt32(_remainingDataLength);
                }

                int currentDataLength = Math.Min(temp, data.Length - startIndex);
                byte[] payload;
                if (currentDataLength == data.Length)
                {
                    payload = data;
                }
                else
                {
                    payload = new byte[currentDataLength];
                    Array.Copy(data, startIndex, payload, 0, currentDataLength);
                }
                _remainingDataLength -= Convert.ToUInt64(currentDataLength);

                if (_masked)
                {
                    payload = Mask(payload);
                }

                switch (_currentFrameOpcode)
                {
                    case OpCode.Continue:
                    case OpCode.Binary:
                    case OpCode.Text:
                        AppendDataToFrame(payload);
                        break;
                    case OpCode.Close:
                        InternalState = DataState.Closed;
                        break;
                    case OpCode.Ping:
                        InternalState = DataState.Ping;
                        break;
                    case OpCode.Pong:
                        InternalState = DataState.Pong;
                        break;
                }

                if (_remainingDataLength == 0)
                {
                    InternalState = _isEnd ? DataState.Complete : DataState.Waiting;
                }
            }
        }

        private byte[] Mask(byte[] someBytes, int? mask = null, byte? maskIndex = null)
        {
            if (mask == null)
            {
                mask = _currentMask;
            }
            if (maskIndex == null)
            {
                maskIndex = _currentMaskIndex;
            }

            var newBytes = new byte[someBytes.Length];
            byte[] byteKeys = BitConverter.GetBytes((int) mask);
            for (int index = 0; index < someBytes.Length; index++)
            {
                newBytes[index] = (byte) (someBytes[index] ^ byteKeys[(byte) maskIndex]);
                if (maskIndex == 3)
                {
                    maskIndex = 0;
                }
                else
                {
                    maskIndex++;
                }
            }
            _currentMaskIndex = (byte) maskIndex;
            return newBytes;
        }
    }
}