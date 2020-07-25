using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using IceCoffee.Network.Sockets.Primitives;
using IceCoffee.Common;
using IceCoffee.Network.CatchException;

namespace IceCoffee.Network.Sockets.Pool
{
    class SessionPool<TSession> : ObjectPool<TSession> where TSession : BaseSession<TSession>, new()
    {
        private readonly ISocketDispatcher _socketDispatcher;

        private readonly InternalSendDataEventHandler<TSession> _sendData;

        private readonly Action<SocketAsyncEventArgs> _saeaCollectEventHandler;

        private readonly ExceptionCaughtEventHandler _emitExceptionCaughtSignal;
        public SessionPool(ISocketDispatcher socketDispatcher, 
            InternalSendDataEventHandler<TSession> sendData,
            Action<SocketAsyncEventArgs> saeaCollectEventHandler,
            ExceptionCaughtEventHandler exceptionCaughtEventHandler)
        {
            this._socketDispatcher = socketDispatcher;
            this._sendData = sendData;
            this._saeaCollectEventHandler = saeaCollectEventHandler;
            this._emitExceptionCaughtSignal = exceptionCaughtEventHandler;
        }

        protected override TSession Create()
        {
            TSession socketSession = new TSession();
            socketSession.Initialize(_socketDispatcher, _sendData, _saeaCollectEventHandler, _emitExceptionCaughtSignal);
            return socketSession;
        }
    }
}
