using FZK.Application.Initialize.Services;
using FZK.Application.Initialize.Views;
using FZK.Application.Share.Init;
using FZK.Core.Models;
using Prism.Ioc;
using Prism.Modularity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FZK.Application.Initialize
{
    [Module(ModuleName = Names.InitializeModule, OnDemand = true)] //延迟加载
    [ModuleDependency(Names.ConfigModule)]
    //[ModuleDependency(Names.CameraHikModule)]//HIk相机模块
    //[ModuleDependency(Names.ControlCardZmotionModule)]//正运动控制卡模块

    //[ModuleDependency(Names.CameraEmulationModule)]//仿真相机模块
    //[ModuleDependency(Names.ControlCardEmulationModule)]//仿真控制卡模块
    //[ModuleDependency(Names.LightEmulationModule)]//仿真光源模块
    //[ModuleDependency(Names.BridgeEmulationModule)]//仿真电桥模块
    public class InitializeModule : IModule
    {
        public void OnInitialized(IContainerProvider containerProvider)
        {
             
        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterForNavigation<InitializeView>();
            containerRegistry.RegisterSingleton<IHardwareManager, HardwareManager>();//注册硬件初始化管理类
        }
    }
}
