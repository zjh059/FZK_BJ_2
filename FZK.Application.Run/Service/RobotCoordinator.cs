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
using System.Diagnostics;
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
        private readonly IMesService _mes;
        private readonly IEventAggregator _eventAggregator;

        public RobotCoordinator(IHardwareService hardware,
             ISystemConfigManager systemConfigManager,
             IEventAggregator eventAggregator,
              IMesService mes
            )
        {
            _hardware = hardware;
            _mes = mes;
            _SoftwareConfig = systemConfigManager.SoftwareConfig;
            _robotScannerConfig = systemConfigManager.RobotScannerConfig;
            _eventAggregator = eventAggregator;
        }

        //public async Task ProcessCommandAsync()
        //{

        //    var command = await _hardware.GetRobotCommand();
        //    if (command == RobotCommand.RobAsc.ToString())
        //    {
        //        Logs.LogInfo("机械臂到达扫码位");
        //        _eventAggregator.GetEvent<UILogEvent>().Publish("机械臂到达扫码位");
        //        // string spCode = await _hardware.TriggerScanner(ScannerType.机械臂);
        //        var result = await _hardware.TriggerScannerAndValidateAsync(
        //                                                      ScannerType.机械臂,
        //                                                       _robotScannerConfig.SnLength,
        //                                                            _SoftwareConfig.IsDebug);


        //        // 5. MES 校验
        //        bool reportResult;
        //        string spCode = result.PrimaryCode; // 条码
        //        if (_SoftwareConfig.IsDebug)
        //        {
        //            reportResult = true;
        //        }
        //        else
        //        {
        //            if (result.IsValid)
        //            {
        //                //if (_SoftwareConfig.IsSFC && !_SoftwareConfig.IsDebug)
        //                //{
        //                //    // 在这里调用 MES
        //                //    bool mesOk = await _mes.ReportStation(spCode);
        //                //    reportResult = mesOk;
        //                //}
        //                //else
        //                //{
        //                //    reportResult = true;
        //                //}

        //                reportResult = true;
        //            }
        //            else
        //            {
        //                reportResult = false;
        //            }
        //        }


        //        _eventAggregator.GetEvent<UILogEvent>().Publish("回复机械臂:" + reportResult);
        //        await _hardware.SendRobotResponse(reportResult);
        //    }
        //}         

        //(7)
        public async Task ProcessCommandAsync()
        {
            var command = await _hardware.GetRobotCommand();

            if (command == RobotCommand.RobAsc.ToString())
            {
                var robotWatch = Stopwatch.StartNew();
                Logs.LogInfo($"[RobotFlow] 收到机械臂到位命令 RobAsc，时间={DateTime.Now:HH:mm:ss.fff}");
                _eventAggregator.GetEvent<UILogEvent>().Publish("机械臂到达扫码位");

                // 这里触发机械臂上的扫码枪。
                var scanWatch = Stopwatch.StartNew();
                await Task.Delay(200);
                var result = await _hardware.TriggerScannerAndValidateAsync(
                    ScannerType.机械臂,
                    _robotScannerConfig.SnLength,
                    _SoftwareConfig.IsDebug);

                scanWatch.Stop();

                Logs.LogInfo($"[RobotFlow] 机械臂扫码结束，耗时={scanWatch.ElapsedMilliseconds}ms，结果={(result.IsValid ? "OK" : "NG")}，条码={result.PrimaryCode}");

            
                bool reportResult = result.IsValid;
                if (result.IsValid)
                {                   
                    reportResult = true;
                }
                else
                {
                    reportResult = false;
                }

                //}

                _eventAggregator.GetEvent<UILogEvent>().Publish("回复机械臂:" + reportResult);

                // 这里统计回复机械臂 OK/NG 到底用了多久。
                var sendWatch = Stopwatch.StartNew();
                await _hardware.SendRobotResponse(reportResult);
                sendWatch.Stop();
                robotWatch.Stop();
                Logs.LogInfo($"[RobotFlow] 回复机械臂结束，发送耗时={sendWatch.ElapsedMilliseconds}ms，本次机械臂流程总耗时={robotWatch.ElapsedMilliseconds}ms");
            }
        }

    }
}
