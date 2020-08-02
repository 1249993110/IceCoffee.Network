using IceCoffee.Common;
using IceCoffee.Network.CatchException;
using IceCoffee.Network.Sockets.Pool;
using IceCoffee.Network.Sockets.Primitives;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace IceCoffee.Network.Sockets.MulitThreadTcpServer
{
    public class BaseServer : BaseServer<BaseSession>
    {
        public BaseServer()
        {
        }
    }

    public abstract class BaseServer<TSession> : ISocketDispatcher, IExceptionCaught where TSession : BaseSession<TSession>, new()
    {
        #region 字段

        private Socket _socketWatcher = null;

        private IPAddress _ipAddress = IPAddress.Any;

        private ushort _port = 0;

        private bool _isListening = false;

        private SessionPool<TSession> _sessionPool;
        private SaeaPool _recvSaeaPool;
        private SaeaPool _sendSaeaPool;

        private readonly ManualResetEvent _acceptEvent;

        private readonly ConcurrentDictionary<int, TSession> _sessions;

        private int _recvBufferSize = 4096;

        private int _sendBufferSize = 4096;

        private readonly SocketAsyncEventArgs _acceptSaea;

        #endregion 字段

        #region 属性

        /// <summary>
        /// 获取所有会话
        /// </summary>
        public IReadOnlyDictionary<int, TSession> Sessions => _sessions as IReadOnlyDictionary<int, TSession>;

        /// <summary>
        /// 连接的会话数量
        /// </summary>
        public int SessionCount => _sessions.Count;

        /// <summary>
        /// <para>每次接收数据的缓冲区大小，默认为4096字节/会话</para>
        /// <para>必须在socket启动前设置。注意此属性与ReadBuffer读取缓冲区是不同的</para>
        /// </summary>
        public int ReceiveBufferSize
        {
            get => _recvBufferSize;
            set => SetBufferSize(ref _recvBufferSize, value);
        }

        /// <summary>
        /// <para>每次发送数据的缓冲区大小，默认为4096字节/会话</para>
        /// <para>必须在socket启动前设置。大于此大小的数据包将被作为临时的新缓冲区发送，此过程影响性能</para>
        /// </summary>
        public int SendBufferSize
        {
            get => _sendBufferSize;
            set => SetBufferSize(ref _sendBufferSize, value);
        }

        /// <summary>
        /// 本地IP终结点
        /// </summary>
        public IPEndPoint LocalIPEndPoint => _socketWatcher.LocalEndPoint as IPEndPoint;

        /// <summary>
        /// 服务端是否正在监听
        /// </summary>
        public bool IsListening => _isListening;

        /// <summary>
        /// 是否作为服务端
        /// </summary>
        public bool AsServer => true;

        #endregion 属性

        #region 事件

        /// <summary>
        /// 开始监听，OnStarted 引发 Started 事件。
        /// </summary>
        public event StartedEventHandler Started;

        /// <summary>
        /// 停止监听，OnStopped 引发 Started 事件。
        /// </summary>
        public event StoppedEventHandler Stopped;

        /// <summary>
        /// 新会话建立，OnNewSessionSetup 引发 Started 事件。
        /// </summary>
        public event NewSessionSetupEventHandler<TSession> NewSessionSetup;

        /// <summary>
        /// 会话结束，OnSessionClosed 引发 Started 事件。
        /// </summary>
        public event SessionClosedEventHandler<TSession> SessionClosed;

        /// <summary>
        /// 异常捕获
        /// </summary>
        public event ExceptionCaughtEventHandler ExceptionCaught;

        #endregion 事件

        #region 方法

        #region 构造方法

        public BaseServer()
        {
            _acceptEvent = new ManualResetEvent(false);
            _sessions = new ConcurrentDictionary<int, TSession>();
            _acceptSaea = new SocketAsyncEventArgs();
            _acceptSaea.Completed += OnAcceptAsyncRequestCompleted;
        }

        ~BaseServer()
        {
            Stop();
        }

        #endregion 构造方法

        #region 私有方法

        [CatchException(ErrorMessage = "设置缓冲区错误", CustomExceptionType = CustomExceptionType.Checked)]
        private void SetBufferSize(ref int buffer, int value)
        {
            if (value < 1 || value > 1048576) //65535
                throw new ArgumentOutOfRangeException("缓冲区大小范围为：1-1048576");
            buffer = value;
        }

        [CatchException(ErrorMessage = "开始监听异常", CustomExceptionType = CustomExceptionType.Checked)]
        private bool StartListen()
        {
            if (_isListening)
                Stop();

            _recvSaeaPool = new SaeaPool(OnRecvAsyncRequestCompleted, _recvBufferSize);
            _sendSaeaPool = new SaeaPool(OnSendAsyncRequestCompleted, _sendBufferSize);
            _sessionPool = new SessionPool<TSession>(this, OnInternalSend, _recvSaeaPool.Put);

            _socketWatcher = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _socketWatcher.ExclusiveAddressUse = true;
            _socketWatcher.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);

            _socketWatcher.Bind(new IPEndPoint(this._ipAddress, this._port));

            _socketWatcher.Listen(511);

            _acceptEvent.Set();

            _isListening = true;

            Thread myServer = new Thread(AcceptSocket);
            // 设置这个线程是后台线程
            myServer.IsBackground = true;
            myServer.Start();

            //Task.Factory.StartNew(new Action(acceptSocket), TaskCreationOptions.LongRunning);

            return _isListening;
        }

        [CatchException(ErrorMessage = "异步接受客户异常")]
        private void AcceptSocket()
        {
            OnStarted();
            while (_isListening)
            {
                _acceptSaea.AcceptSocket = null;
                if (_socketWatcher.AcceptAsync(_acceptSaea) == false)
                {
                    Task.Run(() =>
                    {
                        OnAcceptAsyncRequestCompleted(_socketWatcher, _acceptSaea);
                    });
                }
                _acceptEvent.Reset();
                _acceptEvent.WaitOne();
            }
        }

        [CatchException(ErrorMessage = "异步接收数据异常")]
        private void OnAcceptAsyncRequestCompleted(object sender, SocketAsyncEventArgs e)
        {
            Socket socket = e.AcceptSocket;
            if (_isListening)
            {
                // 异常socket连接
                if (e.SocketError != SocketError.Success || socket.Connected == false)
                {
                    throw new NetworkException("异常socket连接，SocketError：" + e.SocketError.ToString());
                }
            }
            else
            {
                return;
            }

            _acceptEvent.Set();

            TSession session = _sessionPool.Take();

            int sessionID = socket.Handle.ToInt32();

            SocketAsyncEventArgs receiveSaea = _recvSaeaPool.Take();

            receiveSaea.UserToken = session;

            session.Attach(socket, sessionID);

            bool flag = _sessions.ContainsKey(sessionID);
            if (flag)
            {
                TSession tempSession;
                _sessions.TryRemove(sessionID, out tempSession);
            }

            _sessions.TryAdd(sessionID, session);

            try
            {
                OnNewSessionSetup(session);
                if (session.socket.ReceiveAsync(receiveSaea) == false)
                {
                    Task.Run(() =>
                    {
                        OnRecvAsyncRequestCompleted(session.socket, receiveSaea);
                    });
                }
            }
            catch
            {
                OnInternalClose(session, e);
                throw;
            }
            if (flag)
                throw new NetworkException(string.Format("添加会话错误，sessionID: {0} 已存在", session.SessionID.ToString()));
        }

        [CatchException(ErrorMessage = "异步接收数据异常")]
        private void OnRecvAsyncRequestCompleted(object sender, SocketAsyncEventArgs e)
        {
            TSession session = (TSession)e.UserToken;
            if (e.BytesTransferred > 0 && e.SocketError == SocketError.Success)
            {
                try
                {
                    session.ReadBuffer.CacheSaea(e);

                    SocketAsyncEventArgs receiveSaea = _recvSaeaPool.Take();
                    receiveSaea.UserToken = session;

                    // 服务端主动关闭会话
                    if (session.socket == null)
                    {
                        OnInternalClose(session, e);
                    }
                    else if (session.socket.ReceiveAsync(receiveSaea) == false)
                    {
                        OnRecvAsyncRequestCompleted(sender, receiveSaea);
                    }
                }
                catch
                {
                    OnInternalClose(session, e);
                    throw;
                }
            }
            else
            {
                OnInternalClose(session, e);
            }
        }

        [CatchException(ErrorMessage = "会话即将关闭异常")]
        private void OnInternalClose(TSession session, SocketAsyncEventArgs e, bool collectSaea = true)
        {
            if (_isListening) // 不是正在停止监听
            {
                session.Close();
                if(collectSaea)
                {
                    _recvSaeaPool.Put(e);
                }
                else
                {
                    e.Dispose();
                }

                if (_sessions.TryRemove(session.SessionID, out session))
                {
                    OnSessionClosed(session, e.SocketError);
                    session.Detach(e.SocketError);

                    CollectSession(session);
                }
                else
                {
                    CollectSession(session);
                    throw new NetworkException("从会话列表中移除一个不存在的会话");
                }
                
            }
        }

        private void CollectSession(TSession session)
        {
            session.ReadBuffer.CollectAllRecvSaeaAndReset();
            _sessionPool.Put(session);
        }

        [CatchException(ErrorMessage = "异步发送数据异常")]
        private void OnInternalSend(TSession session, byte[] data)
        {
            SocketAsyncEventArgs sendSaea = _sendSaeaPool.Take();
            sendSaea.UserToken = session;
            long dataLen = data.LongLength;
            if (dataLen <= _sendBufferSize)//如果data长度小于发送缓冲区大小，此时dataLen应不大于int.Max
            {
                Array.Copy(data, 0, sendSaea.Buffer, 0, (int)dataLen);
                sendSaea.SetBuffer(0, (int)dataLen);
                if (session.socket.SendAsync(sendSaea) == false)
                {
                    OnSendAsyncRequestCompleted(session.socket, sendSaea);
                }
            }
            else//否则创建一个新的BufferList进行发送
            {
                sendSaea.Completed -= OnSendAsyncRequestCompleted;
                sendSaea.Completed += OnSendAsyncRequestCompleted_UseBufferList;
                sendSaea.SetBuffer(null, 0, 0);
                sendSaea.BufferList = new ArraySegment<byte>[1] { new ArraySegment<byte>(data) };
                if (session.socket.SendAsync(sendSaea) == false)
                {
                    OnSendAsyncRequestCompleted_UseBufferList(session.socket, sendSaea);
                }
            }
        }

        private void OnSendAsyncRequestCompleted(object sender, SocketAsyncEventArgs e)
        {
            TSession session = (TSession)e.UserToken;
            if (e.SocketError == SocketError.Success)
            {
                //session.OnSent();
            }
            else
            {
                OnInternalClose(session, e);
            }
            _sendSaeaPool.Put(e);
        }

        private void OnSendAsyncRequestCompleted_UseBufferList(object sender, SocketAsyncEventArgs e)
        {
            TSession session = (TSession)e.UserToken;
            if (e.SocketError == SocketError.Success)
            {
                //session.OnSent();
            }
            else
            {
                OnInternalClose(session, e, false);
            }
            e.BufferList = null;
        }

        #endregion 私有方法

        #region 保护方法

        /// <summary>
        /// 当服务端开始监听后调用
        /// </summary>
        virtual protected void OnStarted()
        {
            Started?.Invoke();
        }

        /// <summary>
        /// 当服务端停止监听后调用
        /// </summary>
        virtual protected void OnStopped()
        {
            Stopped?.Invoke();
        }

        /// <summary>
        /// 当新会话建立时调用
        /// </summary>
        /// <param name="session"></param>
        virtual protected void OnNewSessionSetup(TSession session)
        {
            NewSessionSetup?.Invoke(session);
        }

        /// <summary>
        /// 当会话关闭时调用
        /// </summary>
        /// <param name="session"></param>
        /// <param name="closedReason"></param>
        virtual protected void OnSessionClosed(TSession session, SocketError closedReason)
        {
            SessionClosed?.Invoke(session, closedReason);
        }

        #endregion 保护方法

        #region 公开方法

        /// <summary>
        /// 开始监听
        /// </summary>
        /// <param name="port">监听端口，端口范围为：0-65535</param>
        /// <returns>成功返回true,否则返回false</returns>
        public bool Start(ushort port)
        {
            _port = port;
            return StartListen();
        }

        /// <summary>
        /// 开始监听
        /// </summary>
        /// <param name="ipStr">监听IP地址(字符串形式)，默认为Any</param>
        /// <param name="port">监听端口，端口范围为：0-65535</param>
        /// <returns>成功返回true,否则返回false</returns>
        [CatchException(ErrorMessage = "开始监听异常", CustomExceptionType = CustomExceptionType.Checked)]
        public bool Start(string ipStr, ushort port)
        {
            return Start(IPAddress.Parse(ipStr), port);
        }

        /// <summary>
        /// 开始监听
        /// </summary>
        /// <param name="ipAddress">监听IP地址</param>
        /// <param name="port">监听端口，端口范围为：0-65535</param>
        /// <returns>成功返回true,否则返回false</returns>
        public bool Start(IPAddress ipAddress, ushort port)
        {
            _ipAddress = ipAddress;
            _port = port;
            return StartListen();
        }

        /// <summary>
        /// 使用上次开始监听的参数重新开始监听
        /// </summary>
        /// <returns>成功返回true,否则返回false</returns>
        public bool Restart()
        {
            Stop();
            return StartListen();
        }

        /// <summary>
        /// 停止监听
        /// </summary>
        [CatchException(ErrorMessage = "停止监听异常")]
        public void Stop()
        {
            if (_socketWatcher != null && _isListening)
            {
                _isListening = false;

                _acceptEvent.Set();

                foreach (var item in _sessions)
                {
                    item.Value.Close();
                }

                _socketWatcher.Close();

                _recvSaeaPool.Dispose();
                _sendSaeaPool.Dispose();
                _sessionPool.Dispose();
                OnStopped();
            }
        }

        /// <summary>
        /// 关闭会话
        /// </summary>
        /// <param name="session"></param>
        [CatchException(ErrorMessage = "关闭会话错误，sessionID不存在", CustomExceptionType = CustomExceptionType.Checked)]
        public void CloseSession(int sessionID)
        {
            _sessions[sessionID].Close();
        }

        /// <summary>
        /// 对指定sessionID发送数据
        /// </summary>
        /// <param name="data">待发送数据</param>
        /// <param name="sessionID">sessionID</param>
        [CatchException(ErrorMessage = "发送数据错误，sessionID不存在")]
        public void SendByID(byte[] data, int sessionID)
        {
            _sessions[sessionID].Send(data);
        }

        void IExceptionCaught.EmitSignal(object sender, NetworkException ex)
        {
            ExceptionCaught?.Invoke(sender, ex);
        }

        #endregion 公开方法

        #endregion 方法
    }
}