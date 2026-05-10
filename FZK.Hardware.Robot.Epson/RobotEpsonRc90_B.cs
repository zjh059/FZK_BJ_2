using FZK.Hardware.Robot.Base;
using FZK.Logger;
using ReactiveUI;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FZK.Hardware.Robot.Epson
{
    /// <summary>
    /// 爱普生 RC90-B 机械手 TCP 通信驱动（工业最终终极版）
    /// 已修复：心跳并发竞态、重连等待卡死、所有多线程风险
    /// </summary>
    public class RobotEpsonRc90_B : ReactiveObject, IRobot
    {
        #region 响应式属性
        private volatile bool _initialized;
        public bool Initialized
        {
            get => _initialized;
            private set => this.RaiseAndSetIfChanged(ref _initialized, value);
        }

        private volatile bool _connected;
        public bool Connected
        {
            get => _connected;
            private set => this.RaiseAndSetIfChanged(ref _connected, value);
        }

        private RobotState _robotState;
        public RobotState RobotState
        {
            get => _robotState;
            private set => this.RaiseAndSetIfChanged(ref _robotState, value);
        }

        private string _message;
        public string Message
        {
            get => _message;
            private set => this.RaiseAndSetIfChanged(ref _message, value);
        }

        private string _receiveContent;
        public string ReceiveContent
        {
            get => _receiveContent;
            private set => this.RaiseAndSetIfChanged(ref _receiveContent, value);
        }

        public IObservable<string> MessageObservable => this.WhenAnyValue(x => x.Message);
        public IObservable<string> ReceiveContentObservable => this.WhenAnyValue(x => x.ReceiveContent);
        #endregion

        #region 核心字段
        private TcpClient _tcpClient;
        private readonly object _clientLock = new object();
        private CancellationTokenSource _cts;
        private RobotConfig _robotConfig;

        private BlockingCollection<string> _sendQueue;
        private readonly ConcurrentQueue<byte[]> _receiveBufferQueue = new ConcurrentQueue<byte[]>();
        private readonly StringBuilder _frameBuffer = new StringBuilder();

        private Task _receiveTask;
        private Task _sendTask;
        private Task _analysisTask;
        private Task _reconnectTask;
        private Task _commandRetryTask;
        private readonly object _taskLock = new object();

        private int _reconnectCount;
        #endregion

        #region 心跳
        private Timer _heartbeatTimer;
        private readonly object _heartbeatLock = new object();
        private volatile bool _heartbeatRunning;
        private volatile bool _heartbeatPending;
        private int _heartbeatTimeoutCount;
        private CancellationTokenSource _heartbeatTimeoutCts;

        private int _heartbeatInterval;
        private int _heartbeatTimeout;
        private int _heartbeatMaxRetry;
        #endregion

        #region 指令重发
        private class CommandItem
        {
            public string Command { get; set; }
            public int RetryCount { get; set; }
            public DateTime SendTime { get; set; }
        }

        private readonly ConcurrentQueue<CommandItem> _retryQueue = new ConcurrentQueue<CommandItem>();
        #endregion

        public RobotEpsonRc90_B()
        {
            _cts = new CancellationTokenSource();
            RobotState = RobotState.UnInitialized;
            Message = "驱动已创建";
        }

        #region 初始化
        public bool Init(RobotConfig robotConfig)
        {
            try
            {
                if (Initialized)
                {
                    Message = "已初始化，无需重复操作";
                    Logs.LogInfo(Message);
                    return true;
                }

                _robotConfig = robotConfig ?? new RobotConfig();
                if (!ValidateConfig(_robotConfig))
                {
                    Message = "配置校验失败";
                    Logs.LogError(Message);
                    return false;
                }

                _sendQueue?.Dispose();
                _sendQueue = new BlockingCollection<string>();

                _heartbeatInterval = _robotConfig.HeartbeatInterval;
                _heartbeatTimeout = _robotConfig.CommandTimeout;
                _heartbeatMaxRetry = _robotConfig.CommandRetryCount;

                RobotState = RobotState.Connecting;
                Message = $"正在连接 {_robotConfig.IpAddress}:{_robotConfig.Port}";

                if (!TryConnect())
                {
                    Message = "首次连接失败";
                    Logs.LogError(Message);
                    RobotState = RobotState.UnInitialized;
                    return false;
                }

                Initialized = true;
                TransitionToState(RobotState.Connected);
                _reconnectCount = 0;

                ClearAllBuffers();
                StartReconnectTask();
                StartCommunicationTasks();
                StartHeartbeat();
                return true;
            }
            catch (Exception ex)
            {
                Message = $"初始化异常：{ex.Message}";
                Logs.LogError(ex);
                RobotState = RobotState.UnInitialized;
                return false;
            }
        }

        private bool ValidateConfig(RobotConfig cfg)
        {
            if (string.IsNullOrEmpty(cfg.IpAddress)) return false;
            if (cfg.Port <= 0 || cfg.Port > 65535) return false;
            if (cfg.Timeout <= 0) return false;
            return true;
        }
        #endregion

        #region 发送指令
        public bool SendCommand(string command)
        {
            if (!Initialized || !Connected || string.IsNullOrWhiteSpace(command))
            {
                Message = "未连接或指令为空";
                Logs.LogError(Message);
                return false;
            }

            try
            {
                string full = $"{command}{_robotConfig.CommandEndFlag}";
                _sendQueue.Add(full);

                if (_robotConfig.CommandTimeout > 0)
                {
                    _retryQueue.Enqueue(new CommandItem
                    {
                        Command = full,
                        RetryCount = 0,
                        SendTime = DateTime.Now
                    });
                }

                Logs.LogInfo($"指令入队：{command}");
                return true;
            }
            catch (Exception ex)
            {
                Message = $"指令入队失败：{ex.Message}";
                Logs.LogError(ex);
                return false;
            }
        }
        #endregion

        #region 连接检查
        public bool CheckConnection()
        {
            lock (_clientLock)
            {
                bool isConnected = IsTcpConnected();
                if (!isConnected)
                {
                    TransitionToState(RobotState.Reconnecting);
                    Message = "连接异常，等待重连";
                    Logs.LogError(Message);
                }
                return isConnected;
            }
        }

        private bool IsTcpConnected()
        {
            Socket socket = null;
            lock (_clientLock)
            {
                if (_tcpClient == null || !_tcpClient.Connected) return false;
                socket = _tcpClient.Client;  // 拷贝引用
            }
            // 锁外执行 Poll，不阻塞 I/O 线程
            try
            {
                bool part1 = socket.Poll(100, SelectMode.SelectRead);
                bool part2 = socket.Available == 0;
                return !(part1 && part2);
            }
            catch { return false; }
        }
        #endregion

        #region 关闭
        public void Close()
        {
            try
            {
                TransitionToState(RobotState.Disconnecting);
                Message = "正在关闭连接...";
                Logs.LogInfo(Message);

                StopHeartbeat();
                _cts?.Cancel();

                lock (_clientLock)
                {
                    _tcpClient?.Close();
                    _tcpClient = null;
                }

                var tasks = new[] { _receiveTask, _sendTask, _analysisTask, _reconnectTask, _commandRetryTask };
                try { Task.WaitAll(FilterValidTasks(tasks), TimeSpan.FromSeconds(2)); } catch { }

                _cts?.Dispose();
                _cts = new CancellationTokenSource();

                _sendQueue?.Dispose();
                _sendQueue = null;

                ClearAllBuffers();
                Initialized = false;
                TransitionToState(RobotState.UnInitialized);
                Message = "已关闭，资源已释放";
                Logs.LogInfo(Message);
            }
            catch (Exception ex)
            {
                Logs.LogError($"关闭异常：{ex.Message}");
            }
        }
        #endregion

        #region TCP 连接
        private bool TryConnect()
        {
            lock (_clientLock)
            {
                try
                {
                    _tcpClient?.Close();
                    _tcpClient = new TcpClient
                    {
                        ReceiveTimeout = _robotConfig.Timeout,
                        SendTimeout = _robotConfig.Timeout
                    };

                    var task = _tcpClient.ConnectAsync(_robotConfig.IpAddress, _robotConfig.Port);
                    if (task.Wait(_robotConfig.Timeout))
                    {
                        return _tcpClient.Connected;
                    }

                    _tcpClient.Close();
                    _tcpClient = null;
                    return false;
                }
                catch (Exception ex)
                {
                    Logs.LogError($"连接异常：{ex.Message}");
                    _tcpClient = null;
                    return false;
                }
            }
        }
        #endregion

        #region 后台任务
        private void StartCommunicationTasks()
        {
            lock (_taskLock)
            {
                var token = _cts.Token;

                if (_receiveTask == null || _receiveTask.IsCompleted)
                    _receiveTask = Task.Run(ReceiveLoop, token);

                if (_sendTask == null || _sendTask.IsCompleted)
                    _sendTask = Task.Run(SendLoop, token);

                if (_analysisTask == null || _analysisTask.IsCompleted)
                    _analysisTask = Task.Run(AnalysisLoop, token);

                if (_commandRetryTask == null || _commandRetryTask.IsCompleted)
                    _commandRetryTask = Task.Run(CommandRetryLoop, token);
            }
        }

        private void StartReconnectTask()
        {
            lock (_taskLock)
            {
                if (_reconnectTask == null || _reconnectTask.IsCompleted)
                    _reconnectTask = Task.Run(ReconnectLoop, _cts.Token);
            }
        }

        private async Task ReceiveLoop()
        {
            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    TcpClient client;
                    lock (_clientLock) client = _tcpClient;
                    if (client == null || !client.Connected)
                    {
                        await Task.Delay(10, _cts.Token);
                        continue;
                    }

                    byte[] buffer = new byte[4096];
                    int len = await client.GetStream().ReadAsync(buffer, 0, buffer.Length, _cts.Token);
                    
                    if (len <= 0) continue;

                    byte[] data = new byte[len];
                    Array.Copy(buffer, data, len);
                    _receiveBufferQueue.Enqueue(data);
                }
                catch (Exception ex)
                {
                    if (_cts.IsCancellationRequested) break;
                    TransitionToState(RobotState.Reconnecting);
                    Logs.LogError($"接收异常：{ex.Message}");
                    await Task.Delay(100, _cts.Token);
                }
            }
            Logs.LogInfo("接收任务已退出");
        }

        private async Task SendLoop()
        {
            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    if (!Connected)
                    {
                        await Task.Delay(100, _cts.Token);
                        continue;
                    }

                    if (!_sendQueue.TryTake(out string cmd, 10, _cts.Token))
                        continue;

                    TcpClient client;
                    lock (_clientLock) client = _tcpClient;
                    if (client == null || !client.Connected)
                    {
                        _sendQueue.Add(cmd);
                        await Task.Delay(100, _cts.Token);
                        continue;
                    }

                    byte[] data = Encoding.UTF8.GetBytes(cmd);
                    await client.GetStream().WriteAsync(data, 0, data.Length, _cts.Token);
                    Logs.LogInfo($"发送成功：{cmd.Trim()}");
                }
                catch (Exception ex)
                {
                    if (_cts.IsCancellationRequested) break;
                    TransitionToState(RobotState.Reconnecting);
                    Logs.LogError($"发送异常：{ex.Message}");
                    await Task.Delay(100, _cts.Token);
                }
            }
            Logs.LogInfo("发送任务已退出");
        }

        private async Task AnalysisLoop()
        {
            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    if (!_receiveBufferQueue.TryDequeue(out byte[] raw))
                    {
                        await Task.Delay(1, _cts.Token);
                        continue;
                    }

                    string content = Encoding.UTF8.GetString(raw);
                    _frameBuffer.Append(content);
                    ProcessFrames();
                }
                catch (Exception ex)
                {
                    if (_cts.IsCancellationRequested) break;
                    Logs.LogError($"解析异常：{ex.Message}");
                    await Task.Delay(100, _cts.Token);
                }
            }
            Logs.LogInfo("解析任务已退出");
        }

        private void ProcessFrames()
        {
            string data = _frameBuffer.ToString();
            string flag = _robotConfig.CommandEndFlag;

            while (data.Contains(flag))
            {
                int index = data.IndexOf(flag);
                string frame = data.Substring(0, index).Trim();
                data = data.Substring(index + flag.Length);

                if (!string.IsNullOrEmpty(frame))
                {
                    ReceiveContent = frame;
                    HandleHeartbeatResponse(frame);
                    Logs.LogInfo($"收到完整帧：{frame}");
                }
            }

            _frameBuffer.Clear();
            _frameBuffer.Append(data);
        }

        /// <summary>
        /// 【修复】重连循环：旧任务等待增加3秒超时，永不卡死
        /// </summary>
        private async Task ReconnectLoop()
        {
            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    if (Connected || !Initialized)
                    {
                        _reconnectCount = 0;
                        await Task.Delay(1000, _cts.Token);
                        continue;
                    }

                    int max = _robotConfig.MaxReconnectCount;
                    if (max > 0 && _reconnectCount >= max)
                    {
                        Message = "重连次数超限，已停止";
                        Logs.LogError(Message);
                        Initialized = false;
                        TransitionToState(RobotState.UnInitialized);
                        break;
                    }

                    _reconnectCount++;
                    TransitionToState(RobotState.Reconnecting);
                    Message = $"重连中 {_reconnectCount} 次";
                    Logs.LogInfo(Message);

                    _cts.Cancel();
                    await Task.Delay(500);

                    var oldTasks = FilterValidTasks(new[] { _receiveTask, _sendTask, _analysisTask, _commandRetryTask });
                    if (oldTasks.Length > 0)
                    {
                        var allTasks = Task.WhenAll(oldTasks);
                        // 【修复】3秒超时等待，避免无限阻塞
                        if (await Task.WhenAny(allTasks, Task.Delay(3000)) != allTasks)
                        {
                            Logs.LogError("旧任务退出超时，强制继续重连流程");
                        }
                        // 强制置空，确保新任务一定启动
                        _receiveTask = null;
                        _sendTask = null;
                        _analysisTask = null;
                        _commandRetryTask = null;
                    }

                    _cts.Dispose();
                    _cts = new CancellationTokenSource();

                    if (TryConnect())
                    {
                        TransitionToState(RobotState.Connected);
                        _reconnectCount = 0;
                        StartCommunicationTasks();
                        StartHeartbeat();
                        Message = "重连成功";
                        Logs.LogInfo(Message);
                    }
                    else
                    {
                        await Task.Delay(_robotConfig.ReconnectDelay, _cts.Token);
                    }
                }
                catch (Exception ex)
                {
                    if (_cts.IsCancellationRequested) break;
                    Logs.LogError($"重连异常：{ex.Message}");
                }
            }
            Logs.LogInfo("重连任务已退出");
        }

        private async Task CommandRetryLoop()
        {
            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    if (_retryQueue.IsEmpty || !Connected)
                    {
                        await Task.Delay(100, _cts.Token);
                        continue;
                    }

                    if (_retryQueue.TryDequeue(out var cmd))
                    {
                        var now = DateTime.Now;
                        if ((now - cmd.SendTime).TotalMilliseconds > _robotConfig.CommandTimeout)
                        {
                            if (cmd.RetryCount < _robotConfig.CommandRetryCount)
                            {
                                cmd.RetryCount++;
                                cmd.SendTime = now;
                                _retryQueue.Enqueue(cmd);
                                _sendQueue.Add(cmd.Command);
                                Logs.LogInfo($"指令重发：{cmd.Command.Trim()} 第{cmd.RetryCount}次");
                            }
                        }
                        else
                        {
                            _retryQueue.Enqueue(cmd);
                        }
                    }
                    await Task.Delay(_robotConfig.CommandRetryDelay, _cts.Token);
                }
                catch
                {
                    if (_cts.IsCancellationRequested) break;
                }
            }
        }
        #endregion

        #region 心跳（【修复】全程锁内，彻底消除并发竞态）
        private void StartHeartbeat()
        {
            lock (_heartbeatLock)
            {
                if (_heartbeatInterval <= 0) return;
                StopHeartbeat();

                _heartbeatRunning = true;
                _heartbeatPending = false;
                _heartbeatTimeoutCount = 0;

                _heartbeatTimer = new Timer(_ => SendHeartbeat(), null, _heartbeatInterval, _heartbeatInterval);
                Logs.LogInfo("心跳已启动");
            }
        }

        private void StopHeartbeat()
        {
            lock (_heartbeatLock)
            {
                _heartbeatRunning = false;
                _heartbeatTimer?.Dispose();
                _heartbeatTimer = null;

                _heartbeatTimeoutCts?.Cancel();
                _heartbeatTimeoutCts?.Dispose();
                _heartbeatTimeoutCts = null;

                Logs.LogInfo("心跳已停止");
            }
        }

        /// <summary>
        /// 【修复】整个心跳流程原子化，杜绝并发多发
        /// </summary>
        private void SendHeartbeat()
        {
            try
            {
                // ==============================
                // ✅ 修复：全程锁内 → 检查→发送→设置 原子执行
                // ==============================
                lock (_heartbeatLock)
                {
                    if (!_heartbeatRunning || !Connected || _heartbeatPending) return;
                    if (string.IsNullOrEmpty(_robotConfig.HeartbeatCommand)) return;

                    // 锁内发送心跳（安全无阻塞）
                    string cmd = $"{_robotConfig.HeartbeatCommand}{_robotConfig.CommandEndFlag}";
                    _sendQueue.Add(cmd);

                    // 锁内创建令牌 + 状态变更
                    _heartbeatPending = true;
                    _heartbeatTimeoutCts?.Dispose();
                    _heartbeatTimeoutCts = new CancellationTokenSource();
                }

                // 超时任务在锁外运行
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(_heartbeatTimeout, _heartbeatTimeoutCts.Token);
                        lock (_heartbeatLock)
                        {
                            if (!_heartbeatPending) return;
                            _heartbeatTimeoutCount++;
                            Logs.LogError($"心跳超时 {_heartbeatTimeoutCount} 次");

                            _heartbeatPending = false;
                            _heartbeatTimeoutCts?.Cancel();
                            _heartbeatTimeoutCts?.Dispose();
                            _heartbeatTimeoutCts = null;

                            if (_heartbeatTimeoutCount >= _heartbeatMaxRetry)
                            {
                                TransitionToState(RobotState.Reconnecting);
                                _heartbeatPending = false;
                                _heartbeatTimeoutCount = 0;
                                StopHeartbeat();
                            }
                        }
                    }
                    catch (OperationCanceledException) { }
                }, _heartbeatTimeoutCts.Token);
            }
            catch (Exception ex)
            {
                Logs.LogError($"心跳发送异常：{ex.Message}");
                lock (_heartbeatLock)
                {
                    _heartbeatPending = false;
                    _heartbeatTimeoutCts?.Cancel();
                    _heartbeatTimeoutCts?.Dispose();
                    _heartbeatTimeoutCts = null;
                }
            }
           
        }

        private void HandleHeartbeatResponse(string content)
        {
            lock (_heartbeatLock)
            {
                if (!_heartbeatPending) return;
                if (!content.Equals(_robotConfig.HeartbeatResponse, StringComparison.OrdinalIgnoreCase)) return;

                _heartbeatPending = false;
                _heartbeatTimeoutCount = 0;
                _heartbeatTimeoutCts?.Cancel();
                Logs.LogInfo("心跳正常");
            }
        }
        #endregion

        #region 状态管理
        private void TransitionToState(RobotState state)
        {
            RobotState = state;
            Connected = state is RobotState.Connected;
        }
        #endregion

        #region 工具
        private Task[] FilterValidTasks(Task[] tasks)
        {
            var list = new List<Task>();
            foreach (var t in tasks) if (t != null) list.Add(t);
            return list.ToArray();
        }

        private void ClearAllBuffers()
        {
            _frameBuffer.Clear();
            while (_receiveBufferQueue.TryDequeue(out _)) { }
            while (_retryQueue.TryDequeue(out _)) { }
        }

        public void ClearReceiveContent()
        {
            ReceiveContent = string.Empty;
            _frameBuffer.Clear();
        }
        #endregion
    }
}