using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FZK.Hardware.Robot.Epson
{
    /// <summary>
    /// 机械臂指令状态
    /// </summary>
    internal enum RobotCommandState
    {
        Pending = 0,    // 待发送（入队未发送）
        Sending = 1,    // 已发送，等待机械臂确认
        Success = 2,    // 机械臂执行成功（收到确认）
        Fail = 3,       // 机械臂执行失败（收到失败确认）
        Timeout = 4,    // 指令超时（未收到确认）
        Retrying = 5    // 指令超时，重发中
    }
}
