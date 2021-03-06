﻿namespace IceCoffee.Network.CatchException
{
    internal interface IExceptionCaught
    {
        /// <summary>
        /// 发射异常捕获信号
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="ex"></param>
        void EmitSignal(object sender, NetworkException ex);
    }
}