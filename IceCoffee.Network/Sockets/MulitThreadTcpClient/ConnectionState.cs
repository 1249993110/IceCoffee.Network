namespace IceCoffee.Network.Sockets.MulitThreadTcpClient
{
    /// <summary>
    /// 连接状态
    /// </summary>
    public enum ConnectionState
    {
        /// <summary>
        /// 未连接
        /// </summary>
        Disconnected,

        /// <summary>
        /// 正在断开
        /// </summary>
        Disconnecting,

        /// <summary>
        /// 正在连接
        /// </summary>
        Connecting,

        /// <summary>
        /// 已连接
        /// </summary>
        Connected,

        /// <summary>
        /// 正在自动重连
        /// </summary>
        AutoReconnecting
    }
}