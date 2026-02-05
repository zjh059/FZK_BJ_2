
using FZK.Application.Share.Config;
using FZK.Application.Share.Init;
using FZK.Core;
using FZK.Core.Config;

using FZK.Logger;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace FZK.Application.Config.Services
{
    class CalibrationManager : ReactiveObject
    {
        private CancellationTokenSource cts = new CancellationTokenSource();
        /// <summary>
        /// 是否显示保存按钮
        /// </summary>
        ///         [Reactive]
        public Visibility Visibility { get; private set; } = Visibility.Collapsed;

        private ISystemConfigManager SystemConfigManager { get; }
        private IHardwareManager HardwareManager { get; }

        private readonly object _lock = new object();
        public CalibrationManager(
    ISystemConfigManager systemConfigManager)
        {
            SystemConfigManager = systemConfigManager;

        }
    }
}
