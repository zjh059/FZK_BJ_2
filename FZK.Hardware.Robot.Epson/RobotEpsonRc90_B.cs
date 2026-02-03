using FZK.Hardware.Robot.Base;
using FZK.Logger;
using ReactiveUI;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace FZK.Hardware.Robot.Epson
{
    internal class RobotEpsonRc90_B:IRobot
    {
        #region 响应式属性（实现状态实时通知）
      
        /// <summary>
        /// 是否初始化完成
        /// </summary>
        public bool Initialized { get; set; }
    
        /// <summary>
        /// 是否连接成功
        /// </summary>
        public bool Connected { get; set; }

        /// <summary>
        /// 最新异常/状态消息
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// 消息可观察对象（外部订阅获取异常/状态）
        /// </summary>
        public IObservable<string> MessageObservable => this.WhenAnyValue(x => x.Message);

        /// <summary>
        /// 最新接收的机械手数据/状态/响应
        /// </summary>
        public string ReceiveContent { get; set; }

        /// <summary>
        /// 接收数据可观察对象（外部订阅获取机械手返回数据）
        /// </summary>
        public IObservable<string> ReceiveContentObservable => this.WhenAnyValue(x => x.ReceiveContent);

    
        /// <summary>
        /// 机械手当前通信状态（辅助监控）
        /// </summary>
        public RobotState RobotState { get; set; }
        public CommandManager _commandManager { get; set; } = new CommandManager();
        #endregion

        #region 私有核心字段
        /// <summary>
        /// TCP客户端（与机械手建立长连接）
        /// </summary>
        private TcpClient _tcpClient;
        /// <summary>
        /// 全局取消令牌（统一控制所有后台任务）
        /// </summary>
        private CancellationTokenSource _cancellationTokenSource;
        /// <summary>
        /// 机械手配置
        /// </summary>
        private RobotConfig _robotConfig;
        /// <summary>
        /// 发送指令并发队列（生产消费，避免指令丢失）
        /// </summary>
        private readonly ConcurrentQueue<string> _sendQueue = new ConcurrentQueue<string>();
        /// <summary>
        /// 接收数据并发队列（生产消费，解耦接收与解析）
        /// </summary>
        private readonly ConcurrentQueue<byte[]> _receiveQueue = new ConcurrentQueue<byte[]>();
        private readonly System.Timers.Timer _heartbeatTimer = new System.Timers.Timer();
        private readonly object _commandReEnqueueLock = new object(); // 指令重入队锁，关键依赖
        
        //后台任务标识（避免重复启动任务）
        private Task _receiveTask;
        private Task _sendTask;
        private Task _analysisTask;
        private Task _reconnectTask;
        private Task _timeoutCheckTask;
        /// <summary>
        /// 线程锁（保证任务单例启动）
        /// </summary>
        private readonly object _taskLock = new object();
        /// <summary>
        /// 重连计数（用于控制最大重连次数）
        /// </summary>
        private int _reconnectCount;
        /// <summary>
        /// 接收缓冲区（4K，适配工业机械手常规指令长度）
        /// </summary>
        private readonly byte[] _receiveBuffer = new byte[4096];
        #endregion



        #region 构造函数
        public RobotEpsonRc90_B()
        {
            // 初始化默认状态
            RobotState = RobotState.UnInitialized;
            _cancellationTokenSource = new CancellationTokenSource();
            // ********************* 新增：初始化心跳计时器 *********************
            _heartbeatTimer.AutoReset = true; // 定时重复触发
            _heartbeatTimer.Elapsed += (s, e) =>
            {
                // 心跳触发时，执行心跳指令入队
                SendHeartbeatCommand();
            };
            // 初始禁用计时器（Init成功后根据配置启用）
            _heartbeatTimer.Enabled = false;
        }
        #endregion

        #region 核心接口实现：初始化
        /// <summary>
        /// 初始化机械手连接（入口方法，启动所有后台任务）
        /// </summary>
        /// <param name="config">机械手配置</param>
        /// <returns>是否初始化成功</returns>
        public bool Init(RobotConfig robotConfig)
        {
            try
            {
                // 重复初始化校验
                if (Initialized)
                {
                    Message = "机械手已完成初始化，无需重复执行";
                    Logs.LogInfo(Message);
                    return true;
                }
                // 配置校验
                if (robotConfig == null)
                {
                    string errorMsg = "机械手配置为空，初始化失败";
                    Message = errorMsg;
                    Logs.LogError(errorMsg);
                    return false;
                }
                _robotConfig = robotConfig;
                // 初始化TCP客户端
                _tcpClient = new TcpClient();
                // 更新TCP超时配置
                _tcpClient.ReceiveTimeout = robotConfig.Timeout;
                _tcpClient.SendTimeout = robotConfig.Timeout;

                // 尝试建立首次连接
                RobotState = RobotState.Connecting;
                Message = $"正在连接机械手：{robotConfig.IpAddress}:{robotConfig.Port}";
                Logs.LogInfo(Message);
                var connectSuccess = TryConnect();
                if (!connectSuccess)
                {
                    string errorMsg = $"首次连接机械手失败：{robotConfig.IpAddress}:{robotConfig.Port}";
                    Message = errorMsg;
                    Logs.LogError(errorMsg);
                    RobotState = RobotState.UnInitialized;
                    return false;
                }

                // 初始化状态
                Initialized = true;
                Connected = true;
                RobotState = RobotState.Connected;
                Message = $"机械手连接成功：{robotConfig.IpAddress}:{robotConfig.Port}";
                Logs.LogInfo(Message);
                _reconnectCount = 0;

                // 启动后台任务（单例，避免重复启动）
                StartBackgroundTasks();

                return true;
            }
            catch (Exception ex)
            {
                string errorMsg = $"机械手初始化异常：{ex.Message}";
                Message = errorMsg;
                Logs.LogError(ex);
                RobotState = RobotState.UnInitialized;
                return false;
            }
        }
        #endregion

        #region 核心接口实现：发送指令
        /// <summary>
        /// 发送指令给机械手（外部调用入口，线程安全）
        /// </summary>
        /// <param name="command">指令内容（无需带结束符，内部自动拼接）</param>
        /// <returns>是否入队成功（连接正常则入队）</returns>
        public bool SendCommand(string command)
        {
            // 基础校验
            if (!Initialized || !Connected || string.IsNullOrWhiteSpace(command))
            {
                string errorMsg = !Connected ? "机械手未连接，指令发送失败" : "指令为空，发送失败";
                Message = errorMsg;
                Logs.LogError(errorMsg);
                return false;
            }

            try
            {
                // ********************* 新增：指令ID绑定+实体封装 *********************
                long commandId = _commandManager.GetNextCommandId();
                // 拼接带ID的完整指令（协议：[ID]|原始指令+结束符）
                string fullCommand = $"{commandId}|{command}{_robotConfig.CommandEndFlag}";
                // 封装指令实体
                var robotCommand = new RobotCommand
                {
                    Id = commandId,
                    OriginalCommand = command,
                    FullCommand = fullCommand,
                    State = RobotCommandState.Pending, // 初始状态：待发送
                    CreateTime = DateTime.Now,
                    LastSendTime = DateTime.Now
                };

                // 注册指令到管理器（失败则直接返回）
                if (!_commandManager.RegisterCommand(robotCommand))
                {
                    string errorMsg = $"机械手指令注册失败：ID={commandId}，指令={command}";
                    Message = errorMsg;
                    Logs.LogError(errorMsg);
                    return false;
                }

                // 指令入队（复用原有并发队列）
                _sendQueue.Enqueue(fullCommand);
                Logs.LogInfo($"机械手指令入队成功：ID={commandId}，指令={command}（完整指令：{fullCommand.Trim()}）");
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
        /// <summary>
        /// 检查并重新建立连接（外部可手动调用）
        /// </summary>
        /// <returns>是否连接成功</returns>
        public bool CheckConnection()
        {
            if (Connected && _tcpClient != null && _tcpClient.Connected)
                return true;

            RobotState = RobotState.Reconnecting;
            Message = "机械手连接异常，正在手动重连";
            Logs.LogError(Message);
            return TryConnect();
        }
        #endregion

        #region 核心接口实现：关闭连接
        /// <summary>
        /// 优雅关闭连接（释放所有资源，终止所有后台任务）
        /// </summary>
        public void Close()
        {
            try
            {
                RobotState = RobotState.Disconnecting;
                Message = "正在关闭机械手连接，释放资源...";
                Logs.LogInfo(Message);

                // 1. 终止所有后台任务
                if (!_cancellationTokenSource.IsCancellationRequested)
                    _cancellationTokenSource.Cancel();
                // 等待任务终止（非阻塞，避免主线程卡）
                //Task.Factory.StartNew(() =>
                //{
                //    Task.WaitAll(_receiveTask, _sendTask, _analysisTask, _reconnectTask);
                //    Logs.LogInfo("机械手所有后台任务已终止");
                //});
                // Close方法中替换原Task.WaitAll代码
                var tasks = new List<Task>();
                if (_receiveTask != null) tasks.Add(_receiveTask);
                if (_sendTask != null) tasks.Add(_sendTask);
                if (_analysisTask != null) tasks.Add(_analysisTask);
                if (_reconnectTask != null) tasks.Add(_reconnectTask);
                if (tasks.Count > 0) Task.WaitAll(tasks.ToArray(), 1000); // 加超时，避免阻塞
                // 2. 清空收发队列               
                while (_receiveQueue.TryDequeue(out _)) { }
                Logs.LogInfo("机械手收发队列已清空");

                // 3. 关闭TCP连接
                if (_tcpClient != null)
                {
                    if (_tcpClient.Connected)
                        _tcpClient.Client.Shutdown(SocketShutdown.Both);
                    _tcpClient.Close();
                    _tcpClient = null;
                    Logs.LogInfo("机械手TCP连接已关闭");
                }

                // 4. 重置状态
                Initialized = false;
                Connected = false;
                RobotState = RobotState.UnInitialized;
                Message = "机械手连接已关闭，资源释放完成";
                Logs.LogInfo(Message);
            }
            catch (Exception ex)
            {
                string errorMsg = $"关闭机械手连接异常：{ex.Message}";
                Message = errorMsg;
                Logs.LogError(ex);
            }
        }
        #endregion

        #region 私有核心方法：TCP连接（首次+重连）
        /// <summary>
        /// 尝试建立TCP连接（同步，带超时）
        /// </summary>
        /// <returns>是否连接成功</returns>
        private bool TryConnect()
        {
            try
            {
                // 先关闭旧连接
                if (_tcpClient != null)
                {
                    _tcpClient.Close();
                    _tcpClient = new TcpClient();
                    _tcpClient.ReceiveTimeout = _robotConfig.Timeout;
                    _tcpClient.SendTimeout = _robotConfig.Timeout;
                }
                // 同步连接（带超时，.NET4.5.2兼容）
                var connectTask = _tcpClient.ConnectAsync(_robotConfig.IpAddress, _robotConfig.Port);
                return connectTask.Wait(_robotConfig.Timeout);
            }
            catch
            {
                return false;
            }
        }
        #endregion

        #region 私有核心方法：启动后台任务（单例）
        /// <summary>
        /// 启动后台任务（接收/发送/解析/重连），保证单例启动
        /// </summary>
        private void StartBackgroundTasks()
        {
            lock (_taskLock)
            {
                // 接收任务：持续读取机械手返回数据
                if (_receiveTask == null || _receiveTask.IsCompleted)
                    _receiveTask = Task.Factory.StartNew(ReceiveTask, _cancellationTokenSource.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
                // 发送任务：持续从队列取指令发送给机械手
                if (_sendTask == null || _sendTask.IsCompleted)
                    _sendTask = Task.Factory.StartNew(SendTask,     _cancellationTokenSource.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
                // 解析任务：持续解析接收的字节数据为字符串
                if (_analysisTask == null || _analysisTask.IsCompleted)
                    _analysisTask = Task.Factory.StartNew(AnalysisTask, _cancellationTokenSource.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
                // 重连任务：持续检查连接状态，断线自动重连
                if (_reconnectTask == null || _reconnectTask.IsCompleted)
                    _reconnectTask = Task.Factory.StartNew(ReconnectTask, _cancellationTokenSource.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
                // 重连任务：持续检查连接状态，断线自动重连
                if (_timeoutCheckTask == null || _reconnectTask.IsCompleted)
                    _timeoutCheckTask = Task.Factory.StartNew(ReconnectTask, _cancellationTokenSource.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            }
        }
        #endregion

        #region 后台任务1：接收机械手数据（生产者）
        /// <summary>
        /// 接收任务：持续读取机械手返回的字节数据，入队到接收队列
        /// </summary>
        private void ReceiveTask()
        {
            while (!_cancellationTokenSource.IsCancellationRequested)
            {
                try
                {
                    if (_tcpClient == null || !_tcpClient.Connected || !Initialized)
                    {
                        Task.Delay(10).Wait();
                        continue;
                    }

                    // 检查是否有数据可读取，避免阻塞
                    if (_tcpClient.Available <= 0)
                    {
                        Task.Delay(1).Wait();
                        continue;
                    }

                    // 读取数据（阻塞，直到有数据/超时/断线）
                    int receiveLength = _tcpClient.Client.Receive(_receiveBuffer);
                    if (receiveLength <= 0)
                    {
                        Task.Delay(1).Wait();
                        continue;
                    }

                    // 截取有效数据，入队解析
                    byte[] validData = new byte[receiveLength];
                    Array.Copy(_receiveBuffer, 0, validData, 0, receiveLength);
                    _receiveQueue.Enqueue(validData);
                }
                catch (Exception ex)
                {
                    // 接收异常，标记为断线
                    if (!_cancellationTokenSource.IsCancellationRequested)
                    {
                        Connected = false;
                        RobotState = RobotState.Disconnecting;
                        string errorMsg = $"机械手数据接收异常：{ex.Message}，触发断线重连";
                        Message = errorMsg;
                        Logs.LogError(errorMsg);
                    }
                    Task.Delay(100).Wait();
                }
            }
            Logs.LogInfo("机械手接收任务已终止");
        }
        #endregion

        #region 后台任务2：发送指令给机械手（消费者）
        /// <summary>
        /// 发送任务：持续从发送队列取指令，发送给机械手
        /// </summary>
        private void SendTask()
        {
            while (!_cancellationTokenSource.IsCancellationRequested)
            {
                try
                {
                    if (_tcpClient == null || !_tcpClient.Connected || !Initialized || _sendQueue.IsEmpty)
                    {
                        Task.Delay(1).Wait();
                        continue;
                    }

                    // 从队列取指令
                    if (_sendQueue.TryDequeue(out string command))
                    {
                        // 转换为字节数组
                        byte[] sendBytes = _robotConfig.Encode.GetBytes(command);
                        // 发送指令
                        _tcpClient.Client.Send(sendBytes);
                        Logs.LogInfo($"机械手指令发送成功：{command.Trim()}（字节长度：{sendBytes.Length}）");
                        // ********************* 新增：解析指令ID，更新状态为Sending *********************
                        if (_robotConfig.CommandTimeout > 0) // 开启指令确认才更新
                        {
                            // 解析指令ID（协议：[ID]|指令内容...）
                            var commandIdStr = command.Split('|').FirstOrDefault();
                            if (long.TryParse(commandIdStr, out long commandId) && commandId > 0)
                            {
                                _commandManager.UpdateCommand(commandId, cmd =>
                                {
                                    cmd.State = RobotCommandState.Sending;
                                    cmd.LastSendTime = DateTime.Now; // 更新最后发送时间
                                });
                            }
                        }
                    }
                }

                catch (Exception ex)
                {
                    // 发送异常，标记为断线
                    if (!_cancellationTokenSource.IsCancellationRequested)
                    {
                        Connected = false;
                        RobotState = RobotState.Disconnecting;
                        string errorMsg = $"机械手指令发送异常：{ex.Message}，触发断线重连";
                        Message = errorMsg;
                        Logs.LogError(errorMsg);
                    }
                    Task.Delay(100).Wait();
                }
            }
            Logs.LogInfo("机械手发送任务已终止");
        }
        #endregion

        #region 后台任务3：解析接收数据（消费者）
        /// <summary>
        /// 解析任务：将接收的字节数据解析为字符串，更新到响应式属性
        /// </summary>
        private void AnalysisTask()
        {
            while (!_cancellationTokenSource    .IsCancellationRequested)
            {
                try
                {
                    if (_receiveQueue.IsEmpty || _robotConfig == null)
                    {
                        Task.Delay(1).Wait();
                        continue;
                    }

                    // 从队列取数据并解析
                    if (_receiveQueue.TryDequeue(out byte[] receiveBytes))
                    {
                        string content = _robotConfig.Encode.GetString(receiveBytes).Trim();
                        if (!string.IsNullOrWhiteSpace(content))
                        {
                            ReceiveContent = content;
                            Logs.LogInfo($"机械手返回数据：{content}（字节长度：{receiveBytes.Length}）");
                        }
                        // 1. 先判断是否为心跳响应
                        if (content.Equals(_robotConfig.HeartbeatResponse, StringComparison.OrdinalIgnoreCase))
                        {
                            Logs.LogInfo($"机械臂心跳响应正常：{_robotConfig.HeartbeatResponse}");
                            continue; // 心跳响应处理完成，跳过后续指令解析
                        }

                        // 2. 不是心跳响应，则按指令确认协议解析（协议：[ID]|SUCCESS/[ID]|FAIL[原因]）
                        if (_robotConfig.CommandTimeout > 0) // 开启指令确认才解析
                        {
                            try
                            {
                                // 分割响应：按|分割，第一个元素为指令ID，第二个为执行结果
                                var responseParts = content.Split('|'); // 分割为2部分，避免结果中包含|
                                if (responseParts.Length == 2 && long.TryParse(responseParts[0].Trim(), out long commandId) && commandId > 0)
                                {
                                    string result = responseParts[1].Trim();
                                    // 更新指令状态并移除
                                    _commandManager.UpdateCommand(commandId, cmd =>
                                    {
                                        if (result.StartsWith("SUCCESS", StringComparison.OrdinalIgnoreCase))
                                        {
                                            // 执行成功
                                            cmd.State = RobotCommandState.Success;
                                            cmd.Response = result;
                                            Message = $"机械臂指令执行成功：ID={cmd.Id}，指令={cmd.OriginalCommand}，响应={result}";
                                            Logs.LogInfo(Message);
                                        }
                                        else
                                        {
                                            // 执行失败
                                            cmd.State = RobotCommandState.Fail;
                                            cmd.Response = result;
                                            Message = $"机械臂指令执行失败：ID={cmd.Id}，指令={cmd.OriginalCommand}，原因={result}";
                                            Logs.LogError(Message);
                                        }
                                    });

                                    // 从管理器中移除已完成的指令
                                    if (_commandManager.RemoveCommand(commandId, out var completedCmd))
                                    {
                                        Logs.LogInfo($"机械臂指令已完成并移除：ID={completedCmd.Id}，状态={completedCmd.State}");
                                    }
                                }
                                else
                                {
                                    // 响应格式不合法，记录警告
                                    Logs.LogError($"机械臂返回响应格式不合法，无法解析指令确认：{content}");
                                }
                            }
                            catch (Exception ex)
                            {
                                Logs.LogError( $"机械臂指令确认响应解析异常：{content}+{ex}");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    string errorMsg = $"机械手数据解析异常：{ex.Message}";
                    Message = errorMsg;
                    Logs.LogError(ex);
                    Task.Delay(100).Wait();
                }
            }
            Logs.LogInfo("机械手解析任务已终止");
        }
        #endregion

        #region 后台任务4：断线自动重连
        /// <summary>
        /// 重连任务：持续检查连接状态，断线后按配置自动重连
        /// </summary>
        private void ReconnectTask()
        {
            while (!_cancellationTokenSource.IsCancellationRequested)
            {
                try
                {
                    // 连接正常，无需重连
                    if (Connected || !Initialized || _robotConfig == null)
                    {
                        _reconnectCount = 0;
                        Task.Delay(1000).Wait();
                        continue;
                    }

                    // 检查重连次数是否超限
                    if (_robotConfig.MaxReconnectCount > 0 && _reconnectCount >= _robotConfig.MaxReconnectCount)
                    {
                        string errorMsg = $"机械手重连次数超限（最大：{_robotConfig.MaxReconnectCount}），停止重连";
                        Message = errorMsg;
                        Logs.LogError(errorMsg);
                        Initialized = false;
                        RobotState = RobotState.UnInitialized;
                        Task.Delay(1000).Wait();
                        continue;
                    }

                    // 执行重连
                    _reconnectCount++;
                    RobotState = RobotState.Reconnecting;
                    Message = $"机械手重连中（第{_reconnectCount}次）：{_robotConfig.IpAddress}:{_robotConfig.Port}";
                    Logs.LogError(Message);
                    bool reconnectSuccess = TryConnect();
                    if (reconnectSuccess)
                    {
                        // 重连成功，重置状态
                        Connected = true;
                        RobotState = RobotState.Connected;
                        Message = $"机械手重连成功（第{_reconnectCount}次）：{_robotConfig.IpAddress}:{_robotConfig.Port}";
                        Logs.LogInfo(Message);
                        _reconnectCount = 0;
                        // 重新启动后台任务（防止任务异常终止）
                        StartBackgroundTasks();
                    }
                    else
                    {
                        Message = $"机械手重连失败（第{_reconnectCount}次）：{_robotConfig.IpAddress}:{_robotConfig.Port}";
                        Logs.LogError(Message);
                        // 重连失败，延迟后重试
                        Task.Delay(_robotConfig.ReconnectDelay).Wait();
                    }
                }
                catch (Exception ex)
                {
                    string errorMsg = $"机械手重连异常：{ex.Message}";
                    Message = errorMsg;
                    Logs.LogError(ex);
                    Task.Delay(_robotConfig.ReconnectDelay).Wait();
                }
            }
            Logs.LogInfo("机械手重连任务已终止");
        }
        #endregion
        #region 后台任务5：指令超时检查（新增：核心）
        /// <summary>
        /// 超时检查任务：持续检查待确认指令是否超时，触发重发/标记失败
        /// </summary>
        private void TimeoutCheckTask()
        {
            while (!_cancellationTokenSource.IsCancellationRequested)
            {
                try
                {
                    // 校验状态+配置：未初始化/未连接/关闭指令确认，则不检查
                    if (!Initialized || !Connected || _robotConfig == null || _robotConfig.CommandTimeout <= 0)
                    {
                        Task.Delay(100).Wait();
                        continue;
                    }

                    // 1. 获取所有超时的指令
                    var timeoutCommands = _commandManager.GetTimeoutCommands(_robotConfig.CommandTimeout);
                    if (timeoutCommands.Count == 0)
                    {
                        Task.Delay(100).Wait();
                        continue;
                    }

                    // 2. 遍历超时指令，处理重发/标记失败
                    foreach (var timeoutCmd in timeoutCommands)
                    {
                        lock (timeoutCmd) // 单条指令加锁，避免并发修改
                        {
                            // 再次校验状态（避免多线程下重复处理）
                            if (timeoutCmd.State != RobotCommandState.Sending) continue;

                            // 3. 判断是否超过最大重发次数
                            if (timeoutCmd.RetryCount < _robotConfig.CommandRetryCount)
                            {
                                // 未超限：重发
                                timeoutCmd.RetryCount++;
                                timeoutCmd.State = RobotCommandState.Retrying;
                                timeoutCmd.LastSendTime = DateTime.Now; // 更新最后发送时间
                                Logs.LogWarning($"机械臂指令超时，准备重发：ID={timeoutCmd.Id}，指令={timeoutCmd.OriginalCommand}（已重发：{timeoutCmd.RetryCount}/{_robotConfig.CommandRetryCount}）");
                                // 重入队发送
                                ReEnqueueCommand(timeoutCmd);
                            }
                            else
                            {
                                // 已超限：标记为超时失败，移除指令
                                timeoutCmd.State = RobotCommandState.Timeout;
                                string errorMsg = $"机械臂指令超时失败：ID={timeoutCmd.Id}，指令={timeoutCmd.OriginalCommand}（超时时间：{_robotConfig.CommandTimeout}ms，重发次数：{_robotConfig.CommandRetryCount}）";
                                Message = errorMsg;
                                Logs.LogError(errorMsg);
                                // 从管理器中移除
                                _commandManager.RemoveCommand(timeoutCmd.Id, out _);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    string errorMsg = $"机械臂指令超时检查异常：{ex.Message}";
                    Message = errorMsg;
                    Logs.LogError(ex);
                    Task.Delay(100).Wait();
                }
            }
            Logs.LogInfo("机械手指令超时检查任务已终止");
        }

        #endregion

        #region 私有辅助方法
        /// <summary>
        /// 清空队列（扩展方法，.NET4.5.2兼容）
        /// </summary>
        private void ClearQueue<T>(ConcurrentQueue<T> queue)
        {
            while (queue.TryDequeue(out _)) { }
        }
        #endregion
        #region 新增：心跳+指令确认相关私有方法
        /// <summary>
        /// 发送心跳包（心跳计时器触发/手动触发）
        /// </summary>
        private void SendHeartbeatCommand()
        {
            try
            {
                // 校验状态+配置：未初始化/未连接/关闭心跳，则不发送
                if (!Initialized || !Connected || _robotConfig == null || _robotConfig.HeartbeatInterval <= 0)
                {
                    _heartbeatTimer.Enabled = false;
                    return;
                }

                // 心跳包无需ID确认（简单校验），直接拼接结束符入队
                string heartbeatFullCommand = _robotConfig.HeartbeatCommand + _robotConfig.CommandEndFlag;
                _sendQueue.Enqueue(heartbeatFullCommand);
                Logs.LogInfo($"机械臂心跳包入队成功：{_robotConfig.HeartbeatCommand}");
            }
            catch (Exception ex)
            {
                string errorMsg = $"机械臂心跳包入队异常：{ex.Message}";
                Message = errorMsg;
                Logs.LogError(ex);
            }
        }

        /// <summary>
        /// 指令重入队（超时重发核心方法：将超时指令重新加入发送队列，线程安全）
        /// </summary>
        /// <param name="command">需要重发的超时指令实体</param>
        private void ReEnqueueCommand(RobotCommand command)
        {
            // 1. 基础非空/状态校验：指令为null、未初始化、未连接直接返回
            if (command == null
                || !Initialized
                || !Connected
                || _robotConfig == null
                || string.IsNullOrWhiteSpace(command.FullCommand))
            {
                Logs.LogWarning("机械臂指令重入队失败：基础状态/指令校验不通过");
                return;
            }

            // 2. 加锁保证单条指令不被重复重入队（多线程下超时检查任务可能重复处理）
            lock (_commandReEnqueueLock)
            {
                // 3. 二次状态校验：仅Retrying状态的指令允许入队（避免状态被修改后重复入队）
                if (command.State != RobotCommandState.Retrying)
                {
                    Logs.LogWarning($"机械臂指令重入队失败：指令状态非重发中，ID={command.Id}，当前状态={command.State}");
                    return;
                }

                // 4. 正式重入队：复用原有发送队列，和普通指令统一处理
                _sendQueue.Enqueue(command.FullCommand);
                Logs.LogInfo($"机械臂指令超时重发入队成功：ID={command.Id}，指令={command.OriginalCommand}（已重发{command.RetryCount}/{_robotConfig.CommandRetryCount}次）");
            }
        }
        #endregion

    }

}
