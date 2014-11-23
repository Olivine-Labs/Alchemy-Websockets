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
        internal List<ArraySegment<byte>> Payload = new List<ArraySegment<byte>>(32);

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
            _writeBuf = null;
            _writeBufCount = 0;
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

        private long _streamReadPos;
        private int _segIndexRead;
        private int _segPosRead; // this byte is the next to be read
        private int _writeBufCount; // this byte is the next to be written
        private int _writeBufSize = 64;
        private byte[] _writeBuf = null;


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

        public override int Read(byte[] buffer, int offset, int count)
        {
            return Read(buffer, offset, count, 0);
        }

        private int Read (byte[] buffer, int offset, int count, int previouslyRead)
        {
            List<ArraySegment<byte>> segments = AsRaw();
            if (_segIndexRead >= segments.Count)
            {
                return 0; // end of stream
            }

            var src = Payload[_segIndexRead];
            int srcPos = src.Offset + _segPosRead;
            int remaining = src.Count - srcPos;
            if (remaining <= 0) // zero segment size
            {
                _segIndexRead++;
                _segPosRead = 0;
                return Read(buffer, offset, count, previouslyRead);
            }

            int n = count;
            if (n >= remaining)
            {
                n = remaining;
                _segIndexRead++; // read from next segment next time
                _segPosRead = 0;
            }
            else
            {
                _segPosRead += n;
            }

            _streamReadPos += (long)n;
            Array.Copy(src.Array, srcPos, buffer, offset, n);

            if (n < count)
            {
                return Read(buffer, offset + n, count - n, previouslyRead + n);
            }

            return previouslyRead + n;
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
            if (_writeBufCount + count > _writeBufSize)
            {
                Flush(); // resets _writeBufCount
            }

            if (_writeBufCount + count > _writeBufSize)
            {
                // add large buffers without copying
                Payload.Add(new ArraySegment<byte>(buffer, offset, count));
                return;
            }

            // copy small buffers to the intermediate buffer
            if (_writeBuf == null)
            {
                _writeBuf = new byte[_writeBufSize];
            }
            Array.Copy(buffer, offset, _writeBuf, _writeBufCount, count);
            _writeBufCount += count;
        }


        public override void WriteByte(byte value)
        {
            if (_writeBufCount >= _writeBufSize)
            {
                Flush(); // resets _writeBufCount
            }

            // copy byte to the intermediate buffer
            if (_writeBuf == null)
            {
                _writeBuf = new byte[_writeBufSize];
            }

            _writeBuf[_writeBufCount++] = value;
        }


        public override void Flush()
        {
            if (_writeBuf != null)
            {
                // add the intermediate buffer before sending all data
                Payload.Add(new ArraySegment<byte>(_writeBuf, 0, _writeBufCount));
                _writeBuf = null;
                _writeBufCount = 0;
            }
            Format = DataFormat.Raw; // stream write is used for outgoing messages only
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