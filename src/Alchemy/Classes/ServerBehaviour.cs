namespace Alchemy.Classes
{
    /// <summary>
    /// The server behaviour base class.
    /// </summary>
    public abstract class ServerBehaviour
    {
        /// <summary>
        /// The event triggered when the user is connected.
        /// </summary>
        /// <param name="context">The current user context.</param>
        public virtual void OnConnected(UserContext context)
        { }

        /// <summary>
        /// The event triggered when the server receive data.
        /// </summary>
        /// <param name="context">The current user context.</param>
        public virtual void OnReceive(UserContext context)
        { }

        /// <summary>
        /// The event triggered when the server send data.
        /// </summary>
        /// <param name="context">The current user context.</param>
        public virtual void OnSend(UserContext context)
        { }

        /// <summary>
        /// The event triggered when the user is disconnected.
        /// </summary>
        /// <param name="context">The current user context.</param>
        public virtual void OnDisconnect(UserContext context)
        { }
    }
}