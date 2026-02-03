using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FZK.Hardware.Robot.Epson
{
    /// <summary>
    /// 机械手通信状态
    /// </summary>
    public enum RobotState
    {
        /// <summary>
        /// 未初始化
        /// </summary>
        UnInitialized,
        /// <summary>
        /// 连接中
        /// </summary>
        Connecting,
        /// <summary>
        /// 已连接
        /// </summary>
        Connected,
        /// <summary>
        /// 断线中
        /// </summary>
        Disconnecting,
        /// <summary>
        /// 重连中
        /// </summary>
        Reconnecting
    }
}
