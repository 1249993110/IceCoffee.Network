using IceCoffee.Common.Pools;
using IceCoffee.Network.Sockets.Primitives;
using System;
using System.Net.Sockets;

namespace IceCoffee.Network.Sockets.Pool
{
    internal class SessionPool<TSession> : ConnectionPool<TSession> where TSession : BaseSession<TSession>, new()
    {
        private readonly ISocketDispatcher _socketDispatcher;

        private readonly InternalSendDataEventHandler<TSession> _sendData;

        private readonly Func<SocketAsyncEventArgs, bool> _saeaCollectEventHandler;

        public SessionPool(ISocketDispatcher socketDispatcher,
            InternalSendDataEventHandler<TSession> sendData,
            Func<SocketAsyncEventArgs, bool> saeaCollectEventHandler)
        {
            this._socketDispatcher = socketDispatcher;
            this._sendData = sendData;
            this._saeaCollectEventHandler = saeaCollectEventHandler;

            Min = Environment.ProcessorCount;
            if (Min < 2)
            {
                Min = 2;
            }

            Max = int.MaxValue;

            IdleTime = 60;
        }

        protected override TSession Create()
        {
            TSession socketSession = new TSession();
            socketSession.Initialize(_socketDispatcher, _sendData, _saeaCollectEventHandler);
            return socketSession;
        }
    }
}