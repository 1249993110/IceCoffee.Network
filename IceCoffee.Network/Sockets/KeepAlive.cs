using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace IceCoffee.Network.Sockets
{
    public class KeepAlive
    {
        /// <summary>
        /// 是否启用Keep-Alive
        /// </summary>
        public bool Enable { get; set; } = false;

        /// <summary>
        /// 尝试重连次数，默认3
        /// </summary>
        public int ConnectTimes { get; set; } = 3;

        /// <summary>
        /// 在这个时间间隔内没有数据交互，则发探测包，默认60000  单位: 毫秒
        /// </summary>
        public uint KeepAliveTime { get; set; } = 60000;

        /// <summary>
        /// //发探测包时间间隔，默认1000  单位: 毫秒
        /// </summary>
        public uint KeepAliveInterval { get; set; } = 1000;

        internal byte[] GetKeepAliveData()
        {
            uint dummy = 0;
            byte[] inOptionValues = new byte[Marshal.SizeOf(dummy) * ConnectTimes];
            BitConverter.GetBytes((uint)(Enable ? 1 : 0)).CopyTo(inOptionValues, 0);
            BitConverter.GetBytes(KeepAliveTime).CopyTo(inOptionValues, Marshal.SizeOf(dummy));
            BitConverter.GetBytes(KeepAliveInterval).CopyTo(inOptionValues, Marshal.SizeOf(dummy) * 2);
            return inOptionValues;
        }
    }
}
