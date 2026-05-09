namespace FZK.Hardware.PLC.Base
{
    // 这些配置类应该由 SystemConfigManager 提供
    public class PlcAddressConfig
    {
        /// <summary>
        /// 治具1底板和导向板扫码触发D0
        /// </summary>
        public int Jig1TriggerScan { get; set; } = 0;
        /// <summary>
        /// 治具1焊接后扫码D1
        /// </summary>
        public int Jig1TriggerWeld { get; set; } = 1;
        /// <summary>
        /// 治具1清零D2
        /// </summary>
        public int Jig1TriggerClear { get; set; } = 2;
       
        /// <summary>
        /// 治具1扫码完成D100
        /// </summary>
        public int Jig1ScanFinish { get; set; } = 100;
        /// <summary>
        /// 治具1焊接扫码完成D101
        /// </summary>
        public int Jig1WeldFinish { get; set; } = 101;
        /// <summary>
        /// 治具1焊接扫码结果D104
        /// </summary>
        public int Jig1WeldResult { get; set; } = 104;
        /// <summary>
        /// 治具1底导扫码结果D105
        /// </summary>
        public int Jig1ScanResult { get; set; } = 105;
        /// <summary>
        /// 治具1清零
        /// </summary>
        public int Jig1Counts { get; set; } = 108;



        /// <summary>
        /// 治具2底板和导向板扫码D3
        /// </summary>
        public int Jig2TriggerScan { get; set; } = 3;
        /// <summary>
        /// 治具2焊接结果扫码D4
        /// </summary>
        public int Jig2TriggerWeld { get; set; } = 4;
        /// <summary>
        /// 治具2清零D5
        /// </summary>
        public int Jig2TriggerClear { get; set; } = 5;

        /// <summary>
        /// 治具2底板和导向板扫码完成D102
        /// </summary>
        public int Jig2ScanFinish { get; set; } = 102;
        /// <summary>
        /// 治具2焊接扫码完成D103
        /// </summary>
        public int Jig2WeldFinish { get; set; } = 103;
       
        /// <summary>
        /// 治具2焊接结果D106
        /// </summary>
        public int Jig2WeldResult { get; set; } = 106;
        /// <summary>
        /// 治具2底导扫码结果D107
        /// </summary>
        public int Jig2ScanResult { get; set; } = 107;       
        /// <summary>
        /// 治具2使用次数D109
        /// </summary>
        public int Jig2Counts { get; set; } = 109;
        /// <summary>
        /// 心跳D110
        /// </summary>
        public int HeartbeatMonitor { get; set; } = 110;

    }
}
