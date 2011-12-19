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
        private bool _clientSendDataPassed = false;
        [TestFixtureSetUp]
        public void SetUp()
        {
            _server = new WebSocketServer(54321, IPAddress.Loopback);
            _server.DefaultOnReceive = new OnEventDelegate(OnServerReceive);
            _server.Start();
            _client = new WebSocketClient {Host = "127.0.0.1", Port = 54321, Origin = "localhost"};
            _client.OnReceive = new OnEventDelegate(OnClientReceive);
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

        [Test]
        public void ClientConnect()
        {
            Assert.IsTrue(_client.Connected);
        }

        [Test]
        public void ClientSendData()
        {
            if (_client.Connected)
            {
                _client.Send("Test");
                Thread.Sleep(1000);
            }

            Assert.IsTrue(_clientSendDataPassed);
        }

        private void OnServerReceive(UserContext context)
        {
            string data = context.DataFrame.ToString();
            context.Send(data);
        }

        private void OnClientReceive(UserContext context)
        {
            string data = context.DataFrame.ToString();
            if(data == "Test")
            {
                _clientSendDataPassed = true;
            }
        }
    }
}
