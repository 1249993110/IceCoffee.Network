using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using System.Net.Sockets;
using System.Net;
using System.Collections.Concurrent;
using IceCoffee.Network.Sockets.Primitives;
using IceCoffee.Network.CatchException;
using IceCoffee.Network.Sockets.Pool;
using IceCoffee.Common;

namespace IceCoffee.Network.Sockets.MulitThreadTcpServer
{
    public delegate void StartedEventHandler();
    public delegate void StoppedEventHandler();
    public delegate void NewSessionSetupEventHandler<TSession>(TSession session) where TSession : BaseSession<TSession>, new();
    public delegate void SessionClosedEventHandler<TSession>(TSession session, SocketError closedReason) where TSession : BaseSession<TSession>, new();

    public class BaseServer : BaseServer<BaseSession>
    {
        public BaseServer() { }
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

        private readonly ManualResetEvent _acceptEvent = new ManualResetEvent(false);

        private readonly ConcurrentDictionary<int, TSession> _sessions = new ConcurrentDictionary<int, TSession>();

        private int _recvBufferSize = 4096;

        private int _sendBufferSize = 4096;

        private readonly SocketAsyncEventArgs _acceptSaea;

        #endregion

        #region 属性
        /// <summary>
        /// 获取所有会话
        /// </summary>
        public IReadOnlyDictionary<int, TSession> Sessions
        {
            get
            {
                return (IReadOnlyDictionary<int, TSession>)_sessions;
            }
        }
       
        /// <summary>
        /// 连接的会话数量
        /// </summary>
        public int SessionCount
        {
            get
            {
                return _sessions.Count;
            }
        }
        /// <summary>
        /// <para>每次接收数据的缓冲区大小，默认为4096字节/会话</para> 
        /// <para>必须在socket启动前设置。注意此属性与ReadBuffer读取缓冲区是不同的</para> 
        /// </summary>
        public int ReceiveBufferSize
        {
            get { return _recvBufferSize;}
            set { setBufferSize(ref _recvBufferSize, value); }
        }
        /// <summary>
        /// <para>每次发送数据的缓冲区大小，默认为4096字节/会话</para> 
        /// <para>必须在socket启动前设置。大于此大小的数据包将被作为临时的新缓冲区发送，此过程影响性能</para> 
        /// </summary>
        public int SendBufferSize
        {
            get { return _sendBufferSize; }
            set { setBufferSize(ref _sendBufferSize, value); }
        }
        /// <summary>
        /// 本地IP终结点
        /// </summary>
        public IPEndPoint LocalIPEndPoint
        {
            get
            {
                return (IPEndPoint)_socketWatcher.LocalEndPoint;
            }
        }
        /// <summary>
        /// 服务端是否正在监听
        /// </summary>
        public bool IsListening
        {
            get { return _isListening; }
        }
        /// <summary>
        /// 是否作为服务端
        /// </summary>
        public bool IsServer { get; private set; } = true;
        #endregion

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
        #endregion

        #region 方法

        #region 构造方法
        public BaseServer()
        {
            _acceptSaea = new SocketAsyncEventArgs();
            _acceptSaea.Completed += onAcceptAsyncRequestCompleted;
        }
        ~BaseServer()
        {
            Stop();
        }
        #endregion

        #region 私有方法
        [CatchException(Error = "设置缓冲区错误", CustomExceptionType = CustomExceptionType.Checked)]
        private void setBufferSize(ref int buffer, int value)
        {
            if (value < 1 || value > 1048576) //65535
                throw new ArgumentOutOfRangeException("缓冲区大小范围为：1-1048576");
            buffer = value;
        }
        [CatchException(Error = "开始监听异常", CustomExceptionType = CustomExceptionType.Checked)]
        private bool startListen()
        {
            if (_isListening)
                Stop();
            
            _recvSaeaPool = new SaeaPool(onRecvAsyncRequestCompleted, _recvBufferSize);
            _sendSaeaPool = new SaeaPool(onSendAsyncRequestCompleted, _sendBufferSize);
            _sessionPool = new SessionPool<TSession>(this, onPrivateSend, _recvSaeaPool.Add, emitExceptionCaughtSignal);

            _socketWatcher = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _socketWatcher.ExclusiveAddressUse = true;
            _socketWatcher.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
   

            _socketWatcher.Bind(new IPEndPoint(this._ipAddress, this._port));

            _socketWatcher.Listen(511);

            _acceptEvent.Set();

            _isListening = true;

            Thread myServer = new Thread(acceptSocket);
            // 设置这个线程是后台线程
            myServer.IsBackground = true;
            myServer.Start();

            //Task.Factory.StartNew(new Action(acceptSocket), TaskCreationOptions.LongRunning);   

            return _isListening;
        }
        [CatchException(Error = "异步接受客户异常")]
        private void acceptSocket()
        {
            OnStarted();
            while (_isListening)
            {
                _acceptSaea.AcceptSocket = null;
                if (_socketWatcher.AcceptAsync(_acceptSaea) == false)
                {
                    Task.Run(() =>
                    {
                        onAcceptAsyncRequestCompleted(_socketWatcher, _acceptSaea);
                    });
                }
                _acceptEvent.Reset();
                _acceptEvent.WaitOne();
            }
        }

        [CatchException(Error = "异步接收数据异常")]
        private void onAcceptAsyncRequestCompleted(object sender, SocketAsyncEventArgs e)
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

            TSession session =  _sessionPool.Take();

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
                        onRecvAsyncRequestCompleted(session.socket, receiveSaea);
                    });
                }
            }
            catch
            {
                onPrivateClose(session, e);
                throw;
            }
            if(flag)
                throw new NetworkException(string.Format("添加会话错误，sessionID: {0} 已存在",session.SessionID.ToString()));
        }
        [CatchException(Error = "异步接收数据异常")]
        private void onRecvAsyncRequestCompleted(object sender, SocketAsyncEventArgs e)
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
                    if(session.socket == null)
                    {
                        onPrivateClose(session, e);
                        
                    }
                    else if (session.socket.ReceiveAsync(receiveSaea) == false)
                    {
                        onRecvAsyncRequestCompleted(sender, receiveSaea);
                    }
                }
                catch
                {
                    onPrivateClose(session, e);
                    throw;
                }
            }
            else
            {
                onPrivateClose(session, e);
            }
        }
        [CatchException(Error = "会话即将关闭异常")]
        private void onPrivateClose(TSession session, SocketAsyncEventArgs e)
        {
            if (_isListening) // 不是正在停止监听
            {
                session.Close();
                _recvSaeaPool.Add(e);
                if (_sessions.TryRemove(session.SessionID, out session))
                {
                    OnSessionClosed(session, e.SocketError);
                    session.Detach(e.SocketError);
                }
                else
                {
                    throw new NetworkException("从会话列表中移除一个不存在的会话");
                }
                session.ReadBuffer.CollectAllRecvSaeaAndReset();
                _sessionPool.Add(session);
            }
        }
        [CatchException(Error = "异步发送数据异常")]
        private void onPrivateSend(TSession session, byte[] data)
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
                    onSendAsyncRequestCompleted(session.socket, sendSaea);
                }
            }
            else//否则创建一个新的BufferList进行发送
            {
                sendSaea.Completed -= onSendAsyncRequestCompleted;
                sendSaea.Completed += onSendAsyncRequestCompleted_UseBufferList;
                sendSaea.SetBuffer(null, 0, 0);
                sendSaea.BufferList = new ArraySegment<byte>[1] { new ArraySegment<byte>(data) };
                if (session.socket.SendAsync(sendSaea) == false)
                {
                    onSendAsyncRequestCompleted_UseBufferList(session.socket, sendSaea);
                }
            }
            
        }
        private void onSendAsyncRequestCompleted(object sender, SocketAsyncEventArgs e)
        {
            TSession session = (TSession)e.UserToken;
            if (e.SocketError == SocketError.Success)
            {
                //session.OnSent();
            }
            else
            {
                onPrivateClose(session, e);
            }
            _sendSaeaPool.Add(e);
        }
        private void onSendAsyncRequestCompleted_UseBufferList(object sender, SocketAsyncEventArgs e)
        {
            TSession session = (TSession)e.UserToken;
            if (e.SocketError == SocketError.Success)
            {
                //session.OnSent();
            }
            else
            {
                onPrivateClose(session, e);
            }
            e.BufferList = null;
            e.Dispose();
        }
        private void emitExceptionCaughtSignal(NetworkException e)
        {
            ExceptionCaught?.Invoke(e);
        }
        #endregion

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
        #endregion

        #region 公开方法
        /// <summary>
        /// 开始监听
        /// </summary>
        /// <param name="port">监听端口，端口范围为：0-65535</param>
        /// <returns>成功返回true,否则返回false</returns>
        public bool Start(ushort port)
        {
            _port = port;
            return startListen();
        }

        /// <summary>
        /// 开始监听
        /// </summary>
        /// <param name="ipStr">监听IP地址(字符串形式)，默认为Any</param>
        /// <param name="port">监听端口，端口范围为：0-65535</param>
        /// <returns>成功返回true,否则返回false</returns>
        [CatchException(Error = "开始监听异常", CustomExceptionType = CustomExceptionType.Checked)]
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
            return startListen();
        }

        /// <summary>
        /// 使用上次开始监听的参数重新开始监听
        /// </summary>
        /// <returns>成功返回true,否则返回false</returns>
        public bool Restart()
        {
            Stop();
            return startListen();
        }

        /// <summary>
        /// 停止监听
        /// </summary>
        [CatchException(Error = "停止监听异常")]
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
        [CatchException(Error = "关闭会话错误，sessionID不存在", CustomExceptionType = CustomExceptionType.Checked)]
        public void CloseSession(int sessionID)
        {
            _sessions[sessionID].Close();
        }

        /// <summary>
        /// 对指定sessionID发送数据
        /// </summary>
        /// <param name="data">待发送数据</param>
        /// <param name="sessionID">sessionID</param>
        [CatchException(Error = "发送数据错误，sessionID不存在")]
        public void SendByID(byte[] data, int sessionID)
        {
            _sessions[sessionID].Send(data);
        }

        void IExceptionCaught.EmitSignal(NetworkException e)
        {
            ExceptionCaught?.Invoke(e);
        }

        #endregion

        

        #endregion
    }
}
