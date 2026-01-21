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
    }
}
