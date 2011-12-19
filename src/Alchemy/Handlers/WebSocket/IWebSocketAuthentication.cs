namespace Alchemy.Handlers.WebSocket
{
    /// <summary>
    /// Handles the handshaking between the client and the host, when a new connection is created
    /// </summary>
    internal interface IWebSocketAuthentication : IAuthentication
    {
        void SetOrigin(string origin);
        void SetLocation(string location);
    }
}