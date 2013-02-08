using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Alchemy;
using Alchemy.Classes;
using System.Net;
using System.Collections.Concurrent;
using System.Threading;
using System.Security.Cryptography.X509Certificates;

namespace AlchemyTest2
{
    class Program
    {

        //Thread-safe collection of Online Connections.
        protected static ConcurrentDictionary<string, UserContext> UserContexts = new ConcurrentDictionary<string, UserContext>();
        
        static void Main(string[] args)
        {
            string SslFileName = @"C:\Astros\AstrosSocket\astros.com.pfx";
            string SslPassword = @"c0sm0s";
            bool IsSecure = true;

            WebSocketServer wss = new WebSocketServer(11234, IPAddress.Any)
            {
                OnReceive = OnReceive,
                OnConnect = OnConnect,
                OnSend = OnSend,
                OnDisconnect = OnDisconnect,
                OnConnected = OnConnected,
                IsSecure = IsSecure,
                SSLCertificate = new X509Certificate2(SslFileName, SslPassword)
            };

            Console.Title = "WebSocket Server Test";
            Console.WriteLine("Starting server...");
            wss.Start();
            Console.WriteLine(" ... Server started");

            Console.WriteLine("Waiting for incomming connections...");
            Console.WriteLine("[Press enter to end server]");

            string str = string.Empty;
            //while (string.IsNullOrEmpty(str) == true)
            {
                str = Console.ReadLine();
            }

            Console.WriteLine("Stopping server...");
            wss.Stop();
            Console.WriteLine(" ... Server stopped");

            // Force the program to terminate
            Environment.Exit(0);
        }

        public static void OnConnect(UserContext context)
        {
            Console.WriteLine("Client Connection From : " + context.ClientAddress.ToString());
            //UserContexts.TryAdd(context.ClientAddress.ToString(), context);
            //context.Send("Hello from server (" + DateTime.Now.ToString() + ")");
        }
        public static void OnConnected(UserContext context)
        {
            Console.WriteLine("Client Connected From : " + context.ClientAddress.ToString());
            UserContexts.TryAdd(context.ClientAddress.ToString(), context);
            context.Send("Hello from server (" + DateTime.Now.ToString() + ")");
        }
        public static void OnReceive(UserContext context)
        {
            string data = string.Empty;

            try
            {
                data = context.DataFrame.ToString();
                Console.WriteLine("Data Received From [" + context.ClientAddress.ToString() + "] - " + context.DataFrame.ToString());

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message.ToString());
            }

            //context.Send("Hello2");
            if (data == "PING")
            {
                context.Send("PONG!");
            }
        }
        public static void OnSend(UserContext context)
        {
            Console.WriteLine("Data Sent To : " + context.ClientAddress.ToString());
        }
        public static void OnDisconnect(UserContext context)
        {
            Console.WriteLine("Client Disconnected : " + context.ClientAddress.ToString());

            // Remove the connection Object from the thread-safe collection
            UserContext uc;
            UserContexts.TryRemove(context.ClientAddress.ToString(), out uc);
        }

    }
}
