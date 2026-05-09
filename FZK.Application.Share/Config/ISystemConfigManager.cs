using FZK.Hardware.PLC.Base;
using FZK.Hardware.Robot.Base;
using FZK.Hardware.Scanner.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FZK.Application.Share.Config
{
    public interface ISystemConfigManager
    {
        /// <summary>
        /// 保存系统参数
        /// </summary>
        void Save();
        ScannerConfig Jig1UpScannerConfig { get; }
        ScannerConfig Jig2UpScannerConfig { get; }
        ScannerConfig Jig1DownScannerConfig { get; }
        ScannerConfig Jig2DownScannerConfig { get; }
        ScannerConfig RobotScannerConfig { get; }
        PLCConfig pLCConfig { get; }
        PlcAddressConfig plcAddressConfig { get; }
        RobotConfig robotConfig { get; }
        RunConfig runConfig { get; }
        SoftwareConfig SoftwareConfig { get; }
        //CameraConfig LeftCameraConfig { get; }
        //CameraConfig RightCameraConfig { get; }
        //ControlCardConfig ControlCardConfig { get; }
        //LightConfig LightConfig { get; }
        //BridgeConfig BridgeConfig { get; }
        //XAxisParameter XAxisParameter { get; }
        //YAxisParameter YAxisParameter { get; }
        //ZAxisParameter ZAxisParameter { get; }
        //MainAxisParameter MainAxisParameter { get; }
        //KnifeAxisParameter KnifeAxisParameter { get; }
        //TrackAxisParameter TrackAxisParameter { get; }
        //CalibrationParameter CalibrationParameter { get; }
    }
}
