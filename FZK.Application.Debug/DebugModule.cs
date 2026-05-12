using FZK.Application.Debug.Service;
using FZK.Application.Debug.ViewModels;
using FZK.Application.Debug.Views;
using FZK.Application.Share.DebugFolder;
using Prism.Ioc;
using Prism.Modularity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FZK.Application.Debug
{
    public class DebugModule : IModule
    {
        public void OnInitialized(IContainerProvider containerProvider)
        {
            
        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterForNavigation<DataBaseDebugView>();
            containerRegistry.RegisterForNavigation<CodeEntityDebugView>();
            containerRegistry.RegisterForNavigation<BTEntityDebugView>();
            containerRegistry.RegisterForNavigation<UserEntityDebugView>();

            containerRegistry.RegisterSingleton<IDatabaseManager, DatabaseManager>();   
        }
    }
}
