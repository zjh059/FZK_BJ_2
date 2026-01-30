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
        ///// <summary>
        ///// 手动控制轴运动的步长mm
        ///// </summary>
        ////public float AxisMoveStepMM { get; set; } = 1f;
        ////public CppSetting CppSetting { get; set; } = new CppSetting() { threshold_circle = 127, threshold_element = 127 };
        ///// <summary>
        ///// 齿轮孔间距
        ///// </summary>
        ////public float CalibrateInterval { get; set; } = 4f;
        ///// <summary>
        ///// 表示初进料判断值
        ///// </summary>
        ////public int FeedingPixels { get; set; } = 10000;
        ///// <summary>
        ///// 最多测3个元件
        ///// </summary>
        ////public int MaxMeasureTimes { get; set; } = 3;
        ///// <summary>
        ///// 最多下降3次
        ///// </summary>
        ////public int MaxDescendTimes { get; set; } = 3;
        ///// <summary>
        ///// 丝印对比的匹配阈值,相似度
        ///// </summary>
        ////public double SilkThreshold { get; set; } = 0.85;

    }
}
