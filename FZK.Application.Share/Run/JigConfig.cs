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
        public int ScanResultAddr { get; set; }   // D100/D102
        public int WeldResultAddr { get; set; }   // D101/D103
        public int CompareResultAddr { get; set; }// D106/D107
        public int WeldFinalAddr { get; set; }    // D104/D105
        public int CountAddr { get; set; }        // D108/D109
        public ScannerType BottomScanner { get; set; }
        public ScannerType? TopScanner { get; set; } // 有的治具顶部扫码枪可能为null
        public string JigName { get; set; }
    }
}
