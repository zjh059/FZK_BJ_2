using FZK.Application.Run.Service;
using FZK.Application.Run.Views;
using FZK.Application.Share.Config;
using FZK.Application.Share.Run;
using FZK.Core.Models;
using Prism.Events;
using Prism.Ioc;
using Prism.Modularity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading.Tasks;

namespace FZK.Application.Run
{
    [Module(ModuleName = Names.RunModule, OnDemand = true)]//延迟加载
    public class RunModule : IModule
    {
        public void OnInitialized(IContainerProvider containerProvider)
        {
            
        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterForNavigation<RunView>();
            containerRegistry.RegisterSingleton<IHardwareService, HardwareService>();//注册硬件初始化管理类
            containerRegistry.RegisterSingleton<IMesService, MesService>();//注册硬件初始化管理类 

            // 业务服务注册（瞬态或单宿，根据实际情况选择）
            containerRegistry.Register<IPlcService, PlcService>();
            containerRegistry.Register<IDatabaseService, DatabaseService>();
            containerRegistry.Register<IRobotCoordinator, RobotCoordinator>();          
        }
    }
}
