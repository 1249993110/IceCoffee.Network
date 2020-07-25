using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IceCoffee.Network.CatchException
{
    internal interface IExceptionCaught
    {
        /// <summary>
        /// 发射异常捕获信号
        /// </summary>
        /// <param name="ex"></param>
        void EmitSignal(NetworkException ex);
    }
}
