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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Alchemy.Server.Classes;
using log4net;
using System.IO;

namespace Alchemy.Server
{
    public delegate void OnEventDelegate(UserContext AContext);

    /// <summary>
    /// The Main WebSocket Server
    /// </summary>
    public class WSServer : IDisposable
    {

        /// <summary>
        /// Private, Internal variables.
        /// </summary>
        private int _Port = 81;
        private IPAddress _ListenerAddress = IPAddress.Any;

        /// <summary>
        /// The number of connected clients.
        /// </summary>
        /// 
        private int _Clients = 0;

        /// <summary>
        /// This Semaphore protects out clients variable on increment/decrement when a user connects/disconnects.
        /// </summary>
        private SemaphoreSlim ClientLock = new SemaphoreSlim(1);
        private int DefaultBufferSize = 512;
        private TcpListener Listener = null;

        /// <summary>
        /// This Semaphore limits how many connection events we have active at a time.
        /// </summary>
        private SemaphoreSlim ConnectReady = new SemaphoreSlim(10);

        private string _OriginHost = String.Empty;
        private string _DestinationHost = String.Empty;

        /// <summary>
        /// These are the default OnEvent delegates for the server. By default, all new UserContexts will use these events.
        /// It is up to you whether you want to replace them at runtime or even manually set the events differently per connection in OnReceive.
        /// </summary>
        public OnEventDelegate DefaultOnConnect = (x) => { };
        public OnEventDelegate DefaultOnDisconnect = (x) => { };
        public OnEventDelegate DefaultOnReceive = (x) => { };
        public OnEventDelegate DefaultOnSend = (x) => { };

        /// <summary>
        /// This is the Flash Access Policy Server. It allows us to facilitate flash socket connections much more quickly in most cases.
        /// Don't mess with it through here. It's only public so we can access it later from all the IOCPs.
        /// </summary>
        public APServer AccessPolicyServer = null;


        /// <summary>
        /// 
        /// </summary>
        public ILog Log = LogManager.GetLogger("Alchemy.Log");


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
        public int MaxPingsInSequence = 0;

        /// <summary>
        /// Enables or disables the Flash Access Policy Server(APServer).
        /// This is used when you would like your app to only listen on a single port rather than 2.
        /// Warning, any flash socket connections will have an added delay on connection due to the client looking to port 843 first for the connection restrictions.
        /// </summary>
        public bool FlashAPEnabled = true;

        /// <summary>
        /// Gets the client count.
        /// </summary>
        public int ClientCount
        { get { return _Clients; } }

        /// <summary>
        /// Gets or sets the port.
        /// </summary>
        /// <value>
        /// The port.
        /// </value>
        public int Port
        {
            get
            {
                return _Port;
            }
            set
            {
                _Port = value;
            }
        }

        /// <summary>
        /// Gets or sets the listener address.
        /// </summary>
        /// <value>
        /// The listener address.
        /// </value>
        public IPAddress ListenerAddress
        {
            get
            {
                return _ListenerAddress;
            }
            set
            {
                _ListenerAddress = value;
            }
        }

        /// <summary>
        /// Gets or sets the origin host.
        /// </summary>
        /// <value>
        /// The origin host.
        /// </value>
        public string OriginHost
        {
            get
            {
                return _OriginHost;
            }
            set
            {
                _OriginHost = value;
                Alchemy.Server.Handlers.WebSocket.hybi10.WebSocketAuthentication.Origin = value;
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
            get
            {
                return _DestinationHost;
            }
            set
            {
                _DestinationHost = value;
                Alchemy.Server.Handlers.WebSocket.hybi10.WebSocketAuthentication.Location = value;
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
            set
            {
                Log = LogManager.GetLogger(value);
            }
        }

        /// <summary>
        /// Sets the log config file name.
        /// </summary>
        /// <value>
        /// The log config file name.
        /// </value>
        public string LogConfigFile
        {
            set
            {
                log4net.Config.XmlConfigurator.Configure(new FileInfo(value));
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="WSServer"/> class.
        /// </summary>
        /// <param name="ListenPort">The listen port.</param>
        /// <param name="ListenIp">The listen ip.</param>
        public WSServer(int ListenPort = 0, IPAddress ListenIp = null)
        {
            LogConfigFile = "Alchemy.config";
            LoggerName = "Alchemy.Log";
            if(ListenPort > 0)
                _Port = ListenPort;
            if(ListenIp != null)
                _ListenerAddress = ListenIp;
        }

        /// <summary>
        /// Starts this instance.
        /// </summary>
        public void Start()
        {
            if (Listener == null)
            {
                try
                {
                    AccessPolicyServer = new APServer(ListenerAddress, OriginHost, Port);

                    if (FlashAPEnabled)
                    {
                        AccessPolicyServer.Start();
                    }

                    Listener = new TcpListener(ListenerAddress, Port);
                    ThreadPool.QueueUserWorkItem(Listen, null);
                }
                catch { /* Ignore */ }
            }
            Log.Info("Alchemy Server Started");
        }

        /// <summary>
        /// Stops this instance.
        /// </summary>
        public void Stop()
        {
            if (Listener != null)
            {
                try
                {
                    Listener.Stop();
                    if((AccessPolicyServer != null) && (FlashAPEnabled))
                        AccessPolicyServer.Stop();
                }
                catch { /* Ignore */ }
            }
            Listener = null;
            AccessPolicyServer = null;
            Log.Info("Alchemy Server Stopped");
        }

        /// <summary>
        /// Restarts this instance.
        /// </summary>
        public void Restart()
        {
            Stop();
            Start();
        }

        /// <summary>
        /// Listens for new connections.
        /// Utilizes a semaphore(ConnectReady) to manage how many active connect attempts we can manage concurrently.
        /// </summary>
        /// <param name="State">The state.</param>
        private void Listen(object State)
        {
            Listener.Start();
            while (Listener != null)
            {
                try
                {
                    Listener.BeginAcceptTcpClient(RunClient, null);
                    ConnectReady.Wait();
                }
                catch {/* Ignore */ }
            }
        }

        /// <summary>
        /// Runs the client.
        /// Sets up the UserContext.
        /// Executes in it's own thread.
        /// Utilizes a semaphore(ReceiveReady) to limit the number of receive events active for this client to 1 at a time.
        /// </summary>
        /// <param name="AResult">The A result.</param>
        private void RunClient(IAsyncResult AResult)
        {
            TcpClient AConnection = null;
            try
            {
                if (Listener != null)
                    AConnection = Listener.EndAcceptTcpClient(AResult);
            }
            catch (Exception e) { Log.Debug("Connect Failed", e); }

            ConnectReady.Release();
            if(AConnection != null)
            {
                ClientLock.Wait();
                _Clients++;
                ClientLock.Release();

                using (Context AContext = new Context())
                {
                    AContext.Server = this;
                    AContext.Connection = AConnection;
                    AContext.UserContext.ClientAddress = AContext.Connection.Client.RemoteEndPoint;
                    AContext.UserContext.SetOnConnect(DefaultOnConnect);
                    AContext.UserContext.SetOnDisconnect(DefaultOnDisconnect);
                    AContext.UserContext.SetOnSend(DefaultOnSend);
                    AContext.UserContext.SetOnReceive(DefaultOnReceive);
                    AContext.BufferSize = DefaultBufferSize;
                    AContext.UserContext.OnConnect();
                    try
                    {
                        while (AContext.Connection.Connected)
                        {
                            if (AContext.ReceiveReady.Wait(TimeOut))
                            {
                                AContext.Connection.Client.BeginReceive(AContext.Buffer, 0, AContext.Buffer.Length, SocketFlags.None, DoReceive, AContext);
                            }
                            else
                            {
                                break;
                            }
                        }
                    }
                    catch (Exception e) { Log.Debug("Client Forcefully Disconnected", e); }
                }

                ClientLock.Wait();
                _Clients--;
                ClientLock.Release();
            }
        }

        /// <summary>
        /// The root receive event for each client. Executes in it's own thread.
        /// </summary>
        /// <param name="AResult">The Async result.</param>
        private void DoReceive(IAsyncResult AResult)
        {
            Context AContext = (Context)AResult.AsyncState;
            AContext.Reset();
            try
            {
                AContext.ReceivedByteCount = AContext.Connection.Client.EndReceive(AResult);
            }
            catch (Exception e) { Log.Debug("Client Forcefully Disconnected", e); }

            if (AContext.ReceivedByteCount > 0)
            {
                AContext.ReceiveReady.Release();
                AContext.Handler.HandleRequest(AContext);
            }
            else
            {
                AContext.Dispose();
                AContext.ReceiveReady.Release();
            }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            Stop();
        }
    }
}