using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FZK.Hardware.PLC.Base
{
    public enum PLCRegisterType
    {
        /// <summary>
        /// DM区：数据寄存器（存数值/拆BOOL位，掉电保持）
        /// </summary>
        DM,
        /// <summary>
        /// CIO区：I/O控制区（物理I/O/通用BOOL位，掉电不保持）
        /// </summary>
        CIO,
        /// <summary>
        /// TIM区：定时器当前值（仅数值）
        /// </summary>
        TIM,
        /// <summary>
        /// CNTR区：计数器当前值（仅数值）
        /// </summary>
        CNTR
    }
}
