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
    internal class ConfigAxisViewModel : ReactiveObject
    {
        public ICommand MoveAbsoluteCommand { get; }
        public ICommand AxisJogPlusCommand { get; }
        public ICommand AxisJogMinusCommand { get; }
        public ICommand AxisRecoverCommand { get; }
        public ICommand AxisStopCommand { get; }
        public ICommand AxisResetCommand { get; }
        public ICommand AxisEnableCommand { get; }





        public ISystemConfigManager SystemConfigManager { get; }

        public ConfigAxisViewModel(ISystemConfigManager systemConfigManager)
        {
            SystemConfigManager = systemConfigManager;            
        }
 
        
    }
}
