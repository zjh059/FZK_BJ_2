using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FZK.Hardware.Scanner.Base
{
    public class ScannerConfig
    {
        public string IpAddress { get; set; } = "127.0.0.1";
        public int Port { get; set; } = 8888;
        public string Direction { get; set; }
        /// <summary>
        /// 间隔时间毫秒(ms)
        /// </summary>
        public int DelayTime { get; set; } = 200;
        public string TriggerCommand { get; set; } = "T";
        public int MaxReconnectCount { get; set; } = 5;
        public int ReconnectDelay { get; set; } = 1000;
    }
}
