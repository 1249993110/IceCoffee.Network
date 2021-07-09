using IceCoffee.Network.CatchException;
using IceCoffee.Network.Sockets.Primitives.TcpSession;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace IceCoffee.Network.Sockets.Primitives.TcpServer
{
    /// <summary>
    /// ITcpServer
    /// </summary>
    public interface ITcpServerBase : ISocketDispatcherBase
    {
        /// <summary>
        /// 获取所有会话
        /// </summary>
        IReadOnlyDictionary<int, ITcpSessionBase> Sessions { get; }

        /// <summary>
        /// 连接的会话数量
        /// </summary>
        int SessionCount { get; }

        /// <summary>
        /// 服务端是否正在监听
        /// </summary>
        bool IsListening { get; }

        /// <summary>
        /// 开始监听，OnStarted 引发 Started 事件。
        /// </summary>
        event StartedEventHandler Started;

        /// <summary>
        /// 停止监听，OnStopped 引发 Started 事件。
        /// </summary>
        event StoppedEventHandler Stopped;

        /// <summary>
        /// 新会话建立，OnNewSessionSetup 引发 Started 事件。
        /// </summary>
        event NewSessionSetupEventHandler NewSessionSetup;

        /// <summary>
        /// 会话结束，OnSessionClosed 引发 Started 事件。
        /// </summary>
        event SessionClosedEventHandler SessionClosed;

        /// <summary>
        /// 异常捕获
        /// </summary>
        event ExceptionCaughtEventHandler ExceptionCaught;

        /// <summary>
        /// 开始监听
        /// </summary>
        /// <param name="port">监听端口，端口范围为：0-65535</param>
        /// <returns>成功返回true,否则返回false</returns>
        bool Start(ushort port);

        /// <summary>
        /// 开始监听
        /// </summary>
        /// <param name="ipStr">监听IP地址(字符串形式)，默认为Any</param>
        /// <param name="port">监听端口，端口范围为：0-65535</param>
        /// <returns>成功返回true,否则返回false</returns>
        bool Start(string ipStr, ushort port);

        /// <summary>
        /// 开始监听
        /// </summary>
        /// <param name="ipAddress">监听IP地址</param>
        /// <param name="port">监听端口，端口范围为：0-65535</param>
        /// <returns>成功返回true,否则返回false</returns>
        bool Start(IPAddress ipAddress, ushort port);

        /// <summary>
        /// 使用上次开始监听的参数重新开始监听
        /// </summary>
        /// <returns>成功返回true,否则返回false</returns>
        bool Restart();

        /// <summary>
        /// 停止监听
        /// </summary>
        void Stop();

        /// <summary>
        /// 关闭会话
        /// </summary>
        /// <param name="sessionID"></param>
        void CloseSession(int sessionID);

        /// <summary>
        /// 对指定sessionID发送数据
        /// </summary>
        /// <param name="data">待发送数据</param>
        /// <param name="sessionID">sessionID</param>
        void SendById(byte[] data, int sessionID);
    }
}
