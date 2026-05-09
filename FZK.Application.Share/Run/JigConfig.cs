using FZK.Hardware.Scanner.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FZK.Application.Share.Run
{
    public class JigConfig
    {
        public int TriggerScanAddr { get; set; }
        public int TriggerWeldAddr { get; set; }
        public int TriggerClearAddr { get; set; }
        public int ScanFinishAddr { get; set; }   // D100/D102
        public int WeldFinishAddr { get; set; }   // D101/D103
        public int ScanResultAddr { get; set; }// D106/D107
        public int WeldResultAddr { get; set; }    // D104/D105
        public int CountsAddr { get; set; }        // D108/D109
        public ScannerType BottomScanner { get; set; }
        public ScannerType? TopScanner { get; set; } // 有的治具顶部扫码枪可能为null
        public string JigName { get; set; }
        public int OKFlag { get; set; } = 1;
        public int NGFlag { get; set; } = 2;
        public int FinishFlag { get; set; } = 1;
        public int ClearCounts { get; set; } = 40;


    }
}
