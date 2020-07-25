using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IceCoffee.Common;

namespace IceCoffee.Network.CatchException
{
    /// <summary>
    /// 网络异常
    /// </summary>
    public class NetworkException : CustomExceptionBase
    {
        public NetworkException(string message) : base(message)
        {

        }
        public NetworkException(string message, Exception innerException) : base(message, innerException)
        {

        }
    }
}
