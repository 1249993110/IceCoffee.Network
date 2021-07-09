using IceCoffee.Common;
using IceCoffee.Network.CatchException;
using IceCoffee.Network.Sockets.Primitives.Internal;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

namespace IceCoffee.Network.Sockets.Primitives.TcpSession
{
    public abstract class CustomSession<TSession> : TcpSessionBase<TSession> where TSession : CustomSession<TSession>, new()
    {
        internal bool isAlive;

        private int _currentObjLen;

        private const int _headSize = sizeof(DataType) + sizeof(int) + sizeof(ushort);

        private const int _headSizeNonCRC = _headSize - sizeof(ushort);

        private readonly static byte[] _heartbeatHead;

        private DateTime _lastCommunicateTime;

        /// <summary>
        /// 存活
        /// </summary>
        public bool IsAlive => isAlive;

        /// <summary>
        /// 上次通讯时间
        /// </summary>
        public DateTime LastCommunicateTime => _lastCommunicateTime;

        static CustomSession()
        {
            byte[] temp = new byte[5] { (byte)DataType.Heartbeat, 0, 0, 0, 0 };
            byte[] crc16 = BitConverter.GetBytes(CommonHelper.CRC16(temp));

            _heartbeatHead = new byte[_headSize];
            Array.Copy(temp, 0, _heartbeatHead, 0, _headSizeNonCRC);
            Array.Copy(crc16, 0, _heartbeatHead, _headSizeNonCRC, sizeof(ushort));
        }

        public CustomSession()
        {
        }

        internal protected override void OnStarted()
        {
            isAlive = true;
            _currentObjLen = 0;
            _lastCommunicateTime = DateTime.Now;
            base.OnStarted();
        }

        internal protected override void OnClosed(CloseReason closedReason)
        {
            isAlive = false;
            base.OnClosed(closedReason);
        }

        public void Send(object obj)
        {
            using (MemoryStream ms = new MemoryStream(256))
            {
                ms.Position = _headSize;
                BinaryFormatter formatter = new BinaryFormatter();
                formatter.Serialize(ms, obj);

                ms.Position = 0L;

                DataType dataType = SocketDispatcher.IsServerSide ? DataType.Response : DataType.Request;
                ms.WriteByte((byte)dataType);

                byte[] dataLength = BitConverter.GetBytes((int)ms.Length - _headSize);
                ms.Write(dataLength, 0, sizeof(int));

                byte[] buffer = ms.GetBuffer();

                ms.Write(BitConverter.GetBytes(CommonHelper.CRC16(buffer.Take(_headSizeNonCRC).ToArray())), 0, sizeof(ushort));
                base.Send(buffer, 0, (int)ms.Length);
            }
        }

        protected sealed override void OnReceived()
        {
            isAlive = true;
            _lastCommunicateTime = DateTime.Now;

            while (readBuffer.BytesAvailable >= _headSize)
            {
                byte[] bufferHead = readBuffer.Read(_headSize);

                if (_currentObjLen == 0)
                {
                    DataType dataType = (DataType)bufferHead[0];
                    ushort crc16 = BitConverter.ToUInt16(bufferHead, _headSizeNonCRC);

                    if (crc16 != CommonHelper.CRC16(bufferHead, _headSizeNonCRC))
                    {
                        Close();
                        throw new NetworkException("crc16 校验失败,会话自动关闭");
                    }

                    switch (dataType)
                    {
                        case DataType.Request:
                        case DataType.Response:
                            _currentObjLen = BitConverter.ToInt32(bufferHead, sizeof(DataType));
                            break;
                        case DataType.Heartbeat:
                            if (SocketDispatcher.IsServerSide)
                            {
                                SendHeartbeat();
                            }
                            return;
                        default:
                            throw new NetworkException("未知的 DataType: " + dataType);
                    }
                }

                if (readBuffer.BytesAvailable < _currentObjLen)
                {
                    return;
                }

                using (MemoryStream ms = new MemoryStream(readBuffer.Read(_currentObjLen)))
                {
                    BinaryFormatter formatter = new BinaryFormatter();

                    object result = formatter.Deserialize(ms);

                    this.OnReceived(result);
                }

                _currentObjLen = 0;
            }
        }

        protected virtual void OnReceived(object obj)
        {
        }

        /// <summary>
        /// 发送一个自定义的心跳包
        /// </summary>
        internal void SendHeartbeat()
        {
            base.Send(_heartbeatHead);
        }
    }
}