using FZK.Application.Share.Config;
using FZK.Hardware.PLC.Base;
using FZK.Hardware.Robot.Base;
using FZK.Hardware.Scanner.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FZK.Application.Config.Models
{
    /// <summary>
    /// 本系统所有的设置项,这是一个需要序列化成JSON的对象
    /// </summary>
    public class SystemConfigModel
    {
        public SoftwareConfig SoftwareConfig { get; set; } = new SoftwareConfig()
        {
            InitUrl = "http://localhost:5000/api/Product",
            TestResultUrl = "http://localhost:5000/api/Product",
            ReportStationUrl = "http://localhost:5000/api/Product",
             
        };

        public ScannerConfig LeftUpScannerConfig { get; set; } = new ScannerConfig()
        {
            IpAddress = "127.0.0.1",
            Port = 8888,
            Direction = ScannerType.治具1上
        };
        public ScannerConfig LeftDownScannerConfig { get; set; } = new ScannerConfig()
        {
            IpAddress = "127.0.0.1",
            Port = 8889,
            Direction = ScannerType.治具1下

        };
        public ScannerConfig RightUpScannerConfig { get; set; } = new ScannerConfig()
        {
            IpAddress = "127.0.0.1",
            Port = 8890,
            Direction = ScannerType.治具2上
        };
        public ScannerConfig RightDownScannerConfig { get; set; } = new ScannerConfig()
        {
            IpAddress = "127.0.0.1",
            Port = 8891,
            Direction = ScannerType.治具2下
        };
        public ScannerConfig RobotScannerConfig { get; set; } = new ScannerConfig()
        {
            IpAddress = "127.0.0.1",
            Port = 8892,
            Direction = ScannerType.机械臂
        };
        public PLCConfig pLCConfig { get; set; } = new PLCConfig()
        {
            IpAddress = "127.0.0.1",
            Port = 8893
        };
        public RobotConfig robotConfig { get; set; } = new RobotConfig()
        {
            IpAddress = "127.0.0.1",
            Port = 8894
        };
        public PlcAddressConfig plcAddressConfig { get; set; } = new PlcAddressConfig()
        { 
            Jig1TriggerScan = 0,
            Jig1TriggerWeld = 1,
            Jig1TriggerClear = 2,
            Jig2TriggerScan = 3,
            Jig2TriggerWeld = 4,
            Jig2TriggerClear = 5,
            Jig1ScanFinish = 100,
            Jig1WeldFinish = 101,
            Jig2ScanFinish = 102,
            Jig2WeldFinish = 103,
            Jig1WeldResult = 104,
            Jig1ScanResult = 105,
            Jig2WeldResult = 106,
            Jig2ScanResult = 107,
            Jig1Counts = 108,
            Jig2Counts = 109,
            HeartbeatMonitor = 110
        };
        public RunConfig runConfig { get; set; } = new RunConfig()
        {
            PlcReadInterval = 5000,
            StatusCheckInterval = 1000,
            MaxScanRecords = 1000,
            ScanRetryCount = 3,
            ScanRetryDelay = 200,

        };
    }
}
