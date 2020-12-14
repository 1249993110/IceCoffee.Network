using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IceCoffee.Network.Sockets.Primitives.Internal
{
    internal class Packet
    {
        #region 头部
        /// <summary>
        /// 数据类型
        /// </summary>
        public DataType DataType;

        /// <summary>
        /// 数据部分长度
        /// </summary>
        public int DataLength;

        /// <summary>
        /// CRC16，只对头部做校验
        /// </summary>
        public ushort CRC16;
        #endregion

        #region 身体
        /// <summary>
        /// 数据
        /// </summary>
        public object Data;
        #endregion

        #region 尾部
        
        #endregion
    }
}
