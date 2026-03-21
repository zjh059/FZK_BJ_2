using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FZK.Core.Enums
{
    public enum RobotCommand
    {
        None,       // 无指令
        RobAsc,     // 到达扫码位
        Move,       // 移动
        Stop,      // 停止
        ArriveScanPos
    }
}
