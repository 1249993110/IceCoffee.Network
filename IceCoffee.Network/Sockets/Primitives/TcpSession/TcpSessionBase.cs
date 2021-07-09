using IceCoffee.Network.CatchException;
using IceCoffee.Network.Sockets.Primitives;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace IceCoffee.Network.Sockets.Primitives.TcpSession
{
    public abstract class TcpSessionBase<TSession> : ITcpSessionBase, IExceptionCaught where TSession : TcpSessionBase<TSession>, new()
    {
        #region 字段

        internal Socket socket;

        internal readonly ReadBufferManager readBuffer;

        private SocketDispatcherBase _socketDispatcher;

        private InternalSendDataEventHandler _sendData;

        private int _sessionID;

        private DateTime _connectTime;

        private KeepAlive _keepAlive;

        #endregion 字段

        #region 属性
        
        public ReadBufferManager ReadBuffer => readBuffer;

        public ISocketDispatcherBase SocketDispatcher => _socketDispatcher;

        public int SessionID => _sessionID;

        public DateTime ConnectTime => _connectTime;

        public IPEndPoint RemoteIPEndPoint => socket.RemoteEndPoint as IPEndPoint;

        public KeepAlive KeepAlive => _keepAlive;

        #endregion 属性

        #region 事件

        public event InitializedEventHandler SessionInitialized;

        public event ReceivedDataEventHandler ReceivedData;

        public event SessionStartedEventHandler SessionStarted;

        public event ClosedEventHandler SessionClosed;

        #endregion 事件

        #region 方法

        public TcpSessionBase()
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
        protected virtual void OnReceived()
        {
            ReceivedData?.Invoke(this);
        }

        /// <summary>
        /// 会话开始后调用
        /// </summary>
        internal protected virtual void OnStarted()
        {
            SessionStarted?.Invoke();
        }

        /// <summary>
        /// 会话关闭后调用
        /// </summary>
        internal protected virtual void OnClosed(CloseReason closedReason)
        {
            SessionClosed?.Invoke(closedReason);
        }

        
        /// <inheritdoc/>
        public virtual void Send(byte[] data)
        {
            Send(data, 0, data.Length);
        }

        [CatchException("发送数据异常")]
        public virtual void Send(byte[] data, int offset, int count)
        {
            if (socket != null)
            {
                _sendData.Invoke(this, data, offset, count);
            }
            else
            {
                throw new NetworkException("会话已经关闭");
            }
        }

        [CatchException("关闭会话异常")]
        public virtual void Close()
        {
            var _socket = socket;

            if (_socket == null)
                return;

            if (Interlocked.CompareExchange(ref socket, null, _socket) == _socket)
            {
                try
                {
                    _socket.Shutdown(SocketShutdown.Both);
                }
                finally
                {
                    _socket.Close();
                }
            }
        }

        /// <summary>
        /// 为会话附加信息
        /// </summary>
        /// <param name="socket"></param>
        /// <param name="sessionID"></param>
        [CatchException("为会话附加信息异常")]
        internal void Attach(Socket socket, int sessionID)
        {
            this.socket = socket;
            this._sessionID = sessionID;
            this._connectTime = DateTime.Now;

            if (_keepAlive.Enable)
            {
                socket.IOControl(IOControlCode.KeepAliveValues, _keepAlive.GetKeepAliveData(), null);
            }

        }

        /// <summary>
        /// 清除会话附加信息
        /// </summary>
        internal void Detach()
        {
            this.socket = null;
            this._sessionID = 0;
            this._connectTime = default;
        }

        /// <summary>
        /// 初始化
        /// </summary>
        internal void Initialize(SocketDispatcherBase socketDispatcher,
            InternalSendDataEventHandler internalSendDataEventHandler,
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
            _socketDispatcher.EmitExceptionCaughtSignal(sender, ex);
        }

        #endregion 方法
    }
}
