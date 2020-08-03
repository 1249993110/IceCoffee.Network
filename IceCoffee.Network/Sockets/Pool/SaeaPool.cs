using IceCoffee.Common.Pools;
using System;
using System.Net.Sockets;

namespace IceCoffee.Network.Sockets.Pool
{
    internal class SaeaPool : ConnectionPool<SocketAsyncEventArgs>
    {
        private readonly EventHandler<SocketAsyncEventArgs> _io_CompletedEventHandler;

        private readonly int _bufferSize;

        public SaeaPool(EventHandler<SocketAsyncEventArgs> io_CompletedEventHandler, int bufferSize)
        {
            this._io_CompletedEventHandler = io_CompletedEventHandler;
            this._bufferSize = bufferSize;

            Min = Environment.ProcessorCount;
            if (Min < 2)
            {
                Min = 2;
            }

            Max = int.MaxValue;

            IdleTime = 60;
            AllIdleTime = 180;
        }

        protected override SocketAsyncEventArgs Create()
        {
            SocketAsyncEventArgs saea = new SocketAsyncEventArgs();
            saea.Completed += _io_CompletedEventHandler;

            //设置缓冲区
            saea.SetBuffer(new byte[_bufferSize], 0, _bufferSize);
            return saea;
        }
    }
}