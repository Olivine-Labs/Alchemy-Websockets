using System;
using System.Collections.Concurrent;
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
    public class Handler : IDisposable
    {
        private static Handler _instance;

        protected static object createLock = new object();
        internal IAuthentication Authentication;

        private Thread[] ProcessSendThreads = new Thread[Environment.ProcessorCount];

        private ConcurrentQueue<HandlerMessage> MessageQueue { get; set; }

        /// <summary>
        /// Cancellation of threads if disposing
        /// </summary>
        public static CancellationTokenSource Shutdown = new CancellationTokenSource();

        /// <summary>
        /// The default behaviour for Alchemy clients and servers is to start one send-thread per CPU.
        /// These threads will dequeue messages from a single queue and send it to the sockets opened for each WebSocket partner.
        /// This scales good for a large number of clients ( > 1000), see issue #52.
        ///
        /// When FastDirectSendingMode is set to true before any Handlers are started up, the send-threads are not started.
        /// Messages are then sent directly from the multithreaded application to the underlaying socket buffer.
        /// The Send method may block a short time until the previous send operation has copied its data do the socket buffer.
        /// There is a considerable speed increase for applications needing fast roundtrip times and have a small number of clients:
        ///    FastDirectSendingMode = false:   33 request+responses per second - caused by a delay of 10ms in every dequeue operation.
        ///    FastDirectSendingMode = true:  2500 request+responses per second
        /// </summary>
        public static bool FastDirectSendingMode;

        protected Handler() {

            MessageQueue = new ConcurrentQueue<HandlerMessage>();

            int n = FastDirectSendingMode ? 0 : ProcessSendThreads.Length;
            for (int i = 0; i < n; i++)
            {
                ProcessSendThreads[i] = new Thread(ProcessSend);
                ProcessSendThreads[i].Name = "Alchemy Send Handler Thread " + (i + 1);
                ProcessSendThreads[i].Start();
            }
        }

        public static Handler Instance
        {
            get
            {
                if (_instance == null)
                {
                     lock(createLock){
                        if(_instance == null){
                            _instance = new Handler();
                        }
                    }
                }

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
                        context.Handler.UnregisterContext(context);
                        context.Handler = WebSocket.hybi00.Handler.Instance;
                        context.UserContext.DataFrame = new WebSocket.hybi00.DataFrame();
                        context.Handler.RegisterContext(context);
                        break;
                    case Protocol.WebSocketRFC6455:
                        context.Handler.UnregisterContext(context);
                        context.Handler = WebSocket.rfc6455.Handler.Instance;
                        context.UserContext.DataFrame = new WebSocket.rfc6455.DataFrame();
                        context.Handler.RegisterContext(context);
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

        private void ProcessSend()
        {
            while (!Shutdown.IsCancellationRequested)
            {
                while (MessageQueue.IsEmpty)
                {
                    Thread.Sleep(10);
                    if (Shutdown.IsCancellationRequested) return;
                }

                HandlerMessage message;

                if (!MessageQueue.TryDequeue(out message))
                {
                    continue;
                }

                Send(message);
            }
        }

        private void Send(HandlerMessage message)
        {
            message.Context.SendEventArgs.UserToken = message;

            try
            {
              message.Context.SendReady.Wait(message.Context.Cancellation.Token);
            }
            catch (OperationCanceledException)
            {
              return;
            }

            try
            {
                if (message.Context.Connected)
                {
                    List<ArraySegment<byte>> data = message.IsRaw ? message.DataFrame.AsRaw() : message.DataFrame.AsFrame();
                    message.Context.SendEventArgs.BufferList = data;
                    message.Context.Connection.Client.SendAsync(message.Context.SendEventArgs);
                }
            }
            catch
            {
                message.Context.Disconnect();
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
                HandlerMessage message = new HandlerMessage { DataFrame = dataFrame, Context = context, IsRaw = raw, DoClose = close };
                if (FastDirectSendingMode)
                {
                    Send(message);
                }
                else
                {
                    MessageQueue.Enqueue(message);
                }
            }
        }

        void SendEventArgs_Completed(object sender, SocketAsyncEventArgs e)
        {
            HandlerMessage message = (HandlerMessage)e.UserToken;

            if (e.SocketError != SocketError.Success)
            {
                message.Context.Disconnect();
                return;
            }

            message.Context.SendReady.Release();
            message.Context.UserContext.OnSend();

            if (message.DoClose)
            {
                message.Context.Disconnect();
            }
        }

        public void RegisterContext(Context context)
        {
            context.SendEventArgs.Completed += SendEventArgs_Completed;
        }

         public void UnregisterContext(Context context)
        {
            context.SendEventArgs.Completed -= SendEventArgs_Completed;
        }

        private class HandlerMessage
        {
            public DataFrame DataFrame { get; set;}
            public Context Context { get; set;}
            public Boolean IsRaw { get; set;}
            public Boolean DoClose { get; set;}
        }

        /// <summary>
        /// Dispose stops all send threads of this singleton.
        /// Therefore, Dispose may only be used, when the application shuts down.
        /// </summary>
        public void Dispose()
        {
            Shutdown.Cancel();
        }
    }
}
