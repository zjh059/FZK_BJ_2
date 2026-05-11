using FZK.Application.Share.Config;
using FZK.Application.Share.Init;
using FZK.Application.Share.Models;
using FZK.Application.Share.Run;
using FZK.Core.Enums;
using FZK.Hardware.Scanner.Base;
using FZK.Logger;
using Prism.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FZK.Application.Run.Service
{
    public class RobotCoordinator : IRobotCoordinator
    {
        private readonly IHardwareService _hardware;
       
        private readonly ScannerConfig _robotScannerConfig;
        private readonly SoftwareConfig _SoftwareConfig;
        private readonly ISystemConfigManager _systemConfigManager;

        private readonly IEventAggregator _eventAggregator;

        public RobotCoordinator(IHardwareService hardware,          
             ISystemConfigManager systemConfigManager,             
             IEventAggregator eventAggregator
            )
        {
            _hardware = hardware;           
            _SoftwareConfig = systemConfigManager.SoftwareConfig;
            _robotScannerConfig = systemConfigManager.RobotScannerConfig;
            _eventAggregator = eventAggregator;
        }

        public async Task ProcessCommandAsync()
        {

            var command = await _hardware.GetRobotCommand();
            if (command == RobotCommand.RobAsc.ToString())
            {
                Logs.LogInfo("机械臂到达扫码位");
                _eventAggregator.GetEvent<UILogEvent>().Publish("机械臂到达扫码位");
               // string spCode = await _hardware.TriggerScanner(ScannerType.机械臂);
                var result = await _hardware.TriggerScannerAndValidateAsync(ScannerType.机械臂, _robotScannerConfig.SnLength,
                    _SoftwareConfig.IsDebug, _SoftwareConfig.IsSFC);
                _eventAggregator.GetEvent<UILogEvent>().Publish("回复机械臂:" + result);
                await _hardware.SendRobotResponse(result);
                Logs.LogInfo($"机械臂上报结果: {(result ? "成功" : "失败")}");
            }
        }         
    }
}
