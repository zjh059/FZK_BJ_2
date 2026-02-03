using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FZK.Hardware.PLC.Base
{
    /// <summary>
    /// PLC通信状态枚举
    /// </summary>
    public enum PLCState
    {
        /// <summary>
        /// 未初始化
        /// </summary>
        UnInitialized = 0,
        /// <summary>
        /// 正在连接
        /// </summary>
        Connecting = 1,
        /// <summary>
        /// 已连接
        /// </summary>
        Connected = 2,
        /// <summary>
        /// 正在重连
        /// </summary>
        Reconnecting = 3,
        /// <summary>
        /// 正在断开
        /// </summary>
        Disconnecting = 4
    }
}
