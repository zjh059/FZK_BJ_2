using FZK.Logger;
using FZK.Shell.Views;
using Prism.Ioc;
using Prism.Unity;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace FZK.Shell
{
    /// <summary>
    /// App.xaml 的交互逻辑
    /// </summary>
    public partial class App : PrismApplication
    {
        protected override Window CreateShell()
        {
            return new ShellView();
        }

        protected override void RegisterTypes(IContainerRegistry containerRegistry)
        {
          
        }
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            Logs.LogInfo("启动应用程序");
        }
        protected override void OnExit(ExitEventArgs e)
        {
            base.OnExit(e);

            Logs.LogInfo("关闭应用程序");

        }
    }
}
