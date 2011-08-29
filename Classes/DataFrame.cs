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


namespace Alchemy.Server.Classes
{
    /// <summary>
    /// Simple WebSocket Data Frame implementation. 
    /// Automatically manages adding received data to an existing frame and checking whether or not we've received the entire frame yet.
    /// See http://www.whatwg.org/specs/web-socket-protocol/ for more details on the WebSocket Protocol.
    /// </summary>
    public abstract class DataFrame
    {
        /// <summary>
        /// The Dataframe's state
        /// </summary>
        public enum DataState
        {
            Empty = -1,
            Receiving = 0,
            Complete = 1,
            Closed = 2,
            Ping = 3,
            Pong = 4
        }

        /// <summary>
        /// The internal byte buffer used to store received data until the entire frame comes through.
        /// </summary>
        protected byte[] RawFrame = null;
        protected DataState _State = DataState.Empty;

        /// <summary>
        /// Gets the current length of the received frame.
        /// </summary>
        public int Length
        {
            get
            {
                return RawFrame.Length;
            }
        }

        /// <summary>
        /// Gets the state.
        /// </summary>
        public DataState State
        {
            get
            {
                return _State;
            }
        }

        /// <summary>
        /// Wraps the specified data.
        /// Accepts a string, converts to bytes, sends to the real wrap function.
        /// </summary>
        /// <param name="Data">The data.</param>
        /// <returns></returns>
        public byte[] Wrap(string Data)
        {
            byte[] SomeBytes = Encoding.UTF8.GetBytes(Data);
            return Wrap(SomeBytes);
        }

        /// <summary>
        /// Wraps the specified data in WebSocket Start/End Bytes.
        /// Accepts a byte array.
        /// </summary>
        /// <param name="Data">The data.</param>
        /// <returns>The Data array wrapped in WebSocket DataFrame Start/End qualifiers.</returns>
        public abstract byte[] Wrap(byte[] Data);

        /// <summary>
        /// Appends the specified data to the internal byte buffer.
        /// </summary>
        /// <param name="Data">The data.</param>
        public abstract void Append(byte[] Data);

        /// <summary>
        /// Returns a <see cref="System.String"/> that represents this Data Frame.
        /// </summary>
        /// <returns>
        /// A <see cref="System.String"/> that represents this Data Frame.
        /// </returns>
        public override string ToString()
        {
            if (RawFrame != null)
                return UTF8Encoding.UTF8.GetString(RawFrame);
            else
                return String.Empty;
        }

        /// <summary>
        /// Returns a Byte Array that represents this Data Frame.
        /// </summary>
        /// <returns>
        /// A Byte Array that represents this Data Frame.
        /// </returns>
        public byte[] ToBytes()
        {
            if (RawFrame != null)
                return RawFrame;
            else
                return new byte[0];
        }

        /// <summary>
        /// Resets and clears this instance.
        /// </summary>
        public void Clear()
        {
            RawFrame = null;
            _State = DataState.Empty;
        }

    }
}