using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace IceCoffee.Network.Sockets.Primitives
{
    public interface ISession
    {
        /// <summary>
        /// 会话ID，Socket的操作系统句柄
        /// </summary>
        int SessionID { get; }

        /// <summary>
        /// 套接字调度者
        /// </summary>
        ISocketDispatcher SocketDispatcher { get; }

        /// <summary>
        /// 会话连接时间
        /// </summary>
        DateTime ConnectTime { get; }

        /// <summary>
        /// 远程IP终结点
        /// </summary>
        IPEndPoint RemoteIPEndPoint { get; }

        /// <summary>
        /// 关闭会话
        /// </summary>
        void Close();

    }
}
