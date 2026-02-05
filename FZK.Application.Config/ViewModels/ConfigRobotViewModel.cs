using FZK.Application.Config.Services;
using FZK.Application.Share.Config;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace FZK.Application.Config.ViewModels
{
    internal class ConfigRobotViewModel : ReactiveObject
    {
        public ISystemConfigManager SystemConfigManager { get; }
        public ConfigRobotViewModel(ISystemConfigManager systemConfigManager)
        {
            SystemConfigManager = systemConfigManager;
        }
        
    }
}
