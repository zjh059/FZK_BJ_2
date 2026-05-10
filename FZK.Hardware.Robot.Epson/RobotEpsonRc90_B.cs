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
    /// 爱普生 RC90-B 机械手 TCP 通信驱动（高可用版本）
    /// 设计要点：
    /// - 完全线程安全，支持多线程并发调用
    /// - 后台任务统一管理，重连时自动重启任务
    /// - 响应式属性实时通知状态变化
    /// </summary>
    public class RobotEpsonRc90_B : ReactiveObject, IRobot
    {
        #region 响应式属性（使用 RaiseAndSetIfChanged 触发变更通知）

        private volatile bool _initialized;
        public bool Initialized
        {
            get => _initialized;
            set => this.RaiseAndSetIfChanged(ref _initialized, value);
        }

        private volatile bool _connected;
        public bool Connected
        {
            get => _connected;
            set => this.RaiseAndSetIfChanged(ref _connected, value);
        }

        private volatile RobotState _robotState;
        public RobotState RobotState
        {
            get => _robotState;
            set => this.RaiseAndSetIfChanged(ref _robotState, value);
        }

        private string _message;
        public string Message
        {
            get => _message;
            set => this.RaiseAndSetIfChanged(ref _message, value);
        }

        private string _receiveContent;
        public string ReceiveContent
        {
            get => _receiveContent;
            set => this.RaiseAndSetIfChanged(ref _receiveContent, value);
        }

        public IObservable<string> MessageObservable => this.WhenAnyValue(x => x.Message);
        public IObservable<string> ReceiveContentObservable => this.WhenAnyValue(x => x.ReceiveContent);

        #endregion

        #region 私有核心字段

        private TcpClient _tcpClient;
        private CancellationTokenSource _cancellationTokenSource;
        private RobotConfig _robotConfig;
        private readonly ConcurrentQueue<string> _sendQueue = new ConcurrentQueue<string>();
        private readonly ConcurrentQueue<byte[]> _receiveQueue = new ConcurrentQueue<byte[]>();

        private Task _receiveTask;
        private Task _sendTask;
        private Task _analysisTask;
        private Task _reconnectTask;
        private readonly object _taskLock = new object();

        private int _reconnectCount;
        private readonly byte[] _receiveBuffer = new byte[4096];

        #region 新增：心跳检测相关字段

        private Timer _heartbeatTimer;
        /// <summary>
        /// 是否等待心跳响应
        /// </summary>
        private volatile bool _heartbeatPending;  
        /// <summary>
        /// 连续超时次数
        /// </summary>
        private int _heartbeatTimeoutCount;       
        private readonly object _heartbeatLock = new object();
        /// <summary>
        /// 心跳发送间隔
        /// </summary>
        private int _heartbeatInterval;
        /// <summary>
        /// 心跳超时时间 
        /// </summary>
        private int _heartbeatTimeout = 5000;     
        /// <summary>
        /// 最大失败次数，超过则判定断线
        /// </summary>
        private int _heartbeatMaxRetry = 3;

        #endregion

        #endregion

        #region 构造函数

        public RobotEpsonRc90_B()
        {
            _cancellationTokenSource = new CancellationTokenSource();
            RobotState = RobotState.UnInitialized;
            Message = "机械手驱动已实例化";
        }

        #endregion

        #region 核心接口实现：初始化

        public bool Init(RobotConfig robotConfig)
        {
            try
            {
              
                if (Initialized)
                {
                    Message = "机械手已完成初始化，无需重复执行";
                    Logs.LogInfo(Message);
                    return true;
                }

                // 统一处理配置：若为 null 则使用默认值，并记录日志
                _robotConfig = robotConfig ?? new RobotConfig();
                if (robotConfig == null)
                {
                    Message = "机械手配置为空，使用默认设置";
                    Logs.LogInfo(Message);
                }
                _heartbeatInterval = robotConfig.HeartbeatInterval;
                _heartbeatTimeout = robotConfig.CommandTimeout;
                _heartbeatMaxRetry = robotConfig.CommandRetryCount;


                RobotState = RobotState.Connecting;



                Message = $"正在连接机械手：{_robotConfig.IpAddress}:{_robotConfig.Port}";
                Logs.LogInfo(Message);

                if (!TryConnect())   // 调用真正的连接方法
                {
                    Message = $"首次连接机械手失败：{_robotConfig.IpAddress}:{_robotConfig.Port}";
                    Logs.LogError(Message);
                    RobotState = RobotState.UnInitialized;
                    return false;
                }

                Initialized = true;
                Connected = true;
                RobotState = RobotState.Connected;
                Message = $"机械手连接成功：{_robotConfig.IpAddress}:{_robotConfig.Port}";
                Logs.LogInfo(Message);
                _reconnectCount = 0;

                StartBackgroundTasks();
                StartHeartbeat();
                return true;
            }
            catch (Exception ex)
            {
                Message = $"机械手初始化异常：{ex.Message}";
                Logs.LogError(ex);
                RobotState = RobotState.UnInitialized;
                return false;
            }
        }

        #endregion

        #region 核心接口实现：发送指令

        public bool SendCommand(string command)
        {
            if (!Initialized || !Connected || string.IsNullOrWhiteSpace(command))
            {
                string errorMsg = !Connected ? "机械手未连接，指令发送失败" : "指令为空，发送失败";
                Message = errorMsg;
                Logs.LogError(errorMsg);
                return false;
            }

            try
            {
                string fullCommand = $"{command}{_robotConfig.CommandEndFlag}";
                _sendQueue.Enqueue(fullCommand);
                Logs.LogInfo($"机械手指令入队成功：{command}（完整指令：{fullCommand.Trim()}）");
                return true;
            }
            catch (Exception ex)
            {
                string errorMsg = $"机械手指令入队失败：{ex.Message}，指令内容：{command}";
                Message = errorMsg;
                Logs.LogError(ex);
                return false;
            }
        }

        #endregion

        #region 核心接口实现：检查连接

        public bool CheckConnection()
        {

            if (_heartbeatPending && _heartbeatTimeoutCount > 0)
            {
                Logs.LogError($"心跳状态异常：待响应={_heartbeatPending}，超时次数={_heartbeatTimeoutCount}");
                return false;
            }

            if (Connected && _tcpClient != null && _tcpClient.Connected)
                return true;

            RobotState = RobotState.Reconnecting;
            Message = "机械手连接异常，正在手动重连";
            Logs.LogError(Message);
            return TryConnect();
        }


        #endregion

        #region 核心接口实现：关闭连接

        public void Close()
        {
            try
            {
                RobotState = RobotState.Disconnecting;
                Message = "正在关闭机械手连接，释放资源...";
                Logs.LogInfo(Message);
                StopHeartbeat();
                // 1. 取消所有后台任务
                _cancellationTokenSource.Cancel();

                // 2. 关闭 TCP 连接以中断阻塞的 I/O
                _tcpClient?.Close();

                // 3. 等待所有任务退出（最多 5 秒）
                var tasks = new[] { _receiveTask, _sendTask, _analysisTask, _reconnectTask };
                Task.WaitAll(FilterNullTasks(tasks), TimeSpan.FromSeconds(5));

                // 4. 清空队列
                ClearQueue(_sendQueue);
                ClearQueue(_receiveQueue);

                // 5. 释放资源
                _cancellationTokenSource.Dispose();
                _cancellationTokenSource = new CancellationTokenSource();
                _tcpClient = null;

                // 6. 重置状态
                Initialized = false;
                Connected = false;
                _reconnectCount = 0;
                RobotState = RobotState.UnInitialized;
                Message = "机械手连接已关闭，资源释放完成";
                Logs.LogInfo(Message);
            }
            catch (Exception ex)
            {
                Message = $"关闭机械手连接异常：{ex.Message}";
                Logs.LogError(ex);
            }
        }

        #endregion

        #region 私有核心方法：TCP 连接

        private bool TryConnect()
        {
            try
            {
                // 如果已有客户端且已连接，直接返回成功
                if (_tcpClient != null && _tcpClient.Connected)
                    return true;

                // 清理旧客户端（如有）
                _tcpClient?.Close();
                _tcpClient = new TcpClient
                {
                    ReceiveTimeout = _robotConfig.Timeout,
                    SendTimeout = _robotConfig.Timeout
                };

                // 异步连接并等待超时
                var connectTask = _tcpClient.ConnectAsync(_robotConfig.IpAddress, _robotConfig.Port);
                if (connectTask.Wait(TimeSpan.FromMilliseconds(_robotConfig.Timeout)))
                {
                    return _tcpClient.Connected;
                }

                // 超时：关闭客户端并置空
                _tcpClient.Close();
                _tcpClient = null;
                return false;
            }
            catch
            {
                // 发生异常时清理客户端
                _tcpClient?.Close();
                _tcpClient = null;
                return false;
            }
        }

        #endregion

        #region 后台任务管理

        private void StartBackgroundTasks()
        {
            lock (_taskLock)
            {
                var token = _cancellationTokenSource.Token;

                if (_receiveTask == null || _receiveTask.IsCompleted)
                    _receiveTask = Task.Factory.StartNew(ReceiveTask, token, TaskCreationOptions.LongRunning, TaskScheduler.Default);

                if (_sendTask == null || _sendTask.IsCompleted)
                    _sendTask = Task.Factory.StartNew(SendTask, token, TaskCreationOptions.LongRunning, TaskScheduler.Default);

                if (_analysisTask == null || _analysisTask.IsCompleted)
                    _analysisTask = Task.Factory.StartNew(AnalysisTask, token, TaskCreationOptions.LongRunning, TaskScheduler.Default);

                if (_reconnectTask == null || _reconnectTask.IsCompleted)
                    _reconnectTask = Task.Factory.StartNew(ReconnectTask, token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            }
        }

        private Task[] FilterNullTasks(Task[] tasks)
        {
            var list = new List<Task>();
            foreach (var t in tasks)
                if (t != null) list.Add(t);
            return list.ToArray();
        }

        #endregion

        #region 后台任务：接收数据

        private void ReceiveTask()
        {
            var token = _cancellationTokenSource.Token;
            while (!token.IsCancellationRequested)
            {
                TcpClient client = _tcpClient;
                if (client == null || !client.Connected)
                {
                    Thread.Sleep(10);
                    continue;
                }

                try
                {
                    if (client.Available <= 0)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    int received = client.Client.Receive(_receiveBuffer);
                    if (received <= 0) continue;

                    byte[] data = new byte[received];
                    Array.Copy(_receiveBuffer, data, received);
                    _receiveQueue.Enqueue(data);
                }
                catch (Exception ex)
                {
                    if (token.IsCancellationRequested)
                        break;

                    Connected = false;
                    Logs.LogError($"接收异常：{ex.Message}");
                    Thread.Sleep(100);
                }
            }
            Logs.LogInfo("接收任务已终止");
        }

        #endregion

        #region 后台任务：发送指令

        private void SendTask()
        {
            var token = _cancellationTokenSource.Token;
            while (!token.IsCancellationRequested)
            {
                TcpClient client = _tcpClient;
                if (client == null || !client.Connected || _sendQueue.IsEmpty)
                {
                    Thread.Sleep(1);
                    continue;
                }

                try
                {
                    if (_sendQueue.TryDequeue(out string command))
                    {
                        byte[] data = Encoding.UTF8.GetBytes(command);
                        client.Client.Send(data);
                        Logs.LogInfo($"发送指令成功：{command.Trim()} (长度：{data.Length})");
                    }
                }
                catch (Exception ex)
                {
                    if (token.IsCancellationRequested)
                        break;

                    Connected = false;
                    Logs.LogError($"发送异常：{ex.Message}");
                    Thread.Sleep(100);
                }
            }
            Logs.LogInfo("发送任务已终止");
        }

        #endregion

        #region 后台任务：解析数据

        private void AnalysisTask()
        {
            var token = _cancellationTokenSource.Token;
            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (_receiveQueue.IsEmpty)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    if (_receiveQueue.TryDequeue(out byte[] raw))
                    {
                        string content = Encoding.UTF8.GetString(raw).Trim();
                        if (!string.IsNullOrEmpty(content))
                        {
                            ReceiveContent = content;
                            HandleHeartbeatResponse(content);
                            Logs.LogInfo($"收到数据：{content} (长度：{raw.Length})");
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (token.IsCancellationRequested)
                        break;

                    Logs.LogError($"解析异常：{ex.Message}");
                    Thread.Sleep(100);
                }
            }
            Logs.LogInfo("解析任务已终止");
        }

        #endregion

        #region 后台任务：断线重连

        private void ReconnectTask()
        {
            var token = _cancellationTokenSource.Token;
            while (!token.IsCancellationRequested)
            {
                try
                {
                    // 如果已连接或未初始化，重置计数并休眠
                    if (Connected || !Initialized || _robotConfig == null)
                    {
                        _reconnectCount = 0;
                        Thread.Sleep(1000);
                        continue;
                    }

                    if (_robotConfig.MaxReconnectCount > 0 && _reconnectCount >= _robotConfig.MaxReconnectCount)
                    {
                        Initialized = false;
                        RobotState = RobotState.UnInitialized;
                        Message = $"重连次数超限（{_robotConfig.MaxReconnectCount}），停止重连";
                        Logs.LogError(Message);
                        Thread.Sleep(1000);
                        continue;
                    }

                    _reconnectCount++;
                    RobotState = RobotState.Reconnecting;
                    Message = $"重连中（第{_reconnectCount}次）...";
                    Logs.LogError(Message);

                    // 1. 取消现有任务
                    _cancellationTokenSource.Cancel();

                    // 2. 关闭 TCP 连接以中断阻塞 I/O
                    _tcpClient?.Close();

                    // 3. 等待现有任务退出
                    var tasks = new[] { _receiveTask, _sendTask, _analysisTask };
                    Task.WaitAll(FilterNullTasks(tasks), TimeSpan.FromSeconds(5));

                    // 4. 重建 CancellationTokenSource
                    _cancellationTokenSource = new CancellationTokenSource();

                    // 5. 尝试重新连接
                    if (TryConnect())
                    {
                        Connected = true;
                        RobotState = RobotState.Connected;
                        Message = $"重连成功（第{_reconnectCount}次）";
                        Logs.LogInfo(Message);
                        _reconnectCount = 0;
                        // 6. 重新启动后台任务
                        StartBackgroundTasks();
                    }
                    else
                    {
                        Connected = false;
                        Message = $"重连失败（第{_reconnectCount}次）";
                        Logs.LogError(Message);
                        Thread.Sleep(_robotConfig.ReconnectDelay);
                    }
                }
                catch (Exception ex)
                {
                    if (token.IsCancellationRequested)
                        break;

                    Logs.LogError($"重连任务异常：{ex.Message}");
                    Thread.Sleep(_robotConfig.ReconnectDelay);
                }
            }
            Logs.LogInfo("重连任务已终止");
        }

        #endregion


        #region 后台任务:心跳
       

        /// <summary>
        /// 启动心跳检测
        /// </summary>
        private void StartHeartbeat()
        {
            lock (_heartbeatLock)
            {

                StopHeartbeat(); // 停止旧的定时器

                _heartbeatPending = false;
                _heartbeatTimeoutCount = 0;

                _heartbeatTimer = new Timer(
                    OnHeartbeatTimerCallback,
                    null,
                    _heartbeatInterval,  // 首次延迟
                    _heartbeatInterval   // 周期性执行
                );

                Logs.LogInfo($"心跳检测已启动，间隔：{_heartbeatInterval}ms，超时：{_heartbeatTimeout}ms，最大失败次数：{_heartbeatMaxRetry}");
            }
        }

        /// <summary>
        /// 停止心跳检测
        /// </summary>
        private void StopHeartbeat()
        {
            lock (_heartbeatLock)
            {
                _heartbeatTimer?.Dispose();
                _heartbeatTimer = null;
                _heartbeatPending = false;
                _heartbeatTimeoutCount = 0;
                Logs.LogInfo("心跳检测已停止");
            }
        }

        /// <summary>
        /// 心跳定时器回调
        /// </summary>
        private void OnHeartbeatTimerCallback(object state)
        {
            try
            {                
                if (!Connected || !Initialized || _tcpClient == null || !_tcpClient.Connected)
                {
                    return;
                }
             
                if (_heartbeatPending)
                {
                    _heartbeatTimeoutCount++;
                    Logs.LogError($"心跳响应超时（第{_heartbeatTimeoutCount}次），预计响应时间：{_heartbeatTimeout}ms");

                    if (_heartbeatTimeoutCount >= _heartbeatMaxRetry)
                    {
                        // 连续超时次数达到上限，判定连接断开
                        Logs.LogError($"连续{_heartbeatMaxRetry}次心跳超时，判定连接已断开，触发重连");

                        // 主动断开标志，触发重连任务
                        Connected = false;
                        RobotState = RobotState.Reconnecting;
                        Message = "心跳超时，连接断开";

                        // 重置心跳状态，避免重复触发
                        _heartbeatPending = false;
                        _heartbeatTimeoutCount = 0;
                        StopHeartbeat();
                        return;
                    }
                    return; // 继续等待响应
                }
              
                string heartbeatCommand = _robotConfig.HeartbeatCommand;
                if (string.IsNullOrEmpty(heartbeatCommand))
                {
                    Logs.LogError("心跳指令为空，无法发送");
                    return;
                }

                // 标记等待响应
                _heartbeatPending = true;
                string fullCommand = $"{heartbeatCommand}{_robotConfig.CommandEndFlag}";
                _sendQueue.Enqueue(fullCommand);

                Logs.LogInfo($"心跳指令已发送：{heartbeatCommand}");


            }
            catch (Exception ex)
            {
                Logs.LogError($"心跳检测异常：{ex.Message}");
            }
        }



        /// <summary>
        /// 处理心跳响应（在 AnalysisTask 中调用）
        /// </summary>
        private void HandleHeartbeatResponse(string receivedContent)
        {
            // 如果当前有心跳正在等待响应
            if (_heartbeatPending)
            {
                // 根据实际协议判断收到的内容是否是心跳响应
                if (IsHeartbeatResponse(receivedContent))
                {
                    lock (_heartbeatLock)
                    {
                        _heartbeatPending = false;
                        _heartbeatTimeoutCount = 0;
                        Logs.LogInfo("心跳响应正常，连接状态良好");
                    }
                }
            }
        }

        /// <summary>
        /// 判断收到的数据是否为心跳响应（根据实际协议修改）
        /// </summary>
        private bool IsHeartbeatResponse(string content)
        {
            if (string.IsNullOrEmpty(content))
                return false;
            return content.Contains(_robotConfig.HeartbeatResponse);
        }

        #endregion

        #region 辅助方法

        private void ClearQueue<T>(ConcurrentQueue<T> queue)
        {
            while (queue.TryDequeue(out _)) { }
        }
        public void ClearReceiveContent()
        {
            ReceiveContent = string.Empty;
        }
        #endregion
    }
}