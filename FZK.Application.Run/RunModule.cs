using FZK.Application.Run.Views;
using FZK.Core.Models;
using Prism.Ioc;
using Prism.Modularity;
using System;
using System.Collections.Generic;
using System.Linq;
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
        }
    }
}
