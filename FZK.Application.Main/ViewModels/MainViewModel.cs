
using FZK.Application.Share.Login;
using FZK.Application.Share.Models;
using FZK.Core.Models;
using Prism.Events;
using Prism.Regions;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace FZK.Application.Main.ViewModels
{
    internal class MainViewModel : ReactiveObject
    {
        public ICommand LoadedCommand { get; }
        public ICommand LogoutCommand { get; }
        public ICommand NavigationgToVewCommand { get; }


        private IRegionManager RegionManager { get; }
        private IEventAggregator EventAggregator { get; }
        public ISession Session { get; }
        public MainViewModel(
          ISession session,
          IRegionManager regionManager,
          IEventAggregator eventAggregator)
        {
            Session = session;
            RegionManager = regionManager;
            EventAggregator = eventAggregator;
            LoadedCommand = ReactiveCommand.Create<ContentControl>(OnLoadedCommand);
            LogoutCommand = ReactiveCommand.Create(OnLogoutCommand);
            NavigationgToVewCommand = ReactiveCommand.Create<string>(OnNavigationgToVewCommand);
        }
        private void OnNavigationgToVewCommand(string view)
        {
            RegionManager.RequestNavigate(Names.MainRegion, view);
        }

        /// <summary>
        /// 通知切换用户
        /// </summary>
        private void OnLogoutCommand()
        {          
            EventAggregator.GetEvent<LogoutEvent>().Publish();
        }

        private void OnLoadedCommand(ContentControl contentControl)
        {
            if (contentControl.Content == null)
            {
                RegionManager.RequestNavigate(Names.MainRegion, Names.RunView);
            }

            var mainWindow = Session.MainWindow;
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


            mainWindow.WindowState = WindowState.Maximized;


            //var mainWindow = Session.MainWindow;
            //mainWindow.Width = 1800;
            //mainWindow.Height = 1000;
            //Rect workArea = SystemParameters.WorkArea;
            //mainWindow.Left = (workArea.Width - mainWindow.Width) / 2 + workArea.Left;
            //mainWindow.Top = (workArea.Height - mainWindow.Height) / 2 + workArea.Top;
            //mainWindow.WindowState = WindowState.Maximized;




        }
    }
}
