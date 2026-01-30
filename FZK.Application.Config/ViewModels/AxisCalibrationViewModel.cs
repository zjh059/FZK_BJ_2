using FZK.Application.Config.Services;
using FZK.Application.Share.Config;
using FZK.Application.Share.Init;
using FZK.Application.UI.Controls;
using FZK.Core;
using FZK.Core.Models;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace FZK.Application.Config.ViewModels
{
    internal class AxisCalibrationViewModel : ReactiveObject
    {


     
        [Reactive]
        public Rect RightSelectRect { get; set; }

        public ICommand LeftRectChangedCommand { get; }
        public ICommand RightRectChangedCommand { get; }
        public ICommand TakePictureCommand { get; }
        public ICommand FindPointCommand { get; }
        public ICommand FindMatrixCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand LeftPointCommand { get; }
        public ICommand RightPointCommand { get; }


        public ISystemConfigManager SystemConfigManager { get; }
        public CalibrationManager CalibrationManager { get; }
        public AxisCalibrationViewModel(
            CalibrationManager calibrationManager,
            SystemConfigManager systemConfigManager,
            IHardwareManager hardwareManager)
        {
            CalibrationManager = calibrationManager;
            SystemConfigManager = systemConfigManager;
            SaveCommand = ReactiveCommand.Create(() => systemConfigManager.Save());

        }

    }
}
