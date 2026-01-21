using FZK.Application.Share.Models;
using FZK.Core.Models;
using FZK.Database.Base.Models;
using Prism.Events;
using Prism.Modularity;
using Prism.Regions;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace FZK.Shell.ViewModels
{
    internal class ShellViewModel : ReactiveObject
    {
        public ICommand LoadedCommand { get; }
        private IRegionManager RegionManager { get; }
        private IEventAggregator EventAggregator { get; }
        private IModuleManager ModuleManager { get; }

        public ShellViewModel(
            IRegionManager regionManager,
              IModuleManager moduleManager,
             IEventAggregator eventAggregator)
        {
            ModuleManager = moduleManager;
            RegionManager = regionManager;
            EventAggregator = eventAggregator;
            LoadedCommand = ReactiveCommand.Create(OnLoadedComand);

        }

        private void OnLoadedComand()
        {
            RegionManager.RequestNavigate(Names.ShellRegion, Names.LoginView);
            EventAggregator.GetEvent<LoginEvent>().Subscribe(LoginSuccess, ThreadOption.UIThread);
            EventAggregator.GetEvent<InitSuccessEvent>().Subscribe(InitSuccess, ThreadOption.UIThread);
            EventAggregator.GetEvent<LogoutEvent>().Subscribe(Logout, ThreadOption.UIThread);
        }
        /// <summary>
        /// 加载登录页面
        /// </summary>
        private void Logout()
        {
            RegionManager.RequestNavigate(Names.ShellRegion, Names.LoginView);
            Window mainWindow = System.Windows.Application.Current.MainWindow;
            mainWindow.Width = 1500;
            mainWindow.Height = 850;
            Rect workArea = SystemParameters.WorkArea;
            mainWindow.Left = (workArea.Width - mainWindow.Width) / 2 + workArea.Left;
            mainWindow.Top = (workArea.Height - mainWindow.Height) / 2 + workArea.Top;
            mainWindow.WindowState = WindowState.Normal;
        }
        private bool IsHardwareInitialized = false;
        private void InitSuccess()
        {
            IsHardwareInitialized = true;
        }
        private void LoginSuccess(UserEntity user)
        {
            if (IsHardwareInitialized)
            {
                ModuleManager.LoadModule(Names.MainModule);//先加载主模块
                RegionManager.RequestNavigate(Names.ShellRegion, Names.MainView);//导航到首页面
            }
            else
            {       
                ModuleManager.LoadModule(Names.InitializeModule);//先加载模块
                RegionManager.RequestNavigate(Names.ShellRegion, Names.InitView);//导航到硬件加载页面
            }
        }
    }
}
