using FZK.Application.Share.Config;
using FZK.Application.Share.Language;
using FZK.Application.Share.Login;
using FZK.Core.Models;
using Prism.Regions;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace FZK.Application.Config.ViewModels
{
    internal class ConfigViewModel : ReactiveObject
    {
        public ICommand LoadedCommand { get; }
        public ICommand NavigatonToViewCommand { get; }
        public ICommand SaveConfigCommand { get; }

        private IRegionManager RegionManager { get; }
        private ISystemConfigManager SystemConfigManager { get; }

        [Reactive]
        public ISession Session { get; private set; }
        public ConfigViewModel(
            ISession session,
            IRegionManager regionManager,
            ISystemConfigManager systemConfigManager)
        {
            Session = session;
            RegionManager = regionManager;
            SystemConfigManager = systemConfigManager;
            LoadedCommand = ReactiveCommand.Create(OnRegionManager);
            NavigatonToViewCommand = ReactiveCommand.Create<string>(OnNavigatonToViewCommand);
            SaveConfigCommand = ReactiveCommand.Create(OnSaveConfigCommand);
        }

        private void OnSaveConfigCommand()
        {
            SystemConfigManager.Save();
            MessageBox.Show(MultiLang.SaveSuccess);
        }

        private void OnNavigatonToViewCommand(string view)
        {
            RegionManager.RequestNavigate(Names.ConfigRegion, view);
        }

        private bool loaded = false;
        private void OnRegionManager()
        {
            if (loaded) return;
            RegionManager.RequestNavigate(Names.ConfigRegion, Names.ConfigSoftwareView);
            loaded = true;
        }
    }
}
