using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace Alchemy.Handlers.WebSocket
{
    /// <summary>
    /// Simple WebSocket data Frame implementation. 
    /// Automatically manages adding received data to an existing frame and checking whether or not we've received the entire frame yet.
    /// See https://tools.ietf.org/html/rfc6455/ for more details on the WebSocket Protocol.
    /// </summary>
    public abstract class DataFrame : Stream
    {
        #region Enumerations

        /// <summary>
        /// Dataformat for user (Raw) or for network (Frame).
        /// </summary>
        public enum DataFormat
        {
            Unknown = -1,

            /// <summary>
            /// Raw format: User data without websocket header. Ready to read (unmasked).
            /// </summary>
            Raw = 0,

            /// <summary>
            /// Frame format: Network data frame with websocket header, optionally masked.
            /// </summary>
            Frame = 1
        }

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

        internal DataFormat Format = DataFormat.Unknown;
        internal DataState  InternalState = DataState.Empty;

        /// <summary>
        /// The internal byte buffer used to store data
        /// </summary>
        internal List<ArraySegment<byte>> Payload = new List<ArraySegment<byte>>();

        /// <summary>
        /// Gets the state.
        /// </summary>
        public DataState State
        {
            get { return InternalState; }
            internal set { InternalState = value; }
        }

        public abstract DataFrame CreateInstance();

        /// <summary>
        /// Converts the Payload to a websocket frame with header and masked for network transfer.
        /// </summary>
        /// <returns></returns>
        public abstract List<ArraySegment<byte>> AsFrame();

        /// <summary>
        /// Converts the Payload to raw data without header and unmasked for the user.
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
            _streamReadPos = 0;
            _segIndexRead = 0;
            _segPosRead = 0;
        }

        /// <summary>
        /// Appends a string (raw, unmasked data).
        /// </summary>
        /// <param name="aString">Some data.</param>
        public void Append(String aString)
        {
            Append(Encoding.UTF8.GetBytes(aString));
        }

        /// <summary>
        /// Appends a byte buffer.
        /// </summary>
        /// <param name="buffer">Contains data to be copied into this DataFrame.</param>
        /// <param name="byteCount">Count of bytes to be copied from buffer. -1 indicates whole buffer.</param>
        /// <param name="asFrame">Default=false: Raw data; True: For internal use inside alchemy.</param>
        /// <param name="maxLength">Default=0: Raw data; Else: For internal use inside alchemy.</param>
        /// <returns>Count of bytes copied into the frame buffer. 0=Header not ready; -1=Invalid data - disconnect.</returns>
        public abstract int Append(byte[] buffer, int byteCount = -1, bool asFrame = false, long maxLength = 0);

        /// <summary>
        /// Returns the raw payload data as string (unmasked, without header) for the user.
        /// </summary>
        public override string ToString()
        {
            var sb = new StringBuilder();
            List<ArraySegment<byte>> data = AsRaw();
            foreach (var item in data)
            {
                sb.Append(Encoding.UTF8.GetString(item.Array));
            }
            return sb.ToString();
        }

        /// <summary>
        /// Binary data gets a different websocket header than text.
        /// </summary>
        public bool IsBinary { get; set; }


        #region implemented abstract members of Stream


        public override void Flush ()
        {
            throw new NotImplementedException ();
        }


        /// <summary>
        /// Gets the current length of the data
        /// </summary>
        public override long Length
        {
            get
            {
                return Payload.Aggregate<ArraySegment<byte>, long>(0,
                                                                    (current, seg) =>
                                                                     current + (long)seg.Count);
            }
        }

        public override int Read (byte[] buffer, int offset, int count)
        {
            List<ArraySegment<byte>> segments = AsRaw();
            if (_segIndexRead >= segments.Count)
            {
                return 0; // end of stream
            }

            var seg = Payload[_segIndexRead];
            int remaining = seg.Count - seg.Offset - _segPosRead;
            if (remaining <= 0) // zero segment size
            {
                _segIndexRead++;
                _segPosRead = 0;
                return Read(buffer, offset, count);
            }

            if (remaining < count)
            {
               count = remaining;
            }
 
            Array.Copy(seg.Array, _segPosRead+seg.Offset, buffer, offset, count);

            if (count >= remaining)
            {
                _segIndexRead++;
                _segPosRead = 0;
            }
            else
            {
                _segPosRead += count;
            }

            _streamReadPos += (long)count;
            return count;
        }

        public override int ReadByte()
        {
            List<ArraySegment<byte>> segments = AsRaw();
            if (_segIndexRead >= segments.Count)
            {
                return -1; // end of stream
            }

            var seg = Payload[_segIndexRead];
            int idx =  seg.Offset + _segPosRead;
            if (idx >= seg.Count) // zero segment length
            {
                _segIndexRead++;
                _segPosRead = 0;
                return ReadByte();
            }

            _segPosRead++;
            _streamReadPos++;
            if (seg.Offset + _segPosRead >= seg.Count)
            {
                _segIndexRead++;
                _segPosRead = 0;
            }

            return seg.Array[idx];
        }


        public override long Seek (long offset, SeekOrigin origin)
        {
            throw new NotImplementedException ();
        }


        public override void SetLength (long value)
        {
            throw new NotImplementedException ();
        }


        public override void Write (byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException ();
        }


        public override bool CanRead {
            get {
                return true;
            }
        }


        public override bool CanSeek {
            get {
                return false;
            }
        }


        public override bool CanWrite {
            get {
                return true;
            }
        }

        private long _streamReadPos;
        private int _segIndexRead;
        private int _segPosRead; // this byte is the next to read

        public override long Position {
            get {
                return _streamReadPos;
            }
            set {
                throw new NotImplementedException ();
            }
        }


        #endregion
    }
}