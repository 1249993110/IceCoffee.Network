using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PostSharp.Aspects;
using IceCoffee.Network.Sockets.Primitives;
using IceCoffee.Common;
using PostSharp.Serialization;

namespace IceCoffee.Network.CatchException
{
    [PSerializable]
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
    internal sealed class CatchExceptionAttribute : OnExceptionAspect
    {
        /// <summary>
        /// 错误信息
        /// </summary>
        public string Error { get; set; }

        /// <summary>
        /// 异常类型
        /// </summary>
        public CustomExceptionType CustomExceptionType { get; set; } = CustomExceptionType.Unchecked;

        public override void OnException(MethodExecutionArgs args)
        {
            NetworkException networkException = new NetworkException(Error, args.Exception) { CustomExceptionType = CustomExceptionType };
            //if (CustomExceptionType == CustomExceptionType.Unchecked)
            //{
            //    args.FlowBehavior = FlowBehavior.Return;
            //}
            //else
            //{
            //    throw networkException;
            //}
            args.FlowBehavior = FlowBehavior.Return;

            IExceptionCaught instance = args.Instance as IExceptionCaught;

            System.Diagnostics.Debug.Assert(instance != null);

            instance.EmitSignal(networkException);
        }
    }
}
