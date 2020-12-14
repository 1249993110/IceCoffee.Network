using System;
using System.Net;

namespace IceCoffee.Network.Sockets.Primitives.TcpSession
{
    /// <summary>
    /// ITcpSession
    /// </summary>
    public interface ITcpSession
    {
        /// <summary>
        /// 内部读取缓冲区
        /// </summary>
        ReadBufferManager ReadBuffer { get; }

        /// <summary>
        /// 套接字调度者
        /// </summary>
        ISocketDispatcher SocketDispatcher { get; }

        /// <summary>
        /// 会话ID，Socket的操作系统句柄
        /// </summary>
        int SessionID { get; }

        /// <summary>
        /// 会话连接时间
        /// </summary>
        DateTime ConnectTime { get; }

        /// <summary>
        /// 远程IP终结点
        /// </summary>
        IPEndPoint RemoteIPEndPoint { get; }

        /// <summary>
        /// Keep-Alive，必须在会话开始前设置（在Initialize中实例化，可在OnInitialized中初始化）
        /// </summary>
        KeepAlive KeepAlive { get; }


        /// <summary>
        /// 会话初始化，OnSessionInitialized 引发 SessionInitialized 事件。
        /// </summary>
        event InitializedEventHandler SessionInitialized;

        /// <summary>
        /// 收到数据，OnReceivedData 引发 ReceivedData 事件。
        /// </summary>
        event ReceivedDataEventHandler ReceivedData;

        /// <summary>
        /// 会话开始，OnSessionStarted 引发 SessionStarted 事件。
        /// </summary>
        event SessionStartedEventHandler SessionStarted;

        /// <summary>
        /// 会话关闭，OnSessionClosed 引发 SessionClosed 事件。
        /// </summary>
        event ClosedEventHandler SessionClosed;

        /// <summary>
        /// 发送数据
        /// </summary>
        /// <param name="data"></param>
        void Send(byte[] data);

        /// <summary>
        /// 发送数据
        /// </summary>
        /// <param name="data"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        void Send(byte[] data, int offset, int count);

        /// <summary>
        /// 关闭会话
        /// </summary>
        void Close();
    }
}