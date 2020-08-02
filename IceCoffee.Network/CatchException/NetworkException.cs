using IceCoffee.Common;
using System;

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