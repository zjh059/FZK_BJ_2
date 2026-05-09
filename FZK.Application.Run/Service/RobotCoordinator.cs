using FZK.Application.Share.Run;
using FZK.Core.Enums;
using FZK.Hardware.Scanner.Base;
using FZK.Logger;
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

        public RobotCoordinator(IHardwareService hardware, IMesService mes,
             ScannerConfig robotScannerConfig)
        {
            _hardware = hardware;
            _mes = mes;
           
            _robotScannerConfig = robotScannerConfig;
        }

        public async Task ProcessCommandAsync()
        {
            

            var command = await _hardware.GetRobotCommand();
            if (command == RobotCommand.RobAsc.ToString())
            {
                Logs.LogInfo("机械臂到达扫码位");
                string spCode = await _hardware.TriggerScanner(ScannerType.机械臂);
                bool reportResult = false;
                if (!string.IsNullOrEmpty(spCode))
                    reportResult = await _mes.ReportStation(spCode);
                await _hardware.SendRobotResponse(reportResult);
                Logs.LogInfo($"机械臂上报结果: {(reportResult ? "成功" : "失败")}");
            }
        }
    }
}
