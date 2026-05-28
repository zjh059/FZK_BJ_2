using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FZK.Application.Share.Config
{
    public class SoftwareConfig
    {
        public string Title { get; set; } = "视觉对位运动控制系统";
        public double WindowWidth { get; set; } = 1800;
        public double WindowHeight { get; set; } = 1000;

        // 原有配置
        public string TestResultUrl { get; set; }
        public string ReportStationUrl { get; set; }
        public string StationCode { get; set; } = "123";
        public string InitUrl { get; set; }
        public string StationID { get; set; } = "123";

        // 新增：获取码信息接口 URL
        public string CodeInfoUrl { get; set; } = "";

        // 新增：本机设备 IP，用于调用码信息接口
        public string EquipmentIp { get; set; } = "";

        // 过站校验配置
        public string Process_dopass { get; set; } = "123";
        public string Station_dopass { get; set; } = "123";

        public bool IsSFC { get; set; } = false;
        public bool IsDebug { get; set; } = false;
        public string siteCode { get; set; } = "123";
        public string userCode { get; set; } = "123";
        public string password { get; set; } = "123";
    }
}
