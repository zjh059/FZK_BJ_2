using FZK.Application.Share.Config;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FZK.Application.Config.ViewModels
{
    internal class ConfigSoftwareViewModel : ReactiveObject
    {
        public ISystemConfigManager SystemConfigManager { get; }
        public ConfigSoftwareViewModel(ISystemConfigManager systemConfigManager)
        {
            SystemConfigManager = systemConfigManager;
        }
    }
}
