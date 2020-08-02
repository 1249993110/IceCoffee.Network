using System;
using System.Timers;

namespace IceCoffee.Network.Sockets.MulitThreadTcpServer
{
    public abstract class CustomServer<TSession> : BaseServer<TSession> where TSession : CustomSession<TSession>, new()
    {
        private Timer _heartbeat;

        public bool HeartbeatEnable { get; set; }

        /// <summary>
        /// 如果某个会话在此时长内未通讯，则主动断开此会话，单位毫秒，默认6,000毫秒
        /// </summary>
        public int Timeout { get; set; } = 60000;

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
                if ((now - session.Value.LastCommunicateTime).TotalMilliseconds >= Timeout)
                {
                    session.Value.Close();
                }
            }
        }

        protected override void OnStarted()
        {
            _heartbeat.Enabled = HeartbeatEnable;
            _heartbeat.Interval = HeartbeatCheckInterval;

            base.OnStarted();
        }

        protected override void OnStopped()
        {
            _heartbeat.Stop();
            base.OnStopped();
        }
    }
}