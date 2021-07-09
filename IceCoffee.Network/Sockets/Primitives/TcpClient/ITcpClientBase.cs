using IceCoffee.Network.CatchException;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IceCoffee.Network.Sockets.Primitives.TcpClient
{
    /// <summary>
    /// TcpClient
    /// </summary>
    public interface ITcpClientBase : ISocketDispatcherBase
    {
        /// <summary>
        /// 连接状态
        /// </summary>
        ConnectionState ConnectionState { get; }

        /// <summary>
        /// 是否已经连接成功
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// 连接超时范围, 默认10秒
        /// </summary>
        int ConnectTimedOutSpan { get; set; }

        /// <summary>
        /// 如果在断开连接后不需要重连；请设置此属性小于或等于0
        /// </summary>
        int AutoReconnectMaxCount { get; set; }

        /// <summary>
        /// 自动重连间隔，默认每20秒重连一次，一般比连接超时范围长
        /// </summary>
        int AutoReconnectInterval { get; set; }


        /// <summary>
        /// 连接成功，OnConnected 引发 Connected 事件。
        /// </summary>
        event ConnectedEventHandler Connected;

        /// <summary>
        /// 失去连接，OnDisconnected 引发 Disconnected 事件。
        /// </summary>
        event DisconnectedEventHandler Disconnected;

        /// <summary>
        /// 自动重连失败
        /// </summary>
        event AutoReconnectDefeatedEventHandler ReconnectDefeated;

        /// <summary>
        /// 连接状态改变
        /// </summary>
        event ConnectionStateChangedEventHandler ConnectionStateChanged;

        /// <summary>
        /// 异常捕获
        /// </summary>
        event ExceptionCaughtEventHandler ExceptionCaught;


        /// <summary>
        /// 连接服务端
        /// </summary>
        /// <param name="ipStr"></param>
        /// <param name="port"></param>
        void Connect(string ipStr, ushort port);

        /// <summary>
        /// 使用上次尝试连接的参数重新连接
        /// </summary>
        void Reconnect();

        /// <summary>
        /// 断开连接
        /// </summary>
        void Disconnect();
    }
}
