using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace IceCoffee.Network.Sockets.MulitThreadTcpClient
{
    public abstract class CustomClient<TSession> : BaseClient<TSession> where TSession : CustomSession<TSession>, new()
    {
        private Timer _heartbeat;

        public bool HeartbeatEnable { get; set; }

        /// <summary>
        /// 心跳包发送周期，单位毫秒，默认6,000毫秒
        /// </summary>
        public int HeartbeatInterval { get; set; } = 60000;


        public CustomClient()
        {
            _heartbeat = new Timer();
            _heartbeat.Elapsed += CheckIsAlive;
            _heartbeat.AutoReset = true;
        }

        protected override void OnConnected()
        {
            _heartbeat.Enabled = HeartbeatEnable;
            _heartbeat.Interval = HeartbeatInterval;
            base.OnConnected();
        }

        protected override void OnDisconnected(SocketError closedReason)
        {
            _heartbeat.Stop();
            base.OnDisconnected(closedReason);
        }

        private void CheckIsAlive(object sender, ElapsedEventArgs e)
        {
            if(Session.IsAlive == false)
            {
                Session.SendHeartbeat();
            }
            Session.IsAlive = false;
        }

    }
}
