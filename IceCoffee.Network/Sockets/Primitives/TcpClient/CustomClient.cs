using IceCoffee.Network.Sockets.Primitives.TcpSession;
using System.Net.Sockets;
using System.Timers;

namespace IceCoffee.Network.Sockets.Primitives.TcpClient
{
    public abstract class CustomClient<TSession> : TcpClientBase<TSession> where TSession : CustomSession<TSession>, new()
    {
        private readonly Timer _heartbeat;
        private int _heartbeatInterval = 60000;
        private bool _heartbeatEnable;

        /// <summary>
        /// 是否启用心跳包
        /// </summary>
        public bool HeartbeatEnable { get => _heartbeatEnable; set => _heartbeatEnable = value; }

        /// <summary>
        /// 心跳包发送周期，单位毫秒，默认60,000毫秒
        /// </summary>
        public int HeartbeatInterval { get => _heartbeatInterval; set => _heartbeatInterval = value; }
        
        public CustomClient()
        {
            _heartbeat = new Timer();
            _heartbeat.Elapsed += CheckIsAlive;
            _heartbeat.AutoReset = true;
        }

        protected override void OnConnected()
        {
            _heartbeat.Interval = _heartbeatInterval;
            _heartbeat.Enabled = _heartbeatEnable;
            base.OnConnected();
        }

        protected override void OnDisconnected(CloseReason closedReason)
        {
            _heartbeat.Stop();
            base.OnDisconnected(closedReason);
        }

        private void CheckIsAlive(object sender, ElapsedEventArgs e)
        {
            if (Session.IsAlive == false)
            {
                Session.SendHeartbeat();
            }

            Session.isAlive = false;
        }
    }
}