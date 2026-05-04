using FZK.Application.Share.Config;
using FZK.Application.Share.Login;
using FZK.Core.Models;
using Prism.Events;
using Prism.Regions;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;

namespace FZK.Application.Debug.ViewModels
{  
    internal class DataBaseDebugViewModel : ReactiveObject
    {
        public ICommand LoadedCommand { get; }
        public ICommand NavigatonToViewCommand { get; }

        private IRegionManager RegionManager { get; }
        [Reactive]
        public ISession Session { get; private set; }
       
        public DataBaseDebugViewModel(IRegionManager regionManager)
        {
            RegionManager = regionManager;
            LoadedCommand = ReactiveCommand.Create(OnRegionManager);
            NavigatonToViewCommand = ReactiveCommand.Create<string>(OnNavigatonToViewCommand);
        }

        private void OnNavigatonToViewCommand(string view)
        {
            RegionManager.RequestNavigate(Names.DBRegion, view);
        }
        private bool loaded = false;
        private void OnRegionManager()
        {
            if (loaded) return;
            RegionManager.RequestNavigate(Names.DBRegion, Names.BTEntityDebugView);
            loaded = true;
        }
    }
}
