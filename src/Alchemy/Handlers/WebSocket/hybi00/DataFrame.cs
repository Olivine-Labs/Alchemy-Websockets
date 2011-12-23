using System;
using System.Collections.Generic;

namespace Alchemy.Handlers.WebSocket.hybi00
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
        /// Accepts a byte array.
        /// </summary>
        /// <returns>The data array wrapped in WebSocket DataFrame Start/End qualifiers.</returns>
        public override List<ArraySegment<Byte>> AsFrame()
        {
            if (Format == DataFormat.Raw)
            {
                // wrap the array with the wrapper bytes
                var startBytes = new byte[1];
                startBytes[0] = StartByte;
                var endBytes = new byte[1];
                endBytes[0] = EndByte;
                Payload.Insert(0, new ArraySegment<byte>(startBytes)); //Add header byte
                Payload.Add(new ArraySegment<byte>(endBytes)); //put termination byte at end
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
                Payload.RemoveAt(0); //remove header byte
                Payload.RemoveAt(Payload.Count - 1); //remove termination byte
                Format = DataFormat.Raw;
            }
            return Payload;
        }

        /// <summary>
        /// Appends the specified data to the internal byte buffer.
        /// </summary>
        /// <param name="data">The data.</param>
        /// /// <param name="asFrame">For internal Alchemy use.</param>
        public override void Append(byte[] data, bool asFrame = false)
        {
            if (asFrame)
            {
                Format = DataFormat.Frame;
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
                        var startBytes = new byte[1];
                        startBytes[0] = StartByte;
                        Payload.Add(new ArraySegment<byte>(startBytes));
                        start++; // Do not include the Start Byte
                    }
                    else //If no match found, default.
                    {
                        start = 0;
                    }

                    var temp = new byte[end - start];
                    Array.Copy(data, start, temp, 0, end - start);
                    Payload.Add(new ArraySegment<byte>(temp));
                    if (State == DataState.Complete)
                    {
                        var endBytes = new byte[1];
                        endBytes[0] = EndByte;
                        Payload.Add(new ArraySegment<byte>(endBytes));
                    }
                }
            }
            else
            {
                Format = DataFormat.Raw;
                var temp = new byte[data.Length];
                Array.Copy(data, 0, temp, 0, data.Length);
                Payload.Add(new ArraySegment<byte>(temp));
            }
        }
    }
}