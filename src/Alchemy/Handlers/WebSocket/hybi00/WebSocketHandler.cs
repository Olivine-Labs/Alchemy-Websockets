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
using Alchemy.Classes;

namespace Alchemy.Handlers.WebSocket.hybi00
{
    /// <summary>
    /// A threadsafe singleton that contains functions which are used to handle incoming connections for the WebSocket Protocol
    /// </summary>
    internal sealed class WebSocketHandler : Handler
    {
        private static WebSocketHandler _instance;

        private WebSocketHandler()
        {
            Authentication = WebSocketAuthentication.Instance;
        }

        public static WebSocketHandler Instance
        {
            get
            {
                CreateLock.Wait();
                if (_instance == null)
                {
                    _instance = new WebSocketHandler();
                }
                CreateLock.Release();
                return _instance;
            }
        }

        /// <summary>
        /// Handles the request.
        /// </summary>
        /// <param name="context">The user context.</param>
        public override void HandleRequest(Context context)
        {
            if (context.IsSetup)
            {
                context.UserContext.DataFrame.Append(context.Buffer, true);
                if (context.UserContext.DataFrame.State == WebSocket.DataFrame.DataState.Complete)
                {
                    context.UserContext.OnReceive();
                }
                else if (context.UserContext.DataFrame.State == WebSocket.DataFrame.DataState.Closed)
                {
                    context.UserContext.Send(new DataFrame(), true);
                }
            }
            else
            {
                Authenticate(context);
            }
        }

        /// <summary>
        /// Attempts to authenticates the specified user context.
        /// If authentication fails it kills the connection.
        /// </summary>
        /// <param name="context">The user context.</param>
        private static void Authenticate(Context context)
        {
            if (context.Handler.Authentication.CheckHandshake(context))
            {
                context.UserContext.Protocol = context.Header.Protocol;
                context.UserContext.RequestPath = context.Header.RequestPath;
                context.Header = null;
                context.IsSetup = true;
                context.UserContext.OnConnected();
            }
            else
            {
                context.Dispose();
            }
        }

        /// <summary>
        /// Sends the specified data.
        /// </summary>
        /// <param name="dataFrame">The data.</param>
        /// <param name="context">The user context.</param>
        /// <param name="close">if set to <c>true</c> [close].</param>
        public override void Send(WebSocket.DataFrame dataFrame, Context context, bool close = false)
        {
            AsyncCallback callback = EndSend;
            if (close)
            {
                callback = EndSendAndClose;
            }
            context.SendReady.Wait();
            try
            {
                context.Connection.Client.BeginSend(dataFrame.AsFrame(), SocketFlags.None, callback,
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
        public override void EndSend(IAsyncResult result)
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
        public override void EndSendAndClose(IAsyncResult result)
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