using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using IceCoffee.Common;

namespace IceCoffee.Network.Sockets.Pool
{
    class SaeaPool : ObjectPool<SocketAsyncEventArgs>
    {
        private readonly EventHandler<SocketAsyncEventArgs> _IO_CompletedEventHandler;

        private readonly int _bufferSize;

        public SaeaPool(EventHandler<SocketAsyncEventArgs> IO_CompletedEventHandler, int bufferSize)
        {
            this._IO_CompletedEventHandler = IO_CompletedEventHandler;
            this._bufferSize = bufferSize;
        }

        protected override SocketAsyncEventArgs Create()
        {
            SocketAsyncEventArgs saea = new SocketAsyncEventArgs();
            saea.Completed += _IO_CompletedEventHandler;

            //设置缓冲区
            saea.SetBuffer(new byte[_bufferSize], 0, _bufferSize);
            return saea;
        }
    }
}
