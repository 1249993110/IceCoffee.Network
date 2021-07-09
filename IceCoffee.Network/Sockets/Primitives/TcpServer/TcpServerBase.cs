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
using System.Linq;
using IceCoffee.Network.Sockets.Primitives.TcpSession;
using IceCoffee.Network.Sockets.Primitives.Internal;

namespace IceCoffee.Network.Sockets.Primitives.TcpServer
{
    public abstract class TcpServerBase<TSession> : SocketDispatcherBase, ITcpServerBase, IExceptionCaught where TSession : TcpSessionBase<TSession>, new()
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

        public override int ReceiveBufferSize
        {
            get => _recvBufferSize;
            set => SetBufferSize(ref _recvBufferSize, value);
        }

        public override int SendBufferSize
        {
            get => _sendBufferSize;
            set => SetBufferSize(ref _sendBufferSize, value);
        }

        public override IPEndPoint LocalIPEndPoint => _socketWatcher.LocalEndPoint as IPEndPoint;

        public override bool IsServerSide => true;

        public IReadOnlyDictionary<int, ITcpSessionBase> Sessions => _sessions.ToDictionary(p => p.Key, p => p.Value as ITcpSessionBase);

        public int SessionCount => _sessions.Count;

        public bool IsListening => _isListening;

        #endregion 属性

        #region 事件

        public event StartedEventHandler Started;

        public event StoppedEventHandler Stopped;

        public event NewSessionSetupEventHandler NewSessionSetup;

        public event SessionClosedEventHandler SessionClosed;

        public event ExceptionCaughtEventHandler ExceptionCaught;

        #endregion 事件

        #region 方法

        #region 构造方法

        public TcpServerBase()
        {
            _acceptEvent = new ManualResetEvent(false);
            _sessions = new ConcurrentDictionary<int, TSession>();
            _acceptSaea = new SocketAsyncEventArgs();
            _acceptSaea.Completed += OnAcceptAsyncRequestCompleted;
        }


        #endregion

        #region 私有方法
        [CatchException("开始监听异常", CustomExceptionType.Checked)]
        private bool InternalStart()
        {
            if (_isListening)
            {
                Stop();
            }

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

        [CatchException("异步接受客户异常")]
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

        [CatchException("异步接受客户异常")]
        private void OnAcceptAsyncRequestCompleted(object sender, SocketAsyncEventArgs e)
        {
            Socket socket = e.AcceptSocket;
            SocketError socketError = e.SocketError;

            _acceptEvent.Set();

            if (_isListening == false)
            {
                return;
            }
            else
            {
                // 异常socket连接
                if (socketError != SocketError.Success || socket.Connected == false)
                {
                    throw new NetworkException("异常socket连接，SocketError：" + socketError);
                }
                else
                {
                    TSession session = _sessionPool.Take();

                    int sessionID = socket.Handle.ToInt32();

                    if (_sessions.ContainsKey(sessionID))
                    {
                        _sessions.TryRemove(sessionID, out _);
                        throw new NetworkException(string.Format("添加会话错误，sessionID: {0} 已存在", session.SessionID));
                    }

                    SocketAsyncEventArgs receiveSaea = _recvSaeaPool.Take();

                    session.Attach(socket, sessionID);
                    receiveSaea.UserToken = session;

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
                        OnInternalClose(session, CloseReason.ApplicationError);
                        throw;
                    }
                }
            }
        }

        [CatchException("异步接收数据异常")]
        private void OnRecvAsyncRequestCompleted(object sender, SocketAsyncEventArgs e)
        {
            TSession session = e.UserToken as TSession;
            SocketError socketError = e.SocketError;

            if (e.BytesTransferred > 0 && socketError == SocketError.Success)
            {
                try
                {
                    session.ReadBuffer.CacheSaea(e);

                    // 服务端主动关闭会话
                    if (session.socket == null)
                    {
                        OnInternalClose(session, CloseReason.ServerClosing);
                    }
                    else
                    {
                        SocketAsyncEventArgs receiveSaea = _recvSaeaPool.Take();
                        receiveSaea.UserToken = session;

                        if (session.socket.ReceiveAsync(receiveSaea) == false)
                        {
                            OnRecvAsyncRequestCompleted(sender, receiveSaea);
                        }
                    }
                }
                catch
                {
                    OnInternalClose(session, CloseReason.InternalError);
                    throw;
                }
            }
            else
            {
                OnInternalClose(session, CloseReason.SocketError);
                throw new NetworkException("异常socket连接，SocketError：" + socketError);
            }
        }

        [CatchException("会话即将关闭异常")]
        private void OnInternalClose(TSession session, CloseReason closeReason)
        {
            if (_isListening) // 不是正在停止监听
            {
                session.Close();

                if (_sessions.TryRemove(session.SessionID, out session))
                {
                    session.OnClosed(closeReason);
                    OnSessionClosed(session, closeReason);
                    session.Detach();

                    CollectSession(session);
                }
                else
                {
                    //CollectSession(session);
                    throw new NetworkException("从会话列表中移除一个不存在的会话，请检查事件订阅，会话ID: " + session.SessionID);
                }
            }
        }

        private void CollectSession(TSession session)
        {
            session.ReadBuffer.CollectAllRecvSaeaAndReset();
            _sessionPool.Put(session);
        }

        [CatchException("异步发送数据异常")]
        private void OnInternalSend(ITcpSessionBase session, byte[] data, int offset, int count)
        {
            Socket socket = (session as TSession).socket;
            SocketAsyncEventArgs sendSaea = _sendSaeaPool.Take();

            if (count <= _sendBufferSize)// 如果count小于发送缓冲区大小，此时count应不大于data.Length
            {
                sendSaea.UserToken = session;
                Array.Copy(data, offset, sendSaea.Buffer, 0, count);
                sendSaea.SetBuffer(0, count);
                if (socket.SendAsync(sendSaea) == false)
                {
                    OnSendAsyncRequestCompleted(socket, sendSaea);
                }
            }
            else// 否则创建一个新的BufferList进行发送
            {
                SocketAsyncEventArgs temp_sendSaea = new SocketAsyncEventArgs();
                temp_sendSaea.UserToken = session;
                temp_sendSaea.Completed += OnSendAsyncRequestCompleted_UseBufferList;
                temp_sendSaea.BufferList = new ArraySegment<byte>[1] { new ArraySegment<byte>(data, offset, count) };
                if (socket.SendAsync(temp_sendSaea) == false)
                {
                    OnSendAsyncRequestCompleted_UseBufferList(socket, temp_sendSaea);
                }
            }
        }

        private void OnSendAsyncRequestCompleted(object sender, SocketAsyncEventArgs e)
        {
            TSession session = e.UserToken as TSession;
            if (e.SocketError != SocketError.Success)
            {
                OnInternalClose(session, CloseReason.SocketError);
                throw new NetworkException("异常socket连接，SocketError：" + e.SocketError);
            }
            _sendSaeaPool.Put(e);
        }

        private void OnSendAsyncRequestCompleted_UseBufferList(object sender, SocketAsyncEventArgs e)
        {
            TSession session = e.UserToken as TSession;
            if (e.SocketError != SocketError.Success)
            {
                OnInternalClose(session, CloseReason.SocketError);
                throw new NetworkException("异常socket连接，SocketError：" + e.SocketError);
            }
            e.BufferList = null;
            e.Dispose();
        }

        #endregion 私有方法

        #region 保护方法

        /// <summary>
        /// 当服务端开始监听后调用，默认引发相应事件
        /// </summary>
        protected virtual void OnStarted()
        {
            Started?.Invoke();
        }

        /// <summary>
        /// 当服务端停止监听后调用，默认引发相应事件
        /// </summary>
        protected virtual void OnStopped()
        {
            Stopped?.Invoke();
        }

        /// <summary>
        /// 当新会话建立时调用，默认引发相应事件
        /// </summary>
        /// <param name="session"></param>
        protected virtual void OnNewSessionSetup(TSession session)
        {
            NewSessionSetup?.Invoke(session);
        }

        /// <summary>
        /// 当会话关闭时调用，默认引发相应事件
        /// </summary>
        /// <param name="session"></param>
        /// <param name="closeReason"></param>
        protected virtual void OnSessionClosed(TSession session, CloseReason closeReason)
        {
            SessionClosed?.Invoke(session, closeReason);
        }

        #endregion 保护方法

        #region 公开方法

        public bool Start(ushort port)
        {
            _port = port;
            return InternalStart();
        }

        [CatchException("开始监听异常", CustomExceptionType.Checked)]
        public bool Start(string ipStr, ushort port)
        {
            return Start(IPAddress.Parse(ipStr), port);
        }

        public bool Start(IPAddress ipAddress, ushort port)
        {
            _ipAddress = ipAddress;
            _port = port;
            return InternalStart();
        }

        public bool Restart()
        {
            Stop();
            return InternalStart();
        }

        [CatchException("停止监听异常")]
        public void Stop()
        {
            if (_socketWatcher != null && _isListening)
            {
                _isListening = false;

                _acceptEvent.Set();

                foreach (var item in _sessions)
                {
                    var session = item.Value;
                    session.Close();

                    session.OnClosed(CloseReason.ServerShutdown);
                    OnSessionClosed(session, CloseReason.ServerShutdown);
                    session.Detach();
                }

                _socketWatcher.Close();

                _recvSaeaPool.Dispose();
                _sendSaeaPool.Dispose();
                _sessionPool.Dispose();
                OnStopped();
            }
        }

        
        [CatchException("关闭会话错误，sessionID不存在", CustomExceptionType.Checked)]
        public void CloseSession(int sessionID)
        {
            _sessions[sessionID].Close();
        }

        [CatchException("发送数据错误，sessionID不存在")]
        public void SendById(byte[] data, int sessionID)
        {
            _sessions[sessionID].Send(data);
        }

        public override void Dispose()
        {
            Stop();
        }
        #endregion 公开方法

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

        #endregion 方法
    }
}