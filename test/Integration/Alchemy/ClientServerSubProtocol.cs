using System.Net;
using System.Threading;
using Alchemy.Classes;
using NUnit.Framework;

namespace Alchemy
{
    [TestFixture]
    public class ClientServerSubProtocol
    {
        private WebSocketServer _server;
        private WebSocketClient _client;

        [TearDown]
        public void TearDown()
        {
            _client.Disconnect();
            _server.Stop();
            _client = null;
            _server = null;
        }

        [Test]
        public void ClientShouldNotConnectWithInvalidProtocol()
        {
            _server = new WebSocketServer(54321, IPAddress.Loopback);
            _server.Start();
            _client = new WebSocketClient("ws://127.0.0.1:54321/path")
                          {Origin = "localhost", SubProtocols = new[] {"test", "test2"}};

            _client.Connect();

            Thread.Sleep(500);

            Assert.IsFalse(_client.IsAuthenticated);
        }

        [Test]
        public void ClientShoulConnectWithValidProtocol()
        {
            _server = new WebSocketServer(54321, IPAddress.Loopback) { SubProtocols = new[] { "test" }};
            _server.Start();
            _client = new WebSocketClient("ws://127.0.0.1:54321/path") { Origin = "localhost", SubProtocols = new[] { "test", "test2" } };

            _client.Connect();

            Assert.IsTrue(_client.IsAuthenticated);
            Assert.AreEqual("test", _client.CurrentProtocol);
        }

        [Test]
        public void ClientShoulConnectWithSecondaryValidProtocol()
        {
            _server = new WebSocketServer(54321, IPAddress.Loopback) { SubProtocols = new[] { "test2" } };
            _server.Start();
            _client = new WebSocketClient("ws://127.0.0.1:54321/path") { Origin = "localhost", SubProtocols = new[] { "test", "test2" } };

            _client.Connect();

            Assert.IsTrue(_client.IsAuthenticated);
            Assert.AreEqual("test2", _client.CurrentProtocol);
        }
    }
}