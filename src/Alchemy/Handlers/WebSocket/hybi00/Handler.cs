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
                if (_instance == null)
                {
                    lock (createLock)
                    {
                        if (_instance == null)
                        {
                            _instance = new Handler();
                        }
                    }
                }

                return _instance;
            }
        }
    }
}