using System;
using System.Collections.Generic;
using System.Linq;

namespace Alchemy.Handlers.WebSocket.rfc6455
{
    internal class FrameHeader
    {
        public const byte EndBit = 0x80;
        public byte CurrentMaskIndex;
        public bool IsEnd = true;
        public bool IsMasked;
        public int Mask;
        public DataFrame.OpCode OpCode = DataFrame.OpCode.Close;

        public long PayloadSize;
        public long PayloadSizeRemaining;

        private const int MaxHeaderLength = 14;
        private byte[] _headerBuffer = new byte[MaxHeaderLength];
        private int _bufferedHeaderBytes;

        public byte[] FromBytes(byte[] data, int byteCount, out int dataStartIndex)
        {
            Array.Copy(data, 0, _headerBuffer, _bufferedHeaderBytes, MaxHeaderLength - _bufferedHeaderBytes);
            byteCount += _bufferedHeaderBytes;
            dataStartIndex = -1;

            int dataBegin = 2;
            var nibble2 = (byte)(_headerBuffer[0] & 0x0F);
            var nibble1 = (byte)(_headerBuffer[0] & 0xF0);

            IsEnd = ((nibble1 & EndBit) == EndBit);
            IsMasked = Convert.ToBoolean((_headerBuffer[1] & 0x80) >> 7);
            int minCount = 2;
            if (IsMasked) minCount += 4;

            //Combine bytes to form one large number
            PayloadSize = (byte)(_headerBuffer[1] & 0x7F);

            switch (PayloadSize)
            {
                case 126:
                    if (byteCount < minCount + 2) return PartialHeader(byteCount); // return before modifying the header
                    Array.Reverse(_headerBuffer, dataBegin, 2);
                    PayloadSize = BitConverter.ToUInt16(_headerBuffer, dataBegin);
                    dataBegin += 2;
                    break;
                case 127:
                    if (byteCount < minCount + 8) return PartialHeader(byteCount);
                    Array.Reverse(_headerBuffer, dataBegin, 8);
                    PayloadSize = BitConverter.ToInt64(_headerBuffer, dataBegin);
                    dataBegin += 8;
                    break;
                default:
                    if (byteCount < minCount) return PartialHeader(byteCount);
                    break;
            }

            PayloadSizeRemaining = PayloadSize;
            Mask = 0;
            CurrentMaskIndex = 0;

            if (IsMasked)
            {
                Mask = BitConverter.ToInt32(_headerBuffer, dataBegin);
                dataBegin += 4;
            }

            OpCode = (DataFrame.OpCode)nibble2;
            var someBytes = new byte[dataBegin];
            Array.Copy(_headerBuffer, 0, someBytes, 0, dataBegin);
            dataStartIndex = dataBegin - _bufferedHeaderBytes;
            _bufferedHeaderBytes = 0;
            return someBytes;
        }

        private byte[] PartialHeader(int byteCount)
        {
            // keep partial header and wait for next received bytes
            _bufferedHeaderBytes = byteCount;
            return null;
        }

        
        public byte[] ToBytes(bool isBinary = false)
        {
            // wrap the array with the wrapper bytes
            var headerBytes = new List<Byte[]>();
            var data = new byte[1];
            
            if(isBinary)
                data[0] = 0x82;
            else
                data[0] = 0x81;

            headerBytes.Add(data);
            if (PayloadSize <= 125)
            {
                data = new byte[1];
                data[0] = (byte) PayloadSize;
                //data[0] = (byte) (data[0] | 0x80); //Tells us that this data is masked
                headerBytes.Add(data);
            }
            else
            {
                if (PayloadSize <= ushort.MaxValue)
                {
                    data = new byte[1];
                    data[0] = 126;
                    //data[0] = (byte) (data[0] | 0x80); //Tells us that this data is masked
                    headerBytes.Add(data);

                    data = BitConverter.GetBytes(Convert.ToUInt16(PayloadSize));
                    Array.Reverse(data);
                    headerBytes.Add(data);
                }
                else
                {
                    data = new byte[1];
                    data[0] = 127;
                    //data[0] = (byte) (data[0] | 0x80); //Tells us that this data is masked
                    headerBytes.Add(data);
                    data = BitConverter.GetBytes(PayloadSize);
                    Array.Reverse(data);
                    headerBytes.Add(data);
                }
            }

            //var random = new Random();
            //Mask = random.Next(Int32.MaxValue);
            //headerBytes.Add(BitConverter.GetBytes(Mask));
            return headerBytes.SelectMany(a => a).ToArray();
        }
    }
}