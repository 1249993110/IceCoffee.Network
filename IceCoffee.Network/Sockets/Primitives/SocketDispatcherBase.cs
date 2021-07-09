using IceCoffee.Common;
using IceCoffee.Network.CatchException;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace IceCoffee.Network.Sockets.Primitives
{
    /// <summary>
    /// SocketDispatcherBase
    /// </summary>
    public abstract class SocketDispatcherBase : ISocketDispatcherBase
    {
        public abstract int ReceiveBufferSize { get; set; }
        public abstract int SendBufferSize { get; set; }

        public abstract IPEndPoint LocalIPEndPoint { get; }

        public abstract bool IsServerSide { get; }

        public abstract void Dispose();

        [CatchException("设置缓冲区错误", CustomExceptionType.Checked)]
        internal void SetBufferSize(ref int buffer, int value)
        {
            if (value < 1 || value > 1048576) // 65535
            {
                throw new ArgumentOutOfRangeException("缓冲区大小范围为：1-1048576");
            }

            buffer = value;
        }
        /// <summary>
        /// 发射异常捕获信号
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="ex"></param>
        internal abstract void EmitExceptionCaughtSignal(object sender, NetworkException ex);
    }
}
