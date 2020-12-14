using IceCoffee.Common;
using IceCoffee.Network.CatchException;
using IceCoffee.Network.Sockets.Pool;
using IceCoffee.Network.Sockets.Primitives;
using IceCoffee.Network.Sockets.Primitives.Internal;
using IceCoffee.Network.Sockets.Primitives.TcpSession;
using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace IceCoffee.Network.Sockets.Primitives.TcpClient
{
    public class BasicClient : TcpClientBase<BasicSession>
    {
    }
}