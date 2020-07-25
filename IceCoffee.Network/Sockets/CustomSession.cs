using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;

namespace IceCoffee.Network.Sockets
{
    public abstract class CustomSession<TSession> : BaseSession<TSession> where TSession : CustomSession<TSession>, new()
    {
        public bool IsAlive { get; internal set; }

        private int _currentObjlen;

        public CustomSession()
        {
            
        }

        protected override void OnStarted()
        {
            IsAlive = true;
            base.OnStarted();
        }

        public void Send(object obj)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                ms.Write(new byte[4], 0, sizeof(int));

                IFormatter formatter = new BinaryFormatter();
                formatter.Serialize(ms, obj);

                ms.Seek(0, SeekOrigin.Begin);
                ms.Write(BitConverter.GetBytes(ms.Length - 4), 0, sizeof(int));
                base.Send(ms.ToArray());
            }
        }

        protected sealed override void OnReceived()
        {
            IsAlive = true;

            while (readBuffer.BytesAvailable > sizeof(int))
            {
                if (_currentObjlen == 0)
                {
                    _currentObjlen = BitConverter.ToInt32(readBuffer.Read(sizeof(int)), 0);
                }
                if (readBuffer.BytesAvailable < _currentObjlen)
                {
                    return;
                }
                using (MemoryStream ms = new MemoryStream(readBuffer.Read(_currentObjlen)))
                {
                    IFormatter formatter = new BinaryFormatter();

                    object result = formatter.Deserialize(ms);

                    // 41是new object()序列化后的长度
                    if (ms.Length == 41L && result.GetType() == typeof(object))
                    {
                        if(SocketDispatcher.IsServer)
                        {
                            SendHeartbeat();
                        }
                    }
                    else
                    {
                        this.OnReceived(result);
                    }
                }
                _currentObjlen = 0;
            }           
        }

        protected virtual void OnReceived(object obj)
        {

        }

        /// <summary>
        /// 发送一个new object()
        /// </summary>
        internal void SendHeartbeat()
        {
            Send(new object());
        }
    }
}
