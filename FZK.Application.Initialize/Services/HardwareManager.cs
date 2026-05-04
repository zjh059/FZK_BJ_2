using FZK.Application.Share;
using FZK.Application.Share.Config;
using FZK.Application.Share.Init;
using FZK.Application.Share.Language;
using FZK.Application.Share.Models;
using FZK.Hardware.PLC.Base;
using FZK.Hardware.Robot.Base;
using FZK.Hardware.Scanner.Base;
using FZK.Logger;
using Prism.Events;
using Prism.Ioc;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FZK.Application.Initialize.Services
{
    class HardwareManager : ReactiveObject, IHardwareManager
    {
        [Reactive]
        public bool Initialized { get; set; }
        [Reactive]
        public string Message { get; set; }

        public IScanner LeftUpScanner { get; private set; }
        public IScanner RightUpScanner { get; private set; }
        public IScanner LeftDownScanner { get; private set; }
        public IScanner RightDownScanner { get; private set; }
        public IScanner SPScanner { get; private set; }
        public IRobot EpsonRobot { get; private set; }
        public IPLC OmronPLC { get; private set; }

        private IEventAggregator EventAggregator { get; }
        private ISystemConfigManager SystemConfigManager { get; }

        public HardwareManager(IEventAggregator eventAggregator, IContainerProvider containerProvider, IPLC pLC, ISystemConfigManager systemConfigManager, IRobot robot)
        {
            OmronPLC = pLC;
            EpsonRobot = robot;
            LeftUpScanner = containerProvider.Resolve<IScanner>(ScannerType.左上.ToString());
            RightUpScanner = containerProvider.Resolve<IScanner>(ScannerType.右上.ToString());
            LeftDownScanner = containerProvider.Resolve<IScanner>(ScannerType.左下.ToString());
            RightDownScanner = containerProvider.Resolve<IScanner>(ScannerType.右下.ToString());
            SPScanner = containerProvider.Resolve<IScanner>(ScannerType.机械臂.ToString());
            SystemConfigManager = systemConfigManager;
            EventAggregator = eventAggregator;
        }

        public async Task<InitResult> InitAsync()
        {
            if (Initialized)
                throw new Exception(MultiLang.重复初始化硬件);

            InitResult result = new InitResult
            {
                Message = MultiLang.初始化硬件失败,
                Success = false
            };

            Task<bool> leftUpTask = !LeftUpScanner.Initialized ? InitWithRetry(LeftUpScanner, SystemConfigManager.LeftUpScannerConfig) : null;
            Task<bool> rightUpTask = !RightUpScanner.Initialized ? InitWithRetry(RightUpScanner, SystemConfigManager.RightUpScannerConfig) : null;
            Task<bool> leftDownTask = !LeftDownScanner.Initialized ? InitWithRetry(LeftDownScanner, SystemConfigManager.LeftDownScannerConfig) : null;
            Task<bool> rightDownTask = !RightDownScanner.Initialized ? InitWithRetry(RightDownScanner, SystemConfigManager.RightDownScannerConfig) : null;
            Task<bool> spTask = !SPScanner.Initialized ? Task.Run(() => SPScanner.Init(SystemConfigManager.RobotScannerConfig)) : null;
            Task<bool> plcTask = !OmronPLC.Initialized ? Task.Run(() => OmronPLC.Init(SystemConfigManager.pLCConfig)) : null;
            Task<bool> robotTask = !EpsonRobot.Initialized ? Task.Run(() => EpsonRobot.Init(SystemConfigManager.robotConfig)) : null;

            var taskList = new List<Task<bool>> { leftUpTask, rightUpTask, leftDownTask, rightDownTask, plcTask }
                .Where(t => t != null).ToList();

            bool[] results = taskList.Count == 0 ? new bool[0] : await Task.WhenAll(taskList);
            bool allSuccess = taskList.Count == 0 || results.All(r => r);

            List<string> failList = new List<string>();
            if (leftUpTask != null && !leftUpTask.Result) failList.Add(MultiLang.左上扫码模块);
            if (rightUpTask != null && !rightUpTask.Result) failList.Add(MultiLang.右上扫码模块);
            if (leftDownTask != null && !leftDownTask.Result) failList.Add(MultiLang.左下扫码模块);
            if (rightDownTask != null && !rightDownTask.Result) failList.Add(MultiLang.右下扫码模块);
            if (spTask != null && !spTask.Result) failList.Add(MultiLang.机械臂扫码模块);
            if (plcTask != null && !plcTask.Result) failList.Add(MultiLang.PLC模块);
            if (robotTask != null && !robotTask.Result) failList.Add(MultiLang.机械手模块);

            if (allSuccess)
            {
                Initialized = true;
                result.Message = MultiLang.初始化硬件成功;
                result.Success = true;
                EventAggregator.GetEvent<InitSuccessEvent>().Publish();
            }
            else
            {
                result.Message = MultiLang.以下硬件模块初始化失败 + "\r\n" + string.Join(" ", failList);
            }

            return result;
        }

        public async Task<InitResult> Stop()
        {
            var result = new InitResult();
            var exceptions = new List<Exception>();

            try
            {
                if (OmronPLC != null)
                {
                    try { OmronPLC.Close(); }
                    catch (Exception ex)
                    {
                        exceptions.Add(ex);
                        Logs.LogError($"{MultiLang.关闭欧姆龙PLC失败}：{ex.Message}");
                    }
                }

                CloseScanner(LeftUpScanner, MultiLang.左上扫码模块.Replace("模块", ""), exceptions);
                CloseScanner(RightUpScanner, MultiLang.右上扫码模块.Replace("模块", ""), exceptions);
                CloseScanner(LeftDownScanner, MultiLang.左下扫码模块.Replace("模块", ""), exceptions);
                CloseScanner(RightDownScanner, MultiLang.右下扫码模块.Replace("模块", ""), exceptions);
                CloseScanner(SPScanner, MultiLang.机械臂扫码模块.Replace("模块", ""), exceptions);

                if (EpsonRobot != null)
                {
                    try { EpsonRobot.Close(); }
                    catch (Exception ex)
                    {
                        exceptions.Add(ex);
                        Logs.LogError($"{MultiLang.关闭EPSON机械臂失败}：{ex.Message}");
                    }
                }

                if (exceptions.Any())
                {
                    result.Success = false;
                    result.Message = $"{MultiLang.停止硬件过程中发生异常} {exceptions.Count} 个异常，请查看日志。";
                    Logs.LogError(result.Message);
                }
                else
                {
                    result.Success = true;
                    result.Message = MultiLang.所有硬件已资源释放完成;
                    Logs.LogInfo(result.Message);
                }

                Initialized = false;
            }
            catch (Exception ex)
            {
                Logs.LogError(ex, MultiLang.Stop方法执行异常);
                result.Success = false;
                result.Message = $"{MultiLang.停止硬件时发生未预期异常}：{ex.Message}";
            }

            return result;
        }

        private void CloseScanner(IScanner scanner, string name, List<Exception> exceptions)
        {
            if (scanner == null) return;
            try { scanner.Close(); }
            catch (Exception ex)
            {
                exceptions.Add(ex);
                Logs.LogError($"{MultiLang.关闭扫码枪失败}({name})：{ex.Message}");
            }
        }

        private async Task<bool> InitWithRetry(IScanner scanner, ScannerConfig config)
        {
            for (int i = 0; i < 2; i++)
            {
                if (await Task.Run(() => scanner.Init(config)))
                    return true;
                await Task.Delay(500);
            }
            return false;
        }
    }
}