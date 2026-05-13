using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FZK.Application.Share.Config
{
    public class RunConfig
    {
        public int PlcReadInterval { get; set; } = 2000;
        public int StatusCheckInterval { get; set; } = 1000;
        public int MaxScanRecords { get; set; } = 10;
        /// <summary>
        /// 重试次数
        /// </summary>
        public int ScanRetryCount { get; set; } = 3;
        public int ScanRetryDelay { get; set; } = 200;
      
    }
}
