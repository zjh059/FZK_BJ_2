using FZK.Application.Config.Services;
using FZK.Application.Share.Config;
using FZK.Application.Share.Init;
using FZK.Application.UI.Controls;
using FZK.Core.Models;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace FZK.Application.Config.ViewModels
{
    internal class CameraCalibrationViewModel : ReactiveObject
    {
        /// <summary>
        /// 左相机标定参数，一个像素 与毫米的当量
        /// </summary>
        [Reactive]
        public double LeftPixelToMillimeter { get; set; }
        /// <summary>
        /// 右相机标定参数，一个像素 与毫米的当量
        /// </summary>
        [Reactive]
        public double RightPixelToMillimeter { get; set; }
        [Reactive]
        public Rect LeftSelectRect { get; set; }
        [Reactive]
        public Rect RightSelectRect { get; set; }


        public ICommand SaveCommand { get; }
        public ICommand CameraCalibrationCommand { get; }
        public ICommand LeftRectChangedCommand { get; }
        public ICommand RightRectChangedCommand { get; }
        public ISystemConfigManager SystemConfigManager { get; }
        public CalibrationManager CalibrationManager { get; }
        public CameraCalibrationViewModel(
            CalibrationManager calibrationManager,
            IHardwareManager hardwareManager,
            ISystemConfigManager systemConfigManager)
        {
            CalibrationManager = calibrationManager;
            SystemConfigManager = systemConfigManager;
            SaveCommand = ReactiveCommand.Create(OnSaveCommand);
        }

        private void OnSaveCommand()
        {
            SystemConfigManager.Save();
            MessageBox.Show("保存成功");
        }
    }
}
