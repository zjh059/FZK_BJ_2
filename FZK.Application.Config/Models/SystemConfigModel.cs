using FZK.Application.Share.Config;
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
