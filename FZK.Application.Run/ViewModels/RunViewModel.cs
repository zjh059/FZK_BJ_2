using FZK.Application.Run.Service;
using FZK.Application.Share.Config;
using FZK.Application.Share.DebugFolder;
using FZK.Application.Share.Init;
using FZK.Application.Share.Language;
using FZK.Application.Share.Models;
using FZK.Application.Share.Run;
using FZK.Core.Config;
using FZK.Core.Enums;
using FZK.Database.Base.Models;
using FZK.Database.Base.Repositories;
using FZK.Hardware.PLC.Base;
using FZK.Hardware.Robot.Base;
using FZK.Hardware.Robot.Epson;
using FZK.Hardware.Scanner.Base;
using FZK.Logger;
using FZK.Application.Run.Models;
using Prism.Events;
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
using System.Diagnostics;

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
        private readonly IPlcService _plcService;
        private readonly IJigFlowEngine _jig1Engine;
        private readonly IJigFlowEngine _jig2Engine;
        private readonly IRobotCoordinator _robotCoordinator;
        private readonly IDatabaseService _databaseService;
        private readonly IEventAggregator _eventAggregator;
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
        private readonly SoftwareConfig _softwareConfig; // 机械臂扫码枪
        private readonly int bottomCodeLength;
        //锁
        private readonly SemaphoreSlim _jig1ProcessLock = new SemaphoreSlim(1, 1);
        private readonly SemaphoreSlim _jig2ProcessLock = new SemaphoreSlim(1, 1);
        private readonly SemaphoreSlim _robotProcessLock = new SemaphoreSlim(1, 1);
        private readonly SemaphoreSlim _timerProcessLock = new SemaphoreSlim(1, 1);
        // 存储治具当前正在处理的底板码
        private string _currentJig1BottomCode;
        private string _currentJig2BottomCode;

        private int _lastD0, _lastD1, _lastD2, _lastD3, _lastD4, _lastD5;
        // 定时器
        private System.Timers.Timer _plcReadTimer;
        private Timer _statusCheckTimer;

        // 释放标记
        private bool _disposed;

        // PLC 写入统计（D100-D109）
        private readonly Dictionary<int, (int total, int success, int fail)> _plcWriteStats = new Dictionary<int, (int, int, int)>();
        private readonly object _statsLock = new object();
        private readonly SubscriptionToken _logSubscriptionToken;
        #endregion

        #region 构造函数与初始化

        public RunViewModel(IDatabaseManager databaseManager,
            IHardwareService hardwareService,
            IMesService mesService,
            IHardwareManager hardwareManager,
            ISystemConfigManager systemConfigManager,
            IPlcService plcService,
            IDatabaseService databaseService,
            IRobotCoordinator robotCoordinator,
             IEventAggregator eventAggregator
            )
        {
            _robotCoordinator = robotCoordinator;
            _plcAddr = systemConfigManager.plcAddressConfig ?? new PlcAddressConfig();
            _runConfig = systemConfigManager.runConfig ?? new RunConfig();
            _robotConfig = systemConfigManager.robotConfig ?? new RobotConfig();
            _softwareConfig = systemConfigManager.SoftwareConfig ?? new SoftwareConfig();
            _hardwareService = hardwareService;
            _hardwareManager = hardwareManager;
            _plcService = plcService;
            _databaseService = databaseService;
            _mesService = mesService;
            // 加载配置
            _plcAddr = systemConfigManager.plcAddressConfig ?? new PlcAddressConfig();
            _runConfig = systemConfigManager.runConfig ?? new RunConfig();
            _robotConfig = systemConfigManager.robotConfig ?? new RobotConfig();
            _leftDownScannerConfig = systemConfigManager.Jig1DownScannerConfig ?? new ScannerConfig();
            _leftUpScannerConfig = systemConfigManager.Jig1UpScannerConfig ?? new ScannerConfig();
            _rightDownScannerConfig = systemConfigManager.Jig2DownScannerConfig ?? new ScannerConfig();
            _rightUpScannerConfig = systemConfigManager.Jig2UpScannerConfig ?? new ScannerConfig();
            __robotScannerConfig = systemConfigManager.RobotScannerConfig ?? new ScannerConfig();
            _softwareConfig = systemConfigManager.SoftwareConfig ?? new SoftwareConfig();
            bottomCodeLength = _leftDownScannerConfig.SnLength;
            _eventAggregator = eventAggregator;
            // 订阅日志事件
            _logSubscriptionToken = _eventAggregator.GetEvent<UILogEvent>()
                .Subscribe(OnUILogReceived, ThreadOption.UIThread);
            // 创建治具1引擎
            var jig1Config = new JigConfig
            {
                JigName = MultiLang.Jig1,
                TriggerScanAddr = _plcAddr.Jig1TriggerScan,
                TriggerWeldAddr = _plcAddr.Jig1TriggerWeld,
                TriggerClearAddr = _plcAddr.Jig1TriggerClear,
                ScanFinishAddr = _plcAddr.Jig1ScanFinish,
                WeldFinishAddr = _plcAddr.Jig1WeldFinish,
                ScanResultAddr = _plcAddr.Jig1ScanResult,
                WeldResultAddr = _plcAddr.Jig1WeldResult,
                CountsAddr = _plcAddr.Jig1Counts,
                BottomScanner = ScannerType.治具1下,
                TopScanner = ScannerType.治具1上
            };
            _jig1Engine = new JigFlowEngine(
                jig1Config, _plcService, hardwareService, _databaseService, _mesService,
                _runConfig, systemConfigManager.Jig1DownScannerConfig,
                systemConfigManager.Jig1UpScannerConfig,
                IsNoHardwareMode, _softwareConfig.IsSFC, _softwareConfig.IsDebug,
                 record => AddScanRecordAsync(record),
                 msg => AppendLog(msg));

            // 创建治具2引擎
            var jig2Config = new JigConfig
            {
                JigName = MultiLang.Jig2,
                TriggerScanAddr = _plcAddr.Jig2TriggerScan,
                TriggerWeldAddr = _plcAddr.Jig2TriggerWeld,
                TriggerClearAddr = _plcAddr.Jig2TriggerClear,
                ScanFinishAddr = _plcAddr.Jig2ScanFinish,
                WeldFinishAddr = _plcAddr.Jig2WeldFinish,
                ScanResultAddr = _plcAddr.Jig2ScanResult,
                WeldResultAddr = _plcAddr.Jig2WeldResult,
                CountsAddr = _plcAddr.Jig2Counts,
                BottomScanner = ScannerType.治具2下,
                TopScanner = ScannerType.治具2上
            };

            _jig2Engine = new JigFlowEngine(
                jig2Config, _plcService, hardwareService, _databaseService, _mesService,
                _runConfig, systemConfigManager.Jig2DownScannerConfig,
                systemConfigManager.Jig2UpScannerConfig,
                IsNoHardwareMode, _softwareConfig.IsSFC, _softwareConfig.IsDebug,
                record => AddScanRecordAsync(record),
                msg => AppendLog(msg));



            ScanRecords = new ObservableCollection<ScanRecord>();

            // 初始化命令
            ToggleRunCommand = ReactiveCommand.Create(OnToggleRun);
            RefreshStatusCommand = ReactiveCommand.Create(OnRefreshStatus);
            ClearLogCommand = ReactiveCommand.Create(OnClearLog);


            // 初始化状态检查定时器
            _statusCheckTimer = new Timer(_runConfig.StatusCheckInterval);
            _statusCheckTimer.Elapsed += StatusCheckTimer_Elapsed;
            _statusCheckTimer.Start();

            // 初始化PLC寄存器默认值
            InitPlcRegisters();

            // 初始化PLC读取定时器（但不启动，等待用户启动）
            InitPlcReadTimer();

            RunLog = MultiLang.DeviceStarting;
            Logs.LogInfo(MultiLang.DeviceInitCompleted);
        }

        private void InitPlcRegisters()
        {
            PlcD0 = PlcD1 = PlcD2 = PlcD3 = PlcD4 = PlcD5 = "0";
            PlcD100 = PlcD101 = PlcD102 = PlcD103 = PlcD104 = PlcD105 = 0;
            PlcD106 = PlcD107 = 0;
            PlcD108 = PlcD109 = 0;
        }

        private void InitPlcReadTimer()
        {
            //(1)
            // 这里是创建 PLC 读取定时器的地方。
            // 现场确认是 5000ms，就要在日志里看到 5000，方便证明“5秒一轮读取”。
            Logs.LogInfo($"(1)-[Timer] 初始化PLC读取定时器，间隔={_runConfig.PlcReadInterval}ms");

            _plcReadTimer = new Timer(_runConfig.PlcReadInterval);
            _plcReadTimer.Elapsed += PlcReadTimer_Elapsed;
            _plcReadTimer.AutoReset = true;
        }

        #endregion

        #region 视图绑定属性

        [Reactive] public bool IsNoHardwareMode { get; set; }
        [Reactive] public bool IsRunning { get; set; }

        public string RunStatusText => IsRunning ? MultiLang.StopDevice : MultiLang.StartDevice;

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

        [Reactive] public string RobotStatus { get; set; } = MultiLang.Idle;
        [Reactive] public string RobotScanPosition { get; set; } = MultiLang.None;
        [Reactive] public string RobotReportResult { get; set; } = MultiLang.None;

        [Reactive] public string RunLog { get; set; }

        public ObservableCollection<ScanRecord> ScanRecords { get; }

        #endregion

        #region 命令定义

        public ICommand ToggleRunCommand { get; }
        public ICommand RefreshStatusCommand { get; }
        public ICommand ClearLogCommand { get; }

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
                Logs.LogWarn($"{MultiLang.StatusCheckTimerFail}:{ex.Message}");
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
                Logs.LogError(ex, MultiLang.RefreshHardwareStatusError);
                AppendLog($"{MultiLang.RefreshHardwareStatusFail}:{ex.Message}");
            }
        }

        #endregion

        #region 设备启动/停止

        private void OnToggleRun()
        {
            IsRunning = !IsRunning;

            if (IsRunning)
            {
                _lastD0 = _lastD1 = _lastD2 = _lastD3 = _lastD4 = _lastD5 = 0;

                //(2)
                // 启动前再设置一次 Interval，避免配置改过但定时器仍用旧值
                _plcReadTimer.Interval = _runConfig.PlcReadInterval;
                Logs.LogInfo($"(2)-[Timer] 启动PLC读取定时器，实际Interval={_plcReadTimer.Interval}ms");

                _plcReadTimer.Start();
                Logs.LogInfo(MultiLang.DeviceStartedMonitorPLC);
                AppendLog(MultiLang.DeviceStarted);

                if (!IsNoHardwareMode)
                {
                   var flag =  _hardwareService?.Init();
                    //if (flag.Value)
                    //{
                    //    Logs.LogInfo(MultiLang.HardwareInitCompleted);
                    //}
                    //else
                    //{
                    //    MessageBox.Show(MultiLang.DeviceInitFail);
                    //    _plcReadTimer.Stop();
                    //    Logs.LogInfo(MultiLang.DeviceStoppedStopMonitor);
                    //    if (!IsNoHardwareMode) _hardwareService?.Stop();
                    //    Logs.LogInfo(MultiLang.DeviceStopping);
                    //    return;
                    //}
                    Logs.LogInfo(MultiLang.HardwareInitCompleted);
                  //  _mesService.GetMesTestResult(" spCode");

                }
                else
                {
                    Logs.LogInfo(MultiLang.SkipHardwareInit);
                }                
            }
            else
            {
                _plcReadTimer.Stop();
                Logs.LogInfo(MultiLang.DeviceStoppedStopMonitor);
                if (!IsNoHardwareMode) _hardwareService?.Stop();
                Logs.LogInfo(MultiLang.DeviceStopping);
            }

            this.RaisePropertyChanged(nameof(RunStatusText));
            this.RaisePropertyChanged(nameof(RunStatusButtonStyle));
        }

        private async void OnRefreshStatus()
        {
            try
            {
                // 1. 读取 PLC 触发寄存器并更新 UI
                var addresses = new List<int>
        {
            _plcAddr.Jig1TriggerScan, _plcAddr.Jig1TriggerWeld, _plcAddr.Jig1TriggerClear,_plcAddr.Jig1Counts,
            _plcAddr.Jig2TriggerScan, _plcAddr.Jig2TriggerWeld, _plcAddr.Jig2TriggerClear,_plcAddr.Jig2Counts,_plcAddr.HeartbeatMonitor
        };
                var values = await _plcService.ReadTriggerRegistersAsync(addresses);

                PlcD0 = values.TryGetValue(_plcAddr.Jig1TriggerScan, out int d0) ? d0.ToString() : "0";
                PlcD1 = values.TryGetValue(_plcAddr.Jig1TriggerWeld, out int d1) ? d1.ToString() : "0";
                PlcD2 = values.TryGetValue(_plcAddr.Jig1TriggerClear, out int d2) ? d2.ToString() : "0";
                PlcD3 = values.TryGetValue(_plcAddr.Jig2TriggerScan, out int d3) ? d3.ToString() : "0";
                PlcD4 = values.TryGetValue(_plcAddr.Jig2TriggerWeld, out int d4) ? d4.ToString() : "0";
                PlcD5 = values.TryGetValue(_plcAddr.Jig2TriggerClear, out int d5) ? d5.ToString() : "0";
                PlcD108 = values.TryGetValue(_plcAddr.Jig1Counts, out int d108) ? d108 : 0;
                PlcD109 = values.TryGetValue(_plcAddr.Jig2Counts, out int d109) ? d109 : 0;
                PlcD110 = values.TryGetValue(_plcAddr.Jig2TriggerClear, out int d110) ? d110.ToString() : "0";



                // 2. 刷新数据库缓存（注意：原代码是同步调用，此处保持同步避免行为变化）
                _databaseManager.GetAll();

                Logs.LogInfo(MultiLang.StatusRefreshCompleted);
                AppendLog(MultiLang.StatusRefreshDone);
            }
            catch (Exception ex)
            {
                Logs.LogError(ex, MultiLang.RefreshStatusFail);
                MessageBox.Show($"{MultiLang.RefreshFail}:{ex.Message}", MultiLang.Error, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnClearLog()
        {
            RunLog = string.Empty;
            this.RaisePropertyChanged(nameof(RunLog));
            Logs.LogInfo(MultiLang.LogCleared);
        }

        #endregion

        #region PLC读写

        //private async void PlcReadTimer_Elapsed(object sender, ElapsedEventArgs e)
        //{
        //    if (_disposed || !IsRunning) return;



        //    //(3)
        //    //if (!await _timerProcessLock.WaitAsync(0)) return;
        //    // 这里用锁保证同一时间只跑一轮 PLC 读取
        //    // 如果上一轮还没跑完，新一轮会直接跳过；现场排查时必须把这个跳过打出来
        //    if (!await _timerProcessLock.WaitAsync(0))
        //    {
        //        Logs.LogWarn($"[Timer] 本轮PLC读取跳过：上一轮还没结束，时间={DateTime.Now:HH:mm:ss.fff}");
        //        return;
        //    }
        //    var timerWatch = Stopwatch.StartNew();
        //    Logs.LogInfo($"[Timer] 本轮PLC读取开始，时间={DateTime.Now:HH:mm:ss.fff}");



        //    try
        //    {
        //        // 读取 PLC 寄存器值
        //        // 第一组地址 D0-D5：PLC 触发信号
        //        // D0=治具1扫码比对，D1=治具1焊接结果，D2=治具1清零
        //        // D3=治具2扫码比对，D4=治具2焊接结果，D5=治具2清零
        //        var addresses = new List<int>
        //        {
        //            _plcAddr.Jig1TriggerScan, _plcAddr.Jig1TriggerWeld, _plcAddr.Jig1TriggerClear,
        //            _plcAddr.Jig2TriggerScan, _plcAddr.Jig2TriggerWeld, _plcAddr.Jig2TriggerClear
        //        };

        //        var values = await _plcService.ReadTriggerRegistersAsync(addresses);

        //        // 获取当前值（默认 0）
        //        int d0 = values.TryGetValue(_plcAddr.Jig1TriggerScan, out int v0) ? v0 : 0;
        //        int d1 = values.TryGetValue(_plcAddr.Jig1TriggerWeld, out int v1) ? v1 : 0;
        //        int d2 = values.TryGetValue(_plcAddr.Jig1TriggerClear, out int v2) ? v2 : 0;
        //        int d3 = values.TryGetValue(_plcAddr.Jig2TriggerScan, out int v3) ? v3 : 0;
        //        int d4 = values.TryGetValue(_plcAddr.Jig2TriggerWeld, out int v4) ? v4 : 0;
        //        int d5 = values.TryGetValue(_plcAddr.Jig2TriggerClear, out int v5) ? v5 : 0;


        //        // 第二组地址 D108-D110：显示/监控用
        //        // D108=治具1使用次数，D109=治具2使用次数，D110=心跳/监控
        //        var addresses2 = new List<int>
        //        {
        //          _plcAddr.Jig1Counts,_plcAddr.Jig2Counts,_plcAddr.HeartbeatMonitor
        //        };

        //        var values2 = await _plcService.ReadTriggerRegistersAsync(addresses2);

        //        // 注意：这里要用 Jig1Counts/Jig2Counts 取值，不要写成 Jig1TriggerClear/Jig2TriggerClear
        //        int d108 = values2.TryGetValue(_plcAddr.Jig1TriggerClear, out int v108) ? v108 : 0;
        //        int d109 = values2.TryGetValue(_plcAddr.Jig2TriggerClear, out int v109) ? v109 : 0;
        //        int d110 = values2.TryGetValue(_plcAddr.HeartbeatMonitor, out int v110) ? v110 : 0;


        //        PlcD0 = d0.ToString(); PlcD1 = d1.ToString(); PlcD2 = d2.ToString();
        //        PlcD3 = d3.ToString(); PlcD4 = d4.ToString(); PlcD5 = d5.ToString();
        //        PlcD108 = d108; PlcD109 = d109; PlcD110 = d110.ToString();


        //        // 边沿检测并处理
        //        // 下面就是“上升沿判断”。
        //        // 只有上一次是0、这一次是1，才认为 PLC 触发了一次动作
        //        if (_lastD0 == 0 && d0 == 1)
        //            await _jig1Engine.ProcessScanAsync();
        //        if (_lastD1 == 0 && d1 == 1)
        //            await _jig1Engine.ProcessWeldAsync();
        //        if (_lastD2 == 0 && d2 == 1)
        //            await _jig1Engine.ProcessClearAsync();
        //        if (_lastD3 == 0 && d3 == 1)
        //            await _jig2Engine.ProcessScanAsync();
        //        if (_lastD4 == 0 && d4 == 1)
        //            await _jig2Engine.ProcessWeldAsync();
        //        if (_lastD5 == 0 && d5 == 1)
        //            await _jig2Engine.ProcessClearAsync();

        //        await _robotCoordinator.ProcessCommandAsync();

        //        // 更新上次值
        //        _lastD0 = d0; _lastD1 = d1; _lastD2 = d2;
        //        _lastD3 = d3; _lastD4 = d4; _lastD5 = d5;
        //    }
        //    catch (ObjectDisposedException)
        //    {
        //        // 信号量已释放，直接返回
        //        return;
        //    }
        //    catch (Exception ex)
        //    {
        //        Logs.LogError(ex, MultiLang.PLCReadProcessError);
        //        // await ResetPlcErrorRegisters();
        //    }
        //    finally
        //    {

        //        //(3)
        //        timerWatch.Stop();
        //        Logs.LogInfo($"[Timer] 本轮PLC读取结束，耗时={timerWatch.ElapsedMilliseconds}ms，时间={DateTime.Now:HH:mm:ss.fff}");

        //        _timerProcessLock.Release();
        //    }
        //}
        private async void PlcReadTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (_disposed || !IsRunning) return;

            //(3)
            // 这里用锁保证同一时间只跑一轮 PLC 读取。
            // 如果上一轮还没跑完，新一轮会直接跳过；现场排查时必须把这个跳过打出来。
            if (!await _timerProcessLock.WaitAsync(0))
            {
              //  Logs.LogWarn($"[Timer] 本轮PLC读取跳过：上一轮还没结束，时间={DateTime.Now:HH:mm:ss.fff}");
                return;
            }

            var timerWatch = Stopwatch.StartNew();
           // Logs.LogInfo($"[Timer] 本轮PLC读取开始，间隔配置={_runConfig.PlcReadInterval}ms，时间={DateTime.Now:HH:mm:ss.fff}");

            try
            {
                // 第一组地址 D0-D5：PLC 触发信号。
                // D0=治具1扫码比对，D1=治具1焊接结果，D2=治具1清零。
                // D3=治具2扫码比对，D4=治具2焊接结果，D5=治具2清零。
                var addresses = new List<int>
        {
            _plcAddr.Jig1TriggerScan, _plcAddr.Jig1TriggerWeld, _plcAddr.Jig1TriggerClear,
            _plcAddr.Jig2TriggerScan, _plcAddr.Jig2TriggerWeld, _plcAddr.Jig2TriggerClear
        };

                var values = await _plcService.ReadTriggerRegistersAsync(addresses);

                int d0 = values.TryGetValue(_plcAddr.Jig1TriggerScan, out int v0) ? v0 : 0;
                int d1 = values.TryGetValue(_plcAddr.Jig1TriggerWeld, out int v1) ? v1 : 0;
                int d2 = values.TryGetValue(_plcAddr.Jig1TriggerClear, out int v2) ? v2 : 0;
                int d3 = values.TryGetValue(_plcAddr.Jig2TriggerScan, out int v3) ? v3 : 0;
                int d4 = values.TryGetValue(_plcAddr.Jig2TriggerWeld, out int v4) ? v4 : 0;
                int d5 = values.TryGetValue(_plcAddr.Jig2TriggerClear, out int v5) ? v5 : 0;

                // 第二组地址 D108-D110：显示/监控用。
                // D108=治具1使用次数，D109=治具2使用次数，D110=心跳/监控。
                var addresses2 = new List<int>
        {
            _plcAddr.Jig1Counts, _plcAddr.Jig2Counts, _plcAddr.HeartbeatMonitor
        };

                var values2 = await _plcService.ReadTriggerRegistersAsync(addresses2);

                // 注意：这里要用 Jig1Counts/Jig2Counts 取值，不要写成 Jig1TriggerClear/Jig2TriggerClear。
                int d108 = values2.TryGetValue(_plcAddr.Jig1Counts, out int v108) ? v108 : 0;
                int d109 = values2.TryGetValue(_plcAddr.Jig2Counts, out int v109) ? v109 : 0;
                int d110 = values2.TryGetValue(_plcAddr.HeartbeatMonitor, out int v110) ? v110 : 0;

                PlcD0 = d0.ToString(); PlcD1 = d1.ToString(); PlcD2 = d2.ToString();
                PlcD3 = d3.ToString(); PlcD4 = d4.ToString(); PlcD5 = d5.ToString();
                PlcD108 = d108; PlcD109 = d109; PlcD110 = d110.ToString();

                // 下面就是“上升沿判断”。
                // 只有上一次是0、这一次是1，才认为 PLC 触发了一次动作。
                if (_lastD0 == 0 && d0 == 1)
                    await _jig1Engine.ProcessScanAsync();
                if (_lastD1 == 0 && d1 == 1)
                    await _jig1Engine.ProcessWeldAsync();
                if (_lastD2 == 0 && d2 == 1)
                    await _jig1Engine.ProcessClearAsync();
                if (_lastD3 == 0 && d3 == 1)
                    await _jig2Engine.ProcessScanAsync();
                if (_lastD4 == 0 && d4 == 1)
                    await _jig2Engine.ProcessWeldAsync();
                if (_lastD5 == 0 && d5 == 1)
                    await _jig2Engine.ProcessClearAsync();

                await _robotCoordinator.ProcessCommandAsync();

                _lastD0 = d0; _lastD1 = d1; _lastD2 = d2;
                _lastD3 = d3; _lastD4 = d4; _lastD5 = d5;
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (Exception ex)
            {
                Logs.LogError(ex, MultiLang.PLCReadProcessError);
            }
            finally
            {
                //(3)
                timerWatch.Stop();
               // Logs.LogInfo($"[Timer] 本轮PLC读取结束，耗时={timerWatch.ElapsedMilliseconds}ms，时间={DateTime.Now:HH:mm:ss.fff}");
                _timerProcessLock.Release();
            }
        }


        #endregion


        #region 辅助方法
        private void OnUILogReceived(string message)
        {
            AppendLog(message);
        }
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
                        Logs.LogError(ex, MultiLang.StopHardwareFailed);
                    }
                }

                // 清空集合
                ScanRecords.Clear();

                // 输出 PLC 写入统计
                lock (_statsLock)
                {
                    if (_plcWriteStats.Count > 0)
                    {
                        Logs.LogInfo(MultiLang.PlcWriteStatsHeader);
                        foreach (var kvp in _plcWriteStats.OrderBy(k => k.Key))
                        {
                            Logs.LogInfo(string.Format(MultiLang.PlcWriteStatsLine, kvp.Key, kvp.Value.total, kvp.Value.success, kvp.Value.fail));
                        }
                        Logs.LogInfo(MultiLang.PlcWriteStatsFooter);
                    }
                }
            }
            _jig1ProcessLock?.Dispose();
            _jig2ProcessLock?.Dispose();
            _robotProcessLock?.Dispose();
            _disposed = true;
            Logs.LogInfo(MultiLang.DeviceResourceReleased);
        }

        ~RunViewModel()
        {
            Dispose(false);
        }

        #endregion




    }
}