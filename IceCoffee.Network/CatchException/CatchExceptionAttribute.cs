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
        private string _errorMessage;
        private CustomExceptionType _customExceptionType = CustomExceptionType.Unchecked;

        /// <summary>
        /// 错误信息
        /// </summary>
        public string ErrorMessage { get => _errorMessage; set => _errorMessage = value; }

        /// <summary>
        /// 异常类型
        /// </summary>
        public CustomExceptionType CustomExceptionType { get => _customExceptionType; set => _customExceptionType = value; }

        public CatchExceptionAttribute(string errorMessage)
        {
            _errorMessage = errorMessage;
        }

        public CatchExceptionAttribute(string errorMessage, CustomExceptionType customExceptionType)
        {
            _errorMessage = errorMessage;
            _customExceptionType = customExceptionType;
        }

        public override void OnException(MethodExecutionArgs args)
        {
            NetworkException networkException = new NetworkException(_errorMessage, args.Exception) { CustomExceptionType = _customExceptionType };

            args.FlowBehavior = FlowBehavior.Return;

            IExceptionCaught instance = args.Instance as IExceptionCaught;

            if (instance == null)
            {
                throw new NetworkException(string.Format("实例: {0} 必须继承IExceptionCaught", instance.GetType().Name));
            }
            else
            {
                instance.EmitSignal(instance, networkException);
            }
        }
    }
}