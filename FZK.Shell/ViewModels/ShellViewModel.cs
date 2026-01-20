using FZK.Core.Models;
using Prism.Regions;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace FZK.Shell.ViewModels
{
    internal class ShellViewModel : ReactiveObject
    {
        public ICommand LoadedCommand { get; }
        private IRegionManager RegionManager { get; }
        public ShellViewModel(IRegionManager regionManager)
        {
            RegionManager = regionManager;
            LoadedCommand = ReactiveCommand.Create(OnLoadedComand);
        }

        private void OnLoadedComand()
        {
            RegionManager.RequestNavigate(Names.ShellRegion, Names.LoginView);
        }
    }
}
