using FZK.Application.Share.Config;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;

namespace FZK.Application.Config.ViewModels
{
    internal class ConfigPLCViewModel : ReactiveObject
    {
        public ISystemConfigManager SystemConfigManager { get; }

        public ConfigPLCViewModel(ISystemConfigManager systemConfigManager)
        {
            SystemConfigManager = systemConfigManager;            
        }
    }
}
