using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FZK.Hardware.PLC.Base
{
    /// <summary>
    /// 欧姆龙PLC寄存器类型枚举（常用类型）
    /// </summary>
    public enum PLCRegisterType
    {
        /// <summary>
        /// 数据寄存器（D区，最常用）
        /// </summary>
        D,
        /// <summary>
        /// 内部辅助继电器（W区）
        /// </summary>
        W,
        /// <summary>
        /// 输入继电器（X区）
        /// </summary>
        X,
        /// <summary>
        /// 输出继电器（Y区）
        /// </summary>
        Y,
        /// <summary>
        /// 计数器当前值（C区）
        /// </summary>
        C,
        /// <summary>
        /// 定时器当前值（T区）
        /// </summary>
        T
    }
}
