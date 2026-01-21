
using FZK.Application.Share.Init;
using FZK.Application.Share.Login;
using FZK.Core.Models;
using Prism.Ioc;
using Prism.Modularity;
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

namespace FZK.Application.Initialize.ViewModels
{
    internal class InitializeViewModel : ReactiveObject
    {
        [Reactive]
        public string Message { get; set; }

        public ICommand LoadedCommand { get; }
        public ICommand InitializeCommand { get; }
        public ICommand OpenMainViewCommand { get; }

        private IHardwareManager HardwareManager { get; }
        private ISession Session { get; }
        private IModuleManager ModuleManager { get; }
        private IRegionManager RegionManager { get; }
        public InitializeViewModel(
            IRegionManager regionManager,
            IModuleManager moduleManager,
            IHardwareManager hardwareManager,
            ISession session)
        {
            RegionManager = regionManager;
            ModuleManager = moduleManager;
            HardwareManager = hardwareManager;
            Session = session;

            LoadedCommand = ReactiveCommand.Create(OnLoadedCommand);
            InitializeCommand = ReactiveCommand.CreateFromTask(OnLoadedCommand);
            OpenMainViewCommand = ReactiveCommand.Create(OnOpenMainViewCommand);
        }
        /// <summary>
        /// 跳过硬件加载，进入首页
        /// </summary>
        /// <exception cref="NotImplementedException"></exception>
        private void OnOpenMainViewCommand()
        {
            ModuleManager.LoadModule(Names.MainModule);
            RegionManager.RequestNavigate(Names.ShellRegion, Names.MainView);
        }

        /// <summary>
        /// 加载硬件
        /// </summary>
        private async Task OnLoadedCommand()
        {
            //最大化窗体
            Rect workArea = SystemParameters.WorkArea;
            Session.MainWindow.Left = (workArea.Width - Session.MainWindow.ActualWidth) / 2 + workArea.Left;
            Session.MainWindow.Top = (workArea.Height - Session.MainWindow.ActualHeight) / 2 + workArea.Top;
            Session.MainWindow.WindowState = WindowState.Maximized;

            if (!HardwareManager.Initialized)
            {
                Message = "正在初始化所有硬件...";
                var result = await HardwareManager.InitAsync();//真正加载硬件
                if (result.Success)
                {
                    ModuleManager.LoadModule(Names.MainModule);
                    RegionManager.RequestNavigate(Names.ShellRegion, Names.MainView);
                }
                else
                {
                    Message = result.Message;
                }
            }
        }
        }
}
