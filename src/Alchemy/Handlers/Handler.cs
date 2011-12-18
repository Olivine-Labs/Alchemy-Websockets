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
using System.Threading;
using Alchemy.Classes;
using Alchemy.Handlers.WebSocket;

namespace Alchemy.Handlers
{
    /// <summary>
    /// Abstract class forcing handlers to implement certain methods so we can use them all the same way.
    /// Polymorphism!
    /// </summary>
    public abstract class Handler
    {
        protected static SemaphoreSlim CreateLock = new SemaphoreSlim(1);
        public IWebSocketAuthentication Authentication;
        public abstract void HandleRequest(Context request);
        public abstract void Send(DataFrame dataFrame, Context context, bool close = false);
        public abstract void EndSend(IAsyncResult result);
        public abstract void EndSendAndClose(IAsyncResult result);
    }
}