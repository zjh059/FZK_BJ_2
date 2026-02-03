using FZK.Core.Config;
using FZK.Hardware.Robot.Base;
using Prism.Ioc;
using Prism.Modularity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading.Tasks;

namespace FZK.Hardware.Robot.Epson
{
    public class RobotEpsonModule : IModule
    {
        public void OnInitialized(IContainerProvider containerProvider)
        {
             
        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            if (!ConfigManager.NoHardwareMode)
            {
                containerRegistry.Register<IRobot, RobotEpsonRc90_B> ();
            }
        }
    }
}
