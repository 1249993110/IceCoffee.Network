using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

namespace IceCoffee.Network.Sockets
{
    public abstract class CustomSession<TSession> : BaseSession<TSession> where TSession : CustomSession<TSession>, new()
    {
        public bool IsAlive { get; internal set; }

        private int _currentObjLen;

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
                if (_currentObjLen == 0)
                {
                    _currentObjLen = BitConverter.ToInt32(readBuffer.Read(sizeof(int)), 0);
                }
                if (readBuffer.BytesAvailable < _currentObjLen)
                {
                    return;
                }
                using (MemoryStream ms = new MemoryStream(readBuffer.Read(_currentObjLen)))
                {
                    IFormatter formatter = new BinaryFormatter();

                    object result = formatter.Deserialize(ms);

                    // 41是new object()序列化后的长度
                    if (_currentObjLen == 41 && result.GetType() == typeof(object))
                    {
                        if (SocketDispatcher.AsServer)
                        {
                            SendHeartbeat();
                        }
                    }
                    else
                    {
                        this.OnReceived(result);
                    }
                }
                _currentObjLen = 0;
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