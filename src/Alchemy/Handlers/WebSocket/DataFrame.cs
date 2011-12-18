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

namespace Alchemy.Handlers.WebSocket
{
    /// <summary>
    /// Simple WebSocket data Frame implementation. 
    /// Automatically manages adding received data to an existing frame and checking whether or not we've received the entire frame yet.
    /// See http://www.whatwg.org/specs/web-socket-protocol/ for more details on the WebSocket Protocol.
    /// </summary>
    public abstract class DataFrame
    {
        #region Enumerations

        #region DataFormat enum

        public enum DataFormat
        {
            Unknown = -1,
            Raw = 0,
            Frame = 1
        }

        #endregion

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

        #endregion

        public DataFormat Format = DataFormat.Unknown;
        protected DataState InternalState = DataState.Empty;

        /// <summary>
        /// The internal byte buffer used to store data
        /// </summary>
        protected List<ArraySegment<byte>> Payload = new List<ArraySegment<byte>>();

        /// <summary>
        /// Gets the current length of the data
        /// </summary>
        public UInt64 Length
        {
            get
            {
                return Payload.Aggregate<ArraySegment<byte>, ulong>(0,
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
            set { InternalState = value; }
        }

        public abstract DataFrame CreateInstance();

        /// <summary>
        /// Converts the Payload to a websocket Frame
        /// </summary>
        /// <returns></returns>
        public abstract List<ArraySegment<byte>> AsFrame();

        /// <summary>
        /// Converts the Payload to raw data
        /// </summary>
        public abstract List<ArraySegment<byte>> AsRaw();

        /// <summary>
        /// Resets and clears this instance.
        /// </summary>
        public void Reset()
        {
            Payload.Clear();
            Format = DataFormat.Unknown;
            InternalState = DataState.Empty;
        }

        /// <summary>
        /// Appends the data
        /// </summary>
        /// <param name="aString">Some data.</param>
        public void Append(String aString)
        {
            Append(Encoding.UTF8.GetBytes(aString));
        }

        /// <summary>
        /// Appends the data
        /// </summary>
        /// <param name="someBytes">Some data.</param>
        /// /// <param name="asFrame">For internal use inside alchemy.</param>
        public abstract void Append(byte[] someBytes, bool asFrame = false);

        public override string ToString()
        {
            var sb = new StringBuilder();
            List<ArraySegment<Byte>> list = AsRaw();
            foreach (var item in list)
            {
                sb.Append(Encoding.UTF8.GetString(item.Array));
            }
            return sb.ToString();
        }
    }
}