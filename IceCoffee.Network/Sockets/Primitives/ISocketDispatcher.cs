using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using IceCoffee.Network.CatchException;

namespace IceCoffee.Network.Sockets.Primitives
{
    public interface ISocketDispatcher
    {
        /// <summary>
        /// <para>每次接收数据的缓冲区大小，默认为4096字节/会话</para> 
        /// <para>必须在socket启动前设置。注意此属性与ReadBuffer读取缓冲区是不同的</para> 
        /// </summary>
        int ReceiveBufferSize { get; set; }

        /// <summary>
        /// <para>每次发送数据的缓冲区大小，默认为4096字节/会话</para> 
        /// <para>必须在socket启动前设置。大于此大小的数据包将被作为临时的新缓冲区发送，此过程影响性能</para> 
        /// </summary>
        int SendBufferSize { get; set; }

        /// <summary>
        /// 本地IP终结点
        /// </summary>
        IPEndPoint LocalIPEndPoint { get; }

        /// <summary>
        /// 是否作为服务端
        /// </summary>
        bool IsServer { get; }
    }
}
