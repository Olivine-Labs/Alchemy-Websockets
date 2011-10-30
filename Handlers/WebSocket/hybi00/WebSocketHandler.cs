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
using Alchemy.Server.Classes;

namespace Alchemy.Server.Handlers.WebSocket.hybi00
{
    /// <summary>
    /// A threadsafe singleton that contains functions which are used to handle incoming connections for the WebSocket Protocol
    /// </summary>
    sealed class WebSocketHandler : Handler
    {
        private static WebSocketHandler _instance;

        private WebSocketHandler()
        {
            Authentication = Alchemy.Server.Handlers.WebSocket.hybi00.WebSocketAuthentication.Instance;
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
        /// <param name="AContext">The user context.</param>
        public override void HandleRequest(Context AContext)
        {
            if (AContext.IsSetup)
            {
                AContext.UserContext.DataFrame.Append(AContext.Buffer);
                if (AContext.UserContext.DataFrame.State == DataFrame.DataState.Complete)
                {
                    switch (AContext.UserContext.DataFrame.Length)
                    {
                        case 1:
                            //Process Command
                            string ACommand = AContext.UserContext.DataFrame.ToString();
                            if (ACommand == AContext.Server.PingCommand)
                            {
                                SendPingResponse(AContext);
                                AContext.Pings++;
                            }
                            else
                            {
                                AContext.Pings = 0;
                                AContext.UserContext.OnReceive();
                            }
                            if ((AContext.Pings >= AContext.Server.MaxPingsInSequence) && (AContext.Server.MaxPingsInSequence != 0))
                            {
                                AContext.Dispose();
                            }
                            break;
                        default:
                            AContext.Pings = 0;
                            AContext.UserContext.OnReceive();
                            break;
                    }
                }
                else if (AContext.UserContext.DataFrame.State == DataFrame.DataState.Closed)
                {
                    AContext.UserContext.Send(new byte[0], true);
                }
            }
            else
            {
                Authenticate(AContext);
            }
        }

        /// <summary>
        /// Attempts to authenticates the specified user context.
        /// If authentication fails it kills the connection.
        /// </summary>
        /// <param name="AContext">The user context.</param>
        private void Authenticate(Context AContext)
        {
            if (AContext.Handler.Authentication.CheckHandshake(AContext))
            {
                AContext.UserContext.Protocol = AContext.Header.Protocol;
                AContext.UserContext.RequestPath = AContext.Header.RequestPath;
                AContext.Header = null;
                AContext.IsSetup = true;
            }
            else
            {
                AContext.Dispose();
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
                byte[] WrappedData = AContext.UserContext.DataFrame.Wrap(Data);
                AsyncCallback ACallback = EndSend;
                if (Close)
                    ACallback = EndSendAndClose;
                AContext.SendReady.Wait();
                try
                {
                    AContext.Connection.Client.BeginSend(WrappedData, 0, WrappedData.Length, SocketFlags.None, ACallback, AContext);
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

        /// <summary>
        /// Sends the ping response.
        /// </summary>
        /// <param name="AContext">The user context.</param>
        private void SendPingResponse(Context AContext)
        {
            AContext.UserContext.Send(AContext.Server.PongCommand);
        }
    }
}
