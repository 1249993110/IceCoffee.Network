using IceCoffee.Common;
using IceCoffee.Network.CatchException;
using IceCoffee.Network.Sockets.Pool;
using IceCoffee.Network.Sockets.Primitives;
using IceCoffee.Network.Sockets.Primitives.Internal;
using IceCoffee.Network.Sockets.Primitives.TcpSession;
using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;


namespace IceCoffee.Network.Sockets.Primitives.TcpClient
{
    public abstract class TcpClientBase<TSession> : SocketDispatcherBase, ITcpClientBase, IExceptionCaught where TSession : TcpSessionBase<TSession>, new()
    {
        #region 字段

        private Socket _socketConnecter;

        private IPAddress _ipAddress;

        private ushort _port = 0;

        private int _recvBufferSize = 4096;

        private int _sendBufferSize = 4096;

        private TSession _session;

        private readonly SocketAsyncEventArgs _connectSaea;

        private SaeaPool _sendSaeaPool;

        private SaeaPool _recvSaeaPool;

        private int _connectTimedOutSpan = 10; // 单位: 秒

        private ConnectionState _connectionState = ConnectionState.Disconnected;

        private int _autoReconnectCount = 0;

        private int _autoReconnectMaxCount = 0;

        private int _autoReconnectInterval = 20; //间隔 单位：秒

        #endregion

        #region 属性

        /// <summary>
        /// 当前会话
        /// </summary>
        public TSession Session => _session;

        public override int ReceiveBufferSize
        {
            get => _recvBufferSize;
            set => this.SetBufferSize(ref _recvBufferSize, value);
        }

        public override int SendBufferSize
        {
            get => _sendBufferSize;
            set => this.SetBufferSize(ref _sendBufferSize, value);
        }

        public override IPEndPoint LocalIPEndPoint => _socketConnecter.LocalEndPoint as IPEndPoint;

        public override bool IsServerSide => false;

        public ConnectionState ConnectionState => _connectionState;

        public bool IsConnected => _connectionState == ConnectionState.Connected;

        [CatchException("连接超时范围设置错误", CustomExceptionType.Checked)]
        public int ConnectTimedOutSpan
        {
            get => _connectTimedOutSpan;
            set
            {
                if (value < 1)
                {
                    throw new ArgumentException("连接超时范围不能小于1");
                }

                _connectTimedOutSpan = value;
            }
        }

        public int AutoReconnectMaxCount
        {
            get => _autoReconnectMaxCount;
            set => _autoReconnectMaxCount = value;
        }

        public int AutoReconnectInterval
        {
            get => _autoReconnectInterval;
            set => _autoReconnectInterval = value;
        }
        #endregion

        #region 事件
        public event ConnectedEventHandler Connected;

        public event DisconnectedEventHandler Disconnected;

        public event AutoReconnectDefeatedEventHandler ReconnectDefeated;

        public event ConnectionStateChangedEventHandler ConnectionStateChanged;

        public event ExceptionCaughtEventHandler ExceptionCaught;
        #endregion

        #region 方法

        #region 构造方法

        public TcpClientBase()
        {
            _connectSaea = new SocketAsyncEventArgs();
            _connectSaea.Completed += OnConnectAsyncRequestCompleted;
        }
        
        #endregion

        #region 私有方法

        private bool CheckIsConnected()
        {
            bool blockingState = _socketConnecter.Blocking;
            try
            {
                byte[] tmp = new byte[1];
                _socketConnecter.Blocking = false;
                _socketConnecter.Send(tmp, 0, 0);
                return true;
            }
            catch (SocketException e)
            {
                // 10035 == WSAEWOULDBLOCK
                if (e.NativeErrorCode.Equals(10035))
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            finally
            {
                _socketConnecter.Blocking = blockingState;
            }
        }
        private void OnInternalConnectionStateChanged(ConnectionState connectionState)
        {
            _connectionState = connectionState;
            OnConnectionStateChanged(connectionState);
        }

        [CatchException("异步连接服务端异常")]
        private void OnConnectAsyncRequestCompleted(object sender, SocketAsyncEventArgs e)
        {
            SocketError socketError = e.SocketError;
            if (socketError != SocketError.Success)
            {
                throw new NetworkException("Socket已关闭，重叠的操作被中止，SocketError：" + socketError.ToString());
            }

            int sessionID = _socketConnecter.Handle.ToInt32();
            SocketAsyncEventArgs receiveSaea = _recvSaeaPool.Take();

            _session.Attach(_socketConnecter, sessionID);

            try
            {
                OnInternalConnectionStateChanged(ConnectionState.Connected);
                OnConnected();
                if (_socketConnecter.ReceiveAsync(receiveSaea) == false)
                {
                    Task.Run(() =>
                    {
                        OnRecvAsyncRequestCompleted(_socketConnecter, receiveSaea);
                    });
                }
            }
            catch
            {
                OnInternalClose(CloseReason.ApplicationError);
                throw;
            }
        }

        [CatchException("异步接收数据异常")]
        private void OnRecvAsyncRequestCompleted(object sender, SocketAsyncEventArgs e)
        {
            SocketError socketError = e.SocketError;
            if (e.BytesTransferred > 0 && socketError == SocketError.Success)
            {
                try
                {
                    _session.ReadBuffer.CacheSaea(e);

                    // 主动关闭会话
                    if (_session.socket == null)
                    {
                        OnInternalClose(CloseReason.ClientClosing);
                    }
                    else
                    {
                        SocketAsyncEventArgs receiveSaea = _recvSaeaPool.Take();

                        if (_socketConnecter.ReceiveAsync(receiveSaea) == false)
                        {
                            OnRecvAsyncRequestCompleted(sender, receiveSaea);
                        }
                    }
                }
                catch
                {
                    OnInternalClose(CloseReason.InternalError);
                    throw;
                }
            }
            else
            {
                OnInternalClose(CloseReason.SocketError);
                throw new NetworkException("异常socket连接，SocketError：" + socketError);
            }
        }

        [CatchException("会话即将关闭异常")]
        private void OnInternalClose(CloseReason closeReason)
        {
            if (_connectionState == ConnectionState.Connected) //已连接
            {
                _session.Close();

                OnInternalConnectionStateChanged(ConnectionState.Disconnected);
                _session.OnClosed(closeReason);
                OnDisconnected(closeReason);
                _session.Detach();

                _session.ReadBuffer.CollectAllRecvSaeaAndReset();

                _session = null;

                if (_autoReconnectMaxCount > 0 && _autoReconnectCount == 0)
                {
                    AutoReconnect();
                }
            }
            // 这里可能还有问题
        }

        [CatchException("异步发送数据异常")]
        private void OnInternalSend(ITcpSessionBase session, byte[] data, int offset, int count)
        {
            if (count <= _sendBufferSize)// 如果count小于发送缓冲区大小，此时count应不大于data.Length
            {
                SocketAsyncEventArgs sendSaea = _sendSaeaPool.Take();
                Array.Copy(data, offset, sendSaea.Buffer, 0, count);
                sendSaea.SetBuffer(0, count);
                if (_socketConnecter.SendAsync(sendSaea) == false)
                {
                    OnSendAsyncRequestCompleted(_socketConnecter, sendSaea);
                }
            }
            else// 否则创建一个新的BufferList进行发送
            {
                SocketAsyncEventArgs sendSaea = new SocketAsyncEventArgs();
                sendSaea.Completed += OnSendAsyncRequestCompleted_UseBufferList;
                sendSaea.BufferList = new ArraySegment<byte>[1] { new ArraySegment<byte>(data, offset, count) };
                if (_socketConnecter.SendAsync(sendSaea) == false)
                {
                    OnSendAsyncRequestCompleted_UseBufferList(_socketConnecter, sendSaea);
                }
            }
        }
        
        [CatchException("发送异步请求完成异常")]
        private void OnSendAsyncRequestCompleted(object sender, SocketAsyncEventArgs e)
        {
            if (e.SocketError != SocketError.Success)
            {
                OnInternalClose(CloseReason.SocketError);
                throw new NetworkException("异常socket连接，SocketError：" + e.SocketError);
            }
            
            _sendSaeaPool.Put(e);
        }
        
        [CatchException("使用BufferList发送异步请求完成异常")]
        private void OnSendAsyncRequestCompleted_UseBufferList(object sender, SocketAsyncEventArgs e)
        {
            if (e.SocketError != SocketError.Success)
            {
                OnInternalClose(CloseReason.SocketError);
                throw new NetworkException("异常socket连接，SocketError：" + e.SocketError);
            }
            
            e.BufferList = null;
            e.Dispose();
        }

        [CatchException("自动重连异常")]
        private void AutoReconnect()
        {
            OnInternalConnectionStateChanged(ConnectionState.AutoReconnecting);
            Interlocked.Increment(ref _autoReconnectCount);
            this.Reconnect();
            Task.Factory.StartNew(() =>
            {
                while (true)
                {
                    Thread.Sleep(_autoReconnectInterval * 1000);
                    if (_autoReconnectCount < _autoReconnectMaxCount) // 没有超出
                    {
                        if (_connectionState == ConnectionState.Connected)
                        {
                            Interlocked.Exchange(ref _autoReconnectCount, 0);
                            break;
                        }
                        else if (_connectionState == ConnectionState.Connecting)
                        {
                            continue;
                        }
                    }
                    else // 超出次数
                    {
                        if (_connectionState != ConnectionState.Connected)
                        {
                            OnReconnectDefeated();
                        }

                        Interlocked.Exchange(ref _autoReconnectCount, 0);
                        break;
                    }
                    OnInternalConnectionStateChanged(ConnectionState.AutoReconnecting);
                    Interlocked.Increment(ref _autoReconnectCount);
                    this.Reconnect();
                }
            }, TaskCreationOptions.LongRunning);
        }

        #endregion

        #region 保护方法
        /// <summary>
        /// 连接成功时调用，默认引发相应事件
        /// </summary>
        protected virtual void OnConnected()
        {
            Connected?.Invoke();
        }

        /// <summary>
        /// 断开连接时调用，默认引发相应事件
        /// </summary>
        protected virtual void OnDisconnected(CloseReason closedReason)
        {
            Disconnected?.Invoke(closedReason);
        }

        /// <summary>
        /// 自动重连失败时调用，默认引发相应事件
        /// </summary>
        protected virtual void OnReconnectDefeated()
        {
            ReconnectDefeated?.Invoke();
        }

        /// <summary>
        /// 连接状态改变时调用，默认引发相应事件
        /// </summary>
        /// <param name="connectionState"></param>
        protected virtual void OnConnectionStateChanged(ConnectionState connectionState)
        {
            ConnectionStateChanged?.Invoke(connectionState);
        }

        #endregion

        #region 公开方法

        [CatchException("连接服务端异常", CustomExceptionType.Checked)]
        public void Connect(string ipStr, ushort port)
        {
            if (_socketConnecter != null && _socketConnecter.Connected)
            {
                Disconnect();
            }

            OnInternalConnectionStateChanged(ConnectionState.Connecting);
            _recvSaeaPool = new SaeaPool(OnRecvAsyncRequestCompleted, _recvBufferSize);
            _sendSaeaPool = new SaeaPool(OnSendAsyncRequestCompleted, _sendBufferSize);

            _socketConnecter = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            _session = new TSession();
            _session.Initialize(this, OnInternalSend, _recvSaeaPool.Put);

            bool isNumberIP = IPAddress.TryParse(ipStr, out _ipAddress);// 数字IP
            if (isNumberIP == false)// 域名
            {
                _ipAddress = Dns.GetHostEntry(ipStr).AddressList.Where(p => p.AddressFamily == AddressFamily.InterNetwork).First();
            }
            _port = port;
            _connectSaea.RemoteEndPoint = new IPEndPoint(_ipAddress, _port);

            if (_socketConnecter.ConnectAsync(_connectSaea) == false)
            {
                OnConnectAsyncRequestCompleted(_socketConnecter, _connectSaea);
            }

            Task.Run(() =>
            {
                Thread.Sleep(_connectTimedOutSpan * 1000);
                if (_connectionState == ConnectionState.Connecting && CheckIsConnected() == false)
                {
                    try
                    {
                        throw new NetworkException("连接尝试超时，或者连接的主机没有响应", new TimeoutException("TimeOut Exception"))
                        {
                            CustomExceptionType = CustomExceptionType.Checked
                        };
                    }
                    catch (NetworkException e)
                    {
                        ExceptionCaught?.Invoke(this, e);
                    }
                    finally
                    {
                        // 此时会话还未建立，不会调用onPrivateClose
                        if (_socketConnecter != null)
                        {
                            _socketConnecter.Close();
                            _socketConnecter = null;
                            OnInternalConnectionStateChanged(ConnectionState.Disconnected);
                        }
                    }
                }
            });
        }

        [CatchException("重新连接服务端异常", CustomExceptionType.Checked)]
        public void Reconnect()
        {
            Connect(_ipAddress.ToString(), _port);
        }
        
        [CatchException("断开连接错误")]
        public void Disconnect()
        {
            if (_session != null && _connectionState != ConnectionState.Disconnected)
            {
                OnInternalConnectionStateChanged(ConnectionState.Disconnecting);

                _session.Close();
                _recvSaeaPool.Dispose();
                _sendSaeaPool.Dispose();

                OnInternalConnectionStateChanged(ConnectionState.Disconnected);
                _session.OnClosed(CloseReason.ClientClosing);
                OnDisconnected(CloseReason.ClientClosing);
                _session.Detach();

                _session = null;
            }
        }

        public override void Dispose()
        {
            Disconnect();
        }

        #endregion

        #region 其他方法
        void IExceptionCaught.EmitSignal(object sender, NetworkException ex)
        {
            EmitExceptionCaughtSignal(sender, ex);
        }

        internal override void EmitExceptionCaughtSignal(object sender, NetworkException ex)
        {
            ExceptionCaught?.Invoke(sender, ex);
        }

        #endregion

        #endregion
    }
}
