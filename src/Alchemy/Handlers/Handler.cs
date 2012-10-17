using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Alchemy.Classes;
using Alchemy.Handlers.WebSocket;

namespace Alchemy.Handlers
{
    /// <summary>
    /// When the protocol has not yet been determined the system defaults to this request handler.
    /// Singleton, just like the other handlers.
    /// </summary>
    public class Handler
    {
        private static Handler _instance;

        protected static SemaphoreSlim CreateLock = new SemaphoreSlim(1);
        internal IAuthentication Authentication;
        protected Handler() {}

        public static Handler Instance
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

        /// <summary>
        /// Handles the initial request.
        /// Attempts to process the header that should have been sent.
        /// Otherwise, through magic and wizardry, the client gets disconnected.
        /// </summary>
        /// <param name="context">The user context.</param>
        public virtual void HandleRequest(Context context)
        {
            if (context.IsSetup)
            {
                context.Disconnect();
            }
            else
            {
                ProcessHeader(context);
            }
        }

        /// <summary>
        /// Processes the header.
        /// </summary>
        /// <param name="context">The user context.</param>
        public void ProcessHeader(Context context)
        {
            string data = Encoding.UTF8.GetString(context.Buffer, 0, context.ReceivedByteCount);
            //Check first to see if this is a flash socket XML request.
            if (data == "<policy-file-request/>\0")
            {
                //if it is, we access the Access Policy Server instance to send the appropriate response.
                context.Server.AccessPolicyServer.SendResponse(context.Connection);
                context.Disconnect();
            }
            else //If it isn't, process http/websocket header as normal.
            {
                context.Header = new Header(data);
                switch (context.Header.Protocol)
                {
                    case Protocol.WebSocketHybi00:
                        context.Handler = WebSocket.hybi00.Handler.Instance;
                        context.UserContext.DataFrame = new WebSocket.hybi00.DataFrame();
                        break;
                    case Protocol.WebSocketRFC6455:
                        context.Handler = WebSocket.rfc6455.Handler.Instance;
                        context.UserContext.DataFrame = new WebSocket.rfc6455.DataFrame();
                        break;
                    default:
                        context.Header.Protocol = Protocol.None;
                        break;
                }
                if (context.Header.Protocol != Protocol.None)
                {
                    context.Handler.HandleRequest(context);
                }
                else
                {
                    context.UserContext.Send(Response.NotImplemented, true, true);
                }
            }
        }

        /// <summary>
        /// Sends the specified data.
        /// </summary>
        /// <param name="dataFrame">The data.</param>
        /// <param name="context">The user context.</param>
        /// <param name="raw">whether or not to send raw data</param>
        /// <param name="close">if set to <c>true</c> [close].</param>
        public void Send(DataFrame dataFrame, Context context, bool raw = false, bool close = false)
        {
            if (context.Connected)
            {
                AsyncCallback callback = EndSend;
                if (close)
                {
                    callback = EndSendAndClose;
                }
                context.SendReady.Wait();
                try
                {
                    List<ArraySegment<byte>> data = raw ? dataFrame.AsRaw() : dataFrame.AsFrame();
                    context.Connection.Client.BeginSend(data, SocketFlags.None,
                                                        callback,
                                                        context);
                }
                catch
                {
                    context.Disconnect();
                }
            }
        }

        /// <summary>
        /// Ends the send.
        /// </summary>
        /// <param name="result">The Async result.</param>
        public void EndSend(IAsyncResult result)
        {
            var context = (Context) result.AsyncState;
            try
            {
                context.Connection.Client.EndSend(result);
                context.SendReady.Release();
            }
            catch
            {
                context.Disconnect();
            }
            context.UserContext.OnSend();
        }

        /// <summary>
        /// Ends the send and closes the connection.
        /// </summary>
        /// <param name="result">The Async result.</param>
        public void EndSendAndClose(IAsyncResult result)
        {
            var context = (Context) result.AsyncState;
            EndSend(result);
            context.Disconnect();
        }
    }
}