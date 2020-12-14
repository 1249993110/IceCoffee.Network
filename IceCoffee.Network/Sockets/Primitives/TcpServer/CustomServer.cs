using IceCoffee.Network.CatchException;
using IceCoffee.Network.Sockets.Primitives.TcpSession;
using System;
using System.Timers;

namespace IceCoffee.Network.Sockets.Primitives.TcpServer
{
    public abstract class CustomServer<TSession> : TcpServerBase<TSession> where TSession : CustomSession<TSession>, new()
    {
        private readonly Timer _heartbeat;

        public bool HeartbeatEnable { get; set; }

        /// <summary>
        /// 如果某个会话在此时长内未通讯，则主动断开此会话，单位毫秒，默认18,000毫秒
        /// </summary>
        public int Timeout { get; set; } = 180000;

        /// <summary>
        /// 心跳包检查周期 单位毫秒，默认180,000毫秒
        /// </summary>
        public int HeartbeatCheckInterval { get; set; } = 180000;

        public CustomServer()
        {
            _heartbeat = new Timer();
            _heartbeat.Elapsed += CheckIsAlive;
            _heartbeat.AutoReset = true;
        }

        private void CheckIsAlive(object sender, ElapsedEventArgs e)
        {
            DateTime now = DateTime.Now;
            foreach (var session in Sessions)
            {
                InternalCheck(session.Value as TSession, now);
            }
        }

        [CatchException("检查会话是否存活异常")]
        private void InternalCheck(TSession session, DateTime now)
        {
            if ((now - session.LastCommunicateTime).TotalMilliseconds >= Timeout)
            {
                OnSessionTimeOut(session);
            }
        }

        protected override void OnStarted()
        {
            _heartbeat.Interval = HeartbeatCheckInterval;
            _heartbeat.Enabled = HeartbeatEnable;

            base.OnStarted();
        }

        protected override void OnStopped()
        {
            _heartbeat.Stop();
            base.OnStopped();
        }

        /// <summary>
        /// 会话超时时调用，默认主动关闭会话
        /// </summary>
        /// <param name="session"></param>
        protected virtual void OnSessionTimeOut(TSession session)
        {
            session?.Close();
        }
    }
}