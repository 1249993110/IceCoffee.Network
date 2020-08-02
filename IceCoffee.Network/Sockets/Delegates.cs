using IceCoffee.Network.Sockets.MulitThreadTcpClient;
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
    public delegate void DisconnectedEventHandler(SocketError closeReason);

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
    /// <typeparam name="TSession"></typeparam>
    /// <param name="session"></param>
    public delegate void NewSessionSetupEventHandler<TSession>(TSession session) where TSession : BaseSession<TSession>, new();

    /// <summary>
    /// 会话关闭事件处理器
    /// </summary>
    /// <typeparam name="TSession"></typeparam>
    /// <param name="session"></param>
    /// <param name="closedReason"></param>
    public delegate void SessionClosedEventHandler<TSession>(TSession session, SocketError closedReason) where TSession : BaseSession<TSession>, new();

    #endregion Server

    #region Session

    /// <summary>
    /// 内部数据发送事件处理器
    /// </summary>
    /// <typeparam name="TSession"></typeparam>
    /// <param name="session"></param>
    /// <param name="data"></param>
    internal delegate void InternalSendDataEventHandler<TSession>(TSession session, byte[] data) where TSession : BaseSession<TSession>, new();

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
    public delegate void ReceivedDataEventHandler();

    /// <summary>
    /// 会话开始事件处理器
    /// </summary>
    public delegate void SessionStartedEventHandler();

    /// <summary>
    /// 会话关闭事件处理器
    /// </summary>
    /// <param name="closedReason"></param>
    public delegate void ClosedEventHandler(SocketError closedReason);

    #endregion Session
}