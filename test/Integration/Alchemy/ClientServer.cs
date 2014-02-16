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
        private static int _requestId;
        private static int _requestId2;
        private static int _responseId;
        private static int _responseId2;

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
            Thread.Sleep(1);
            var data = context.DataFrame.ToString();
            if (data.StartsWith("Test" + _requestId.ToString()))
            {
                _requestId++;
            }
            else if (data.StartsWith("Hallo" + _requestId2.ToString()))
            {
                _requestId2++;
            }

            context.Send(data);
        }

        private void OnClientReceive(UserContext context)
        {
            var data = context.DataFrame.ToString();
            if (data.StartsWith("Test" + _responseId.ToString()))
            {
                _responseId++;
                if (_forever && _clientDataPass)
                {
                    context.Send("Test" + _responseId.ToString());
                }
            }
            else
            {
                _clientDataPass = false;
            }
        }

        private void OnClientReceive2(UserContext context)
        {
            var data = context.DataFrame.ToString();
            if (data.StartsWith("Hallo" + _responseId2.ToString()))
            {
                _responseId2++;
                if (_forever && _clientDataPass)
                {
                    context.Send("Hallo" + _responseId2.ToString());
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
            _clientDataPass = true;
            Alchemy.Handlers.Handler.FastDirectSendingMode = false;
            _requestId = 1;
            _responseId = 1;
            if (_client.Connected)
            {
                _client.Send("Test1");
                Thread.Sleep(200);
            }
            Assert.IsTrue(_clientDataPass);
            Assert.AreEqual(2, _requestId);
            Assert.AreEqual(2, _responseId);
        }

        [Test]
        public void ClientSendDataConcurrent()
        {
            _forever = true;
            _clientDataPass = true;
            Alchemy.Handlers.Handler.FastDirectSendingMode = false;
            _requestId = 1000;
            _responseId = 1000;
            _requestId2 = 2000;
            _responseId2 = 2000;
            var client2 = new WebSocketClient("ws://127.0.0.1:54321/path") { OnReceive = OnClientReceive2 };
            if (_client.Connected)
            {
                client2.Connect();

                if (client2.Connected)
                {
                    _client.Send("Test1000");
                    client2.Send("Hallo2000");
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
            Assert.IsTrue(_requestId == _responseId || _requestId - 1 == _responseId);
            Assert.IsTrue(_requestId2 == _responseId2 || _requestId2 - 1 == _responseId2);
            Assert.IsTrue(_responseId > 1010);
            Assert.IsTrue(_responseId2 > 2010);

            _client.Disconnect();
            client2.Disconnect();
            Assert.IsFalse(_client.Connected);
            Assert.IsFalse(client2.Connected);

            _client.Connect();
            Assert.IsTrue(_client.Connected);
        }

        [Test]
        public void ClientSendMultipleMessages()
        {
            _forever = false;
            _clientDataPass = true;
            Alchemy.Handlers.Handler.FastDirectSendingMode = true;
            _requestId = 10;
            _responseId = 10;
            Assert.IsTrue(_client.Connected);
            var longstring = " ".PadRight(500, '*'); // splitted header when 500 bytes !

            for (int i = 10; i < 20; i++)
            {
                _client.Send("Test" + i.ToString() + longstring);
            }
            Thread.Sleep(100);

            Assert.IsTrue(_clientDataPass);
            Assert.AreEqual(20, _requestId);
            Assert.AreEqual(20, _responseId);


            longstring = longstring.PadRight(400, 'X'); // 800 bytes ( > buffersize)

            for (int i = 20; i < 30; i++)
            {
                _client.Send("Test" + i.ToString() + longstring);
            }
            Thread.Sleep(100);

            Assert.IsTrue(_clientDataPass);
            Assert.AreEqual(30, _requestId);
            Assert.AreEqual(30, _responseId);
        }
    }
}