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
using Alchemy.Classes;

namespace Alchemy.Handlers.WebSocket
{
    internal class WebSocketHandler : Handler
    {
        /// <summary>
        /// Handles the request.
        /// </summary>
        /// <param name="context">The user context.</param>
        public override void HandleRequest(Context context)
        {
            if (context.IsSetup)
            {
                context.UserContext.DataFrame.Append(context.Buffer, true);
                switch (context.UserContext.DataFrame.State)
                {
                    case DataFrame.DataState.Complete:
                        context.UserContext.OnReceive();
                        break;
                    case DataFrame.DataState.Closed:
                        context.UserContext.Send(context.UserContext.DataFrame.CreateInstance(), false, true);
                        break;
                    case DataFrame.DataState.Ping:
                        context.UserContext.DataFrame.State = DataFrame.DataState.Complete;
                        DataFrame dataFrame = context.UserContext.DataFrame.CreateInstance();
                        dataFrame.State = DataFrame.DataState.Pong;
                        List<ArraySegment<byte>> pingData = context.UserContext.DataFrame.AsRaw();
                        foreach (var item in pingData)
                        {
                            dataFrame.Append(item.Array);
                        }
                        context.UserContext.Send(dataFrame);
                        break;
                    case DataFrame.DataState.Pong:
                        context.UserContext.DataFrame.State = DataFrame.DataState.Complete;
                        break;
                }
            }
            else
            {
                Authentication.Authenticate(context);
            }
        }
    }
}