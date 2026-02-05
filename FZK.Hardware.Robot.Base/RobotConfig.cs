using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FZK.Hardware.Robot.Base
{
    public class RobotConfig
    {
        // ********************* 基础配置 *********************
        public string IpAddress { get; set; } = "192.168.0.111";
        public int Port { get; set; } = 500;
        /// <summary>
        /// 通信超时时间（ms）
        /// </summary>
        public int Timeout { get; set; } = 3000;

        /// <summary>
        /// 断线重连间隔（ms）
        /// </summary>
        public int ReconnectDelay { get; set; } = 2000;

        /// <summary>
        /// 最大重连次数（-1表示无限重连）
        /// </summary>
        public int MaxReconnectCount { get; set; } = -1;

        /// <summary>
        /// 指令结束符（如\r\n，根据机械手协议配置）
        /// </summary>
        public string CommandEndFlag { get; set; } = "\r\n";
        // ********************* 新增：心跳包配置 *********************
        /// <summary>
        /// 心跳发送间隔(ms)，0则关闭心跳
        /// </summary>
        public int HeartbeatInterval { get; set; } = 3000;
        /// <summary>
        /// 心跳指令内容
        /// </summary>
        public string HeartbeatCommand { get; set; } = "PING";
        /// <summary>
        /// 心跳成功响应内容
        /// </summary>
        public string HeartbeatResponse { get; set; } = "PONG";

        // ********************* 新增：指令确认/超时配置 *********************
        /// <summary>
        /// 指令等待确认超时时间(ms)，0则关闭确认/重发
        /// </summary>
        public int CommandTimeout { get; set; } = 5000;
        /// <summary>
        /// 指令超时后重发次数，0则不重发
        /// </summary>
        public int CommandRetryCount { get; set; } = 3;
        /// <summary>
        /// 指令重发延迟(ms)
        /// </summary>
        public int CommandRetryDelay { get; set; } = 1000; 
    }
}
