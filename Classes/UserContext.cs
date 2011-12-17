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
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Alchemy.Server.Handlers;
using Alchemy.Server.Handlers.WebSocket;

namespace Alchemy.Server.Classes
{
    /// <summary>
    /// Contains data we will export to the Event Delegates.
    /// </summary>
    public class UserContext
    {
        public readonly Header Header;

        /// <summary>
        /// AQ Link to the parent User Context
        /// </summary>
        private readonly Context _context;

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
        /// What character encoding to use.
        /// </summary>
        public UTF8Encoding Encoding = new UTF8Encoding();

        /// <summary>
        /// OnEvent Delegates specific to this connection.
        /// </summary>
        protected OnEventDelegate OnConnectDelegate = x => { };

        protected OnEventDelegate OnConnectedDelegate = x => { };
        protected OnEventDelegate OnDisconnectDelegate = x => { };
        protected OnEventDelegate OnReceiveDelegate = x => { };
        protected OnEventDelegate OnSendDelegate = x => { };

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
            _context = context;
            Header = context.Header;
        }

        /// <summary>
        /// Called when [connect].
        /// </summary>
        public void OnConnect()
        {
            try
            {
                OnConnectDelegate(this);
            }
            catch (Exception e)
            {
                _context.Server.Log.Error("Fatal Error in user specified OnConnect", e);
            }
        }

        /// <summary>
        /// Called when [connected].
        /// </summary>
        public void OnConnected()
        {
            try
            {
                OnConnectedDelegate(this);
            }
            catch (Exception e)
            {
                _context.Server.Log.Error("Fatal Error in user specified OnConnected", e);
            }
        }

        /// <summary>
        /// Called when [disconnect].
        /// </summary>
        public void OnDisconnect()
        {
            try
            {
                _context.Connected = false;
                OnDisconnectDelegate(this);
            }
            catch (Exception e)
            {
                _context.Server.Log.Error("Fatal Error in user specified OnDisconnect", e);
            }
        }

        /// <summary>
        /// Called when [send].
        /// </summary>
        public void OnSend()
        {
            try
            {
                OnSendDelegate(this);
            }
            catch (Exception e)
            {
                _context.Server.Log.Error("Fatal Error in user specified OnSend", e);
            }
        }

        /// <summary>
        /// Called when [receive].
        /// </summary>
        public void OnReceive()
        {
            try
            {
                OnReceiveDelegate(this);
            }
            catch (Exception e)
            {
                _context.Server.Log.Error("Fatal Error in user specified OnReceive", e);
            }
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
        /// <param name="data">The data.</param>
        /// <param name="close">if set to <c>true</c> [close].</param>
        public void Send(string data, bool close = false)
        {
            Send(Encoding.GetBytes(data), close);
        }

        /// <summary>
        /// Sends the specified data.
        /// </summary>
        /// <param name="data">The data.</param>
        /// <param name="close">if set to <c>true</c> [close].</param>
        public void Send(byte[] data, bool close = false)
        {
            _context.Handler.Send(data, _context, close);
        }

        /// <summary>
        /// Sends raw data.
        /// </summary>
        /// <param name="data">The data.</param>
        public void SendRaw(byte[] data)
        {
            DefaultHandler.Instance.Send(data, _context);
        }
    }

    /// <summary>
    /// This class contains the required data for each connection to the server.
    /// </summary>
    public class Context : IDisposable
    {
        /// <summary>
        /// The exported version of this context.
        /// </summary>
        public readonly UserContext UserContext;

        /// <summary>
        /// The buffer used for accepting raw data from the socket.
        /// </summary>
        public byte[] Buffer;

        /// <summary>
        /// Whether or not the TCPClient is still connected.
        /// </summary>
        public bool Connected = true;

        /// <summary>
        /// The raw client connection.
        /// </summary>
        public TcpClient Connection;

        /// <summary>
        /// The current connection handler.
        /// </summary>
        public Handler Handler = DefaultHandler.Instance;

        /// <summary>
        /// The Header
        /// </summary>
        public Header Header;

        /// <summary>
        /// Whether or not this client has passed all the setup routines for the current handler(authentication, etc)
        /// </summary>
        public Boolean IsSetup;

        /// <summary>
        /// How many pings in a row we've had from this client, indicates inactivity.
        /// </summary>
        public int Pings;

        /// <summary>
        /// Semaphores that limit sends and receives to 1 and a time.
        /// </summary>
        public SemaphoreSlim ReceiveReady = new SemaphoreSlim(1);

        /// <summary>
        /// How many bytes we received this tick.
        /// </summary>
        public int ReceivedByteCount;

        public SemaphoreSlim SendReady = new SemaphoreSlim(1);

        /// <summary>
        /// A link to the server listener instance this client is currently hosted on.
        /// </summary>
        public WebSocketServer Server;

        private int _bufferSize = 512;

        /// <summary>
        /// Initializes a new instance of the <see cref="Context"/> class.
        /// </summary>
        public Context()
        {
            Buffer = new byte[_bufferSize];
            UserContext = new UserContext(this);
        }

        /// <summary>
        /// Gets or sets the size of the buffer.
        /// </summary>
        /// <value>
        /// The size of the buffer.
        /// </value>
        public int BufferSize
        {
            get { return _bufferSize; }
            set
            {
                _bufferSize = value;
                Buffer = new byte[_bufferSize];
            }
        }

        #region IDisposable Members

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            try
            {
                Connection.Client.Close();
                Connection = null;
            }
            catch (Exception e)
            {
                Server.Log.Debug("Client Already Disconnected", e);
            }
            finally
            {
                if (Connected)
                {
                    Connected = false;
                }
                UserContext.OnDisconnect();
            }
        }

        #endregion

        /// <summary>
        /// Resets this instance.
        /// Clears the dataframe if necessary. Resets Received byte count.
        /// </summary>
        public void Reset()
        {
            if (UserContext.DataFrame != null)
            {
                if (UserContext.DataFrame.State == DataFrame.DataState.Complete)
                {
                    UserContext.DataFrame.Clear();
                }
            }
            ReceivedByteCount = 0;
        }
    }
}