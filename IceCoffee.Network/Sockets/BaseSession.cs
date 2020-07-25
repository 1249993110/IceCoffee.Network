using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using IceCoffee.Network.CatchException;
using IceCoffee.Network.Sockets.Primitives;

namespace IceCoffee.Network.Sockets
{
    /// <summary>
    /// 内部数据发送器
    /// </summary>
    /// <typeparam name="TSession"></typeparam>
    /// <param name="session"></param>
    /// <param name="data"></param>
    internal delegate void InternalSendDataEventHandler<TSession>(TSession session, byte[] data) where TSession : BaseSession<TSession>, new();

    /// <summary>
    /// 内部数据接收器
    /// </summary>
    internal delegate void InternalReceivedEventHandler();

    public delegate void InitializedEventHandler();
    public delegate void ReceivedDataEventHandler();
    public delegate void SessionStartedEventHandler();
    public delegate void ClosedEventHandler(SocketError closedReason);

    public class BaseSession : BaseSession<BaseSession>
    {
        public BaseSession() { }
    }

    public abstract class BaseSession<TSession> : ISession, IExceptionCaught where TSession : BaseSession<TSession>, new()
    {
        #region 字段 
        internal Socket socket;

        private ISocketDispatcher _socketDispatcher;

        private InternalSendDataEventHandler<TSession> _sendData;

        private ExceptionCaughtEventHandler _emitExceptionCaughtSignal;

        private int _sessionID;

        private DateTime _connectTime;

        internal readonly ReadBufferManager readBuffer;

        private KeepAlive _keepAlive;
        
        #endregion

        #region 属性
        /// <summary>
        /// 套接字调度者
        /// </summary>
        public ISocketDispatcher SocketDispatcher
        {
            get { return _socketDispatcher; }
        }
        /// <summary>
        /// 会话ID，Socket的操作系统句柄
        /// </summary>
        public int SessionID
        {
            get { return _sessionID; }
        }
        /// <summary>
        /// 会话连接时间
        /// </summary>
        public DateTime ConnectTime
        {
            get { return _connectTime; }
        }
        /// <summary>
        /// 远程IP终结点
        /// </summary>
        public IPEndPoint RemoteIPEndPoint
        {
            get { return (IPEndPoint)socket.RemoteEndPoint; }
        }
        /// <summary>
        /// 内部读取缓冲区
        /// </summary>
        public ReadBufferManager ReadBuffer { get { return readBuffer; } }

        /// <summary>
        /// Keep-Alive，必须在会话开始前设置（在Initialize中实例化，可在OnInitialized中初始化）
        /// </summary>
        public KeepAlive KeepAlive { get { return _keepAlive; } }

        /// <summary>
        /// 上次通讯时间
        /// </summary>
        public DateTime LastCommunicateTime { get; private set; }

        #endregion

        #region 事件
        /// <summary>
        /// 会话初始化，OnSessionInitialized 引发 SessionInitialized 事件。
        /// </summary>
        public event InitializedEventHandler SessionInitialized;

        /// <summary>
        /// 收到数据，OnReceivedData 引发 ReceivedData 事件。
        /// </summary>
        public event ReceivedDataEventHandler ReceivedData;

        /// <summary>
        /// 会话开始，OnSessionStarted 引发 SessionStarted 事件。
        /// </summary>
        public event SessionStartedEventHandler SessionStarted;

        /// <summary>
        /// 会话关闭，OnSessionClosed 引发 SessionClosed 事件。
        /// </summary>
        public event ClosedEventHandler SessionClosed;
        #endregion

        #region 方法
        public BaseSession()
        {
            readBuffer = new ReadBufferManager(this.OnReceived);            
        }

        /// <summary>
        /// 会话初始化后调用，会话池中的每个会话只会被初始化一次，而客户端会话会在每次重新连接时重新创建
        /// </summary>
        protected virtual void OnInitialized()
        {
            SessionInitialized?.Invoke();
        }

        /// <summary>
        /// 收到数据时调用
        /// </summary>
        /// <param name="data"></param>
        protected virtual void OnReceived()
        {
            LastCommunicateTime = DateTime.Now;
            ReceivedData?.Invoke();
        }

        /// <summary>
        /// 会话开始后调用
        /// </summary>
        protected virtual void OnStarted()
        {
            LastCommunicateTime = DateTime.Now;
            SessionStarted?.Invoke();
        }

        /// <summary>
        /// 会话关闭后调用
        /// </summary>
        protected virtual void OnClosed(SocketError closedReason)
        {
            SessionClosed?.Invoke(closedReason);
        }

        /// <summary>
        /// 发送数据
        /// </summary>
        /// <param name="data"></param>
        public virtual void Send(byte[] data)
        {
            _sendData.Invoke((TSession)this, data);
        } 

        /// <summary>
        /// 关闭会话
        /// </summary>
        [CatchException(Error = "关闭会话异常")]
        public void Close()
        {
            if (socket != null && socket.Connected)
            {
                try
                {
                    socket.Shutdown(SocketShutdown.Both);
                }
                finally
                {
                    socket.Close();
                    socket = null;
                }
            }
        }

        /// <summary>
        /// 为会话附加信息
        /// </summary>
        /// <param name="socket"></param>
        /// <param name="sessionID"></param>
        [CatchException(Error = "为会话附加信息异常")]
        internal void Attach(Socket socket, int sessionID)
        {
            this.socket = socket;
            this._sessionID = sessionID;
            this._connectTime = DateTime.Now;

            if (_keepAlive.Enable)
            {
                socket.IOControl(IOControlCode.KeepAliveValues, _keepAlive.GetKeepAliveData(), null);
            }

            OnStarted();
        }

        /// <summary>
        /// 清楚会话附加信息
        /// </summary>
        internal void Detach(SocketError closedReason)
        {
            OnClosed(closedReason);

            this.socket = null;
            this._sessionID = 0;
            this._connectTime = default(DateTime);//DateTime.MinValue
            this.LastCommunicateTime = default;
        }

        /// <summary>
        /// 初始化
        /// </summary>
        internal void Initialize(ISocketDispatcher socketDispatcher, 
            InternalSendDataEventHandler<TSession> internalSendDataEventHandler,
            Action<SocketAsyncEventArgs> saeaCollectEventHandler,
            ExceptionCaughtEventHandler exceptionCaughtEventHandler)
        {
            _keepAlive = new KeepAlive();
            this._socketDispatcher = socketDispatcher;
            this._sendData = internalSendDataEventHandler;
            this._emitExceptionCaughtSignal = exceptionCaughtEventHandler;
            readBuffer.Initialize(saeaCollectEventHandler, this);
            OnInitialized();
        }

        void IExceptionCaught.EmitSignal(NetworkException e)
        {
            _emitExceptionCaughtSignal.Invoke(e);
        }

        #endregion
    }
}
