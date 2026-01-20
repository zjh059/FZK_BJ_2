using FZK.Core;
using FZK.Logger;
using FZK.Shell.Views;
using Prism.Ioc;
using Prism.Modularity;
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
        public App()
        {
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                Logs.LogError((Exception)e.ExceptionObject);
            };

            Current.DispatcherUnhandledException += (s, e) =>
            {
                Logs.LogError(e.Exception);
            };

            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                Logs.LogError(e.Exception);
            };
        }
        protected override Window CreateShell()
        {
            return new ShellView();
        }
        protected override void ConfigureModuleCatalog(IModuleCatalog moduleCatalog)
        {
            moduleCatalog.AddModule<CoreModule>();
        }
        protected override IModuleCatalog CreateModuleCatalog()
        {
            return new DirectoryModuleCatalog()
            {
                ModulePath = @"./"
            };
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
