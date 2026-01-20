using FZK.Core.Models;
using Prism.Ioc;
using Prism.Modularity;
using System;
using FZK.Application.Login.Views;
using Prism.Ioc;
using Prism.Modularity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FZK.Application.Login
{
    [Module(ModuleName = Names.LoginModule, OnDemand = false)] //立即加载
    public class LoginModule : IModule
    {
        public void OnInitialized(IContainerProvider containerProvider)
        {
            
        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterForNavigation<LoginView>();
        }
    }
}
