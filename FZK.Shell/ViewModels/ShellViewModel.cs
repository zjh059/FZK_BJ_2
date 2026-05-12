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
            SettingScreen();
            RegionManager.RequestNavigate(Names.ShellRegion, Names.LoginView);

            EventAggregator.GetEvent<LoginEvent>().Subscribe(LoginSuccess, ThreadOption.UIThread);
            EventAggregator.GetEvent<InitSuccessEvent>().Subscribe(InitSuccess, ThreadOption.UIThread);
            EventAggregator.GetEvent<LogoutEvent>().Subscribe(Logout, ThreadOption.UIThread, true);
        }
        /// <summary>
        /// 加载登录页面
        /// </summary>
        private void Logout()
        {
            RegionManager.RequestNavigate(Names.ShellRegion, Names.LoginView);
            SettingScreen();
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
        private void SettingScreen()
        {
            Window mainWindow = System.Windows.Application.Current.MainWindow;
            if (mainWindow == null) return; // 空值保护，防止启动时崩溃


            Rect workArea = SystemParameters.WorkArea;


            //大屏幕适用
            //double desiredWidth = Math.Min(1800, workArea.Width - 40); // 左右各留20px边距
            //double desiredHeight = Math.Min(1000, workArea.Height - 40); // 上下各留20px边距

            //小屏幕适用
            double desiredWidth = Math.Min(1200, workArea.Width - 40);
            double desiredHeight = Math.Min(900, workArea.Height - 40);

            mainWindow.Width = desiredWidth;
            mainWindow.Height = desiredHeight;


            mainWindow.Left = Math.Max(workArea.Left, (workArea.Width - desiredWidth) / 2 + workArea.Left);
            mainWindow.Top = Math.Max(workArea.Top, (workArea.Height - desiredHeight) / 2 + workArea.Top);
            mainWindow.WindowState = WindowState.Normal;

        }
    }
}
