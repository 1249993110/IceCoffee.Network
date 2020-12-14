using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IceCoffee.Network.Sockets.Primitives.Internal
{
    /// <summary>
    /// 数据类型
    /// </summary>
    internal enum DataType : byte
    {
        /// <summary>
        /// 请求
        /// </summary>
        Request,

        /// <summary>
        /// 响应
        /// </summary>
        Response,

        /// <summary>
        /// 心跳包
        /// </summary>
        Heartbeat
    }
}
