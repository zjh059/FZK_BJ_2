using FZK.Application.Share.Config;
using FZK.Application.Share.Events;
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
        private readonly IMesService _mes;
        private readonly ScannerConfig _robotScannerConfig;
        private readonly SoftwareConfig _SoftwareConfig;
        private readonly IEventAggregator _eventAggregator;
        
        public RobotCoordinator(IHardwareService hardware,
            IMesService mes,
             ScannerConfig robotScannerConfig,
             SoftwareConfig softwareConfig,
             IEventAggregator eventAggregator
            )
        {
            _hardware = hardware;
            _mes = mes;
            _SoftwareConfig = softwareConfig;
            _robotScannerConfig = robotScannerConfig;
            _eventAggregator = eventAggregator;
        }

        public async Task ProcessCommandAsync()
        {

            var command = await _hardware.GetRobotCommand();
            if (command == RobotCommand.RobAsc.ToString())
            {
                Logs.LogInfo("机械臂到达扫码位");
                _eventAggregator.GetEvent<UILogEvent>().Publish("机械臂到达扫码位");
                string spCode = await _hardware.TriggerScanner(ScannerType.机械臂);
                
                bool reportResult = false;
               
               
                if (_SoftwareConfig.IsSFC)               
                    reportResult = await _mes.ReportStation(spCode);                
                else                
                    reportResult = true;


                if (string.IsNullOrEmpty(spCode))
                    reportResult = false;

                if (_SoftwareConfig.IsDebug)
                    reportResult = true;


                _eventAggregator.GetEvent<UILogEvent>().Publish("回复机械臂:" +reportResult);
                await _hardware.SendRobotResponse(reportResult);
                Logs.LogInfo($"机械臂上报结果: {(reportResult ? "成功" : "失败")}");
            }
        }
    }
}
