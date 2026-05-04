using FZK.Application.Share.Init;
using FZK.Core.Config;
using FZK.Core.Extension;
using Prism.Ioc;
using Prism.Modularity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace FZK.Core
{
    public class CoreModule : IModule
    {
        public void OnInitialized(IContainerProvider containerProvider)
        {
            containerProvider.InitializeAssembly(Assembly.GetExecutingAssembly());
        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterSingleton<IConfigManager, ConfigManager>();
            containerRegistry.RegisterAssembly(Assembly.GetExecutingAssembly());//
        }
    }
}
