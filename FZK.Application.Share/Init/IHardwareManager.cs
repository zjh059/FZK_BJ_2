using FZK.Hardware.PLC.Base;
using FZK.Hardware.Robot.Base;
using FZK.Hardware.Scanner.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;

namespace FZK.Application.Share.Init
{
    /// <summary>
    /// 硬件加载及管理类接口
    /// </summary>
    public interface IHardwareManager
    {
        bool Initialized { get; }
        Task<InitResult> InitAsync();
        Task<InitResult> Stop();
        IPLC OmronPLC { get; }
        IScanner LeftUpScanner { get; }
        IScanner LeftDownScanner { get; }
        IScanner RightUpScanner { get; }
        IScanner RightDownScanner { get; }
        IScanner SPScanner { get; }
        IRobot EpsonRobot { get; }
    }
}
