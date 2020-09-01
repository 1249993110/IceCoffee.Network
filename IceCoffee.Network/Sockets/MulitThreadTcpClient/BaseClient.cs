using IceCoffee.Common;
using IceCoffee.Network.CatchException;
using IceCoffee.Network.Sockets.Pool;
using IceCoffee.Network.Sockets.Primitives;
using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace IceCoffee.Network.Sockets.MulitThreadTcpClient
{
    public class BaseClient : BaseClient<BaseSession>
    {
    }

    public abstract class BaseClient<TSession> : ISocketDispatcher, IExceptionCaught, IDisposable where TSession : BaseSession<TSession>, new()
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
        /// <para>每次接收数据的缓冲区大小，默认为4096字节/会话</para>
        /// <para>必须在socket启动前设置。注意此属性与ReadBuffer读取缓冲区是不同的</para>
        /// </summary>
        public int ReceiveBufferSize
        {
            get => _recvBufferSize;
            set => this.SetBufferSize(ref _recvBufferSize, value);
        }

        /// <summary>
        /// <para>每次发送数据的缓冲区大小，默认为4096字节/会话</para>
        /// <para>必须在socket启动前设置。大于此大小的数据包将被作为临时的新缓冲区发送，此过程影响性能</para>
        /// </summary>
        public int SendBufferSize
        {
            get => _sendBufferSize;
            set => this.SetBufferSize(ref _sendBufferSize, value);
        }

        /// <summary>
        /// 本地IP终结点
        /// </summary>
        public IPEndPoint LocalIPEndPoint => _socketConnecter.LocalEndPoint as IPEndPoint;

        /// <summary>
        /// 当前会话
        /// </summary>
        public TSession Session => _session;

        /// <summary>
        /// 连接状态
        /// </summary>
        public ConnectionState ConnectionState => _connectionState;

        /// <summary>
        /// 是否已经连接成功
        /// </summary>
        public bool IsConnected => _connectionState == ConnectionState.Connected;

        /// <summary>
        /// 连接超时范围, 默认10秒
        /// </summary>
        [CatchException(ErrorMessage = "连接超时范围设置错误", CustomExceptionType = CustomExceptionType.Checked)]
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

        /// <summary>
        /// 如果在断开连接后不需要重连；请设置此属性小于或等于0
        /// </summary>
        public int AutoReconnectMaxCount
        {
            get => _autoReconnectMaxCount;
            set => _autoReconnectMaxCount = value;
        }

        /// <summary>
        /// 自动重连间隔，默认每20秒重连一次，一般比连接超时范围长
        /// </summary>
        public int AutoReconnectInterval
        {
            get => _autoReconnectInterval;
            set => _autoReconnectInterval = value;
        }
        /// <summary>
        /// 是否作为服务端
        /// </summary>
        public bool AsServer => false;
        #endregion

        #region 事件

        /// <summary>
        /// 连接成功，OnConnected 引发 Connected 事件。
        /// </summary>
        public event ConnectedEventHandler Connected;

        /// <summary>
        /// 失去连接，OnDisconnected 引发 Disconnected 事件。
        /// </summary>
        public event DisconnectedEventHandler Disconnected;

        /// <summary>
        /// 自动重连失败
        /// </summary>
        public event AutoReconnectDefeatedEventHandler ReconnectDefeated;

        /// <summary>
        /// 连接状态改变
        /// </summary>
        public event ConnectionStateChangedEventHandler ConnectionStateChanged;

        /// <summary>
        /// 异常捕获
        /// </summary>
        public event ExceptionCaughtEventHandler ExceptionCaught;
        #endregion

        #region 方法

        #region 构造方法

        public BaseClient()
        {
            _connectSaea = new SocketAsyncEventArgs();
            _connectSaea.Completed += OnConnectAsyncRequestCompleted;
        }
        public void Dispose()
        {
            Disconnect();
        }
        #endregion

        #region 私有方法

        [CatchException(ErrorMessage = "设置缓冲区错误", CustomExceptionType = CustomExceptionType.Checked)]
        private void SetBufferSize(ref int buffer, int value)
        {
            if (value < 1 || value > 1048576) //65535
            {
                throw new ArgumentOutOfRangeException("缓冲区大小范围为：1-1048576");
            }

            buffer = value;
        }

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

        [CatchException(ErrorMessage = "异步连接服务端异常")]
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
                OnInternalClose(socketError);
                throw;
            }
        }

        [CatchException(ErrorMessage = "异步接收数据异常")]
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
                        OnInternalClose(socketError);
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
                    OnInternalClose(socketError);
                    throw;
                }
            }
            else
            {
                OnInternalClose(socketError);
            }
        }

        [CatchException(ErrorMessage = "会话即将关闭异常")]
        private void OnInternalClose(SocketError socketError)
        {
            if (_connectionState == ConnectionState.Connected) //已连接
            {
                _session.Close();

                OnInternalConnectionStateChanged(ConnectionState.Disconnected);
                OnDisconnected(socketError);
                _session.Detach(socketError);

                _session.ReadBuffer.CollectAllRecvSaeaAndReset();

                _session = null;

                if (_autoReconnectMaxCount > 0 && _autoReconnectCount == 0)
                {
                    AutoReconnect();
                }
            }
            // 这里可能还有问题
        }

        [CatchException(ErrorMessage = "异步发送数据异常")]
        private void OnInternalSend(TSession session, byte[] data)
        {
            long dataLen = data.LongLength;
            if (dataLen <= _sendBufferSize)//如果data长度小于发送缓冲区大小，此时dataLen应不大于int.Max
            {
                SocketAsyncEventArgs sendSaea = _sendSaeaPool.Take();
                Array.Copy(data, 0, sendSaea.Buffer, 0, (int)dataLen);
                sendSaea.SetBuffer(0, (int)dataLen);
                if (_socketConnecter.SendAsync(sendSaea) == false)
                {
                    OnSendAsyncRequestCompleted(_socketConnecter, sendSaea);
                }
            }
            else//否则创建一个新的BufferList进行发送
            {
                SocketAsyncEventArgs sendSaea = new SocketAsyncEventArgs();
                sendSaea.Completed += OnSendAsyncRequestCompleted_UseBufferList;
                sendSaea.BufferList = new ArraySegment<byte>[1] { new ArraySegment<byte>(data) };
                if (_socketConnecter.SendAsync(sendSaea) == false)
                {
                    OnSendAsyncRequestCompleted_UseBufferList(_socketConnecter, sendSaea);
                }
            }
        }

        private void OnSendAsyncRequestCompleted(object sender, SocketAsyncEventArgs e)
        {
            if (e.SocketError == SocketError.Success)
            {
                //session.OnSent();
            }
            else
            {
                OnInternalClose(e.SocketError);
            }
            _sendSaeaPool.Put(e);
        }

        private void OnSendAsyncRequestCompleted_UseBufferList(object sender, SocketAsyncEventArgs e)
        {
            if (e.SocketError == SocketError.Success)
            {
                //session.OnSent();
            }
            else
            {
                OnInternalClose(e.SocketError);
            }
            e.BufferList = null;
            e.Dispose();
        }

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
                    if (_autoReconnectCount < _autoReconnectMaxCount) //没有超出
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
                    else //超出次数
                    {
                        if (_connectionState != ConnectionState.Connected)
                            OnReconnectDefeated();
                        Interlocked.Exchange(ref _autoReconnectCount, 0);
                        break;
                    }
                    OnInternalConnectionStateChanged(ConnectionState.AutoReconnecting);
                    Interlocked.Increment(ref _autoReconnectCount);
                    this.Reconnect();
                }
            }, TaskCreationOptions.LongRunning);
        }

        private void OnInternalConnectionStateChanged(ConnectionState connectionState)
        {
            _connectionState = connectionState;
            OnConnectionStateChanged(connectionState);
        }

        #endregion

        #region 保护方法

        protected virtual void OnConnected()
        {
            Connected?.Invoke();
        }

        /// <summary>
        /// 断开连接时调用
        /// </summary>
        protected virtual void OnDisconnected(SocketError closedReason)
        {
            Disconnected?.Invoke(closedReason);
        }

        /// <summary>
        /// 自动重连失败时调用
        /// </summary>
        protected virtual void OnReconnectDefeated()
        {
            ReconnectDefeated?.Invoke();
        }

        /// <summary>
        /// 连接状态改变时调用
        /// </summary>
        /// <param name="connectionState"></param>
        protected virtual void OnConnectionStateChanged(ConnectionState connectionState)
        {
            ConnectionStateChanged?.Invoke(connectionState);
        }

        #endregion

        #region 公开方法

        /// <summary>
        /// 连接服务端
        /// </summary>
        /// <param name="ipStr"></param>
        /// <param name="port"></param>
        [CatchException(ErrorMessage = "连接服务端异常", CustomExceptionType = CustomExceptionType.Checked)]
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

            bool isNumberIP = IPAddress.TryParse(ipStr, out _ipAddress);//数字IP
            if (isNumberIP == false)//域名
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

        /// <summary>
        /// 使用上次尝试连接的参数重新连接
        /// </summary>
        public void Reconnect()
        {
            Connect(_ipAddress.ToString(), _port);
        }

        /// <summary>
        /// 断开连接
        /// </summary>
        [CatchException(ErrorMessage = "断开连接错误")]
        public void Disconnect()
        {
            if (_session != null && (_connectionState != ConnectionState.Disconnected))
            {
                OnInternalConnectionStateChanged(ConnectionState.Disconnecting);

                _session.Close();
                _recvSaeaPool.Dispose();
                _sendSaeaPool.Dispose();

                OnInternalConnectionStateChanged(ConnectionState.Disconnected);
                OnDisconnected(SocketError.Success);
                _session.Detach(SocketError.Success);

                _session = null;
            }
        }

        void IExceptionCaught.EmitSignal(object sender, NetworkException e)
        {
            ExceptionCaught?.Invoke(sender, e);
        }

        #endregion

        #endregion
    }
}