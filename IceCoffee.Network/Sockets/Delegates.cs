using IceCoffee.Network.Sockets.Primitives.TcpClient;
using IceCoffee.Network.Sockets.Primitives.TcpSession;
using System.Net.Sockets;

namespace IceCoffee.Network.Sockets
{
    #region Client

    /// <summary>
    /// 连接成功事件处理器
    /// </summary>
    public delegate void ConnectedEventHandler();

    /// <summary>
    /// 失去连接事件处理器
    /// </summary>
    /// <param name="closeReason"></param>
    public delegate void DisconnectedEventHandler(CloseReason closeReason);

    /// <summary>
    /// 自动重连失败事件处理器
    /// </summary>
    public delegate void AutoReconnectDefeatedEventHandler();

    /// <summary>
    /// 连接状态改变事件处理器
    /// </summary>
    /// <param name="connectionState"></param>
    public delegate void ConnectionStateChangedEventHandler(ConnectionState connectionState);
    #endregion Client

    #region Server

    /// <summary>
    /// 服务端启动事件处理器
    /// </summary>
    public delegate void StartedEventHandler();

    /// <summary>
    /// 服务端停止事件处理器
    /// </summary>
    public delegate void StoppedEventHandler();

    /// <summary>
    /// 新会话建立事件处理器
    /// </summary>
    /// <param name="session"></param>
    public delegate void NewSessionSetupEventHandler(ITcpSession session);

    /// <summary>
    /// 会话关闭事件处理器
    /// </summary>
    /// <param name="session"></param>
    /// <param name="closedReason"></param>
    public delegate void SessionClosedEventHandler(ITcpSession session, CloseReason closedReason);

    #endregion Server

    #region Session

    /// <summary>
    /// 内部数据发送事件处理器
    /// </summary>
    /// <param name="session"></param>
    /// <param name="data"></param>
    /// <param name="offset"></param>
    /// <param name="count"></param>
    internal delegate void InternalSendDataEventHandler(ITcpSession session, byte[] data, int offset, int count);

    /// <summary>
    /// 内部数据接收事件处理器
    /// </summary>
    internal delegate void InternalReceivedEventHandler();

    /// <summary>
    /// 初始化事件处理器
    /// </summary>
    public delegate void InitializedEventHandler();

    /// <summary>
    /// 收到数据事件处理器
    /// </summary>
    public delegate void ReceivedDataEventHandler(ITcpSession session);

    /// <summary>
    /// 会话开始事件处理器
    /// </summary>
    public delegate void SessionStartedEventHandler();

    /// <summary>
    /// 会话关闭事件处理器
    /// </summary>
    /// <param name="closedReason"></param>
    public delegate void ClosedEventHandler(CloseReason closedReason);

    #endregion Session
}