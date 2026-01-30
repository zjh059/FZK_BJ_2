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

        //CameraConfig LeftCameraConfig { get; }
        //CameraConfig RightCameraConfig { get; }
        //SoftwareConfig SoftwareConfig { get; }
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
