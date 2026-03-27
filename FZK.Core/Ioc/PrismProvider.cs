using FZK.Core.Language;
using Prism.Events;
using Prism.Ioc;
using Prism.Modularity;
using Prism.Regions;
using Prism.Services.Dialogs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace FZK.Core.Ioc
{
    [ExposedServiceAttribute(Lifetime.Singleton, AutoInitialize = true)]
    public sealed class PrismProvider
    {
        public PrismProvider(
                             ILanguageManager language,
                             IContainerExtension coantainer,
                             IRegionManager regionManager,
                             IDialogService dialogService,
                             IEventAggregator eventAggregator,
                             ModuleManager moduleManager)
        {
            LanguageManager = language;
            Container = coantainer;
            RegionManager = regionManager;
            DialogService = dialogService;
            EventAggregator = eventAggregator;
            ModuleManager = moduleManager;

            Dispatcher = System.Windows.Application.Current.Dispatcher;

        }



        /// <summary>
        /// 语言
        /// </summary>
        public static ILanguageManager LanguageManager { get; private set; }

        /// <summary>
        /// 容器
        /// </summary>
        public static IContainerExtension Container { get; private set; }
        /// <summary>
        /// 区域管理器接口
        /// </summary>
        public static IRegionManager RegionManager { get; private set; }
        /// <summary>
        /// 对话框管理器:Prism框架弹出对话框要借助于这个服务
        /// </summary>
        public static IDialogService DialogService { get; private set; }
        /// <summary>
        /// 事件聚合器:事件通知,触发管理,两个模块两个类型之间的通讯
        /// </summary>
        public static IEventAggregator EventAggregator { get; private set; }
        /// <summary>
        /// 模块管理器
        /// </summary>
        public static IModuleManager ModuleManager { get; private set; }
        public static Dispatcher Dispatcher { get; private set; }
        public static Window MainWindow { get; private set; }
    }
}
