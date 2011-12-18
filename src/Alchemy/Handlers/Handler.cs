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
using System.Net.Sockets;
using System.Threading;
using Alchemy.Classes;
using Alchemy.Handlers.WebSocket;

namespace Alchemy.Handlers
{
    /// <summary>
    /// When the protocol has not yet been determined the system defaults to this request handler.
    /// Singleton, just like the other handlers.
    /// </summary>
    public class Handler
    {
        private static Handler _instance;

        protected static SemaphoreSlim CreateLock = new SemaphoreSlim(1);
        internal IAuthentication Authentication;
        protected Handler() {}

        public static Handler Instance
        {
            get
            {
                CreateLock.Wait();
                if (_instance == null)
                {
                    _instance = new Handler();
                }
                CreateLock.Release();
                return _instance;
            }
        }

        /// <summary>
        /// Handles the initial request.
        /// Attempts to process the header that should have been sent.
        /// Otherwise, through magic and wizardry, the client gets disconnected.
        /// </summary>
        /// <param name="context">The user context.</param>
        public virtual void HandleRequest(Context context)
        {
            if (context.IsSetup)
            {
                context.Dispose();
            }
            else
            {
                ProcessHeader(context);
            }
        }

        /// <summary>
        /// Processes the header.
        /// </summary>
        /// <param name="context">The user context.</param>
        public void ProcessHeader(Context context)
        {
            string data = context.UserContext.Encoding.GetString(context.Buffer, 0, context.ReceivedByteCount);
            //Check first to see if this is a flash socket XML request.
            if (data == "<policy-file-request/>\0")
            {
                try
                {
                    //if it is, we access the Access Policy Server instance to send the appropriate response.
                    context.Server.AccessPolicyServer.SendResponse(context.Connection);
                }
                    // ReSharper disable EmptyGeneralCatchClause
                catch {}
                // ReSharper restore EmptyGeneralCatchClause
                context.Dispose();
            }
            else //If it isn't, process http/websocket header as normal.
            {
                context.Header = new Header(data);
                switch (context.Header.Protocol)
                {
                    case Protocol.WebSocketHybi00:
                        context.Handler = WebSocket.hybi00.Handler.Instance;
                        context.UserContext.DataFrame = new WebSocket.hybi00.DataFrame();
                        break;
                    case Protocol.WebSocketHybi10:
                        context.Handler = WebSocket.hybi10.Handler.Instance;
                        context.UserContext.DataFrame = new WebSocket.hybi10.DataFrame();
                        break;
                    default:
                        context.Header.Protocol = Protocol.None;
                        break;
                }
                if (context.Header.Protocol != Protocol.None)
                {
                    context.Handler.HandleRequest(context);
                }
                else
                {
                    context.UserContext.Send(Response.NotImplemented, true, true);
                }
            }
        }

        /// <summary>
        /// Sends the specified data.
        /// </summary>
        /// <param name="dataFrame">The data.</param>
        /// <param name="context">The user context.</param>
        /// <param name="raw">whether or not to send raw data</param>
        /// <param name="close">if set to <c>true</c> [close].</param>
        public void Send(DataFrame dataFrame, Context context, bool raw = false, bool close = false)
        {
            AsyncCallback callback = EndSend;
            if (close)
            {
                callback = EndSendAndClose;
            }
            context.SendReady.Wait();
            try
            {
                context.Connection.Client.BeginSend(raw ? dataFrame.AsRaw() : dataFrame.AsFrame(), SocketFlags.None,
                                                    callback,
                                                    context);
            }
            catch
            {
                context.SendReady.Release();
            }
        }

        /// <summary>
        /// Ends the send.
        /// </summary>
        /// <param name="result">The Async result.</param>
        public void EndSend(IAsyncResult result)
        {
            var context = (Context) result.AsyncState;
            try
            {
                context.Connection.Client.EndSend(result);
                context.SendReady.Release();
            }
            catch
            {
                context.SendReady.Release();
            }
            context.UserContext.OnSend();
        }

        /// <summary>
        /// Ends the send and closes the connection.
        /// </summary>
        /// <param name="result">The Async result.</param>
        public void EndSendAndClose(IAsyncResult result)
        {
            var context = (Context) result.AsyncState;
            try
            {
                context.Connection.Client.EndSend(result);
                context.SendReady.Release();
            }
            catch
            {
                context.SendReady.Release();
            }
            context.UserContext.OnSend();
            context.Dispose();
        }
    }
}