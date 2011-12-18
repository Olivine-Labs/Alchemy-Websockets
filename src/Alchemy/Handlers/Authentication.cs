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

using Alchemy.Classes;

namespace Alchemy.Handlers
{
    internal abstract class Authentication : IAuthentication
    {
        #region IAuthentication Members

        /// <summary>
        /// Attempts to authenticates the specified user context.
        /// If authentication fails it kills the connection.
        /// </summary>
        /// <param name="context">The user context.</param>
        public void Authenticate(Context context)
        {
            if (CheckAuthentication(context))
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

        #endregion

        protected abstract bool CheckAuthentication(Context context);
    }
}