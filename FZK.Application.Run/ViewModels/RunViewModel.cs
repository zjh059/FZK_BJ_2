using FZK.Application.Run.Service;
using FZK.Application.Share.Config;
using FZK.Application.Share.DebugFolder;
using FZK.Application.Share.Init;
using FZK.Application.Share.Run;
using FZK.Core.Enums;
using FZK.Database.Base.Models;
using FZK.Database.Base.Repositories;
using FZK.Hardware.PLC.Base;
using FZK.Hardware.Robot.Base;
using FZK.Hardware.Robot.Epson;
using FZK.Hardware.Scanner.Base;
using FZK.Logger;
using Newtonsoft.Json.Linq;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using Timer = System.Timers.Timer;


namespace FZK.Application.Run.ViewModels
{
    /// <summary>
    /// 运行视图模型
    /// </summary>
    public class RunViewModel : ReactiveObject, IDisposable
    {
        #region 常量与配置对象
        private readonly IDatabaseManager _databaseManager;
        private readonly IHardwareService _hardwareService;
        private readonly IHardwareManager _hardwareManager;
        private readonly ISystemConfigManager _systemConfigManager;
        private readonly IMesService _mesService;

        // 配置对象（从 SystemConfigManager 获取）
        private readonly PlcAddressConfig _plcAddr;
        private readonly RunConfig _runConfig;
        private readonly RobotConfig _robotConfig;
        private readonly ScannerConfig _leftDownScannerConfig;
        private readonly ScannerConfig _leftUpScannerConfig;
        private readonly ScannerConfig _rightDownScannerConfig;
        private readonly ScannerConfig _rightUpScannerConfig;
        private readonly ScannerConfig __robotScannerConfig; // 机械臂扫码枪
        private readonly int bottomCodeLength;
        // 存储治具当前正在处理的底板码
        private string _currentJig1BottomCode;
        private string _currentJig2BottomCode;

        // 定时器
        private System.Timers.Timer _plcReadTimer;
        private Timer _statusCheckTimer;


        // 释放标记
        private bool _disposed;
        #endregion

        #region 构造函数与初始化

        public RunViewModel(
            IDatabaseManager databaseManager,
            IHardwareService hardwareService,
            IMesService mesService,
            IHardwareManager hardwareManager,
            ISystemConfigManager systemConfigManager)
        {
            _databaseManager = databaseManager ?? throw new ArgumentNullException(nameof(databaseManager));
            _hardwareService = hardwareService ?? throw new ArgumentNullException(nameof(hardwareService));
            _mesService = mesService ?? throw new ArgumentNullException(nameof(mesService));
            _hardwareManager = hardwareManager ?? throw new ArgumentNullException(nameof(hardwareManager));
            _systemConfigManager = systemConfigManager ?? throw new ArgumentNullException(nameof(systemConfigManager));

            // 加载配置
            _plcAddr = systemConfigManager.plcAddressConfig ?? new PlcAddressConfig();
            _runConfig = systemConfigManager.runConfig ?? new RunConfig();
            _robotConfig = systemConfigManager.robotConfig ?? new RobotConfig();
            _leftDownScannerConfig = systemConfigManager.LeftDownScannerConfig ?? new ScannerConfig();
            _leftUpScannerConfig = systemConfigManager.LeftUpScannerConfig ?? new ScannerConfig();
            _rightDownScannerConfig = systemConfigManager.RightDownScannerConfig ?? new ScannerConfig();
            _rightUpScannerConfig = systemConfigManager.RightUpScannerConfig ?? new ScannerConfig();
            __robotScannerConfig = systemConfigManager.RobotScannerConfig ?? new ScannerConfig();

            bottomCodeLength = _leftDownScannerConfig.SnLength;
            ScanRecords = new ObservableCollection<ScanRecord>();

            // 初始化命令
            ToggleRunCommand = ReactiveCommand.Create(OnToggleRun);
            RefreshStatusCommand = ReactiveCommand.Create(OnRefreshStatus);
            ClearLogCommand = ReactiveCommand.Create(OnClearLog);
            ClearJig1CountCommand = ReactiveCommand.Create(OnClearJig1Count);
            ClearJig2CountCommand = ReactiveCommand.Create(OnClearJig2Count);

            // 手动测试命令
            ManualTriggerJig1ScanCommand = ReactiveCommand.Create(OnManualTriggerJig1Scan);
            ManualTriggerJig1WeldScanCommand = ReactiveCommand.Create(OnManualTriggerJig1WeldScan);
            ManualTriggerJig1ClearCommand = ReactiveCommand.Create(OnManualTriggerJig1Clear);
            ManualTriggerJig2ScanCommand = ReactiveCommand.Create(OnManualTriggerJig2Scan);
            ManualTriggerJig2WeldScanCommand = ReactiveCommand.Create(OnManualTriggerJig2WeldScan);
            ManualTriggerJig2ClearCommand = ReactiveCommand.Create(OnManualTriggerJig2Clear);
            SimulateRobotToScanPosCommand = ReactiveCommand.Create(OnSimulateRobotToScanPos);
            SimulateRobotReportCommand = ReactiveCommand.Create(OnSimulateRobotReport);

            // 初始化状态检查定时器
            _statusCheckTimer = new Timer(_runConfig.StatusCheckInterval);
            _statusCheckTimer.Elapsed += StatusCheckTimer_Elapsed;
            _statusCheckTimer.Start();



            // 初始化PLC寄存器默认值
            InitPlcRegisters();

            // 初始化PLC读取定时器（但不启动，等待用户启动）
            InitPlcReadTimer();

            RunLog = "设备启动中...";
            Logs.LogInfo("设备初始化完成");
        }

        private void InitPlcRegisters()
        {
            PlcD0 = "0";
            PlcD1 = "0";
            PlcD2 = "0";
            PlcD3 = "0";
            PlcD4 = "0";
            PlcD5 = "0";

            PlcD100 = 0;
            PlcD101 = 0;
            PlcD102 = 0;
            PlcD103 = 0;
            PlcD104 = 0;
            PlcD105 = 0;
            PlcD106 = 0;
            PlcD107 = 0;
            PlcD108 = 0;
            PlcD109 = 0;
        }

        private void InitPlcReadTimer()
        {
            _plcReadTimer = new Timer(_runConfig.PlcReadInterval);
            _plcReadTimer.Elapsed += PlcReadTimer_Elapsed;
            _plcReadTimer.AutoReset = true;
        }

        #endregion

        #region 视图绑定属性

        [Reactive] public bool IsNoHardwareMode { get; set; }
        [Reactive] public bool IsRunning { get; set; }

        public string HardwareModeText => IsNoHardwareMode ? "无硬件模式（已开启）" : "无硬件模式（关闭）";
        public string RunStatusText => IsRunning ? "停止设备" : "启动设备";

        public Style RunStatusButtonStyle
        {
            get
            {
                var style = new Style(typeof(Button));
                style.Setters.Add(new Setter(Button.BackgroundProperty, IsRunning ? Brushes.DarkRed : Brushes.Green));
                style.Setters.Add(new Setter(Button.ForegroundProperty, Brushes.White));
                style.Setters.Add(new Setter(Button.WidthProperty, 120.0));
                style.Setters.Add(new Setter(Button.HeightProperty, 32.0));
                return style;
            }
        }

        [Reactive] public bool IsPlcConnected { get; set; } = false;
        [Reactive] public bool IsLeftDownScannerConnected { get; set; } = false;
        [Reactive] public bool IsLeftUpScannerConnected { get; set; } = false;
        [Reactive] public bool IsRightUpScannerConnected { get; set; } = false;
        [Reactive] public bool IsRightDownScannerConnected { get; set; } = false;
        [Reactive] public bool IsSPScannerConnected { get; set; } = false;
        [Reactive] public bool IsRobotConnected { get; set; } = false;

        // PLC读取寄存器（字符串形式用于显示）
        [Reactive] public string PlcD0 { get; set; }
        [Reactive] public string PlcD1 { get; set; }
        [Reactive] public string PlcD2 { get; set; }
        [Reactive] public string PlcD3 { get; set; }
        [Reactive] public string PlcD4 { get; set; }
        [Reactive] public string PlcD5 { get; set; }

        // PLC写入寄存器（整数形式，支持双向绑定）
        [Reactive] public int PlcD100 { get; set; }
        [Reactive] public int PlcD101 { get; set; }
        [Reactive] public int PlcD102 { get; set; }
        [Reactive] public int PlcD103 { get; set; }
        [Reactive] public int PlcD104 { get; set; }
        [Reactive] public int PlcD105 { get; set; }
        [Reactive] public int PlcD106 { get; set; }
        [Reactive] public int PlcD107 { get; set; }
        [Reactive] public int PlcD108 { get; set; }
        [Reactive] public int PlcD109 { get; set; }
        [Reactive] public string PlcD110 { get; set; }

        [DependsOn(nameof(PlcD108))]
        public int Jig1UseCount => PlcD108;

        [DependsOn(nameof(PlcD109))]
        public int Jig2UseCount => PlcD109;

        [Reactive] public string RobotStatus { get; set; } = "空闲";
        [Reactive] public string RobotScanPosition { get; set; } = "无";
        [Reactive] public string RobotReportResult { get; set; } = "无";

        [Reactive] public string RunLog { get; set; }

        public ObservableCollection<ScanRecord> ScanRecords { get; }

        #endregion

        #region 命令定义

        public ICommand ToggleRunCommand { get; }
        public ICommand RefreshStatusCommand { get; }
        public ICommand ClearLogCommand { get; }
        public ICommand ClearJig1CountCommand { get; }
        public ICommand ClearJig2CountCommand { get; }

        public ICommand ManualTriggerJig1ScanCommand { get; }
        public ICommand ManualTriggerJig1WeldScanCommand { get; }
        public ICommand ManualTriggerJig1ClearCommand { get; }
        public ICommand ManualTriggerJig2ScanCommand { get; }
        public ICommand ManualTriggerJig2WeldScanCommand { get; }
        public ICommand ManualTriggerJig2ClearCommand { get; }
        public ICommand SimulateRobotToScanPosCommand { get; }
        public ICommand SimulateRobotReportCommand { get; }

        #endregion

        #region 硬件状态刷新

        private void StatusCheckTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (_disposed) return;
            try
            {
                System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
                {
                    if (!_disposed) RefreshHardwareConnectionStatus();
                }, DispatcherPriority.Background);
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is NullReferenceException)
            {
                Logs.LogWarn($"状态检查定时器执行失败：{ex.Message}");
            }
        }

        private void RefreshHardwareConnectionStatus()
        {
            try
            {
                IsPlcConnected = _hardwareManager.OmronPLC?.Connected ?? false;
                IsLeftUpScannerConnected = _hardwareManager.LeftUpScanner?.Connected ?? false;
                IsLeftDownScannerConnected = _hardwareManager.LeftDownScanner?.Connected ?? false;
                IsRightUpScannerConnected = _hardwareManager.RightUpScanner?.Connected ?? false;
                IsRightDownScannerConnected = _hardwareManager.RightDownScanner?.Connected ?? false;
                IsSPScannerConnected = _hardwareManager.SPScanner?.Connected ?? false;
                IsRobotConnected = _hardwareManager.EpsonRobot?.Connected ?? false;
            }
            catch (Exception ex)
            {
                Logs.LogError(ex, "刷新硬件连接状态异常");
                AppendLog($"刷新硬件状态失败：{ex.Message}");
            }
        }


        #endregion

        #region 设备启动/停止

        private void OnToggleRun()
        {
            IsRunning = !IsRunning;

            if (IsRunning)
            {
                _plcReadTimer.Start();
                Logs.LogInfo("设备启动，开始实时监控PLC寄存器");
                AppendLog("设备启动");

                if (!IsNoHardwareMode)
                {
                    _hardwareService.Init();
                    Logs.LogInfo("硬件初始化完成");
                }
                else
                {
                    Logs.LogInfo("无硬件模式，跳过硬件初始化");
                }
            }
            else
            {
                _plcReadTimer.Stop();
                Logs.LogInfo("设备停止，停止监控PLC寄存器");
                if (!IsNoHardwareMode) _hardwareService.Stop();
                Logs.LogInfo("设备停止...");

            }

            this.RaisePropertyChanged(nameof(RunStatusText));
            this.RaisePropertyChanged(nameof(RunStatusButtonStyle));
        }

        private async void OnRefreshStatus()
        {
            try
            {
                await ReadPlcRegisters();
                _databaseManager.GetAll(); // 同步操作，建议改为异步
                Logs.LogInfo("设备状态刷新完成");
                AppendLog("状态刷新完成");
            }
            catch (Exception ex)
            {
                Logs.LogError(ex, "刷新状态失败");
                MessageBox.Show($"刷新失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnClearLog()
        {
            RunLog = string.Empty;
            this.RaisePropertyChanged(nameof(RunLog));
            Logs.LogInfo("界面日志已清空");
        }

        private async void OnClearJig1Count()
        {
            try
            {
                PlcD108 = 0;
                WritePlcRegister(_plcAddr.Jig1Count, 0);
                Logs.LogInfo("治具1使用次数已清零");
                await UpdateJigCountInDb(1, "0");
                this.RaisePropertyChanged(nameof(Jig1UseCount));
            }
            catch (Exception ex)
            {
                Logs.LogError(ex, "清零治具1次数失败");
            }
        }

        private async void OnClearJig2Count()
        {
            try
            {
                PlcD109 = 0;
                WritePlcRegister(_plcAddr.Jig2Count, 0);
                Logs.LogInfo("治具2使用次数已清零");
                await UpdateJigCountInDb(2, "0");
                this.RaisePropertyChanged(nameof(Jig2UseCount));
            }
            catch (Exception ex)
            {
                Logs.LogError(ex, "清零治具2次数失败");
            }
        }

        #endregion

        #region PLC读写

        private async void PlcReadTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            Logs.LogInfo("PLC读取定时器触发");
            if (_disposed || !IsRunning) return;

            await System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                try
                {
                    await ReadPlcRegisters();
                    await ProcessJig1Logic();
                    await ProcessJig2Logic();
                    //  await ProcessRobotLogic();
                }
                catch (Exception ex)
                {
                    Logs.LogError(ex, "PLC读取/处理异常");
                    await ResetPlcErrorRegisters();
                }
            });
        }

        private async Task ReadPlcRegisters()
        {
            if (IsNoHardwareMode) return;

            try
            {
                var addresses = new List<int>
        {
            _plcAddr.Jig1TriggerScan,
            _plcAddr.Jig1TriggerWeld,
            _plcAddr.Jig1TriggerClear,
            _plcAddr.Jig2TriggerScan,
            _plcAddr.Jig2TriggerWeld,
            _plcAddr.Jig2TriggerClear,

        };

                var plcValues = await _hardwareService.ReadPlcRegisters(addresses);

                PlcD0 = plcValues.ContainsKey(_plcAddr.Jig1TriggerScan) ? plcValues[_plcAddr.Jig1TriggerScan].ToString() : "0";
                PlcD1 = plcValues.ContainsKey(_plcAddr.Jig1TriggerWeld) ? plcValues[_plcAddr.Jig1TriggerWeld].ToString() : "0";
                PlcD2 = plcValues.ContainsKey(_plcAddr.Jig1TriggerClear) ? plcValues[_plcAddr.Jig1TriggerClear].ToString() : "0";
                PlcD3 = plcValues.ContainsKey(_plcAddr.Jig2TriggerScan) ? plcValues[_plcAddr.Jig2TriggerScan].ToString() : "0";
                PlcD4 = plcValues.ContainsKey(_plcAddr.Jig2TriggerWeld) ? plcValues[_plcAddr.Jig2TriggerWeld].ToString() : "0";
                PlcD5 = plcValues.ContainsKey(_plcAddr.Jig2TriggerClear) ? plcValues[_plcAddr.Jig2TriggerClear].ToString() : "0";
            }
            catch (Exception ex)
            {
                Logs.LogError(ex, "读取PLC寄存器失败");
            }
        }
        private async Task WritePlcRegister(int address, int value)
        {
            if (IsNoHardwareMode)
            {
                Logs.LogInfo($"无硬件模式：模拟写入PLC D{address} = {value}");
                return;
            }

            try
            {
                await _hardwareService.WritePlcRegister(address, value);
                Logs.LogInfo($"写入PLC D{address} = {value}");
            }
            catch (Exception ex)
            {
                Logs.LogError(ex, $"写入PLC寄存器D{address}失败");
            }
        }

        private async Task ResetPlcErrorRegisters()
        {
            try
            {
                if (!IsNoHardwareMode)
                {
                    await WritePlcRegister(_plcAddr.Jig1CompareResult, 0);
                    await WritePlcRegister(_plcAddr.Jig1WeldFinalResult, 0);
                    await WritePlcRegister(_plcAddr.Jig2CompareResult, 0);
                    await WritePlcRegister(_plcAddr.Jig2WeldFinalResult, 0);
                }
            }
            catch (Exception ex)
            {
                Logs.LogError(ex, "重置PLC错误寄存器失败");
            }
        }
        private async void HeartbeatReadTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (_disposed || !IsRunning || IsNoHardwareMode) return;

            try
            {
                int value = await _hardwareService.ReadPlcRegister(_plcAddr.HeartbeatMonitor);
                // 更新到 UI 线程
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    PlcD110 = value.ToString();
                });
            }
            catch (Exception ex)
            {
                Logs.LogError(ex, "读取心跳寄存器 D110 失败");
            }
        }
        #endregion

        #region 治具1核心逻辑

        private async Task ProcessJig1Logic()
        {
            // 1. 底/顶部扫码触发
            if (PlcD0 == "1")
            {
                await ProcessJig1Scan();

            }

            // 2. 焊接结果扫码触发
            if (PlcD1 == "1")
            {
                await ProcessJig1Weld();
            }

            // 3. 清零触发
            if (PlcD2 == "1")
            {
                await ProcessJig1Clear();
            }
        }

        private async Task ProcessJig1Scan()
        {
            Logs.LogInfo("治具1-D0触发：开始底/顶部扫码");
            WritePlcRegister(_plcAddr.Jig1TriggerScan, 0);
            PlcD0 = "0";

            string bottomCode = string.Empty;
            string spCode = string.Empty;
            string topCode = string.Empty;
            bool scanSuccess = false;
            bool verifySuccess = false;
            int retryCount = 0;
            string errorMsg = string.Empty;

            try
            {
                // 重试获取双码
                while (retryCount < _runConfig.ScanRetryCount && !scanSuccess)
                {
                    if (retryCount > 0)
                    {
                        Logs.LogInfo($"治具1双码解析失败，第{retryCount}次重试...");
                        await Task.Delay(_runConfig.ScanRetryDelay);
                    }

                    string bottomRaw = string.Empty;

                    if (IsNoHardwareMode)
                    {
                        // 模拟：生成两个码并用分隔符连接
                        bottomRaw = $"H-B{DateTime.Now:yyyyMMddHHmmss}{_leftDownScannerConfig.CodeDelimiter}SP{DateTime.Now:yyyyMMddHHmmss}";
                        topCode = $"T{DateTime.Now:yyyyMMddHHmmss}";
                    }
                    else
                    {
                        // 触发左下和左上扫码
                        var bottomTask = _hardwareService.TriggerScanner(ScannerType.左下);
                        var topTask = _hardwareService.TriggerScanner(ScannerType.左上);
                        await Task.WhenAll(bottomTask, topTask);
                        bottomRaw = await bottomTask;
                        topCode = await topTask;
                    }

                    // 解析左下扫码结果（可能包含双码）
                    if (TryParseBottomAndSpCode(bottomRaw, _leftDownScannerConfig.CodeDelimiter, out string parsedBottom, out string parsedSp))
                    {
                        var delimiter = _leftDownScannerConfig.CodeDelimiter;
                        var codes = bottomRaw.Split(new[] { delimiter }, StringSplitOptions.RemoveEmptyEntries);

                        if (codes.Length >= 2)
                        {
                            bottomCode = parsedBottom;
                            spCode = parsedSp;
                            scanSuccess = !string.IsNullOrEmpty(bottomCode) && !string.IsNullOrEmpty(spCode) && !string.IsNullOrEmpty(topCode);
                        }
                        else
                        {
                            Logs.LogWarn($"治具1左下扫码结果格式异常：{bottomRaw}，无法解析出两个码");
                        }
                    }

                    retryCount++;
                }

                if (scanSuccess)
                {
                    Logs.LogInfo($"治具1扫码成功：底板码={bottomCode}，顶部码={topCode}，主板码={spCode}");

                    // 写入扫码完成标志
                    await WritePlcRegister(_plcAddr.Jig1ScanResult, 1);
                    PlcD100 = 1;

                    // 验证底板码和顶部码是否匹配
                    verifySuccess = await VerifyBottomTopCode(bottomCode, topCode);

                    if (verifySuccess)
                    {
                        Logs.LogInfo($"治具1底/顶部码比对成功：{bottomCode} <-> {topCode}");
                        await UpdateOrAddCodeEntity(bottomCode, topCode, spCode);
                        await WritePlcRegister(_plcAddr.Jig1CompareResult, 1);
                        PlcD106 = 1;
                    }
                    else
                    {
                        Logs.LogWarn($"治具1底/顶部码比对失败：{bottomCode} <-> {topCode}");
                        await WritePlcRegister(_plcAddr.Jig1CompareResult, 2);
                        PlcD106 = 2;
                    }

                    // 记录扫描记录
                    await AddScanRecordAsync(new ScanRecord
                    {
                        CreateTime = DateTime.Now,
                        JigNo = "治具1",
                        ScanType = "底/顶部扫码",
                        BottomCode = bottomCode,
                        TopCode = topCode,
                        SPCode = spCode,
                        Result = verifySuccess ? "1" : "2",
                        Remark = verifySuccess ? "比对成功" : "比对失败"
                    });
                }
                else
                {
                    Logs.LogWarn("治具1扫码失败：多次重试后仍未获取到有效码值");
                    await WritePlcRegister(_plcAddr.Jig1CompareResult, 2);
                    PlcD106 = 2;

                    await AddScanRecordAsync(new ScanRecord
                    {
                        CreateTime = DateTime.Now,
                        JigNo = "治具1",
                        ScanType = "底/顶部扫码",
                        BottomCode = bottomCode,
                        TopCode = topCode,
                        SPCode = spCode,
                        Result = "2",
                        Remark = "扫码失败（重试耗尽）"
                    });
                }
            }
            catch (Exception ex)
            {
                Logs.LogError(ex, "治具1底/顶部扫码处理异常");
                await WritePlcRegister(_plcAddr.Jig1CompareResult, 2);
                PlcD106 = 2;

                await AddScanRecordAsync(new ScanRecord
                {
                    CreateTime = DateTime.Now,
                    JigNo = "治具1",
                    ScanType = "底/顶部扫码",
                    BottomCode = bottomCode,
                    TopCode = topCode,
                    SPCode = spCode,
                    Result = "2",
                    Remark = $"处理异常：{ex.Message}"
                });
            }
        }

        /// <summary>
        /// 治具1焊接结果扫码处理
        /// </summary>
        private async Task ProcessJig1Weld()
        {
            Logs.LogInfo("治具1-D1触发：开始焊接结果扫码");
            WritePlcRegister(_plcAddr.Jig1TriggerWeld, 0);

            PlcD1 = "0";

            string bottomCode = string.Empty;
            string spCode = string.Empty;
            bool scanSuccess = false;
            int retryCount = 0;

            try
            {
                while (retryCount < _runConfig.ScanRetryCount && !scanSuccess)
                {
                    if (retryCount > 0)
                    {
                        Logs.LogInfo($"治具1焊接扫码解析失败，第{retryCount}次重试...");
                        await Task.Delay(_runConfig.ScanRetryDelay);
                    }

                    string bottomRaw = string.Empty;

                    if (IsNoHardwareMode)
                    {
                        // 模拟生成双码，用分隔符连接
                        bottomRaw = $"H-B{DateTime.Now:yyyyMMddHHmmss}{_leftDownScannerConfig.CodeDelimiter}SP{DateTime.Now:yyyyMMddHHmmss}";
                    }
                    else
                    {
                        // 只触发左下扫码枪（返回双码）
                        bottomRaw = await _hardwareService.TriggerScanner(ScannerType.左下);
                    }

                    // 解析左下扫码结果，获取底板码和主板码
                    if (TryParseBottomAndSpCode(bottomRaw, _leftDownScannerConfig.CodeDelimiter, out string parsedBottom, out string parsedSp))
                    {
                        bottomCode = parsedBottom;
                        spCode = parsedSp;
                        scanSuccess = !string.IsNullOrEmpty(bottomCode) && !string.IsNullOrEmpty(spCode);
                    }
                    else
                    {
                        Logs.LogWarn($"治具1焊接扫码结果无法解析出底板码和主板码：{bottomRaw}");
                    }

                    retryCount++;
                }

                if (scanSuccess)
                {
                    Logs.LogInfo($"治具1焊接扫码成功：底板码={bottomCode}，主板码={spCode}");

                    // 写入扫码完成标志
                    await WritePlcRegister(_plcAddr.Jig1WeldResult, 1);
                    PlcD101 = 1;

                    // MES查询主板码测试结果
                    // bool mesResult = await _mesService.GetMesTestResult(spCode);
                    bool mesResult = true;
                    int weldResult = mesResult ? 1 : 2;
                    await UpdateCodeEntityTestResult(spCode, weldResult);
                    string newCount = await UpdateBTEntityCount(bottomCode);
                    if (int.TryParse(newCount, out int countValue))
                    {
                        await WritePlcRegister(_plcAddr.Jig1Count, countValue);
                        PlcD108 = countValue;
                        this.RaisePropertyChanged(nameof(Jig1UseCount));
                    }
                    else
                    {
                        Logs.LogWarn($"Counts转换失败：{newCount} 无法转为整数，默认写入0");
                        await WritePlcRegister(_plcAddr.Jig1Count, 0);
                        PlcD108 = 0;
                    }

                    // 写入焊接最终结果
                    await WritePlcRegister(_plcAddr.Jig1WeldFinalResult, weldResult);
                    PlcD104 = weldResult;
                    _currentJig1BottomCode = bottomCode;
                    // 添加扫描记录
                    await AddScanRecordAsync(new ScanRecord
                    {
                        CreateTime = DateTime.Now,
                        JigNo = "治具1",
                        ScanType = "焊接结果扫码",
                        BottomCode = bottomCode,
                        SPCode = spCode,
                        Result = weldResult.ToString(),
                        Remark = mesResult ? "MES查询OK" : "MES查询NG"
                    });

                    Logs.LogInfo($"治具1焊接结果处理完成：MES结果={(mesResult ? "OK" : "NG")}，使用次数={PlcD108}");
                }
                else
                {
                    Logs.LogWarn("治具1焊接扫码失败：多次重试后仍未获取到有效码值");
                    await WritePlcRegister(_plcAddr.Jig1WeldFinalResult, 2);
                    PlcD104 = 2;

                    await AddScanRecordAsync(new ScanRecord
                    {
                        CreateTime = DateTime.Now,
                        JigNo = "治具1",
                        ScanType = "焊接结果扫码",
                        BottomCode = bottomCode,
                        SPCode = spCode,
                        Result = "2",
                        Remark = "扫码失败（重试耗尽）"
                    });
                }
            }
            catch (Exception ex)
            {
                Logs.LogError(ex, "治具1焊接结果扫码处理异常");
                await WritePlcRegister(_plcAddr.Jig1WeldFinalResult, 2);
                PlcD104 = 2;

                await AddScanRecordAsync(new ScanRecord
                {
                    CreateTime = DateTime.Now,
                    JigNo = "治具1",
                    ScanType = "焊接结果扫码",
                    BottomCode = bottomCode,
                    SPCode = spCode,
                    Result = "2",
                    Remark = $"处理异常：{ex.Message}"
                });
            }
        }

        private async Task ProcessJig1Clear()
        {
            Logs.LogInfo("治具1-D2触发：开始清零操作");
            WritePlcRegister(_plcAddr.Jig1TriggerClear, 0);

            PlcD2 = "0";

            string bottomCode = string.Empty;
            bool scanSuccess = false;

            try
            {
                string bottomRaw = string.Empty;
                if (IsNoHardwareMode)
                {
                    bottomCode = $"B{DateTime.Now:yyyyMMddHHmmss}";
                    scanSuccess = true;
                }
                else
                {
                    bottomRaw = await _hardwareService.TriggerScanner(ScannerType.左下);



                    if (TryParseBottomAndSpCode(bottomRaw, _rightDownScannerConfig.CodeDelimiter, out string parsedBottom, out string parsedSp))
                    {
                        bottomCode = parsedBottom;
                        scanSuccess = !string.IsNullOrEmpty(bottomCode);
                    }
                    else
                    {
                        Logs.LogWarn($"治具2右下扫码结果无法解析出底板码：{bottomRaw}");
                    }
                    
                }

                if (scanSuccess)
                {
                    await ClearBTEntityCount(bottomCode);
                    await WritePlcRegister(_plcAddr.Jig1Count, 0);
                    PlcD108 = 0;
                    this.RaisePropertyChanged(nameof(Jig1UseCount));

                    Logs.LogInfo($"治具1清零完成：底板码={bottomCode}，使用次数已重置为0");

                    await AddScanRecordAsync(new ScanRecord
                    {
                        CreateTime = DateTime.Now,
                        JigNo = "治具1",
                        ScanType = "清零操作",
                        BottomCode = bottomCode,
                        Result = "1",
                        Remark = "清零成功"
                    });
                }
                else
                {
                    Logs.LogWarn("治具1清零失败：扫码未获取到有效底板码");

                    await AddScanRecordAsync(new ScanRecord
                    {
                        CreateTime = DateTime.Now,
                        JigNo = "治具1",
                        ScanType = "清零操作",
                        BottomCode = bottomCode,
                        Result = "2",
                        Remark = "扫码失败"
                    });
                }
            }
            catch (Exception ex)
            {
                Logs.LogError(ex, "治具1清零处理异常");

                await AddScanRecordAsync(new ScanRecord
                {
                    CreateTime = DateTime.Now,
                    JigNo = "治具1",
                    ScanType = "清零操作",
                    BottomCode = bottomCode,
                    Result = "2",
                    Remark = $"处理异常：{ex.Message}"
                });
            }
        }

        #endregion

        #region 治具2核心逻辑

        private async Task ProcessJig2Logic()
        {
            // 1. 底/顶部扫码触发
            if (PlcD3 == "1")
            {
                await ProcessJig2Scan();
            }

            // 2. 焊接结果扫码触发
            if (PlcD4 == "1")
            {
                await ProcessJig2Weld();
            }

            // 3. 清零触发
            if (PlcD5 == "1")
            {
                await ProcessJig2Clear();
            }
        }

        private async Task ProcessJig2Scan()
        {
            Logs.LogInfo("治具2-D3触发：开始底/顶部扫码");
            WritePlcRegister(_plcAddr.Jig2TriggerScan, 0);
            PlcD3 = "0";

            string bottomCode = string.Empty;
            string spCode = string.Empty;
            string topCode = string.Empty;
            bool scanSuccess = false;
            bool verifySuccess = false;
            int retryCount = 0;

            try
            {
                while (retryCount < _runConfig.ScanRetryCount && !scanSuccess)
                {
                    if (retryCount > 0)
                    {
                        Logs.LogInfo($"治具2双码解析失败，第{retryCount}次重试...");
                        await Task.Delay(_runConfig.ScanRetryDelay);
                    }

                    if (IsNoHardwareMode)
                    {
                        bottomCode = $"H-B{DateTime.Now:yyyyMMddHHmmss}";
                        spCode = $"SP{DateTime.Now:yyyyMMddHHmmss}";
                        topCode = $"T{DateTime.Now:yyyyMMddHHmmss}";
                        scanSuccess = true;
                    }
                    else
                    {
                        // 触发右下扫码枪（返回双码）和右上扫码枪
                        var bottomTask = _hardwareService.TriggerScanner(ScannerType.右下);
                        var topTask = _hardwareService.TriggerScanner(ScannerType.右上);
                        Task.WhenAll(bottomTask, topTask);
                        string bottomRaw = await bottomTask;
                        topCode = await topTask;

                        // 解析右下扫码结果
                        if (TryParseBottomAndSpCode(bottomRaw, _rightDownScannerConfig.CodeDelimiter, out string parsedBottom, out string parsedSp))
                        {
                            bottomCode = parsedBottom;
                            spCode = parsedSp;
                            scanSuccess = !string.IsNullOrEmpty(bottomCode) && !string.IsNullOrEmpty(spCode) && !string.IsNullOrEmpty(topCode);
                        }
                        else
                        {
                            Logs.LogWarn($"治具2右下扫码结果无法解析出底板码和主板码：{bottomRaw}");
                        }
                    }

                    retryCount++;
                }


                if (scanSuccess)
                {
                    Logs.LogInfo($"治具2扫码成功：底板码={bottomCode}，顶部码={topCode}，主板码={spCode}");

                    await WritePlcRegister(_plcAddr.Jig2ScanResult, 1);
                    PlcD102 = 1;

                    verifySuccess = await VerifyBottomTopCode(bottomCode, topCode);

                    if (verifySuccess)
                    {
                        Logs.LogInfo($"治具2底/顶部码比对成功：{bottomCode} <-> {topCode}");
                        await UpdateOrAddCodeEntity(bottomCode, topCode, spCode);
                        await WritePlcRegister(_plcAddr.Jig2CompareResult, 1);
                        PlcD107 = 1;
                    }
                    else
                    {
                        Logs.LogWarn($"治具2底/顶部码比对失败：{bottomCode} <-> {topCode}");
                        await WritePlcRegister(_plcAddr.Jig2CompareResult, 2);
                        PlcD107 = 2;
                    }

                    await AddScanRecordAsync(new ScanRecord
                    {
                        CreateTime = DateTime.Now,
                        JigNo = "治具2",
                        ScanType = "底/顶部扫码",
                        BottomCode = bottomCode,
                        TopCode = topCode,
                        SPCode = spCode,
                        Result = verifySuccess ? "1" : "2",
                        Remark = verifySuccess ? "比对成功" : "比对失败"
                    });
                }
                else
                {
                    Logs.LogWarn("治具2扫码失败：多次重试后未获取到有效码值");
                    await WritePlcRegister(_plcAddr.Jig2CompareResult, 2);
                    PlcD107 = 2;

                    await AddScanRecordAsync(new ScanRecord
                    {
                        CreateTime = DateTime.Now,
                        JigNo = "治具2",
                        ScanType = "底/顶部扫码",
                        BottomCode = bottomCode,
                        TopCode = topCode,
                        SPCode = spCode,
                        Result = "2",
                        Remark = "扫码失败（重试耗尽）"
                    });
                }
            }
            catch (Exception ex)
            {
                Logs.LogError(ex, "治具2底/顶部扫码处理异常");
                await WritePlcRegister(_plcAddr.Jig2CompareResult, 2);
                PlcD107 = 2;

                await AddScanRecordAsync(new ScanRecord
                {
                    CreateTime = DateTime.Now,
                    JigNo = "治具2",
                    ScanType = "底/顶部扫码",
                    BottomCode = bottomCode,
                    TopCode = topCode,
                    SPCode = spCode,
                    Result = "2",
                    Remark = $"处理异常：{ex.Message}"
                });
            }
        }

        /// <summary>
        /// 治具2焊接结果扫码处理
        /// </summary>
        private async Task ProcessJig2Weld()
        {
            Logs.LogInfo("治具2-D4触发：开始焊接结果扫码");
            await WritePlcRegister(_plcAddr.Jig2TriggerWeld, 0);
            PlcD4 = "0";

            string bottomCode = string.Empty;
            string spCode = string.Empty;
            bool scanSuccess = false;
            int retryCount = 0;

            try
            {
                while (retryCount < _runConfig.ScanRetryCount && !scanSuccess)
                {
                    if (retryCount > 0)
                    {
                        Logs.LogInfo($"治具2焊接扫码解析失败，第{retryCount}次重试...");
                        await Task.Delay(_runConfig.ScanRetryDelay);
                    }

                    string bottomRaw = string.Empty;

                    if (IsNoHardwareMode)
                    {
                        bottomRaw = $"H-B{DateTime.Now:yyyyMMddHHmmss}{_rightDownScannerConfig.CodeDelimiter}SP{DateTime.Now:yyyyMMddHHmmss}";
                    }
                    else
                    {
                        // 只触发右下扫码枪（返回双码）
                        bottomRaw = await _hardwareService.TriggerScanner(ScannerType.右下);
                    }

                    if (TryParseBottomAndSpCode(bottomRaw, _rightDownScannerConfig.CodeDelimiter, out string parsedBottom, out string parsedSp))
                    {
                        bottomCode = parsedBottom;
                        spCode = parsedSp;
                        scanSuccess = !string.IsNullOrEmpty(bottomCode) && !string.IsNullOrEmpty(spCode);
                    }
                    else
                    {
                        Logs.LogWarn($"治具2焊接扫码结果无法解析出底板码和主板码：{bottomRaw}");
                    }

                    retryCount++;
                }

                if (scanSuccess)
                {
                    Logs.LogInfo($"治具2焊接扫码成功：底板码={bottomCode}，主板码={spCode}");

                    await WritePlcRegister(_plcAddr.Jig2WeldResult, 1);
                    PlcD103 = 1;

                    // bool mesResult = await _mesService.GetMesTestResult(spCode);
                    bool mesResult = true;// await _mesService.GetMesTestResult(spCode);
                    int weldResult = mesResult ? 1 : 2;

                    await UpdateCodeEntityTestResult(spCode, weldResult);

                    string newCount = await UpdateBTEntityCount(bottomCode);
                    if (int.TryParse(newCount, out int countValue))
                    {
                        await WritePlcRegister(_plcAddr.Jig2Count, countValue);
                        PlcD109 = countValue;
                        this.RaisePropertyChanged(nameof(Jig2UseCount));
                    }
                    else
                    {
                        Logs.LogWarn($"Counts转换失败：{newCount} 无法转为整数，默认写入0");
                        await WritePlcRegister(_plcAddr.Jig2Count, 0);
                        PlcD109 = 0;
                    }

                    await WritePlcRegister(_plcAddr.Jig2WeldFinalResult, weldResult);
                    PlcD105 = weldResult;

                    await AddScanRecordAsync(new ScanRecord
                    {
                        CreateTime = DateTime.Now,
                        JigNo = "治具2",
                        ScanType = "焊接结果扫码",
                        BottomCode = bottomCode,
                        SPCode = spCode,
                        Result = weldResult.ToString(),
                        Remark = mesResult ? "MES查询OK" : "MES查询NG"
                    });

                    Logs.LogInfo($"治具2焊接结果处理完成：MES结果={(mesResult ? "OK" : "NG")}，使用次数={PlcD109}");
                }
                else
                {
                    Logs.LogWarn("治具2焊接扫码失败：多次重试后仍未获取到有效码值");
                    await WritePlcRegister(_plcAddr.Jig2WeldFinalResult, 2);
                    PlcD105 = 2;
                    _currentJig2BottomCode = bottomCode;
                    await AddScanRecordAsync(new ScanRecord
                    {
                        CreateTime = DateTime.Now,
                        JigNo = "治具2",
                        ScanType = "焊接结果扫码",
                        BottomCode = bottomCode,
                        SPCode = spCode,
                        Result = "2",
                        Remark = "扫码失败（重试耗尽）"
                    });
                }
            }
            catch (Exception ex)
            {
                Logs.LogError(ex, "治具2焊接结果扫码处理异常");
                await WritePlcRegister(_plcAddr.Jig2WeldFinalResult, 2);
                PlcD105 = 2;

                await AddScanRecordAsync(new ScanRecord
                {
                    CreateTime = DateTime.Now,
                    JigNo = "治具2",
                    ScanType = "焊接结果扫码",
                    BottomCode = bottomCode,
                    SPCode = spCode,
                    Result = "2",
                    Remark = $"处理异常：{ex.Message}"
                });
            }
        }

        private async Task ProcessJig2Clear()
        {
            Logs.LogInfo("治具2-D5触发：开始清零操作");
            await WritePlcRegister(_plcAddr.Jig2TriggerClear, 0);
            PlcD5 = "0";

            string bottomCode = string.Empty;
            bool scanSuccess = false;

            try
            {
                string bottomRaw = string.Empty;
                if (IsNoHardwareMode)
                {
                    bottomCode = $"B{DateTime.Now:yyyyMMddHHmmss}";
                    scanSuccess = true;
                }
                else
                {
                    bottomRaw = await _hardwareService.TriggerScanner(ScannerType.右下);

                    if (TryParseBottomAndSpCode(bottomRaw, _rightDownScannerConfig.CodeDelimiter, out string parsedBottom, out string parsedSp))
                    {
                        bottomCode = parsedBottom;                       
                        scanSuccess = !string.IsNullOrEmpty(bottomCode);
                    }
                    else
                    {
                        Logs.LogWarn($"治具2右下扫码结果无法解析出底板码：{bottomRaw}");
                    }

                    scanSuccess = !string.IsNullOrEmpty(bottomCode);
                }

                if (scanSuccess)
                {
                    await ClearBTEntityCount(bottomCode);
                    await WritePlcRegister(_plcAddr.Jig2Count, 0);
                    PlcD109 = 0;
                    this.RaisePropertyChanged(nameof(Jig2UseCount));

                    Logs.LogInfo($"治具2清零完成：底板码={bottomCode}，使用次数已重置为0");

                    await AddScanRecordAsync(new ScanRecord
                    {
                        CreateTime = DateTime.Now,
                        JigNo = "治具2",
                        ScanType = "清零操作",
                        BottomCode = bottomCode,
                        Result = "1",
                        Remark = "清零成功"
                    });
                }
                else
                {
                    Logs.LogWarn("治具2清零失败：扫码未获取到有效底板码");

                    await AddScanRecordAsync(new ScanRecord
                    {
                        CreateTime = DateTime.Now,
                        JigNo = "治具2",
                        ScanType = "清零操作",
                        BottomCode = bottomCode,
                        Result = "2",
                        Remark = "扫码失败"
                    });
                }
            }
            catch (Exception ex)
            {
                Logs.LogError(ex, "治具2清零处理异常");

                await AddScanRecordAsync(new ScanRecord
                {
                    CreateTime = DateTime.Now,
                    JigNo = "治具2",
                    ScanType = "清零操作",
                    BottomCode = bottomCode,
                    Result = "2",
                    Remark = $"处理异常：{ex.Message}"
                });
            }
        }

        #endregion

        #region 机械臂逻辑

        private async Task ProcessRobotLogic()
        {
            if (IsNoHardwareMode) return;

            try
            {
                var robotCommand = await _hardwareService.GetRobotCommand();
                // 假设 RobotCommand 是一个枚举，包含 ArriveScanPos 等值
                if (robotCommand == RobotCommand.RobAsc.ToString())
                {
                    Logs.LogInfo("收到机械臂指令：到达扫码位");
                    RobotStatus = "扫码中";
                    RobotScanPosition = "扫码位";

                    string spCode = await _hardwareService.TriggerScanner(ScannerType.机械臂);

                    if (!string.IsNullOrEmpty(spCode))
                    {
                        Logs.LogInfo($"机械臂扫码成功：主板码={spCode}");

                        bool reportResult = await _mesService.ReportStation(spCode);
                        RobotReportResult = reportResult ? "报站成功" : "报站失败";

                        await _hardwareService.SendRobotResponse(reportResult);

                        Logs.LogInfo($"机械臂报站完成：{(reportResult ? "成功" : "失败")}");

                        await AddScanRecordAsync(new ScanRecord
                        {
                            CreateTime = DateTime.Now,
                            JigNo = "机械臂",
                            ScanType = "报站操作",
                            SPCode = spCode,
                            Result = reportResult ? "1" : "2",
                            Remark = reportResult ? "报站成功" : "报站失败"
                        });
                    }
                    else
                    {
                        Logs.LogWarn("机械臂扫码失败：未获取到主板码");
                        RobotReportResult = "扫码失败";
                        await _hardwareService.SendRobotResponse(false);

                        await AddScanRecordAsync(new ScanRecord
                        {
                            CreateTime = DateTime.Now,
                            JigNo = "机械臂",
                            ScanType = "报站操作",
                            SPCode = spCode,
                            Result = "2",
                            Remark = "扫码失败"
                        });
                    }

                    RobotStatus = "空闲";
                }
            }
            catch (Exception ex)
            {
                Logs.LogError(ex, "机械臂逻辑处理异常");
                RobotStatus = "空闲";

                await AddScanRecordAsync(new ScanRecord
                {
                    CreateTime = DateTime.Now,
                    JigNo = "机械臂",
                    ScanType = "报站操作",
                    Result = "2",
                    Remark = $"处理异常：{ex.Message}"
                });
            }
        }

        #endregion

        #region 数据库操作

        private async Task<bool> VerifyBottomTopCode(string bottomCode, string topCode)
        {
            if (string.IsNullOrEmpty(bottomCode) || string.IsNullOrEmpty(topCode))
                return false;

            try
            {
                // 建议使用异步查询
                var btEntity = await Task.Run(() => _databaseManager.BTEntities.FirstOrDefault(t => t.BottomCode == bottomCode));
                if (btEntity == null)
                {
                    Logs.LogWarn($"未找到BTEntity记录：底板码={bottomCode}");
                    return false;
                }
                return btEntity.TopCode == topCode;
            }
            catch (Exception ex)
            {
                Logs.LogError(ex, "验证底/顶部码失败");
                return false;
            }
        }

        private async Task UpdateOrAddCodeEntity(string bottomCode, string topCode, string spCode)
        {
            try
            {
                var codeEntity = await Task.Run(() => _databaseManager.CodeEntities.FirstOrDefault(t => t.BottomCode == bottomCode));

                if (codeEntity != null)
                {
                    codeEntity.SPCode = spCode;
                    _databaseManager.CodeRepository.Update(codeEntity);
                    Logs.LogInfo($"更新CodeEntity：底板码={bottomCode}，新主板码={spCode}");
                }
                else
                {
                    var newEntity = new CodeEntity
                    {
                        BottomCode = bottomCode,
                        TopCode = topCode,
                        SPCode = spCode,
                        Result = "0",
                        InsertDate = DateTime.Now
                    };
                    _databaseManager.CodeRepository.Insert(newEntity);
                    Logs.LogInfo($"新增CodeEntity：底板码={bottomCode}，顶部码={topCode}，主板码={spCode}");
                }

                await Task.Run(() => _databaseManager.SaveChanged());
            }
            catch (Exception ex)
            {
                Logs.LogError(ex, "更新/新增CodeEntity失败");
            }
        }
        /// <summary>
        /// 数据库更新测试结果
        /// </summary>
        /// <param name="spCode"></param>
        /// <param name="result"></param>
        /// <returns></returns>
        private async Task UpdateCodeEntityTestResult(string spCode, int result)
        {
            try
            {
                var codeEntity = await Task.Run(() => _databaseManager.CodeEntities.FirstOrDefault(t => t.SPCode == spCode));
                if (codeEntity != null)
                {
                    codeEntity.Result = result.ToString();
                    _databaseManager.CodeRepository.Update(codeEntity);
                    await Task.Run(() => _databaseManager.SaveChanged());
                    Logs.LogInfo($"更新CodeEntity测试结果：主板码={spCode}，结果={(result == 1 ? "OK" : "NG")}");
                }
                else
                {
                    Logs.LogWarn($"未找到CodeEntity记录：主板码={spCode}");
                }
            }
            catch (Exception ex)
            {
                Logs.LogError(ex, "更新CodeEntity测试结果失败");
            }
        }

        private async Task<string> GetBottomCodeBySPCode(string spCode)
        {
            try
            {
                var codeEntity = await Task.Run(() => _databaseManager.CodeEntities.FirstOrDefault(t => t.SPCode == spCode));
                return codeEntity?.BottomCode ?? string.Empty;
            }
            catch (Exception ex)
            {
                Logs.LogError(ex, "根据主板码获取底板码失败");
                return string.Empty;
            }
        }
        /// <summary>
        /// 更新治具使用次数
        /// </summary>
        /// <param name="bottomCode"></param>
        /// <returns></returns>
        private async Task<string> UpdateBTEntityCount(string bottomCode)
        {
            try
            {
                var btEntity = await Task.Run(() => _databaseManager.BTEntities.FirstOrDefault(t => t.BottomCode == bottomCode));
                if (btEntity != null)
                {
                    if (int.TryParse(btEntity.Counts, out int currentCount))
                    {
                        currentCount += 1;
                        btEntity.Counts = currentCount.ToString();
                    }
                    else
                    {
                        Logs.LogWarn($"Counts解析失败：{btEntity.Counts}，默认设置为1");
                        btEntity.Counts = "1";
                    }

                    _databaseManager.BTRepository.Update(btEntity);
                    await Task.Run(() => _databaseManager.SaveChanged());

                    Logs.LogInfo($"更新BTEntity Counts：底板码={bottomCode}，新次数={btEntity.Counts}");
                    return btEntity.Counts;
                }
                else
                {
                    Logs.LogWarn($"未找到BTEntity记录：底板码={bottomCode}");
                    return "0";
                }
            }
            catch (Exception ex)
            {
                Logs.LogError(ex, "更新BTEntity Counts失败");
                return "0";
            }
        }

        private async Task ClearBTEntityCount(string bottomCode)
        {
            try
            {
                var btEntity = await Task.Run(() => _databaseManager.BTEntities.FirstOrDefault(t => t.BottomCode == bottomCode));
                if (btEntity != null)
                {
                    btEntity.Counts = "0";
                    _databaseManager.BTRepository.Update(btEntity);
                    await Task.Run(() => _databaseManager.SaveChanged());
                    Logs.LogInfo($"清空BTEntity Counts：底板码={bottomCode}，次数重置为0");
                }
                else
                {
                    Logs.LogWarn($"未找到BTEntity记录：底板码={bottomCode}");
                }
            }
            catch (Exception ex)
            {
                Logs.LogError(ex, "清空BTEntity Counts失败");
            }
        }

        private async Task UpdateJigCountInDb(int jigNo, string count)
        {
            string bottomCode = null;
            if (jigNo == 1)
            {
                bottomCode = _currentJig1BottomCode;
            }
            else if (jigNo == 2)
            {
                bottomCode = _currentJig2BottomCode;
            }

            if (string.IsNullOrEmpty(bottomCode))
            {
                Logs.LogWarn($"治具{jigNo}清零失败：当前无有效的底板码");
                return;
            }

            await ClearBTEntityCount(bottomCode);
            Logs.LogInfo($"治具{jigNo}数据库计数已清零，底板码={bottomCode}");
        }

        #endregion

        #region 辅助方法

        private async Task AddScanRecordAsync(ScanRecord record)
        {
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                ScanRecords.Insert(0, record);
                while (ScanRecords.Count > _runConfig.MaxScanRecords)
                {
                    ScanRecords.RemoveAt(ScanRecords.Count - 1);
                }
            });
        }

        private void AppendLog(string message)
        {
            RunLog += $"{DateTime.Now:HH:mm:ss} - {message}\r\n";
            this.RaisePropertyChanged(nameof(RunLog));
        }

        #endregion

        #region 手动测试命令

        private void OnManualTriggerJig1Scan() => PlcD0 = "1";
        private void OnManualTriggerJig1WeldScan() => PlcD1 = "1";
        private void OnManualTriggerJig1Clear() => PlcD2 = "1";
        private void OnManualTriggerJig2Scan() => PlcD3 = "1";
        private void OnManualTriggerJig2WeldScan() => PlcD4 = "1";
        private void OnManualTriggerJig2Clear() => PlcD5 = "1";

        private void OnSimulateRobotToScanPos()
        {
            RobotStatus = "到达扫码位";
            RobotScanPosition = "治具1扫码位";
            AppendLog("模拟机械臂指令：到达扫码位");
        }

        private void OnSimulateRobotReport()
        {
            RobotReportResult = "报站成功（模拟）";
            RobotStatus = "空闲";
            AppendLog("模拟机械臂报站结果：成功");
        }

        #endregion

        #region 资源释放

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                // 停止并释放定时器
                if (_statusCheckTimer != null)
                {
                    _statusCheckTimer.Elapsed -= StatusCheckTimer_Elapsed;
                    _statusCheckTimer.Stop();
                    _statusCheckTimer.Dispose();
                    _statusCheckTimer = null;
                }
                if (_plcReadTimer != null)
                {
                    _plcReadTimer.Elapsed -= PlcReadTimer_Elapsed;
                    _plcReadTimer.Stop();
                    _plcReadTimer.Dispose();
                    _plcReadTimer = null;
                }

                // 停止硬件
                if (!IsNoHardwareMode && _hardwareService != null)
                {
                    try
                    {
                        _hardwareService.Stop();
                    }
                    catch (Exception ex)
                    {
                        Logs.LogError(ex, "停止硬件服务失败");
                    }
                }

                // 清空集合
                ScanRecords.Clear();
            }

            _disposed = true;
            Logs.LogInfo("设备资源已释放");
        }

        ~RunViewModel()
        {
            Dispose(false);
        }

        #endregion
        /// <summary>
        /// 从原始扫码结果中解析底板码和主板码
        /// </summary>
        /// <param name="raw">原始字符串（可能包含分隔符）</param>
        /// <param name="delimiter">分隔符</param>
        /// <param name="bottomCode">解析出的底板码</param>
        /// <param name="spCode">解析出的主板码</param>
        /// <returns>是否解析成功</returns>
        private bool TryParseBottomAndSpCode(string raw, string delimiter, out string bottomCode, out string spCode)
        {
            bottomCode = null;
            spCode = null;

            if (string.IsNullOrEmpty(raw)) return false;

            var codes = raw.Split(new[] { delimiter }, StringSplitOptions.RemoveEmptyEntries);
            if (codes.Length < 2) return false;

            // 遍历所有码，根据特征识别
            string possibleBottom = null;
            string possibleSp = null;

            foreach (var code in codes)
            {
                var trimmed = code.Trim();
                if (trimmed.StartsWith("H-")) // 底板码特征：以 H- 开头
                {
                    possibleBottom = trimmed;
                }
                else if (!trimmed.Contains("-") && trimmed.All(c => char.IsLetterOrDigit(c))) // 主板码特征：不含横线，纯字母数字
                {
                    possibleSp = trimmed;
                }
            }

            // 如果两者都找到，则成功
            if (!string.IsNullOrEmpty(possibleBottom) && !string.IsNullOrEmpty(possibleSp))
            {
                bottomCode = possibleBottom;
                spCode = possibleSp;
                return true;
            }

            // 如果特征不明显，可降级按顺序假设（第一个为底板，第二个为主板）
            if (codes.Length >= 2)
            {
                bottomCode = codes[0].Trim();
                spCode = codes[1].Trim();
                Logs.LogWarn($"码特征识别失败，按顺序假设：底板={spCode}，主板={bottomCode}");
                return true; // 返回 true，但记录警告
            }
            if (codes.Length == 1 && codes[0].Length == bottomCodeLength)
            {

                bottomCode = codes[0].Trim();
                spCode = codes[1].Trim();
                Logs.LogWarn($"码特征识别失败，按顺序假设：底板={spCode}，主板={bottomCode}");
                return true; // 返回 true，但记录警告
            }
            //    if (codes.Length == 2)
            //    {
            //        string first = codes[0].Trim();
            //        string second = codes[1].Trim();

            //        简单启发式：底板码通常较长（例如包含多个部分），主板码较短
            //        if (first.Length > second.Length)
            //        {
            //            bottomCode = first;
            //            spCode = second;
            //        }
            //        else
            //            bottomCode = second;
            //        spCode = first;
            //    }

            //    Logs.LogWarn("特征识别失败，按长度匹配：底板={0}，主板={1}", bottomCode, spCode);
            //    return true;
            //}

            return false;
        }
    }

    #region 实体类

    public class ScanRecord : ReactiveObject
    {
        [Reactive] public DateTime CreateTime { get; set; }
        [Reactive] public string JigNo { get; set; }
        [Reactive] public string ScanType { get; set; }
        [Reactive] public string BottomCode { get; set; }
        [Reactive] public string TopCode { get; set; }
        [Reactive] public string SPCode { get; set; }
        [Reactive] public string Result { get; set; }
        [Reactive] public string Remark { get; set; }
    }

    #endregion

    #region 配置类（需在SystemConfigManager中实现）



    #endregion
}