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


namespace Alchemy.Server.Handlers.WebSocket.hybi10
{
    /// <summary>
    /// Simple WebSocket Data Frame implementation. 
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

        private const byte ContinueBit  = 0x0;
        private const byte EndBit       = 0x80;

        /// <summary>
        /// Wraps the specified data in WebSocket Start/End Bytes.
        /// Accepts a byte array.
        /// </summary>
        /// <param name="Data">The data.</param>
        /// <returns>The Data array wrapped in WebSocket DataFrame Start/End qualifiers.</returns>
        public override byte[] Wrap(byte[] Data)
        {
            byte[] WrappedBytes = null;

            if (Data.Length > 0)
            {
                // wrap the array with the wrapper bytes
                int StartIndex = 2;
                byte[] HeaderBytes = new byte[14];
                HeaderBytes[0] = 0x81;
                if (Data.Length <= 125)
                {
                    HeaderBytes[1] = (byte)Data.Length;
                }
                else
                {
                    if (Data.Length <= ushort.MaxValue)
                    {
                        HeaderBytes[1] = 126;
                        byte[] extendedLength = BitConverter.GetBytes((UInt16)Data.Length);
                        HeaderBytes[2] = extendedLength[1];
                        HeaderBytes[3] = extendedLength[0];
                        StartIndex = 4;
                    }
                    else
                    {
                        HeaderBytes[1] = 127;
                        byte[] extendedLength = BitConverter.GetBytes((UInt64)Data.Length);
                        HeaderBytes[2] = extendedLength[7];
                        HeaderBytes[3] = extendedLength[6];
                        HeaderBytes[4] = extendedLength[5];
                        HeaderBytes[5] = extendedLength[4];
                        HeaderBytes[6] = extendedLength[3];
                        HeaderBytes[7] = extendedLength[2];
                        HeaderBytes[8] = extendedLength[1];
                        HeaderBytes[9] = extendedLength[0];
                        StartIndex = 10;
                    }
                }
                HeaderBytes[1] = (byte)(HeaderBytes[1] | 0x80);

                Random ARandom = new Random();
                int Key = ARandom.Next(Int32.MaxValue);
                Array.Copy(BitConverter.GetBytes(Key), 0, HeaderBytes, StartIndex, 4);
                StartIndex += 4;

                Mask(ref Data, Key);

                WrappedBytes = new byte[Data.Length + StartIndex];
                Array.Copy(HeaderBytes, 0, WrappedBytes, 0, StartIndex);
                Array.Copy(Data, 0, WrappedBytes, StartIndex, Data.Length);
                Console.WriteLine(Encoding.UTF8.GetString(WrappedBytes));
            }
            else
            {
                WrappedBytes = new byte[1];
                WrappedBytes[0] = 0x0;
            }
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
                byte Nibble2 = (byte)(Data[0] & 0x0F);
                byte Nibble1 = (byte)(Data[0] & 0xF0);

                if ((Nibble1 & EndBit) == EndBit)
                    _State = DataState.Complete;


                //Combine bytes to form one large number
                int StartIndex = 2;
                Int64 DataLength = 0;
                DataLength = (byte)(Data[1] & 0x7F);
                if (DataLength == 126)
                {
                    BitConverter.ToInt16(Data, StartIndex);
                    StartIndex = 4;
                }
                else if (DataLength == 127)
                {
                    BitConverter.ToInt64(Data, StartIndex);
                    StartIndex = 10;
                }

                bool Masked = Convert.ToBoolean((Data[1] & 0x80) >> 7);
                int MaskingKey = 0;
                if (Masked)
                {
                    MaskingKey = BitConverter.ToInt32(Data, StartIndex);
                    StartIndex = StartIndex + 4;
                }

                byte[] Payload = new byte[DataLength];
                Array.Copy(Data, (int)StartIndex, Payload, 0, (int)DataLength);
                if(Masked)
                    Mask(ref Payload, MaskingKey);

                OpCode CurrentFrameOpcode = (OpCode)Nibble2;
                switch (CurrentFrameOpcode)
                {
                    case OpCode.Continue:
                    case OpCode.Binary:
                    case OpCode.Text:
                        AppendDataToFrame(Payload);
                        break;
                    case OpCode.Close:
                        _State = DataState.Closed;
                        break;
                    case OpCode.Ping:
                        _State = DataState.Ping;
                        break;
                    case OpCode.Pong:
                        _State = DataState.Pong;
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
        private void AppendDataToFrame(byte[] SomeBytes)
        {
            int CurrentFrameLength = 0;
            if (RawFrame != null)
                CurrentFrameLength = RawFrame.Length;
            byte[] NewFrame = new byte[CurrentFrameLength + SomeBytes.Length];
            if(CurrentFrameLength > 0)
                Array.Copy(RawFrame, 0, NewFrame, 0, CurrentFrameLength);
            Array.Copy(SomeBytes, 0, NewFrame, CurrentFrameLength, SomeBytes.Length);
            RawFrame = NewFrame;
        }

        private static void Mask(ref byte[] SomeBytes, Int32 Key)
        {
            byte[] ByteKeys = BitConverter.GetBytes(Key);
            for(int Index = 0; Index < SomeBytes.Length; Index++)
            {
                int KeyIndex = Index % 4;
                SomeBytes[Index] = (byte)(SomeBytes[Index]^ByteKeys[KeyIndex]);
            }
        }
    }
}