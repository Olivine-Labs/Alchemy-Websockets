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