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

        public string TestResultUrl { get; set; }       
        public string ReportStationUrl { get; set; }  
        public string StationCode { get; set; } = "123";     
        public string InitUrl { get; set; }         
        public string StationID { get; set; } = "123";
        public bool IsSFC { get; set; } = false;     
        public string siteCode { get; set; } = "123";
        public string userCode { get; set; } = "123";
        public string password { get; set; } = "123";
    }
}
