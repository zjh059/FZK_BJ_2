using FZK.Application.Config.Views;
using FZK.Application.Share.Config;
using FZK.Core.Models;
using Prism.Ioc;
using Prism.Modularity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FZK.Application.Config.Services;

namespace FZK.Application.Config
{
    [Module(ModuleName = Names.ConfigModule, OnDemand = true)] //延迟加载
    public class ConfigModule : IModule
    {
        public void OnInitialized(IContainerProvider containerProvider)
        {

        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterSingleton<ISystemConfigManager, SystemConfigManager>();//注册系统参数服务类
           
            containerRegistry.RegisterForNavigation<ConfigView>();
            containerRegistry.RegisterForNavigation<ConfigAxisView>();
            containerRegistry.RegisterForNavigation<ConfigIOView>();
            containerRegistry.RegisterForNavigation<ConfigSoftwareView>();
            containerRegistry.RegisterForNavigation<ConfigUserView>();
            containerRegistry.RegisterForNavigation<CameraCalibrationView>();
            containerRegistry.RegisterForNavigation<ConfigMarkView>();
            containerRegistry.RegisterForNavigation<AxisCalibrationView>();

        }
    }
}
