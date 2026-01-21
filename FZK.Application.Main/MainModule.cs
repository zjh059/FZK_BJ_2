using FZK.Application.Main.Views;
using FZK.Core.Models;
using Prism.Ioc;
using Prism.Modularity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FZK.Application.Main
{
    [Module(ModuleName = Names.MainModule, OnDemand = true)] //延迟加载
    [ModuleDependency(Names.RunModule)]
    public class MainModule : IModule
    {
        public void OnInitialized(IContainerProvider containerProvider)
        {

        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterForNavigation<MainView>();
        }
    }
}
