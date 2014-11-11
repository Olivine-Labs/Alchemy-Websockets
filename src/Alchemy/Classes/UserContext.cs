using System;
using System.Net;
using Alchemy.Handlers.WebSocket;

namespace Alchemy.Classes
{
    /// <summary>
    /// Contains data we will export to the Event Delegates.
    /// </summary>
    public class UserContext
    {
        /// <summary>
        /// AQ Link to the parent User Context
        /// </summary>
        internal Context Context {get; private set;}

        /// <summary>
        /// The remote endpoint address.
        /// </summary>
        public EndPoint ClientAddress {get; internal set;}

        /// <summary>
        /// User defined data. Can be anything.
        /// </summary>
        public Object Data {get; set;}

        /// <summary>
        /// The data Frame that this client is currently processing.
        /// </summary>
        public DataFrame DataFrame {get; internal set;}

        /// <summary>
        /// OnEvent Delegates specific to this connection.
        /// </summary>
        private OnEventDelegate OnConnectDelegate = x => { };
        private OnEventDelegate OnConnectedDelegate = x => { };
        private OnEventDelegate OnDisconnectDelegate = x => { };
        private OnEventDelegate OnReceiveDelegate = x => { };
        private OnEventDelegate OnSendDelegate = x => { };

        /// <summary>
        /// The latest exception. Usable when OnDisconnect is called.
        /// </summary>/
        public Exception LatestException {get; internal set;}

        /// <summary>
        /// The type of web socket protocol
        /// </summary>
        public Protocol Protocol {get; internal set;}

        /// <summary>
        /// The path of this request.
        /// </summary>
        public string RequestPath {get; set;}


        /// <summary>
        /// Initializes a new instance of the <see cref="UserContext"/> class.
        /// </summary>
        /// <param name="context">The user context.</param>
        public UserContext(Context context)
        {
            Context = context;
            Protocol = Protocol.None;
            RequestPath = "/";
        }

        /// <summary>
        /// The internal context connection header
        /// </summary>
        public Header Header
        {
            get { return Context.Header; }
        }

        /// <summary>
        /// The maximum frame size
        /// </summary>
        public long MaxFrameSize
        {
            get { return Context.MaxFrameSize; }
            set { Context.MaxFrameSize = value; }
        }

        /// <summary>
        /// Called when [connect].
        /// </summary>
        internal void OnConnect()
        {
            OnConnectDelegate(this);
        }

        /// <summary>
        /// Called when [connected].
        /// </summary>
        internal void OnConnected()
        {
            OnConnectedDelegate(this);
        }

        /// <summary>
        /// Called when [disconnect].
        /// </summary>
        internal void OnDisconnect()
        {
            Context.Connected = false;
            OnDisconnectDelegate(this);
        }

        /// <summary>
        /// Called when [send].
        /// </summary>
        internal void OnSend()
        {
            OnSendDelegate(this);
        }

        /// <summary>
        /// Called when [receive].
        /// </summary>
        internal void OnReceive()
        {
            OnReceiveDelegate(this);
        }

        /// <summary>
        /// Sets the on connect event.
        /// </summary>
        /// <param name="aDelegate">The Event Delegate.</param>
        public void SetOnConnect(OnEventDelegate aDelegate)
        {
            OnConnectDelegate = aDelegate;
        }

        /// <summary>
        /// Sets the on connected event.
        /// </summary>
        /// <param name="aDelegate">The Event Delegate.</param>
        public void SetOnConnected(OnEventDelegate aDelegate)
        {
            OnConnectedDelegate = aDelegate;
        }

        /// <summary>
        /// Sets the on disconnect event.
        /// </summary>
        /// <param name="aDelegate">The Event Delegate.</param>
        public void SetOnDisconnect(OnEventDelegate aDelegate)
        {
            OnDisconnectDelegate = aDelegate;
        }

        /// <summary>
        /// Sets the on send event.
        /// </summary>
        /// <param name="aDelegate">The Event Delegate.</param>
        public void SetOnSend(OnEventDelegate aDelegate)
        {
            OnSendDelegate = aDelegate;
        }

        /// <summary>
        /// Sets the on receive event.
        /// </summary>
        /// <param name="aDelegate">The Event Delegate.</param>
        public void SetOnReceive(OnEventDelegate aDelegate)
        {
            OnReceiveDelegate = aDelegate;
        }

        /// <summary>
        /// Sends the specified data frame.
        /// </summary>
        /// <param name="dataFrame">The data.</param>
        /// <param name="raw">if set to <c>true</c> do not add header and do not mask the buffer before sending.</param>
        /// <param name="close">if set to <c>true</c> close the socket after sending.</param>
        public void Send(DataFrame dataFrame, bool raw = false, bool close = false)
        {
            Context.Handler.Send(dataFrame, Context, raw, close);
        }

        /// <summary>
        /// Sends the specified data string.
        /// </summary>
        /// <param name="aString">The data.</param>
        /// <param name="raw">if set to <c>true</c> do not add header and do not mask the buffer before sending.</param>
        /// <param name="close">if set to <c>true</c> close the socket after sending.</param>
        public void Send(String aString, bool raw = false, bool close = false)
        {
            DataFrame dataFrame = DataFrame.CreateInstance();
            dataFrame.Append(aString);
            Context.Handler.Send(dataFrame, Context, raw, close);
        }

        /// <summary>
        /// Sends the specified data buffer.
        /// </summary>
        /// <param name="buffer">The data.</param>
        /// <param name="byteCount">Count of bytes from beginning of buffer. -1 = all bytes.</param>
        /// <param name="raw">if set to <c>true</c> do not add header and do not mask the buffer before sending.</param>
        /// <param name="close">if set to <c>true</c> close the socket after sending.</param>
        public void Send(byte[] buffer, int byteCount = -1, bool raw = false, bool close = false)
        {
            DataFrame dataFrame = DataFrame.CreateInstance();
            dataFrame.IsBinary = true;
            dataFrame.Append(buffer, byteCount);
            Context.Handler.Send(dataFrame, Context, raw, close);
        }
    }
}