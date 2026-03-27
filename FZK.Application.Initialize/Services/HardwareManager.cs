using FZK.Application.Share.Config;
using FZK.Application.Share.Init;
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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;

namespace FZK.Application.Initialize.Services
{
    /// <summary>
    /// 表示所有硬件是否加载
    /// </summary>
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
        private IEventAggregator EventAggregator { get; }
        private ISystemConfigManager SystemConfigManager { get; }

        public IPLC OmronPLC { get; private set; }

        public HardwareManager(
            IEventAggregator eventAggregator,
            IContainerProvider containerProvider,
            IPLC pLC,
            ISystemConfigManager systemConfigManager,
            IRobot robot
            )
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
        // 修复并行版本的核心错误
        public async Task<InitResult> InitAsync()
        {
            if (Initialized) throw new Exception("重复初始化硬件");

            InitResult result = new InitResult();
            result.Message = "初始化硬件失败";
            result.Success = false;

            // 1. 条件创建任务
            Task<bool> leftUpTask = !LeftUpScanner.Initialized ? InitWithRetry(LeftUpScanner, SystemConfigManager.LeftUpScannerConfig) : null;
            Task<bool> rightUpTask = !RightUpScanner.Initialized ? InitWithRetry(RightUpScanner, SystemConfigManager.RightUpScannerConfig) : null;
            Task<bool> leftDownTask = !LeftDownScanner.Initialized ? InitWithRetry(LeftDownScanner, SystemConfigManager.LeftDownScannerConfig) : null;
            Task<bool> rightDownTask = !RightDownScanner.Initialized ? InitWithRetry(RightDownScanner, SystemConfigManager.RightDownScannerConfig) : null;
            Task<bool> spTask = !SPScanner.Initialized ? Task.Run(() => SPScanner.Init(SystemConfigManager.RobotScannerConfig)) : null;
            Task<bool> plcTask = !OmronPLC.Initialized ? Task.Run(() => OmronPLC.Init(SystemConfigManager.pLCConfig)) : null;
            Task<bool> robotTask = !EpsonRobot.Initialized ? Task.Run(() => EpsonRobot.Init(SystemConfigManager.robotConfig)) : null;

            // 2. 构建列表并过滤null
            var taskList = new List<Task<bool>> { leftUpTask, rightUpTask, leftDownTask, rightDownTask, plcTask }
                .Where(t => t != null)
                .ToList();

            // 3. 执行任务（只调用一次WhenAll）
            bool[] results = taskList.Count == 0 ? new bool[0] : await Task.WhenAll(taskList);
            bool allSuccess = taskList.Count == 0 || results.All(r => r);

            // 4. 安全判断失败的硬件（先判断Task是否为null）
            List<string> failList = new List<string>();
            if (leftUpTask != null && !leftUpTask.Result) failList.Add("左上扫码模块");
            if (rightUpTask != null && !rightUpTask.Result) failList.Add("右上扫码模块");
            if (leftDownTask != null && !leftDownTask.Result) failList.Add("左下扫码模块");
            if (rightDownTask != null && !rightDownTask.Result) failList.Add("右下扫码模块");
            if (spTask != null && !spTask.Result) failList.Add("机械臂扫码模块");
            if (plcTask != null && !plcTask.Result) failList.Add("PLC模块");
            if (robotTask != null && !robotTask.Result) failList.Add("机械手模块");

            // 5. 返回结果
            if (allSuccess)
            {
                Initialized = true;
                result.Message = "初始化硬件成功";
                result.Success = true;
                EventAggregator.GetEvent<InitSuccessEvent>().Publish();
            }
            else
            {
                result.Message = "以下硬件模块初始化失败\r\n" + string.Join(" ", failList);
            }

            return result;
        }

        public async Task<InitResult> Stop()
        {
            var result = new InitResult();
            var exceptions = new List<Exception>();

            try
            {
                // 关闭 PLC
                if (OmronPLC != null)
                {
                    try
                    {
                        OmronPLC.Close();
                    }
                    catch (Exception ex)
                    {
                        exceptions.Add(ex);
                        Logs.LogError($"关闭欧姆龙PLC失败：{ex.Message}");
                    }
                }

                // 关闭扫码枪
                CloseScanner(LeftUpScanner, "左上", exceptions);
                CloseScanner(RightUpScanner, "右上", exceptions);
                CloseScanner(LeftDownScanner, "左下", exceptions);
                CloseScanner(RightDownScanner, "右下", exceptions);
                CloseScanner(SPScanner, "机械臂扫码", exceptions);

                // 关闭机械臂
                if (EpsonRobot != null)
                {
                    try
                    {
                        EpsonRobot.Close();
                    }
                    catch (Exception ex)
                    {
                        exceptions.Add(ex);
                        Logs.LogError($"关闭EPSON机械臂失败：{ex.Message}");
                    }
                }

                if (exceptions.Any())
                {
                    result.Success = false;
                    result.Message = $"停止硬件过程中发生 {exceptions.Count} 个异常，请查看详细日志。";
                    Logs.LogError(result.Message);
                }
                else
                {
                    result.Success = true;
                    result.Message = "所有硬件已停止，资源释放完成";
                    Logs.LogInfo(result.Message);
                }

                // 无论成功与否，标记为未初始化
                Initialized = false;
            }
            catch (Exception ex)
            {
                Logs.LogError(ex, "Stop 方法执行异常");
                result.Success = false;
                result.Message = $"停止硬件时发生未预期异常：{ex.Message}";
            }

            return result;
        }

        private void CloseScanner(IScanner scanner, string name, List<Exception> exceptions)
        {
            if (scanner == null) return;
            try
            {
                scanner.Close();
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
                Logs.LogError($"关闭{name}扫码枪失败：{ex.Message}");
            }
        }

        //public async Task<InitResult> InitAsync()
        //{
        //    if (Initialized) throw new Exception("重复初始化硬件");

        //    InitResult result = new InitResult();
        //    result.Message = "初始化硬件失败";
        //    result.Success = false;

        //    todo 初始化硬件
        //     1.条件创建任务
        //    Task<bool> leftUpTask = !LeftUpScanner.Initialized ? Task.Run(() => LeftUpScanner.Init(SystemConfigManager.LeftUpScannerConfig)) : null;
        //    Task<bool> rightUpTask = !RightUpScanner.Initialized ? Task.Run(() => RightUpScanner.Init(SystemConfigManager.RightUpScannerConfig)) : null;
        //    Task<bool> leftDownTask = !LeftDownScanner.Initialized ? Task.Run(() => LeftDownScanner.Init(SystemConfigManager.LeftDownScannerConfig)) : null;
        //    Task<bool> rightDownTask = !RightDownScanner.Initialized ? Task.Run(() => RightDownScanner.Init(SystemConfigManager.RightDownScannerConfig)) : null;
        //    Task<bool> spTask = !SPScanner.Initialized ? Task.Run(() => SPScanner.Init(SystemConfigManager.RobotScannerConfig)) : null;
        //    Task<bool> plcTask = !OmronPLC.Initialized ? Task.Run(() => OmronPLC.Init(SystemConfigManager.pLCConfig)) : null;
        //    Task<bool> robotTask = !EpsonRobot.Initialized ? Task.Run(() => EpsonRobot.Init(SystemConfigManager.robotConfig)) : null;

        //    2.构建列表并过滤null（一行搞定）
        //    var taskList = new List<Task<bool>> { leftUpTask, rightUpTask, leftDownTask, rightDownTask, spTask, plcTask, robotTask }
        //        .Where(t => t != null) // 关键：过滤掉null的Task
        //        .ToList();

        //    3.执行任务（和上面一样处理空列表）
        //    bool[] results = taskList.Count == 0 ? new bool[0] : await Task.WhenAll(taskList);
        //    bool allSuccess = taskList.Count == 0 || results.All(r => r);

        //    var boolArray = await Task.WhenAll(taskList);
        //    allSuccess = boolArray.All(p => p);//判断所有子任务的加载结果是否都为true

        //    if (allSuccess)
        //    {
        //        Initialized = true;
        //        result.Message = "初始化硬件成功";
        //        result.Success = true;
        //        EventAggregator.GetEvent<InitSuccessEvent>().Publish();
        //        return result;
        //    }
        //    else
        //    {
        //        string msg = "以下硬件模块初始化失败\r\n";
        //        if (!leftUpTask.Result) msg += "左上扫码模块 ";
        //        if (!rightUpTask.Result) msg += "右上扫码模块 ";
        //        if (!leftDownTask.Result) msg += "左下扫码模块 ";
        //        if (!rightDownTask.Result) msg += "右下扫码模块 ";
        //        if (!spTask.Result) msg += "机械臂扫码模块 ";
        //        if (!plcTask.Result) msg += "PLC模块 ";
        //        if (!robotTask.Result) msg += "机械手模块 ";
        //        result.Message = msg;
        //        return result;
        //    }
        //}
        private async Task<bool> InitWithRetry(IScanner scanner, ScannerConfig config)
        {
            for (int i = 0; i < 2; i++) // 最多重试1次
            {
                if (await Task.Run(() => scanner.Init(config)))
                    return true;
                await Task.Delay(500); // 重试前延迟
            }
            return false;
        }
        //bool leftUpOk = await InitWithRetry(LeftUpScanner, SystemConfigManager.LeftUpScannerConfig);
        // 串行：执行完一个，再执行下一个
        //bool leftUpOk = await Task.Run(() => LeftUpScanner.Init(左上图配置));
        //bool rightUpOk = await Task.Run(() => RightUpScanner.Init(右上图配置));
        //bool leftDownOk = await Task.Run(() => LeftDownScanner.Init(左下图配置));
        //bool rightDownOk = await Task.Run(() => RightDownScanner.Init(右下图配置));

        //// 所有都成功才算成功
        //bool allSuccess = leftUpOk && rightUpOk && leftDownOk && rightDownOk;
    }

}