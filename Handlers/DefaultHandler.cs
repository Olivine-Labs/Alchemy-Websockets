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
using Alchemy.Server.Classes;
using System.Net.Sockets;

namespace Alchemy.Server.Handlers
{
    /// <summary>
    /// When the protocol has not yet been determined the system defaults to this request handler.
    /// Singleton, just like the other handlers.
    /// </summary>
    class DefaultHandler : Handler
    {
        private static DefaultHandler _Instance;

        private DefaultHandler() { }

        public static DefaultHandler Instance
        {
            get 
            {
                CreateLock.Wait();
                if (_Instance == null)
                {
                    _Instance = new DefaultHandler();
                }
                CreateLock.Release();
                return _Instance;
            }
        }

        /// <summary>
        /// Handles the initial request.
        /// Attempts to process the header that should have been sent.
        /// Otherwise, through magic and wizardry, the client gets disconnected.
        /// </summary>
        /// <param name="AContext">The user context.</param>
        public override void HandleRequest(Context AContext)
        {
            if (AContext.IsSetup)
            {
                AContext.Dispose();
            }
            else
            {
                ProcessHeader(AContext);
            }
        }

        /// <summary>
        /// Processes the header.
        /// </summary>
        /// <param name="AContext">The user context.</param>
        public void ProcessHeader(Context AContext)
        {
            string Data = AContext.UserContext.Encoding.GetString(AContext.Buffer, 0, AContext.ReceivedByteCount);
            //Check first to see if this is a flash socket XML request.
            if (Data == "<policy-file-request/>\0")
            {
                try
                {
                    //if it is, we access the Access Policy Server instance to send the appropriate response.
                    AContext.Server.AccessPolicyServer.SendResponse(AContext.Connection);
                }
                catch { }
                AContext.Dispose();
            }
            else//If it isn't, process http/websocket header as normal.
            {
                AContext.Header = new Header(Data);
                switch (AContext.Header.Protocol)
                {
                    case Protocol.WebSocketHybi00:
                        AContext.Handler = Alchemy.Server.Handlers.WebSocket.hybi00.WebSocketHandler.Instance;
                        AContext.UserContext.DataFrame = new Alchemy.Server.Handlers.WebSocket.hybi00.DataFrame();
                        break;
                    case Protocol.WebSocketHybi10:
                        AContext.Handler = Alchemy.Server.Handlers.WebSocket.hybi10.WebSocketHandler.Instance;
                        AContext.UserContext.DataFrame = new Alchemy.Server.Handlers.WebSocket.hybi10.DataFrame();
                        break;
                    case Protocol.FlashSocket:
                        AContext.Handler = Alchemy.Server.Handlers.WebSocket.hybi00.WebSocketHandler.Instance;
                        AContext.UserContext.DataFrame = new Alchemy.Server.Handlers.WebSocket.hybi00.DataFrame();
                        break;
                    default:
                        AContext.Header.Protocol = Protocol.None;
                        break;
                }
                if (AContext.Header.Protocol != Protocol.None)
                {
                    AContext.Handler.HandleRequest(AContext);
                }
                else
                {
                    AContext.UserContext.Send(Response.NotImplemented, true);
                }
            }
        }

        /// <summary>
        /// Sends the specified data.
        /// </summary>
        /// <param name="Data">The data.</param>
        /// <param name="AContext">The user context.</param>
        /// <param name="Close">if set to <c>true</c> [close].</param>
        public override void Send(byte[] Data, Context AContext, bool Close = false)
        {
            AsyncCallback ACallback = EndSend;
            if (Close)
                ACallback = EndSendAndClose;
            AContext.SendReady.Wait();
            try
            {
                AContext.Connection.Client.BeginSend(Data, 0, Data.Length, SocketFlags.None, ACallback, AContext);
            }
            catch
            {
                AContext.SendReady.Release();
            }
        }

        /// <summary>
        /// Ends the send.
        /// </summary>
        /// <param name="AResult">The Async result.</param>
        public override void EndSend(IAsyncResult AResult)
        {
            Context AContext = (Context)AResult.AsyncState;
            try
            {
                AContext.Connection.Client.EndSend(AResult);
                AContext.SendReady.Release();
            }
            catch
            {
                AContext.SendReady.Release();
            }
            AContext.UserContext.OnSend();
        }

        /// <summary>
        /// Ends the send and closes the connection.
        /// </summary>
        /// <param name="AResult">The Async result.</param>
        public override void EndSendAndClose(IAsyncResult AResult)
        {
            Context AContext = (Context)AResult.AsyncState;
            try
            {
                AContext.Connection.Client.EndSend(AResult);
                AContext.SendReady.Release();
            }
            catch
            {
                AContext.SendReady.Release();
            }
            AContext.UserContext.OnSend();
            AContext.Dispose();
        }
    }
}