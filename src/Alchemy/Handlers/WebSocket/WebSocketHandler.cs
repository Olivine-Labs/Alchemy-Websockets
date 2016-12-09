using System;
using System.Collections.Generic;
using Alchemy.Classes;

namespace Alchemy.Handlers.WebSocket
{
    internal class WebSocketHandler : Handler
    {
        /// <summary>
        /// Handles the request on the service side.
        /// </summary>
        /// <param name="context">The user context.</param>
        public override void HandleRequest(Context context)
        {
            int count = 0;
            if (context.IsSetup)
            {
                int remaining = context.ReceivedByteCount;
                while (remaining > 0)
                {
                    count++;
                    // add bytes to existing or empty frame
                    int readCount = context.UserContext.DataFrame.Append(context.Buffer, remaining, true, context.MaxFrameSize);

                    if (readCount < 0)
                    {
                        context.Disconnect(); //Disconnect if over MaxFrameSize
                        break;
                    }
                    else if (readCount == 0)
                    {
                        break; // partial header
                    }
                    else
                    {
                        switch (context.UserContext.DataFrame.State)
                        {
                            case DataFrame.DataState.Complete:
                                context.UserContext.OnReceive();
                                break;
                            case DataFrame.DataState.Closed:
                                context.UserContext.DataFrame.State = DataFrame.DataState.Complete;
                                // see http://stackoverflow.com/questions/17176827/websocket-close-packet, DataState.Closed is only set by rfc6455
                                var closeFrame = new byte[] { 0x88, 0x00 };
                                context.UserContext.Send(closeFrame, raw:true, close:true);
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

                        remaining -= readCount; // process rest of received bytes
                        if (remaining > 0)
                        {
                            context.Reset(); // starts new message when DataState.Complete
                            // move remaining bytes to beginning of array
                            Array.Copy(context.Buffer, readCount, context.Buffer, 0, remaining);
                        }
                    }
                }
            }
            else
            {
                Authentication.Authenticate(context);
            }
        }
    }
}