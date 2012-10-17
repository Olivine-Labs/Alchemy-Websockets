using System;
using System.Collections.Generic;

namespace Alchemy.Handlers.WebSocket.rfc6455
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
                switch (State)
                {
                    case DataState.Pong:
                        _header.OpCode = OpCode.Pong;
                        //Setup Opcode for Pong frame if application has specified that we're sending a pong.
                        break;
                }
                byte[] headerBytes = _header.ToBytes(IsByte);
                //Mask(); //Uses _header, must call ToBytes before calling Mask
                Payload.Insert(0, new ArraySegment<byte>(headerBytes)); //put header at first position
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
                Payload.RemoveAt(0); //Remove header bytes
                Mask(); //unmask data
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
                    byte[] headerBytes = _header.FromBytes(someBytes);
                    Payload.Add(new ArraySegment<byte>(headerBytes));
                    int dataStart = headerBytes.Length;
                    data =
                        new byte[
                            Math.Min(Convert.ToInt32(Math.Min(_header.PayloadSizeRemaining, int.MaxValue)),
                                     someBytes.Length - dataStart)];
                    dataLength = Math.Min(_header.PayloadSizeRemaining, Convert.ToUInt64(someBytes.Length - dataStart));
                    Array.Copy(someBytes, dataStart, data, 0, Convert.ToInt32(dataLength));
                    Format = DataFormat.Frame;
                }
                else
                {
                    dataLength = Math.Min(Convert.ToUInt64(data.Length), _header.PayloadSizeRemaining);
                    data = new byte[dataLength];
                    Array.Copy(someBytes, 0, data, 0, Convert.ToInt32(dataLength));
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