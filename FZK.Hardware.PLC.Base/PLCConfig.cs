using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FZK.Hardware.PLC.Base
{
    /// <summary>
    /// PLC通信配置类（适配欧姆龙Fins TCP协议）
    /// </summary>
    public class PLCConfig
    {
        #region 基础TCP配置
        /// <summary>
        /// PLC IP地址
        /// </summary>
        public string IpAddress { get; set; }
        /// <summary>
        /// Fins TCP端口（欧姆龙默认9600）
        /// </summary>
        public int Port { get; set; } = 9600;
        /// <summary>
        /// TCP收发超时时间(ms)
        /// </summary>
        public int Timeout { get; set; } = 3000;
        #endregion

        #region 重连配置
        /// <summary>
        /// 最大重连次数（0则无限重连）
        /// </summary>
        public int MaxReconnectCount { get; set; } = 10;
        /// <summary>
        /// 重连间隔(ms)
        /// </summary>
        public int ReconnectDelay { get; set; } = 2000;
        #endregion

        #region Fins协议专属配置
        /// <summary>
        /// 本地节点号（PC端，默认0x00）
        /// </summary>
        public byte LocalNode { get; set; } = 0x00;
        /// <summary>
        /// PLC节点号（目标，默认0x01，需和PLC实际配置一致）
        /// </summary>
        public byte PlcNode { get; set; } = 0x01;
        /// <summary>
        /// 网络号（默认0x00，单网无需修改）
        /// </summary>
        public byte NetworkNo { get; set; } = 0x00;
        /// <summary>
        /// （默单元号认0x00，单单元无需修改）
        /// </summary>
        public byte UnitNo { get; set; } = 0x00;
        #endregion

        #region 心跳配置（Fins标准心跳，无需自定义指令）
        /// <summary>
        public PLCRegisterType HeartbeatRegisterType { get; set; } = PLCRegisterType.DM; // 心跳寄存器类型（如CIO/DM）
        public ushort HeartbeatAddress { get; set; } = 110; // 心跳寄存器地址
        public int HeartbeatInterval { get; set; } = 1000; // 心跳间隔（ms）
        public bool HeartbeatIsOpen { get; set; } = true; // 心跳间隔（ms）
        #endregion

        #region 指令队列配置
        /// <summary>
        /// 最大发送队列长度（防止内存溢出）
        /// </summary>
        public int MaxSendQueueLength { get; set; } = 100;
        /// <summary>
        /// 指令发送间隔(ms)（适配PLC处理能力，防止指令积压）
        /// </summary>
        public int SendInterval { get; set; } = 100;
        #endregion


    }
}
