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
        protected readonly Context Context;

        /// <summary>
        /// The remote endpoint address.
        /// </summary>
        public EndPoint ClientAddress;

        /// <summary>
        /// User defined data. Can be anything.
        /// </summary>
        public Object Data;

        /// <summary>
        /// The data Frame that this client is currently processing.
        /// </summary>
        public DataFrame DataFrame;

        /// <summary>
        /// OnEvent Delegates specific to this connection.
        /// </summary>
        protected OnEventDelegate OnConnectDelegate = x => { };

        protected OnEventDelegate OnConnectedDelegate = x => { };
        protected OnEventDelegate OnDisconnectDelegate = x => { };
        protected OnEventDelegate OnReceiveDelegate = x => { };
        protected OnEventDelegate OnSendDelegate = x => { };

        /// <summary>
        /// The latest exception. Usable when OnDisconnect is called.
        /// </summary>/
        public Exception LatestException;

        /// <summary>
        /// The type of connection this is
        /// </summary>
        public Protocol Protocol = Protocol.None;

        /// <summary>
        /// The path of this request.
        /// </summary>
        public string RequestPath = "/";

        /// <summary>
        /// Initializes a new instance of the <see cref="UserContext"/> class.
        /// </summary>
        /// <param name="context">The user context.</param>
        public UserContext(Context context)
        {
            Context = context;
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
        public UInt64 MaxFrameSize
        {
            get { return Context.MaxFrameSize; }
            set { Context.MaxFrameSize = value; }
        }

        /// <summary>
        /// Called when [connect].
        /// </summary>
        public void OnConnect()
        {
            OnConnectDelegate(this);
        }

        /// <summary>
        /// Called when [connected].
        /// </summary>
        public void OnConnected()
        {
            OnConnectedDelegate(this);
        }

        /// <summary>
        /// Called when [disconnect].
        /// </summary>
        public void OnDisconnect()
        {
            Context.Connected = false;
            OnDisconnectDelegate(this);
        }

        /// <summary>
        /// Called when [send].
        /// </summary>
        public void OnSend()
        {
            OnSendDelegate(this);
        }

        /// <summary>
        /// Called when [receive].
        /// </summary>
        public void OnReceive()
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
        /// Sends the specified data.
        /// </summary>
        /// <param name="dataFrame">The data.</param>
        /// <param name="raw">Whether or not to send raw data</param>
        /// <param name="close">if set to <c>true</c> [close].</param>
        public void Send(DataFrame dataFrame, bool raw = false, bool close = false)
        {
            Context.Handler.Send(dataFrame, Context, raw, close);
        }

        /// <summary>
        /// Sends the specified data.
        /// </summary>
        /// <param name="aString">The data.</param>
        /// <param name="raw">whether or not to send raw data</param>
        /// <param name="close">if set to <c>true</c> [close].</param>
        public void Send(String aString, bool raw = false, bool close = false)
        {
            DataFrame dataFrame = DataFrame.CreateInstance();
            dataFrame.Append(aString);
            Context.Handler.Send(dataFrame, Context, raw, close);
        }

        /// <summary>
        /// Sends the specified data.
        /// </summary>
        /// <param name="someBytes">The data.</param>
        /// <param name="raw">whether or not to send raw data</param>
        /// <param name="close">if set to <c>true</c> [close].</param>
        public void Send(byte[] someBytes, bool raw = false, bool close = false)
        {
            DataFrame dataFrame = DataFrame.CreateInstance();
            dataFrame.IsByte = true;
            dataFrame.Append(someBytes);
            Context.Handler.Send(dataFrame, Context, raw, close);
        }
    }
}