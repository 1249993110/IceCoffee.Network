using IceCoffee.Network.Sockets.Primitives.TcpClient;
using IceCoffee.Network.Sockets.Primitives.TcpServer;
using IceCoffee.Network.Sockets.Primitives.TcpSession;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Diagnostics;
using System.Threading;

namespace UnitTest
{
    class TcpSession : CustomSession<TcpSession>
    {
        protected override void OnReceived(object obj)
        {
            Debug.WriteLine(obj);
        }
    }

    class TcpServer : CustomServer<TcpSession>
    {
    }

    class TcpClient : CustomClient<TcpSession>
    {
        protected override void OnConnected()
        {
            Session.Send("hello world");
        }
    }

    [TestClass]
    public class UnitTest1
    {
        private TcpServer _tcpServer;

        private TcpClient _tcpClient;

        [TestMethod]
        public void TestMethod1()
        {
            _tcpServer = new TcpServer();
            _tcpServer.ExceptionCaught += _tcpServer_ExceptionCaught;
            _tcpServer.Start(10000);

            _tcpClient = new TcpClient();
            _tcpClient.ExceptionCaught += _tcpClient_ExceptionCaught;
            _tcpClient.Connect("localhost", 10000); 
            
            while (true)
            {
                Thread.Sleep(20);
            }
        }

        private void _tcpClient_ExceptionCaught(object sender, IceCoffee.Network.CatchException.NetworkException ex)
        {
            Debug.WriteLine(ex);
        }

        private void _tcpServer_ExceptionCaught(object sender, IceCoffee.Network.CatchException.NetworkException ex)
        {
            Debug.WriteLine(ex);
        }
    }
}
