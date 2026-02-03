using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FZK.Hardware.Robot.Epson
{
    /// <summary>
    /// 机械臂指令实体（带ID绑定，用于确认/超时/重发）
    /// </summary>
    internal class RobotCommand
    {
        /// <summary>
        /// 唯一指令ID（自增，保证全局唯一）
        /// </summary>
        public long Id { get; set; }

        /// <summary>
        /// 原始指令内容（外部传入，如MOVE J1 100）
        /// </summary>
        public string OriginalCommand { get; set; }

        /// <summary>
        /// 带ID的完整指令（发送给机械臂，如1|MOVE J1 100\r\n）
        /// </summary>
        public string FullCommand { get; set; }

        /// <summary>
        /// 指令当前状态
        /// </summary>
        public RobotCommandState State { get; set; }

        /// <summary>
        /// 指令创建时间（用于判断超时）
        /// </summary>
        public DateTime CreateTime { get; set; } = DateTime.Now;

        /// <summary>
        /// 最后一次发送时间（用于重发时更新）
        /// </summary>
        public DateTime LastSendTime { get; set; } = DateTime.Now;

        /// <summary>
        /// 已重发次数（用于判断是否超过最大重发次数）
        /// </summary>
        public int RetryCount { get; set; } = 0;

        /// <summary>
        /// 机械臂返回的响应信息（成功/失败原因）
        /// </summary>
        public string Response { get; set; }
    }
}
