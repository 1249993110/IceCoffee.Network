using IceCoffee.Common;
using PostSharp.Aspects;
using PostSharp.Serialization;
using System;

namespace IceCoffee.Network.CatchException
{
    [PSerializable]
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
    internal sealed class CatchExceptionAttribute : OnMethodBoundaryAspect
    {
        /// <summary>
        /// 错误信息
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// 异常类型
        /// </summary>
        public CustomExceptionType CustomExceptionType { get; set; } = CustomExceptionType.Unchecked;

        public CatchExceptionAttribute()
        {
        }

        public CatchExceptionAttribute(string errorMessage)
        {
            ErrorMessage = errorMessage;
        }

        public CatchExceptionAttribute(string errorMessage, CustomExceptionType customExceptionType)
        {
            ErrorMessage = errorMessage;
            CustomExceptionType = customExceptionType;
        }

        public override void OnException(MethodExecutionArgs args)
        {
            NetworkException networkException = new NetworkException(ErrorMessage, args.Exception) { CustomExceptionType = CustomExceptionType };

            args.FlowBehavior = FlowBehavior.Return;

            IExceptionCaught instance = args.Instance as IExceptionCaught;

            System.Diagnostics.Debug.Assert(instance != null);

            instance.EmitSignal(instance, networkException);
        }
    }
}