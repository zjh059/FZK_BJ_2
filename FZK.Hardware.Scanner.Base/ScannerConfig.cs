using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FZK.Hardware.Scanner.Base
{
    public class ScannerConfig
    {
        /// <summary>
        /// IP地址 
        /// </summary>
        public string IpAddress { get; set; } = "127.0.0.1";
        /// <summary>
        /// 端口号
        /// </summary>
        public int Port { get; set; } = 8888;
        /// <summary>
        /// 位置
        /// </summary>
        public ScannerType Direction { get; set; }
        /// <summary>
        /// 间隔时间毫秒(ms)
        /// </summary>
        public int DelayTime { get; set; } = 200;
        /// <summary>
        /// 触发()
        /// </summary>
        public string TriggerCommand { get; set; } = "T";
        public int MaxReconnectCount { get; set; } = 5;
        public int ReconnectDelay { get; set; } = 1000;
        /// <summary>
        /// 字符编码名称，例如 "utf-8", "gb2312", "ascii"
        /// </summary>
        public string EncodingName { get; set; } = "utf-8";
        /// <summary>
        /// 多码结果之间的分隔符（结束符），例如 "\r\n", "," 等
        /// </summary>
        public string CodeDelimiter { get; set; } = ",";
        public string EndOfMessageDelimiter { get; set; } = "\r\n";

        public int SnLength { get; set; } = 26;
    }
}
