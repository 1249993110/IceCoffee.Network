using IceCoffee.Network.CatchException;
using IceCoffee.Network.Sockets.Primitives;
using System;
using System.Net;
using System.Net.Sockets;

namespace IceCoffee.Network.Sockets
{
    public class BaseSession : BaseSession<BaseSession>
    {
    }

    public abstract class BaseSession<TSession> : ISession, IExceptionCaught where TSession : BaseSession<TSession>, new()
    {
        #region 字段

        internal Socket socket;

        internal readonly ReadBufferManager readBuffer;

        private ISocketDispatcher _socketDispatcher;

        private InternalSendDataEventHandler<TSession> _sendData;

        private int _sessionID;

        private DateTime _connectTime;

        private KeepAlive _keepAlive;

        private DateTime _lastCommunicateTime;
        #endregion 字段

        #region 属性
        /// <summary>
        /// 内部读取缓冲区
        /// </summary>
        public ReadBufferManager ReadBuffer => readBuffer;

        /// <summary>
        /// 套接字调度者
        /// </summary>
        public ISocketDispatcher SocketDispatcher => _socketDispatcher;

        /// <summary>
        /// 会话ID，Socket的操作系统句柄
        /// </summary>
        public int SessionID => _sessionID;

        /// <summary>
        /// 会话连接时间
        /// </summary>
        public DateTime ConnectTime => _connectTime;

        /// <summary>
        /// 远程IP终结点
        /// </summary>
        public IPEndPoint RemoteIPEndPoint => socket.RemoteEndPoint as IPEndPoint;

        /// <summary>
        /// Keep-Alive，必须在会话开始前设置（在Initialize中实例化，可在OnInitialized中初始化）
        /// </summary>
        public KeepAlive KeepAlive => _keepAlive;

        /// <summary>
        /// 上次通讯时间
        /// </summary>
        public DateTime LastCommunicateTime => _lastCommunicateTime;

        #endregion 属性

        #region 事件

        /// <summary>
        /// 会话初始化，OnSessionInitialized 引发 SessionInitialized 事件。
        /// </summary>
        public event InitializedEventHandler SessionInitialized;

        /// <summary>
        /// 收到数据，OnReceivedData 引发 ReceivedData 事件。
        /// </summary>
        public event ReceivedDataEventHandler<TSession> ReceivedData;

        /// <summary>
        /// 会话开始，OnSessionStarted 引发 SessionStarted 事件。
        /// </summary>
        public event SessionStartedEventHandler SessionStarted;

        /// <summary>
        /// 会话关闭，OnSessionClosed 引发 SessionClosed 事件。
        /// </summary>
        public event ClosedEventHandler SessionClosed;

        #endregion 事件

        #region 方法

        public BaseSession()
        {
            readBuffer = new ReadBufferManager(this.OnInternalReceived);
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
        protected virtual void OnReceived()
        {
            ReceivedData?.Invoke(this as TSession);
        }

        private void OnInternalReceived()
        {
            _lastCommunicateTime = DateTime.Now;
            OnReceived();
        }

        /// <summary>
        /// 会话开始后调用
        /// </summary>
        protected virtual void OnStarted()
        {
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
        [CatchException("发送数据异常")]
        public virtual void Send(byte[] data)
        {
            if(socket != null)
            {
                _sendData.Invoke(this as TSession, data);
            }
            else
            {
                throw new NetworkException("会话已经关闭");
            }
        }

        /// <summary>
        /// 关闭会话
        /// </summary>
        [CatchException(ErrorMessage = "关闭会话异常")]
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
        [CatchException(ErrorMessage = "为会话附加信息异常")]
        internal void Attach(Socket socket, int sessionID)
        {
            this.socket = socket;
            this._sessionID = sessionID;
            this._connectTime = DateTime.Now;

            if (_keepAlive.Enable)
            {
                socket.IOControl(IOControlCode.KeepAliveValues, _keepAlive.GetKeepAliveData(), null);
            }

            _lastCommunicateTime = DateTime.Now;
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
            this._connectTime = default;
            this._lastCommunicateTime = default;
        }

        /// <summary>
        /// 初始化
        /// </summary>
        internal void Initialize(ISocketDispatcher socketDispatcher,
            InternalSendDataEventHandler<TSession> internalSendDataEventHandler,
            Func<SocketAsyncEventArgs, bool> saeaCollectEventHandler)
        {
            _keepAlive = new KeepAlive();
            this._socketDispatcher = socketDispatcher;
            this._sendData = internalSendDataEventHandler;
            readBuffer.Initialize(saeaCollectEventHandler, this);
            OnInitialized();
        }

        void IExceptionCaught.EmitSignal(object sender, NetworkException ex)
        {
            (_socketDispatcher as IExceptionCaught).EmitSignal(sender, ex);
        }

        #endregion 方法
    }
}