using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FZK.Hardware.PLC.Base
{
    // 这些配置类应该由 SystemConfigManager 提供
    public class PlcAddressConfig
    {
        /// <summary>
        /// 治具1底板和导向板扫码
        /// </summary>
        public int Jig1TriggerScan { get; set; } = 0;
        /// <summary>
        /// 治具1焊接结果扫码
        /// </summary>
        public int Jig1TriggerWeld { get; set; } = 1;
        /// <summary>
        /// 治具1清零
        /// </summary>
        public int Jig1TriggerClear { get; set; } = 2;
        /// <summary>
        /// 治具2底板和导向板扫码
        /// </summary>
        public int Jig2TriggerScan { get; set; } = 3;
        /// <summary>
        /// 治具2焊接结果扫码
        /// </summary>
        public int Jig2TriggerWeld { get; set; } = 4;
        /// <summary>
        /// 治具2清零
        /// </summary>
        public int Jig2TriggerClear { get; set; } = 5;
        /// <summary>
        /// 治具1底板和导向板扫码完成
        /// </summary>
        public int Jig1ScanResult { get; set; } = 100;
        /// <summary>
        /// 治具1焊接结果扫码完成
        /// </summary>
        public int Jig1WeldResult { get; set; } = 101;
        /// <summary>
        /// 治具2底板和导向板扫码完成
        /// </summary>
        public int Jig2ScanResult { get; set; } = 102;
        /// <summary>
        /// 治具2焊接结果扫码完成
        /// </summary>
        public int Jig2WeldResult { get; set; } = 103;


        /// <summary>
        /// 治具1焊接扫码结果
        /// </summary>
        public int Jig1WeldFinalResult { get; set; } = 104;

        /// <summary>
        /// 治具1底板和导向板扫码结果
        /// </summary>
        public int Jig1CompareResult { get; set; } = 105;
        /// <summary>
        /// 治具2焊接扫码结果
        /// </summary>
        public int Jig2WeldFinalResult { get; set; } = 106;
        /// <summary>
        /// 治具2底板板和导向板扫码结果
        /// </summary>
        public int Jig2CompareResult { get; set; } = 107;
        /// <summary>
        /// 治具1焊接使用次数
        /// </summary>
        public int Jig1Count { get; set; } = 108;
        /// <summary>
        /// 治具2焊接使用次数
        /// </summary>
        public int Jig2Count { get; set; } = 109; 
        /// <summary>
        /// 心跳
        /// </summary>
        public int HeartbeatMonitor { get; set; } = 110;

    }
}
