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

using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using Alchemy.Classes;
using Alchemy.Handlers.WebSocket.hybi10;
using log4net;
using log4net.Config;

namespace Alchemy
{
    public delegate void OnEventDelegate(UserContext context);

    /// <summary>
    /// The Main WebSocket Server
    /// </summary>
    public class WebSocketServer : TcpServer, IDisposable
    {
        /// <summary>
        /// This is the Flash Access Policy Server. It allows us to facilitate flash socket connections much more quickly in most cases.
        /// Don't mess with it through here. It's only public so we can access it later from all the IOCPs.
        /// </summary>
        public AccessPolicyServer AccessPolicyServer;

        /// <summary>
        /// These are the default OnEvent delegates for the server. By default, all new UserContexts will use these events.
        /// It is up to you whether you want to replace them at runtime or even manually set the events differently per connection in OnReceive.
        /// </summary>
        public OnEventDelegate DefaultOnConnect = x => { };

        public OnEventDelegate DefaultOnConnected = x => { };
        public OnEventDelegate DefaultOnDisconnect = x => { };
        public OnEventDelegate DefaultOnReceive = x => { };
        public OnEventDelegate DefaultOnSend = x => { };

        /// <summary>
        /// Enables or disables the Flash Access Policy Server(APServer).
        /// This is used when you would like your app to only listen on a single port rather than 2.
        /// Warning, any flash socket connections will have an added delay on connection due to the client looking to port 843 first for the connection restrictions.
        /// </summary>
        public bool FlashAccessPolicyEnabled = true;


        /// <summary>
        /// 
        /// </summary>
        public ILog Log = LogManager.GetLogger("Alchemy.Log");

        public int MaxPingsInSequence;


        /// <summary>
        /// These are the command strings that the server and client will filter out and treat as heartbeats.
        /// </summary>
        public string PingCommand = "7";

        public string PongCommand = "7";

        /// <summary>
        /// Configuration for the above heartbeat setup.
        /// TimeOut : How long until a connection drops when it doesn't receive anything.
        /// MaxPingsInSequence : A multiple of TimeOut for how long a connection can remain idle(only pings received) before we kill it.
        /// </summary>
        public TimeSpan TimeOut = TimeSpan.FromMinutes(1);

        private string _destinationHost = String.Empty;
        private string _originHost = String.Empty;

        /// <summary>
        /// Initializes a new instance of the <see cref="WebSocketServer"/> class.
        /// </summary>
        /// <param name="listenPort">The listen port.</param>
        /// <param name="listenAddress">The listen ip.</param>
        public WebSocketServer(int listenPort = 0, IPAddress listenAddress = null) : base(listenPort, listenAddress)
        {
            LogConfigFile = "Alchemy.config";
            LoggerName = "Alchemy.Log";
        }

        /// <summary>
        /// Gets or sets the origin host.
        /// </summary>
        /// <value>
        /// The origin host.
        /// </value>
        public string OriginHost
        {
            get { return _originHost; }
            set
            {
                _originHost = value;
                WebSocketAuthentication.Origin = value;
            }
        }

        /// <summary>
        /// Gets or sets the destination host.
        /// </summary>
        /// <value>
        /// The destination host.
        /// </value>
        public string DestinationHost
        {
            get { return _destinationHost; }
            set
            {
                _destinationHost = value;
                WebSocketAuthentication.Location = value;
            }
        }

        /// <summary>
        /// Sets the name of the logger.
        /// </summary>
        /// <value>
        /// The name of the logger.
        /// </value>
        public string LoggerName
        {
            set { Log = LogManager.GetLogger(value); }
        }

        /// <summary>
        /// Sets the log config file name.
        /// </summary>
        /// <value>
        /// The log config file name.
        /// </value>
        public string LogConfigFile
        {
            set { XmlConfigurator.Configure(new FileInfo(value)); }
        }

        /// <summary>
        /// Starts this instance.
        /// </summary>
        public override void Start()
        {
            base.Start();
            if (AccessPolicyServer == null)
            {
                try
                {
                    AccessPolicyServer = new AccessPolicyServer(ListenAddress, OriginHost, Port);

                    if (FlashAccessPolicyEnabled)
                    {
                        AccessPolicyServer.Start();
                    }
                }
                    // ReSharper disable EmptyGeneralCatchClause
                catch
                    // ReSharper restore EmptyGeneralCatchClause
                {
                    /* Ignore */
                }
            }
            Log.Info("Alchemy Server Started");
        }

        /// <summary>
        /// Stops this instance.
        /// </summary>
        public override void Stop()
        {
            try
            {
                if ((AccessPolicyServer != null) && (FlashAccessPolicyEnabled))
                {
                    AccessPolicyServer.Stop();
                }
            }
                // ReSharper disable EmptyGeneralCatchClause
            catch
                // ReSharper restore EmptyGeneralCatchClause
            {
                /* Ignore */
            }
            AccessPolicyServer = null;
            Log.Info("Alchemy Server Stopped");
        }

        /// <summary>
        /// Fires when a client connects.
        /// </summary>
        /// <param name="connection">The TCP Connection.</param>
        protected override void OnRunClient(TcpClient connection)
        {
            using (var context = new Context())
            {
                context.Server = this;
                context.Connection = connection;
                context.UserContext.ClientAddress = context.Connection.Client.RemoteEndPoint;
                context.UserContext.SetOnConnect(DefaultOnConnect);
                context.UserContext.SetOnConnected(DefaultOnConnected);
                context.UserContext.SetOnDisconnect(DefaultOnDisconnect);
                context.UserContext.SetOnSend(DefaultOnSend);
                context.UserContext.SetOnReceive(DefaultOnReceive);
                context.BufferSize = DefaultBufferSize;
                context.UserContext.OnConnect();
                try
                {
                    while (context.Connection.Connected)
                    {
                        if (context.ReceiveReady.Wait(TimeOut))
                        {
                            context.Connection.Client.BeginReceive(context.Buffer, 0, context.Buffer.Length,
                                                                   SocketFlags.None, DoReceive, context);
                        }
                        else
                        {
                            break;
                        }
                    }
                }
                catch (Exception e)
                {
                    Log.Debug("Client Forcefully Disconnected", e);
                }
            }
        }

        /// <summary>
        /// The root receive event for each client. Executes in it's own thread.
        /// </summary>
        /// <param name="result">The Async result.</param>
        private void DoReceive(IAsyncResult result)
        {
            var context = (Context) result.AsyncState;
            context.Reset();
            try
            {
                context.ReceivedByteCount = context.Connection.Client.EndReceive(result);
            }
            catch (Exception e)
            {
                Log.Debug("Client Forcefully Disconnected", e);
            }

            if (context.ReceivedByteCount > 0)
            {
                context.Handler.HandleRequest(context);
                context.ReceiveReady.Release();
            }
            else
            {
                context.Dispose();
                context.ReceiveReady.Release();
            }
        }
    }
}