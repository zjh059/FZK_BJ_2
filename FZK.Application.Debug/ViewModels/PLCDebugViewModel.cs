using FZK.Application.Share.Config;
using FZK.Application.Share.Init;
using FZK.Core.Extensions;
using FZK.Hardware.PLC.Base;
using FZK.Logger;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace FZK.Application.Debug.ViewModels
{
    public class PLCDebugViewModel : ReactiveObject
    {
        // 基础配置
        [Reactive] public string IPAdress { get; set; }
        [Reactive] public string NetworkNo { get; set; }
        [Reactive] public string NodeNo { get; set; }
        [Reactive] public string Port { get; set; }
        [Reactive] public bool Connected { get; set; }

        // 读写相关
        [Reactive] public string RwReadValue { get; set; }
        [Reactive] public string RwWriteValue { get; set; }
        [Reactive] public string RwAddress { get; set; }
        [Reactive] public PLCRegisterType RegisterTypes { get; set; }
        public List<PLCRegisterType> PLCRegisterTypes { get; } = EnumExtension.ToList<PLCRegisterType>();

        // 监听相关
        [Reactive] public string MonitorAddressType { get; set; }
        [Reactive] public string MonitorAddress { get; set; }
        [Reactive] public string MonitorValue { get; set; }
        [Reactive] public string MonitorTips { get; set; }
        [Reactive] public bool IsMonitoring { get; set; }
        public List<string> MonitorAddressTypes { get; } = new List<string> { "DM", "CIO", "TIM", "CNTR" };

        // 心跳相关
        [Reactive] public string HeartbeatValue { get; set; }
        [Reactive] public SolidColorBrush HeartbeatStateColor { get; set; }
        [Reactive] public string HeartbeatStateText { get; set; }

        [Reactive] public string OperationLog { get; set; }
        [Reactive] public bool IsPlcConnected { get; set; }

        // 命令
        public ICommand ConnectPlcCommand { get; }
        public ICommand DisconnectPlcCommand { get; }
        public ICommand ReadCommand { get; }
        public ICommand WriteCommand { get; }
        public ICommand StartMonitorCommand { get; }
        public ICommand StopMonitorCommand { get; }

        private readonly ISystemConfigManager _systemConfigManager;
        private readonly IPLC _plc;
        private System.Timers.Timer _heartbeatTimer;
        private System.Timers.Timer _monitorTimer;
        private bool _heartbeatFlip = false;

        public PLCDebugViewModel(ISystemConfigManager systemConfigManager, IHardwareManager hardwareManager)
        {
            _systemConfigManager = systemConfigManager;
            _plc = hardwareManager.OmronPLC;

            // 加载配置
            IPAdress = _systemConfigManager.pLCConfig.IpAddress;
            Port = _systemConfigManager.pLCConfig.Port.ToString();
            NetworkNo = _systemConfigManager.pLCConfig.NetworkNo.ToString();
            NodeNo = _systemConfigManager.pLCConfig.PlcNode.ToString();

            // 初始化属性
            Connected = _plc.Connected;
            IsPlcConnected = _plc.Connected;

            RwAddress = "0";
            RwWriteValue = "0";
            RwReadValue = "";
            MonitorAddressType = "DM";
            MonitorAddress = "0";
            MonitorValue = "";
            MonitorTips = "未监听";
            HeartbeatValue = "";
            HeartbeatStateColor = Brushes.Gray;
            HeartbeatStateText = "无心跳";
            OperationLog = "";

            // 命令绑定
            ConnectPlcCommand = ReactiveCommand.Create(OnConnectPlcCommand);
            DisconnectPlcCommand = ReactiveCommand.Create(OnDisconnectPlcCommand);
            ReadCommand = ReactiveCommand.Create(OnReadCommand);
            WriteCommand = ReactiveCommand.Create(OnWriteCommand);
            StartMonitorCommand = ReactiveCommand.Create(OnStartMonitorCommand);
            StopMonitorCommand = ReactiveCommand.Create(OnStopMonitorCommand);

            // 监听 PLC 连接状态变化
            this.WhenAnyValue(x => x.Connected).Subscribe(connected =>
            {
                IsPlcConnected = connected;
                if (!connected)
                {
                    StopMonitoring();
                    StopHeartbeat();
                }
            });

            // 启动心跳模拟（如果已连接）
            if (Connected)
            {
              //  StartHeartbeat();
            }
        }

        private async void OnConnectPlcCommand()
        {
            if (Connected) return;

            // 更新配置
            _systemConfigManager.pLCConfig.IpAddress = IPAdress;
            if (int.TryParse(Port, out int port)) _systemConfigManager.pLCConfig.Port = port;
            if (byte.TryParse(NetworkNo, out byte netNo)) _systemConfigManager.pLCConfig.NetworkNo = netNo;
            if (byte.TryParse(NodeNo, out byte plcNode)) _systemConfigManager.pLCConfig.PlcNode = plcNode;

            bool success = await Task.Run(() => _plc.Init(_systemConfigManager.pLCConfig));
            Connected = _plc.Connected;

            if (success)
            {
                AddLog($"PLC 连接成功 ({IPAdress}:{Port})");
                StartHeartbeat();
            }
            else
            {
                AddLog("PLC 连接失败，请检查配置");
            }
        }

        private void OnDisconnectPlcCommand()
        {
            if (!Connected) return;
            _plc.Close();
            Connected = _plc.Connected;
            AddLog("PLC 连接已断开");
            StopHeartbeat();
            StopMonitoring();
        }

        private async void OnReadCommand()
        {
            if (!Connected)
            {
                AddLog("读取失败：PLC未连接");
                return;
            }

            if (!ushort.TryParse(RwAddress, out ushort addr))
            {
                AddLog($"地址格式错误：{RwAddress}");
                return;
            }

            try
            {
                int value = await Task.Run(() => _plc.Read(RegisterTypes, addr));
                RwReadValue = value.ToString();
                AddLog($"读取 {RegisterTypes}{addr} = {value}");
            }
            catch (Exception ex)
            {
                AddLog($"读取异常：{ex.Message}");
            }
        }

        private async void OnWriteCommand()
        {
            if (!Connected)
            {
                AddLog("写入失败：PLC未连接");
                return;
            }

            if (!ushort.TryParse(RwAddress, out ushort addr))
            {
                AddLog($"地址格式错误：{RwAddress}");
                return;
            }

            if (!int.TryParse(RwWriteValue, out int value))
            {
                AddLog($"写入值格式错误：{RwWriteValue}");
                return;
            }

            try
            {
                bool success = await Task.Run(() => _plc.Write(RegisterTypes, addr, value));
                if (success)
                    AddLog($"写入 {RegisterTypes}{addr} = {value} 成功");
                else
                    AddLog($"写入 {RegisterTypes}{addr} = {value} 失败");
            }
            catch (Exception ex)
            {
                AddLog($"写入异常：{ex.Message}");
            }
        }

        private async void OnStartMonitorCommand()
        {
            if (!Connected)
            {
                AddLog("监听失败：PLC未连接");
                return;
            }

            if (!ushort.TryParse(MonitorAddress, out ushort addr))
            {
                AddLog($"监听地址格式错误：{MonitorAddress}");
                return;
            }

            StopMonitoring();
            _monitorTimer = new System.Timers.Timer(500);
            _monitorTimer.Elapsed += async (s, e) =>
            {
                // 后台读取
                int value = await Task.Run(() => _plc.Read(GetRegisterTypeFromString(MonitorAddressType), addr));

                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    MonitorValue = value.ToString();
                    MonitorTips = "监听中...";
                    AddLog($"监听 {MonitorAddressType}{addr} = {value}");
                });
            };
            _monitorTimer.Start();
            IsMonitoring = true;
            AddLog($"开始监听 {MonitorAddressType}{MonitorAddress}");
        }

        private void OnStopMonitorCommand()
        {
            StopMonitoring();
        }

        private void StopMonitoring()
        {
            if (_monitorTimer != null)
            {
                _monitorTimer.Stop();
                _monitorTimer.Dispose();
                _monitorTimer = null;
            }
            IsMonitoring = false;
            MonitorTips = "未监听";
            MonitorValue = "";
            AddLog("停止监听");
        }

        private void StartHeartbeat()
        {
            if (_heartbeatTimer != null) return;

            _heartbeatTimer = new System.Timers.Timer(1000);
            _heartbeatTimer.Elapsed += async (s, e) =>
            {
                // 检查是否应继续
                if (!Connected) return;

                // 在后台线程读取 PLC 寄存器，避免阻塞 UI
                int value = await Task.Run(() => _plc.Read(PLCRegisterType.DM , 110));

                // 切回 UI 线程更新界面
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    HeartbeatValue = value.ToString();
                    _heartbeatFlip = !_heartbeatFlip;
                    HeartbeatStateColor = _heartbeatFlip ? Brushes.Green : Brushes.DarkGreen;
                    HeartbeatStateText = _heartbeatFlip ? "跳动" : "静止";
                });
            };
            _heartbeatTimer.Start();
        }

        private void StopHeartbeat()
        {
            if (_heartbeatTimer != null)
            {
                _heartbeatTimer.Stop();
                _heartbeatTimer.Dispose();
                _heartbeatTimer = null;
            }
            HeartbeatValue = "";
            HeartbeatStateColor = Brushes.Gray;
            HeartbeatStateText = "无心跳";
        }

        private PLCRegisterType GetRegisterTypeFromString(string typeStr)
        {
            switch (typeStr)
            {
                case "DM":
                    return PLCRegisterType.DM;
                case "CIO":
                    return PLCRegisterType.CIO;
                case "TIM":
                    return PLCRegisterType.TIM;
                case "CNTR":
                    return PLCRegisterType.CNTR;
                default:
                    return PLCRegisterType.DM;
            }
        }

        private void AddLog(string msg)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                string timestamp = DateTime.Now.ToString("HH:mm:ss");
                OperationLog = $"{timestamp} - {msg}\r\n" + OperationLog;
                if (OperationLog.Length > 10000)
                    OperationLog = OperationLog.Substring(0, 8000);
                this.RaisePropertyChanged(nameof(OperationLog));
            });
            Logs.LogInfo($"[PLC调试] {msg}");
        }
    }
}