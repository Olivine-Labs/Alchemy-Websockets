using System.Net;
using System.Threading;
using Alchemy.Classes;
using NUnit.Framework;

namespace Alchemy
{
    [TestFixture]
    public class ClientServer
    {
        private WebSocketServer _server;
        private WebSocketClient _client;
        private bool _forever;
        private bool _clientDataPass = true;

        [TestFixtureSetUp]
        public void SetUp()
        {
            _server = new WebSocketServer(54321, IPAddress.Loopback) {OnReceive = OnServerReceive};
            _server.Start();
            _client = new WebSocketClient("ws://127.0.0.1:54321/path") { Origin = "localhost", OnReceive = OnClientReceive };
            _client.Connect();
        }

        [TestFixtureTearDown]
        public void TearDown()
        {
            _client.Disconnect();
            _server.Stop();
            _client = null;
            _server = null;
        }

        private static void OnServerReceive(UserContext context)
        {
            var data = context.DataFrame.ToString();
            context.Send(data);
        }

        private void OnClientReceive(UserContext context)
        {
            var data = context.DataFrame.ToString();
            if (data == "Test")
            {
                if (_forever && _clientDataPass)
                {
                    context.Send(data);
                }
            }
            else
            {
                _clientDataPass = false;
            }
        }

        [Test]
        public void ClientConnect()
        {
            Assert.IsTrue(_client.Connected);
        }

        [Test]
        public void ClientSendData()
        {
            _forever = false;
            if (_client.Connected)
            {
                _client.Send("Test");
                Thread.Sleep(1000);
            }
            Assert.IsTrue(_clientDataPass);
        }

        [Test]
        public void ClientSendDataConcurrent()
        {
            _forever = true;
            if (_client.Connected)
            {
                var client2 = new WebSocketClient("ws://127.0.0.1:54321/path") { OnReceive = OnClientReceive };
                client2.Connect();

                if (client2.Connected)
                {
                    _client.Send("Test");
                    client2.Send("Test");
                }
                else
                {
                    _clientDataPass = false;
                }
                Thread.Sleep(5000);
            }
            else
            {
                _clientDataPass = false;
            }
            Assert.IsTrue(_clientDataPass);
        }
    }
}