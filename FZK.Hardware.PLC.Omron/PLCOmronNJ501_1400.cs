using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using FZK.Hardware.PLC.Base;
using FZK.Logger;
using HslCommunication;
using HslCommunication.Profinet.Omron;
using ReactiveUI;

namespace FZK.Hardware.PLC.Omron
{
    internal class PLCOmronNJ501_1400 : ReactiveObject, IPLC
    {
        // ===================== 公开属性 =====================
        public bool Initialized { get; set; }

        private bool _connected;
        public bool Connected
        {
            get => _connected;
            private set => this.RaiseAndSetIfChanged(ref _connected, value);
        }

        public bool HandshakeCompleted
        {
            get => _connected; // 长连接下 Connected 即为握手完成状态
            private set { _connected = value; }
        }

        public PLCState PLCState { get; set; }

        // ===================== 可观察属性 =====================
        private readonly Subject<string> _messageSubject = new Subject<string>();
        private readonly Subject<string> _receiveFrameSubject = new Subject<string>();

        private string _message;
        public string Message
        {
            get => _message;
            set
            {
                this.RaiseAndSetIfChanged(ref _message, value);
                _messageSubject.OnNext(value);
            }
        }

        private string _receiveFrame;
        public string ReceiveFrame
        {
            get => _receiveFrame;
            set
            {
                this.RaiseAndSetIfChanged(ref _receiveFrame, value);
                _receiveFrameSubject.OnNext(value);
            }
        }

        public IObservable<string> MessageObservable => _messageSubject;
        public IObservable<string> ReceiveFrameObservable => _receiveFrameSubject;

        public byte HeartbeatSid { get; private set; } = 0xFE;

        // ===================== 私有字段 =====================
        private OmronFinsNet _omronFins;          // HslCommunication 客户端
        private PLCConfig _plcConfig;
        private CancellationTokenSource _cancellationTokenSource;

        private Task _heartbeatTask;
        private Task _reconnectTask;
        private readonly object _taskLock = new object();

        private int _reconnectCount;
        private readonly object _reconnectLock = new object();

        #region 构造与初始化
        public PLCOmronNJ501_1400()
        {
            PLCState = PLCState.UnInitialized;
            _cancellationTokenSource = new CancellationTokenSource();
            Message = "PLC驱动已实例化，未初始化连接";
        }

        public bool Init(PLCConfig config)
        {
            try
            {
                if (Initialized)
                {
                    Logs.LogDebug("[PLC] 已完成初始化，无需重复执行");
                    return true;
                }

                if (config == null || string.IsNullOrWhiteSpace(config.IpAddress))
                {
                    string errorMsg = "PLC配置为空或IP地址无效，初始化失败";
                    Message = errorMsg;
                    Logs.LogError($"[PLC] {errorMsg}");
                    return false;
                }

                _plcConfig = config;
                PLCState = PLCState.Connecting;
                Logs.LogInfo($"[PLC] 正在连接 {config.IpAddress}:{config.Port}");

                // 创建 OmronFinsNet 客户端并配置
                _omronFins = new OmronFinsNet(config.IpAddress, config.Port)
                {
                    // 网络节点配置
                    SA1 = config.LocalNode,        // 本机节点号
                    DA1 = config.PlcNode,          // PLC 节点号（如有握手协商，可后续修改）
                    SNA = (byte)(config.NetworkNo & 0xFF),   // 本地网络号
                    DNA = (byte)(config.NetworkNo & 0xFF),   // PLC 网络号

                    // 超时
                    ConnectTimeOut = config.Timeout,
                    ReceiveTimeOut = config.Timeout,

                    // 字节序（与欧姆龙默认一致）
                    ByteTransform = {
                        DataFormat = HslCommunication.Core.DataFormat.DCBA,
                        IsStringReverse = true
                    }
                };

                // 尝试连接
                OperateResult connectResult = _omronFins.ConnectServer();
                if (!connectResult.IsSuccess)
                {
                    string errorMsg = $"首次连接PLC失败：{config.IpAddress}:{config.Port}，原因：{connectResult.Message}";
                    Message = errorMsg;
                    Logs.LogError($"[PLC] {errorMsg}");
                    PLCState = PLCState.UnInitialized;
                    return false;
                }

                // 连接成功，更新状态
                Initialized = true;
                Connected = true;
                HandshakeCompleted = true;
                PLCState = PLCState.Connected;
                Message = $"PLC连接成功：{config.IpAddress}:{config.Port}";
                Logs.LogInfo($"[PLC] {Message}");
                _reconnectCount = 0;

                StartBackgroundTasks();
                return true;
            }
            catch (Exception ex)
            {
                string errorMsg = $"PLC初始化异常：{ex.Message}";
                Message = errorMsg;
                Logs.LogError(ex, $"[PLC] {errorMsg}");
                PLCState = PLCState.UnInitialized;
                return false;
            }
        }
        #endregion

        #region 核心接口：读写
        public int Read(PLCRegisterType registerType, ushort address, bool isBCD = false)
        {
            if (!CheckBaseState()) return -1;

            try
            {
                string addr = GetAddressString(registerType, address);
                OperateResult<ushort> result = _omronFins.ReadUInt16(addr);
                if (!result.IsSuccess)
                {
                    Logs.LogError($"[PLC] 读取{addr}失败：{result.Message}");
                    return -1;
                }

                int value = ConvertFromPlcValue(result.Content, isBCD);
                Logs.LogDebug($"[PLC] 读取{addr}成功 | 值={value}");
                return value;
            }
            catch (Exception ex)
            {
                Logs.LogError(ex, $"[PLC] 读取寄存器异常 | {registerType}{address}");
                return -1;
            }
        }

        public List<int> BatchRead(PLCRegisterType registerType, ushort startAddress, ushort count, bool isBCD = false)
        {
            if (!CheckBaseState() || count <= 0 || count > 100)
            {
                Logs.LogWarn(count > 100 ? "PLC批量读取个数不能超过100" : "PLC基础状态校验不通过");
                return new List<int>();
            }

            try
            {
                string addr = GetAddressString(registerType, startAddress);
                OperateResult<ushort[]> result = _omronFins.ReadUInt16(addr, count);
                if (!result.IsSuccess)
                {
                    Logs.LogError($"[PLC] 批量读取{addr} * {count} 失败：{result.Message}");
                    return new List<int>();
                }

                List<int> values = result.Content.Select(v => ConvertFromPlcValue(v, isBCD)).ToList();
                Logs.LogDebug($"[PLC] 批量读取{addr}({count}个)成功");
                return values;
            }
            catch (Exception ex)
            {
                Logs.LogError(ex, $"[PLC] 批量读取异常 | {registerType}{startAddress} * {count}");
                return new List<int>();
            }
        }

        public bool Write(PLCRegisterType registerType, ushort address, int value, bool isBCD = false, bool Require = true)
        {
            if (!CheckBaseState()) return false;

            try
            {
                ushort writeValue = ConvertToPlcValue(value, isBCD);
                string addr = GetAddressString(registerType, address);
                OperateResult result = _omronFins.Write(addr, writeValue);
                if (!result.IsSuccess)
                {
                    Logs.LogError($"[PLC] 写入{addr}失败：{result.Message}");
                    return false;
                }

                Logs.LogDebug($"[PLC] 写入{addr}成功 | 值={value}");
                return true;
            }
            catch (Exception ex)
            {
                Logs.LogError(ex, $"[PLC] 写入异常 | {registerType}{address}");
                return false;
            }
        }

        public bool BatchWrite(PLCRegisterType registerType, ushort startAddress, List<int> values, bool isBCD = false, bool Require = true)
        {
            if (!CheckBaseState() || values == null || values.Count == 0 || values.Count > 100)
            {
                Logs.LogWarn(values.Count > 100 ? "PLC批量写入个数不能超过100" : "PLC基础状态/写入值校验不通过");
                return false;
            }

            try
            {
                ushort[] writeValues = values.Select(v => ConvertToPlcValue(v, isBCD)).ToArray();
                string addr = GetAddressString(registerType, startAddress);
                OperateResult result = _omronFins.Write(addr, writeValues);
                if (!result.IsSuccess)
                {
                    Logs.LogError($"[PLC] 批量写入{addr}失败：{result.Message}");
                    return false;
                }

                Logs.LogDebug($"[PLC] 批量写入{addr}成功 | 数量={values.Count}");
                return true;
            }
            catch (Exception ex)
            {
                Logs.LogError(ex, $"[PLC] 批量写入异常 | {registerType}{startAddress}");
                return false;
            }
        }
        #endregion

        #region 连接检查与关闭
        public bool CheckConnection()
        {
            lock (_reconnectLock)
            {
                if (Connected)
                    return true;

                Logs.LogWarn("[PLC] 连接异常，尝试重连");
                PLCState = PLCState.Reconnecting;
                return TryReconnect();
            }
        }

        public void Close()
        {
            try
            {
                PLCState = PLCState.Disconnecting;
                Logs.LogInfo("[PLC] 正在关闭连接，释放资源...");

                _cancellationTokenSource?.Cancel();
                StopBackgroundTasks();

                _omronFins?.ConnectClose();
                _omronFins = null;

                Initialized = false;
                Connected = false;
                HandshakeCompleted = false;
                _reconnectCount = 0;

                PLCState = PLCState.UnInitialized;
                Logs.LogInfo("[PLC] 连接已关闭，资源释放完成");
            }
            catch (Exception ex)
            {
                Logs.LogError(ex, "[PLC] 关闭连接异常");
            }
        }
        #endregion

        #region 重连逻辑
        private bool TryReconnect()
        {
            try
            {
                _omronFins?.ConnectClose();
                _omronFins = new OmronFinsNet(_plcConfig.IpAddress, _plcConfig.Port)
                {
                    SA1 = _plcConfig.LocalNode,
                    DA1 = _plcConfig.PlcNode,
                    SNA = (byte)(_plcConfig.NetworkNo & 0xFF),
                    DNA = (byte)(_plcConfig.NetworkNo & 0xFF),
                    ConnectTimeOut = _plcConfig.Timeout,
                    ReceiveTimeOut = _plcConfig.Timeout,
                    ByteTransform = { DataFormat = HslCommunication.Core.DataFormat.DCBA }
                };

                OperateResult result = _omronFins.ConnectServer();
                if (result.IsSuccess)
                {
                    Connected = true;
                    HandshakeCompleted = true;
                    PLCState = PLCState.Connected;
                    Logs.LogInfo("[PLC] 重连成功");
                    return true;
                }

                Logs.LogError($"[PLC] 重连失败：{result.Message}");
                return false;
            }
            catch (Exception ex)
            {
                Logs.LogError(ex, "[PLC] 重连异常");
                return false;
            }
        }
        #endregion

        #region 后台任务（心跳、重连守护）
        private void StartBackgroundTasks()
        {
            lock (_taskLock)
            {
                if (_plcConfig.HeartbeatIsOpen)
                {
                    if (_heartbeatTask == null || _heartbeatTask.IsCompleted)
                        _heartbeatTask = Task.Run(HeartbeatTaskAsync, _cancellationTokenSource.Token);
                }               
                if (_reconnectTask == null || _reconnectTask.IsCompleted)
                    _reconnectTask = Task.Factory.StartNew(ReconnectGuardTask, _cancellationTokenSource.Token,
                        TaskCreationOptions.LongRunning, TaskScheduler.Default);
            }
        }

        private void StopBackgroundTasks()
        {
            lock (_taskLock)
            {
                _cancellationTokenSource.Cancel();
                var tasks = new[] { _heartbeatTask, _reconnectTask }.Where(t => t != null).ToArray();
                if (tasks.Length > 0)
                    Task.WaitAll(tasks, TimeSpan.FromSeconds(3));
                _cancellationTokenSource = new CancellationTokenSource();
                _heartbeatTask = _reconnectTask = null;
            }
        }

        private async Task HeartbeatTaskAsync()
        {
            bool isSetOn = true;
            while (!_cancellationTokenSource.IsCancellationRequested)
            {
                try
                {
                    if (!HandshakeCompleted || _plcConfig == null || _plcConfig.HeartbeatInterval <= 0)
                    {
                        await Task.Delay(1000);
                        continue;
                    }

                    int heartbeatValue = isSetOn ? 1 : 0;
                    ushort writeValue = ConvertToPlcValue(heartbeatValue, false);
                    string addr = GetAddressString(_plcConfig.HeartbeatRegisterType, _plcConfig.HeartbeatAddress);

                    // 使用库的 Write 发送心跳（带响应），不阻塞其他操作
                    await Task.Run(() => _omronFins?.Write(addr, writeValue));
                    isSetOn = !isSetOn;
                    await Task.Delay(_plcConfig.HeartbeatInterval);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    Logs.LogError(ex, "[PLC] 心跳任务异常");
                    await Task.Delay(1000);
                }
            }
        }

        private void ReconnectGuardTask()
        {
            while (!_cancellationTokenSource.IsCancellationRequested)
            {
                try
                {
                    if (HandshakeCompleted || !Initialized || _plcConfig == null)
                    {
                        _reconnectCount = 0;
                        Thread.Sleep(1000);
                        continue;
                    }

                    if (_plcConfig.MaxReconnectCount > 0 && _reconnectCount >= _plcConfig.MaxReconnectCount)
                    {
                        string err = $"PLC重连次数超限（最大：{_plcConfig.MaxReconnectCount}）";
                        Message = err;
                        Logs.LogError("[PLC] " + err);
                        Initialized = false;
                        PLCState = PLCState.UnInitialized;
                        Thread.Sleep(1000);
                        continue;
                    }

                    Thread.Sleep(1000);
                    _reconnectCount++;
                    PLCState = PLCState.Reconnecting;
                    Logs.LogInfo($"[PLC] 重连中（第{_reconnectCount}次）...");

                    if (TryReconnect())
                    {
                        _reconnectCount = 0;
                        StartBackgroundTasks(); // 重启心跳等
                    }
                    else
                    {
                        Thread.Sleep(_plcConfig.ReconnectDelay);
                    }
                }
                catch (Exception ex)
                {
                    Logs.LogError(ex, "[PLC] 重连守护异常");
                    Thread.Sleep(_plcConfig.ReconnectDelay);
                }
            }
        }
        #endregion

        #region 辅助方法
        private bool CheckBaseState()
        {
            if (!Initialized || !Connected)
            {
                // 外部会记录日志，此处仅返回 false
                return false;
            }
            return true;
        }

        // 将寄存器类型和编号转为库所需的地址字符串
        private string GetAddressString(PLCRegisterType type, ushort address)
        {
            switch (type)
            {
                case PLCRegisterType.DM: return $"D{address}";
                case PLCRegisterType.CIO: return $"C{address}";
                case PLCRegisterType.TIM: return $"T{address}";
                case PLCRegisterType.CNTR: return $"CNT{address}";
                default: return $"D{address}";
            }
        }

        private ushort ConvertToPlcValue(int value, bool isBCD)
        {
            int clamped = Math.Max(0, Math.Min(value, 65535));
            if (isBCD)
            {
                clamped = Math.Min(clamped, 9999);
                return (ushort)(((clamped / 1000) << 12) |
                               ((clamped / 100 % 10) << 8) |
                               ((clamped / 10 % 10) << 4) |
                               (clamped % 10));
            }
            return (ushort)clamped;
        }

        private int ConvertFromPlcValue(ushort raw, bool isBCD)
        {
            if (isBCD)
            {
                return ((raw >> 12) & 0x0F) * 1000 +
                       ((raw >> 8) & 0x0F) * 100 +
                       ((raw >> 4) & 0x0F) * 10 +
                       (raw & 0x0F);
            }
            return raw;
        }
        #endregion
    }
}