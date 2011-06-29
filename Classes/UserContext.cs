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
using System.Net.Sockets;
using Alchemy.Server.Handlers;
using System.Threading;
using System.Net;
using Alchemy.Server;

namespace Alchemy.Server.Classes
{
    /// <summary>
    /// Contains data we will export to the Event Delegates.
    /// </summary>
    public class UserContext
    {
        /// <summary>
        /// AQ Link to the parent User Context
        /// </summary>
        private Context Context = null;
        /// <summary>
        /// The Data Frame that this client is currently processing.
        /// </summary>
        public DataFrame DataFrame = new DataFrame();
        /// <summary>
        /// What character encoding to use.
        /// </summary>
        public UTF8Encoding Encoding = new UTF8Encoding();
        /// <summary>
        /// User defined data. Can be anything.
        /// </summary>
        public Object Data = null;
        /// <summary>
        /// The path of this request.
        /// </summary>
        public string RequestPath = "/";
        /// <summary>
        /// The remote endpoint address.
        /// </summary>
        public EndPoint ClientAddress = null;
        /// <summary>
        /// The type of connection this is
        /// </summary>
        public Protocol Protocol = Protocol.None;
        /// <summary>
        /// OnEvent Delegates specific to this connection.
        /// </summary>
        protected OnEventDelegate _OnConnect = (x) => { };
        protected OnEventDelegate _OnDisconnect = (x) => { };
        protected OnEventDelegate _OnReceive = (x) => { };
        protected OnEventDelegate _OnSend = (x) => { };

        public readonly Header Header;

        /// <summary>
        /// Initializes a new instance of the <see cref="UserContext"/> class.
        /// </summary>
        /// <param name="AContext">The user context.</param>
        public UserContext(Context AContext)
        {
            this.Context = AContext;
            this.Header = this.Context.Header;
        }

        /// <summary>
        /// Called when [connect].
        /// </summary>
        public void OnConnect()
        {
            try
            {
                _OnConnect(this);
            }
            catch (Exception e) { Context.Server.Log.Error("Fatal Error in user specified OnConnect", e); }
        }

        /// <summary>
        /// Called when [disconnect].
        /// </summary>
        public void OnDisconnect()
        {
            try
            {
                Context.Connected = false;
                _OnDisconnect(this);
            }
            catch (Exception e) { Context.Server.Log.Error("Fatal Error in user specified OnDisconnect", e); }
        }

        /// <summary>
        /// Called when [send].
        /// </summary>
        public void OnSend()
        {
            try
            {
                _OnSend(this);
            }
            catch (Exception e) { Context.Server.Log.Error("Fatal Error in user specified OnSend", e); }
        }

        /// <summary>
        /// Called when [receive].
        /// </summary>
        public void OnReceive()
        {
            try
            {
                _OnReceive(this);
            }
            catch (Exception e) { Context.Server.Log.Error("Fatal Error in user specified OnReceive", e); }
        }

        /// <summary>
        /// Sets the on connect event.
        /// </summary>
        /// <param name="ADelegate">The Event Delegate.</param>
        public void SetOnConnect(OnEventDelegate ADelegate)
        {
            _OnConnect = ADelegate;
        }

        /// <summary>
        /// Sets the on disconnect event.
        /// </summary>
        /// <param name="ADelegate">The Event Delegate.</param>
        public void SetOnDisconnect(OnEventDelegate ADelegate)
        {
            _OnDisconnect = ADelegate;
        }

        /// <summary>
        /// Sets the on send event.
        /// </summary>
        /// <param name="ADelegate">The Event Delegate.</param>
        public void SetOnSend(OnEventDelegate ADelegate)
        {
            _OnSend = ADelegate;
        }

        /// <summary>
        /// Sets the on receive event.
        /// </summary>
        /// <param name="ADelegate">The Event Delegate.</param>
        public void SetOnReceive(OnEventDelegate ADelegate)
        {
            _OnReceive = ADelegate;
        }

        /// <summary>
        /// Sends the specified data.
        /// </summary>
        /// <param name="Data">The data.</param>
        /// <param name="Close">if set to <c>true</c> [close].</param>
        public void Send(string Data, bool Close = false)
        {
            Send(Encoding.GetBytes(Data), Close);
        }

        /// <summary>
        /// Sends the specified data.
        /// </summary>
        /// <param name="Data">The data.</param>
        /// <param name="Close">if set to <c>true</c> [close].</param>
        public void Send(byte[] Data, bool Close = false)
        {
            Context.Handler.Send(Data, Context, Close);
        }

        /// <summary>
        /// Sends raw data.
        /// </summary>
        /// <param name="Data">The data.</param>
        public void SendRaw(byte[] Data)
        {
            DefaultHandler.Instance.Send(Data, Context);
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
        public readonly UserContext UserContext = null;
        /// <summary>
        /// The raw client connection.
        /// </summary>
        public TcpClient Connection = null;
        /// <summary>
        /// Whether or not the TCPClient is still connected.
        /// </summary>
        public bool Connected = true;
        /// <summary>
        /// The buffer used for accepting raw data from the socket.
        /// </summary>
        public byte[] Buffer = null;
        /// <summary>
        /// How many pings in a row we've had from this client, indicates inactivity.
        /// </summary>
        public int Pings = 0;
        /// <summary>
        /// How many bytes we received this tick.
        /// </summary>
        public int ReceivedByteCount = 0;
        /// <summary>
        /// Whether or not this client has passed all the setup routines for the current handler(authentication, etc)
        /// </summary>
        public Boolean IsSetup = false;
        /// <summary>
        /// The current connection handler.
        /// </summary>
        public Handler Handler = DefaultHandler.Instance;
        /// <summary>
        /// Semaphores that limit sends and receives to 1 and a time.
        /// </summary>
        public SemaphoreSlim ReceiveReady = new SemaphoreSlim(1);
        public SemaphoreSlim SendReady = new SemaphoreSlim(1);
        /// <summary>
        /// A link to the server listener instance this client is currently hosted on.
        /// </summary>
        public WSServer Server = null;
        /// <summary>
        /// The Header
        /// </summary>
        public Header Header = null;

        private int _BufferSize = 512;

        /// <summary>
        /// Gets or sets the size of the buffer.
        /// </summary>
        /// <value>
        /// The size of the buffer.
        /// </value>
        public int BufferSize
        {
            get
            {
                return _BufferSize;
            }
            set
            {
                _BufferSize = value;
                Buffer = new byte[_BufferSize];
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Context"/> class.
        /// </summary>
        public Context()
        {
            Buffer = new byte[_BufferSize];
            this.UserContext = new UserContext(this);
        }

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
            catch (Exception e) { Server.Log.Debug("Client Already Disconnected", e); }
            finally
            {
                if (Connected)
                {
                    Connected = false;
                    UserContext.OnDisconnect();
                }
            }
        }

        /// <summary>
        /// Resets this instance.
        /// Clears the dataframe if necessary. Resets Received byte count.
        /// </summary>
        public void Reset()
        {
            if (UserContext.DataFrame.State == DataFrame.DataState.Complete)
                UserContext.DataFrame.Clear();
            ReceivedByteCount = 0;
        }
    }
}
