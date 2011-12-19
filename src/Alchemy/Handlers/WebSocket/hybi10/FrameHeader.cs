using System;
using System.Collections.Generic;
using System.Linq;

namespace Alchemy.Handlers.WebSocket.hybi10
{
    internal class FrameHeader
    {
        public const byte EndBit = 0x80;
        public byte CurrentMaskIndex;
        public bool IsEnd = true;
        public bool IsMasked;
        public int Mask;
        public DataFrame.OpCode OpCode = DataFrame.OpCode.Close;

        public UInt64 PayloadSize;
        public UInt64 PayloadSizeRemaining;

        public byte[] FromBytes(byte[] data)
        {
            int dataBegin = 2;
            var nibble2 = (byte) (data[0] & 0x0F);
            var nibble1 = (byte) (data[0] & 0xF0);

            if ((nibble1 & EndBit) == EndBit)
            {
                IsEnd = true;
            }


            //Combine bytes to form one large number
            PayloadSize = (byte) (data[1] & 0x7F);
            if (PayloadSize == 126)
            {
                Array.Reverse(data, dataBegin, 2);
                PayloadSize = BitConverter.ToUInt16(data, dataBegin);
                dataBegin += 2;
            }
            else if (PayloadSize == 127)
            {
                Array.Reverse(data, dataBegin, 8);
                PayloadSize = BitConverter.ToUInt64(data, dataBegin);
                dataBegin += 8;
            }
            PayloadSizeRemaining = PayloadSize;
            IsMasked = Convert.ToBoolean((data[1] & 0x80) >> 7);
            Mask = 0;
            CurrentMaskIndex = 0;
            if (IsMasked)
            {
                Mask = BitConverter.ToInt32(data, dataBegin);
                dataBegin += 4;
            }

            OpCode = (DataFrame.OpCode) nibble2;
            var someBytes = new byte[dataBegin];
            Array.Copy(data, 0, someBytes, 0, dataBegin);
            return someBytes;
        }

        public byte[] ToBytes()
        {
            // wrap the array with the wrapper bytes
            var headerBytes = new List<Byte[]>();
            var data = new byte[1];
            data[0] = 0x81;
            headerBytes.Add(data);
            if (PayloadSize <= 125)
            {
                data = new byte[1];
                data[0] = (byte) PayloadSize;
                data[0] = (byte) (data[0] | 0x80); //Tells us that this data is masked
                headerBytes.Add(data);
            }
            else
            {
                if (PayloadSize <= ushort.MaxValue)
                {
                    data = new byte[1];
                    data[0] = 126;
                    data[0] = (byte) (data[0] | 0x80); //Tells us that this data is masked
                    headerBytes.Add(data);

                    data = BitConverter.GetBytes(Convert.ToInt16(PayloadSize));
                    Array.Reverse(data);
                    headerBytes.Add(data);
                }
                else
                {
                    data = new byte[1];
                    data[0] = 127;
                    data[0] = (byte) (data[0] | 0x80); //Tells us that this data is masked
                    headerBytes.Add(data);
                    data = BitConverter.GetBytes(PayloadSize);
                    Array.Reverse(data);
                    headerBytes.Add(data);
                }
            }

            var random = new Random();
            Mask = random.Next(Int32.MaxValue);
            headerBytes.Add(BitConverter.GetBytes(Mask));
            return headerBytes.SelectMany(a => a).ToArray();
        }
    }
}