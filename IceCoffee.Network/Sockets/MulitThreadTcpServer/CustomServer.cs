using IceCoffee.Common.LogManager;
using IceCoffee.Network.CatchException;
using System;
using System.Timers;

namespace IceCoffee.Network.Sockets.MulitThreadTcpServer
{
    public abstract class CustomServer<TSession> : BaseServer<TSession> where TSession : CustomSession<TSession>, new()
    {
        private readonly Timer _heartbeat;

        public bool HeartbeatEnable { get; set; }

        /// <summary>
        /// 如果某个会话在此时长内未通讯，则主动断开此会话，单位毫秒，默认12,000毫秒
        /// </summary>
        public int Timeout { get; set; } = 120000;

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

        [CatchException("检查会话是否存活异常")]
        private void CheckIsAlive(object sender, ElapsedEventArgs e)
        {
            DateTime now = DateTime.Now;
            foreach (var session in Sessions)
            {
                if ((now - session.Value.LastCommunicateTime).TotalMilliseconds >= Timeout)
                {
                    Log.Info("判定会话{0}不再存活，主动断开连接", session.Value.RemoteIPEndPoint);
                    session.Value.Close();
                }
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
    }
}