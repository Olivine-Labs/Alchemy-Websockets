using Alchemy;
using Alchemy.Classes;
using Newtonsoft.Json;
using openHome.Base;
using openHome.GUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace openHome.Server
{
    public class WebSocketSrv
    {

        // Volatile is used as hint to the compiler that this data
        // member will be accessed by multiple threads.
        private volatile bool _shouldStop;
        private WebSocketServer aServer;
        private Dictionary<string, UserContext> connectedPeers;

        public WebSocketSrv()
        {
            this.connectedPeers = new Dictionary<string, UserContext>();

            aServer = new WebSocketServer(81, IPAddress.Any)
            {
                OnReceive = OnReceive,
                OnSend = OnSend,
                OnConnect = OnConnect,
                OnConnected = OnConnected,
                OnDisconnect = OnDisconnect,
                TimeOut = new TimeSpan(0, 5, 0)
            };

            aServer.ConnectRateLimit = 10;
        }

        private void OnDisconnect(Alchemy.Classes.UserContext context)
        {
            string peer = context.ClientAddress.ToString();
            Common.logMessage("WebSockets", peer + " disconnected");
            if (connectedPeers.ContainsKey(peer))
                connectedPeers.Remove(peer);

        }

        private void OnConnected(Alchemy.Classes.UserContext context)
        {
            string peer = context.ClientAddress.ToString();
            Common.logMessage("WebSockets", peer + " connected");
            if (connectedPeers.ContainsKey(peer))
                connectedPeers[peer] = context;
            else
                connectedPeers.Add(peer, context);
        }

        private void OnConnect(Alchemy.Classes.UserContext context)
        {
            return;
        }

        private void OnSend(Alchemy.Classes.UserContext context)
        {


        }

        private void OnReceive(Alchemy.Classes.UserContext context)
        {
            try
            {
                context.Send(DateTime.Now.ToString());
                Common.logMessage("WebSockets", context.ClientAddress.ToString() + " sended " + context.DataFrame.ToString());
            }
            catch { }

        }

        /// <summary>
        /// Stop the timer 
        /// </summary>
        public void RequestStop()
        {
            _shouldStop = true;
        }

        /// <summary>
        /// Start timer sync to system clock and then start creating events
        /// </summary>
        public void startListen()
        {
            Common.logMessage("WebSockets", "Starting WebSockets Server...");
            aServer.Start();
            GuiQueue gq = GuiQueue.Instance;

            while (!_shouldStop)
            {

                if (gq.Count > 0)
                {
                    WidgetUpdateEvent ev = gq.Dequeue();
                    foreach (UserContext ctx in connectedPeers.Values)
                        ctx.Send(JsonConvert.SerializeObject(ev));
                }

                Thread.Sleep(100);
            }
        }

    }
}
