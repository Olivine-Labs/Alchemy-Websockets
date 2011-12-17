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
using System.Linq;
using System.Text;

namespace Alchemy.Server.Handlers.WebSocket
{
    /// <summary>
    /// Simple WebSocket data Frame implementation. 
    /// Automatically manages adding received data to an existing frame and checking whether or not we've received the entire frame yet.
    /// See http://www.whatwg.org/specs/web-socket-protocol/ for more details on the WebSocket Protocol.
    /// </summary>
    public abstract class DataFrame
    {
        #region DataState enum

        /// <summary>
        /// The Dataframe's state
        /// </summary>
        public enum DataState
        {
            Empty = -1,
            Receiving = 0,
            Complete = 1,
            Waiting = 2,
            Closed = 3,
            Ping = 4,
            Pong = 5
        }

        #endregion

        protected DataState InternalState = DataState.Empty;

        /// <summary>
        /// The internal byte buffer used to store received data until the entire frame comes through.
        /// </summary>
        protected List<ArraySegment<byte>> RawFrame = new List<ArraySegment<byte>>();

        /// <summary>
        /// Gets the current length of the received frame.
        /// </summary>
        public UInt64 Length
        {
            get
            {
                return RawFrame.Aggregate<ArraySegment<byte>, ulong>(0,
                                                                     (current, seg) =>
                                                                     current + Convert.ToUInt64(seg.Count));
            }
        }

        /// <summary>
        /// Gets the state.
        /// </summary>
        public DataState State
        {
            get { return InternalState; }
        }

        public abstract DataFrame CreateInstance();

        public List<ArraySegment<byte>> GetRaw()
        {
            return RawFrame;
        }

        /// <summary>
        /// Wraps the specified data.
        /// Accepts a string, converts to bytes, sends to the real wrap function.
        /// </summary>
        /// <returns></returns>
        public abstract void Wrap();

        /// <summary>
        /// Appends the specified data to the internal byte buffer.
        /// </summary>
        /// <param name="data">The data.</param>
        public abstract void Append(byte[] data);

        /// <summary>
        /// Returns a <see cref="System.String"/> that represents this data Frame.
        /// </summary>
        /// <returns>
        /// A <see cref="System.String"/> that represents this data Frame.
        /// </returns>
        public override string ToString()
        {
            var sb = new StringBuilder();
            foreach (var data in RawFrame)
            {
                sb.Append(Encoding.UTF8.GetString(data.Array));
            }
            return sb.ToString();
        }

        /// <summary>
        /// Returns a Byte Array that represents this data Frame.
        /// </summary>
        /// <returns>
        /// A Byte Array that represents this data Frame.
        /// </returns>
        public byte[] ToBytes()
        {
            return Encoding.UTF8.GetBytes(ToString());
        }

        /// <summary>
        /// Resets and clears this instance.
        /// </summary>
        public void Clear()
        {
            RawFrame.Clear();
            InternalState = DataState.Empty;
        }

        /// <summary>
        /// Appends the data to frame. Manages recreating the byte array and such.
        /// </summary>
        /// <param name="someBytes">Some bytes.</param>
        public void AppendDataToFrame(byte[] someBytes)
        {
            RawFrame.Add(new ArraySegment<byte>(someBytes));
        }

        /// <summary>
        /// Appends the data to frame. Manages recreating the byte array and such.
        /// </summary>
        /// <param name="aString">Some bytes.</param>
        public void AppendStringToFrame(String aString)
        {
            byte[] someBytes = Encoding.UTF8.GetBytes(aString);
            AppendDataToFrame(someBytes);
        }
    }
}