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
        public SoftwareConfig SoftwareConfig { get; set; } = new SoftwareConfig();

        public ScannerConfig LeftUpScannerConfig { get; set; } = new ScannerConfig()
        {
            IpAddress = "127.0.0.1",
            Port = 8888,
            Direction =ScannerType.左上.ToString()
        };
        public ScannerConfig LeftDownScannerConfig { get; set; } = new ScannerConfig()
        {
            IpAddress = "127.0.0.3",
            Port = 8889,
            Direction = ScannerType.左下.ToString()

        };
        public ScannerConfig RightUpScannerConfig { get; set; } = new ScannerConfig()
        {
            IpAddress = "127.0.0.2",
            Port = 8890,
            Direction = ScannerType.右上.ToString()
        };
        public ScannerConfig RightDownScannerConfig { get; set; } = new ScannerConfig()
        {
            IpAddress = "127.0.0.4",
            Port = 8891,
            Direction = ScannerType.右下.ToString()
        };
        public ScannerConfig RobotScannerConfig { get; set; } = new ScannerConfig()
        {
            IpAddress = "127.0.0.5",
            Port = 8892,
            Direction = ScannerType.机械臂.ToString()
        };
        public PLCConfig pLCConfig { get; set; } = new PLCConfig()
        {
            IpAddress = "127.0.0.6",
            Port = 8893
        };
        public RobotConfig robotConfig { get; set; } = new RobotConfig()
        {
            IpAddress = "127.0.0.7",
            Port = 8894
        };
        ///// <summary>
        ///// 左相机配置参数
        ///// </summary>
        //public CameraConfig LeftCameraConfig { get; set; } = new CameraConfig()
        //{
        //    IpAddress = "192.68.0.1",
        //    Direction = CameraType.Left.ToString(),
        //};
        ///// <summary>
        ///// 右相机配置参数
        ///// </summary>
        //public CameraConfig RightCameraConfig { get; set; } = new CameraConfig()
        //{
        //    IpAddress = "192.168.0.2",
        //    Direction = CameraType.Right.ToString(),
        //};
        ///// <summary>
        ///// 控制卡参数
        ///// </summary>
        //public ControlCardConfig ControlCardConfig { get; set; } = new ControlCardConfig();
        //public XAxisParameter XAxisParameter { get; set; } = new XAxisParameter();
        //public YAxisParameter YAxisParameter { get; set; } = new YAxisParameter();
        //public ZAxisParameter ZAxisParameter { get; set; } = new ZAxisParameter();
        //public MainAxisParameter MainAxisParameter { get; set; } = new MainAxisParameter();
        //public KnifeAxisParameter KnifeAxisParameter { get; set; } = new KnifeAxisParameter();
        //public TrackAxisParameter TrackAxisParameter { get; set; } = new TrackAxisParameter();
        //public CalibrationParameter CalibrationParameter { get; set; } = new CalibrationParameter();
        //public LightConfig LightConfig { get; set; } = new LightConfig();
        //public BridgeConfig BridgeConfig { get; set; } = new BridgeConfig();



    }
}
