using FZK.Core.Config;
using FZK.Hardware.Scanner.Base;
using Prism.Ioc;
using Prism.Modularity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FZK.Hardware.Scanner.Cognex
{
    public class ScannerCognexModule : IModule
    {
        public void OnInitialized(IContainerProvider containerProvider)
        {
             
        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
           // if (!ConfigManager.NoHardwareMode)
            {
                containerRegistry.Register<IScanner, ScannerCognexDM260>(ScannerType.治具1上.ToString());
                containerRegistry.Register<IScanner, ScannerCognexDM260>(ScannerType.治具1下.ToString());
                containerRegistry.Register<IScanner, ScannerCognexDM260>(ScannerType.治具2上.ToString());
                containerRegistry.Register<IScanner, ScannerCognexDM260>(ScannerType.治具2下.ToString());
                containerRegistry.Register<IScanner, ScannerCognexDM260>(ScannerType.机械臂.ToString());

            }
        }
    }
}
