namespace Alchemy.Handlers.WebSocket.hybi00
{
    /// <summary>
    /// A threadsafe singleton that contains functions which are used to handle incoming connections for the WebSocket Protocol
    /// </summary>
    internal sealed class Handler : WebSocketHandler
    {
        private static Handler _instance;

        private Handler()
        {
            Authentication = new Authentication();
        }

        public new static Handler Instance
        {
            get
            {
                if (_instance != null)
                {
                    return _instance;
                }
                CreateLock.Wait();
                _instance = new Handler();
                CreateLock.Release();
                return _instance;
            }
        }
    }
}