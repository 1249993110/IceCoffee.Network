namespace IceCoffee.Network.CatchException
{
    /// <summary>
    /// 异常捕获事件处理器
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="ex"></param>
    public delegate void ExceptionCaughtEventHandler(object sender, NetworkException ex);
}