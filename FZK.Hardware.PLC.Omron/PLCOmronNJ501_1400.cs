using FZK.Hardware.PLC.Base;
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

namespace FZK.Hardware.PLC.Omron
{
    /// <summary>
    /// 欧姆龙PLC Fins TCP通信驱动类（实现IPLC接口，工业级高可用）
    /// 适配：CJ2M/CP1H/NJ/NX等支持Fins TCP的欧姆龙PLC
    /// </summary>
    internal class PLCOmronNJ501_1400 : ReactiveObject, IPLC
    {
        public bool Initialized { get; set; }
        public bool Connected { get; set; }
        public PLCState PLCState { get; set; }
        public string Message { get; set; }
        public string ReceiveFrame { get; set; }

        public IObservable<string> MessageObservable { get; set; }

        public IObservable<string> ReceiveFrameObservable { get; set; }

        #region 私有核心字段
        /// <summary>
        /// TCP客户端（Fins TCP长连接）
        /// </summary>
        private TcpClient _tcpClient;
        /// <summary>
        /// 全局取消令牌（统一控制后台任务）
        /// </summary>
        private CancellationTokenSource _cancellationTokenSource;
        /// <summary>
        /// PLC配置
        /// </summary>
        private PLCConfig _plcConfig;
        /// <summary>
        /// Fins指令发送队列（字节数组，Fins为十六进制帧）
        /// </summary>
        private readonly ConcurrentQueue<byte[]> _sendQueue = new ConcurrentQueue<byte[]>();
        /// <summary>
        /// Fins响应接收队列（解耦接收和解析）
        /// </summary>
        private readonly ConcurrentQueue<byte[]> _receiveQueue = new ConcurrentQueue<byte[]>();
        /// <summary>
        /// 后台任务字段（保证单例）
        /// </summary>
        private Task _receiveTask;
        private Task _sendTask;
        private Task _heartbeatTask;
        private Task _reconnectTask;
        /// <summary>
        /// 任务启动锁（保证单例启动）
        /// </summary>
        private readonly object _taskLock = new object();
        /// <summary>
        /// 重连计数（控制最大重连次数）
        /// </summary>
        private int _reconnectCount;
        /// <summary>
        /// Fins接收缓冲区（1K，Fins帧最大长度远小于此）
        /// </summary>
        private readonly byte[] _receiveBuffer = new byte[1024];
        /// <summary>
        /// 指令发送锁（避免批量读写时指令混乱）
        /// </summary>
        private readonly object _commandLock = new object();
        /// <summary>
        /// 响应等待信号（同步指令的响应等待）
        /// </summary>
        private readonly AutoResetEvent _responseWaitEvent = new AutoResetEvent(false);
        /// <summary>
        /// 最新Fins响应结果（供同步读写使用）
        /// </summary>
        private byte[] _latestResponse;
        #endregion

        #region 构造函数（初始化默认状态+TCP+取消令牌）
        public PLCOmronNJ501_1400()
        {
            // 初始化默认状态
            PLCState = PLCState.UnInitialized;
            _cancellationTokenSource = new CancellationTokenSource();

            // 初始化TCP客户端
            _tcpClient = new TcpClient();
            _tcpClient.ReceiveTimeout = 3000;
            _tcpClient.SendTimeout = 3000;

            // 初始化消息
            Message = "PLC驱动已实例化，未初始化连接";
            Logs.LogInfo(Message);
        }
        #endregion
      

        #region 核心接口实现：Init（初始化入口，和机械臂一致）
        public bool Init(PLCConfig config)
        {
            try
            {
                // 重复初始化校验
                if (Initialized)
                {
                    Message = "PLC已完成初始化，无需重复执行";
                    Logs.LogInfo(Message);
                    return true;
                }

                // 配置校验
                if (config == null || string.IsNullOrWhiteSpace(config.IpAddress))
                {
                    string errorMsg = "PLC配置为空或IP地址无效，初始化失败";
                    Message = errorMsg;
                    Logs.LogError(errorMsg);
                    return false;
                }

                _plcConfig = config;
                // 更新TCP超时配置
                _tcpClient.ReceiveTimeout = config.Timeout;
                _tcpClient.SendTimeout = config.Timeout;

                // 尝试首次连接
                PLCState = PLCState.Connecting;
                Message = $"正在连接欧姆龙PLC：{config.IpAddress}:{config.Port}";
                Logs.LogInfo(Message);
                bool connectSuccess = TryConnect();
                if (!connectSuccess)
                {
                    string errorMsg = $"首次连接PLC失败：{config.IpAddress}:{config.Port}";
                    Message = errorMsg;
                    Logs.LogError(errorMsg);
                    PLCState = PLCState.UnInitialized;
                    return false;
                }

                // 初始化状态
                Initialized = true;
                Connected = true;
                PLCState = PLCState.Connected;
                Message = $"PLC连接成功：{config.IpAddress}:{config.Port}（Fins TCP）";
                Logs.LogInfo(Message);
                _reconnectCount = 0;

                // 启动后台任务（单例，避免重复）
                StartBackgroundTasks();

                return true;
            }
            catch (Exception ex)
            {
                string errorMsg = $"PLC初始化异常：{ex.Message}";
                Message = errorMsg;
                Logs.LogError(ex);
                PLCState = PLCState.UnInitialized;
                return false;
            }
        }
        #endregion

        #region 核心接口实现：寄存器读写（单条+批量，Fins协议核心）
        public int Read(PLCRegisterType registerType, ushort address, bool isBCD = false)
        {
            lock (_commandLock)
            {
                // 基础校验
                if (!CheckBaseState()) return -1;

                try
                {
                    // 封装Fins读单地址指令帧
                    byte[] finsFrame = PackFinsReadFrame(registerType, address, 1);
                    // 发送指令并等待响应
                    byte[] response = SendAndWaitResponse(finsFrame);
                    if (response == null || !IsFinsResponseSuccess(response))
                    {
                        Message = $"读取PLC寄存器失败：{GetFinsErrorMsg(response)}";
                        Logs.LogError(Message);
                        return -1;
                    }

                    // 解析响应数据
                    int value = ParseFinsReadResponse(response, isBCD);
                    Message = $"读取PLC{registerType}{address}成功，值：{value}（{(isBCD ? "BCD码" : "二进制")}）";
                    Logs.LogInfo(Message);
                    return value;
                }
                catch (Exception ex)
                {
                    string errorMsg = $"读取PLC寄存器异常：{ex.Message}";
                    Message = errorMsg;
                    Logs.LogError(ex);
                    return -1;
                }
            }
        }

        public List<int> BatchRead(PLCRegisterType registerType, ushort startAddress, ushort count, bool isBCD = false)
        {
            lock (_commandLock)
            {
                // 基础校验
                if (!CheckBaseState() || count <= 0 || count > 100) // 单次批量最多读100个字，适配PLC性能
                {
                    Message = count > 100 ? "PLC批量读取个数不能超过100" : "PLC基础状态校验不通过，批量读取失败";
                    Logs.LogError(Message);
                    return new List<int>();
                }

                try
                {
                    // 封装Fins批量读指令帧
                    byte[] finsFrame = PackFinsReadFrame(registerType, startAddress, count);
                    // 发送指令并等待响应
                    byte[] response = SendAndWaitResponse(finsFrame);
                    if (response == null || !IsFinsResponseSuccess(response))
                    {
                        Message = $"批量读取PLC寄存器失败：{GetFinsErrorMsg(response)}";
                        Logs.LogError(Message);
                        return new List<int>();
                    }

                    // 解析批量响应数据
                    List<int> values = ParseFinsBatchReadResponse(response, count, isBCD);
                    Message = $"批量读取PLC{registerType}{startAddress}-{startAddress + count - 1}成功，共{values.Count}个值";
                    Logs.LogInfo(Message);
                    return values;
                }
                catch (Exception ex)
                {
                    string errorMsg = $"批量读取PLC寄存器异常：{ex.Message}";
                    Message = errorMsg;
                    Logs.LogError(ex);
                    return new List<int>();
                }
            }
        }

        public bool Write(PLCRegisterType registerType, ushort address, int value, bool isBCD = false)
        {
            lock (_commandLock)
            {
                // 基础校验
                if (!CheckBaseState()) return false;

                try
                {
                    // 封装Fins写单地址指令帧
                    byte[] finsFrame = PackFinsWriteFrame(registerType, address, new List<int> { value }, isBCD);
                    // 发送指令并等待响应
                    byte[] response = SendAndWaitResponse(finsFrame);
                    if (response == null || !IsFinsResponseSuccess(response))
                    {
                        Message = $"写入PLC{registerType}{address}失败：{GetFinsErrorMsg(response)}，写入值：{value}";
                        Logs.LogError(Message);
                        return false;
                    }

                    Message = $"写入PLC{registerType}{address}成功，值：{value}（{(isBCD ? "BCD码" : "二进制")}）";
                    Logs.LogInfo(Message);
                    return true;
                }
                catch (Exception ex)
                {
                    string errorMsg = $"写入PLC寄存器异常：{ex.Message}，地址：{registerType}{address}，值：{value}";
                    Message = errorMsg;
                    Logs.LogError(ex);
                    return false;
                }
            }
        }

        public bool BatchWrite(PLCRegisterType registerType, ushort startAddress, List<int> values, bool isBCD = false)
        {
            lock (_commandLock)
            {
                // 基础校验
                if (!CheckBaseState() || values == null || values.Count == 0 || values.Count > 100)
                {
                    Message = values.Count > 100 ? "PLC批量写入个数不能超过100" : "PLC基础状态/写入值校验不通过，批量写入失败";
                    Logs.LogError(Message);
                    return false;
                }

                try
                {
                    // 封装Fins批量写指令帧
                    byte[] finsFrame = PackFinsWriteFrame(registerType, startAddress, values, isBCD);
                    // 发送指令并等待响应
                    byte[] response = SendAndWaitResponse(finsFrame);
                    if (response == null || !IsFinsResponseSuccess(response))
                    {
                        Message = $"批量写入PLC{registerType}{startAddress}失败：{GetFinsErrorMsg(response)}";
                        Logs.LogError(Message);
                        return false;
                    }

                    Message = $"批量写入PLC{registerType}{startAddress}成功，共{values.Count}个值";
                    Logs.LogInfo(Message);
                    return true;
                }
                catch (Exception ex)
                {
                    string errorMsg = $"批量写入PLC寄存器异常：{ex.Message}，起始地址：{registerType}{startAddress}";
                    Message = errorMsg;
                    Logs.LogError(ex);
                    return false;
                }
            }
        }
        #endregion
        #region 核心接口实现：CheckConnection + Close（和机械臂架构一致）
        public bool CheckConnection()
        {
            if (Connected && _tcpClient != null && _tcpClient.Connected)
                return true;

            PLCState = PLCState.Reconnecting;
            Message = "PLC连接异常，正在手动重连";
            Logs.LogError(Message);
            return TryConnect();
        }

        public void Close()
        {
            try
            {
                PLCState = PLCState.Disconnecting;
                Message = "正在关闭PLC连接，释放资源...";
                Logs.LogInfo(Message);

                // 1. 终止所有后台任务
                if (!_cancellationTokenSource.IsCancellationRequested)
                    _cancellationTokenSource.Cancel();
                // 等待任务终止（修复空引用，加超时）
                Task.Factory.StartNew(() =>
                {
                    var tasks = new List<Task>();
                    if (_receiveTask != null) tasks.Add(_receiveTask);
                    if (_sendTask != null) tasks.Add(_sendTask);
                    if (_heartbeatTask != null) tasks.Add(_heartbeatTask);
                    if (_reconnectTask != null) tasks.Add(_reconnectTask);
                    if (tasks.Count > 0) Task.WaitAll(tasks.ToArray(), 1000);
                    Logs.LogInfo("PLC所有后台任务已终止");
                });

                // 2. 清空收发队列
                while (_sendQueue.TryDequeue(out _)) { }
                while (_receiveQueue.TryDequeue(out _)) { }
                Logs.LogInfo("PLC收发队列已清空");

                // 3. 关闭TCP连接
                if (_tcpClient != null)
                {
                    if (_tcpClient.Connected)
                        _tcpClient.Client.Shutdown(SocketShutdown.Both);
                    _tcpClient.Close();
                    _tcpClient = null;
                    Logs.LogInfo("PLC TCP连接已关闭");
                }

                // 4. 释放资源+重置状态
                _responseWaitEvent.Dispose();
                _cancellationTokenSource.Dispose();
                Initialized = false;
                Connected = false;
                PLCState = PLCState.UnInitialized;
                Message = "PLC连接已关闭，所有资源释放完成";
                Logs.LogInfo(Message);
            }
            catch (Exception ex)
            {
                string errorMsg = $"关闭PLC连接异常：{ex.Message}";
                Message = errorMsg;
                Logs.LogError(ex);
            }
        }
        #endregion
        #region 私有核心方法：TCP连接（TryConnect）+ 后台任务启动（StartBackgroundTasks）
        /// <summary>
        /// 尝试建立TCP连接（首次+重连，同步带超时）
        /// </summary>
        /// <returns>是否连接成功</returns>
        private bool TryConnect()
        {
            try
            {
                // 关闭旧连接
                if (_tcpClient != null)
                {
                    _tcpClient.Close();
                    _tcpClient = new TcpClient();
                    _tcpClient.ReceiveTimeout = _plcConfig?.Timeout ?? 3000;
                    _tcpClient.SendTimeout = _plcConfig?.Timeout ?? 3000;
                }

                // 同步连接（适配.NET4.5.2）
                var connectTask = _tcpClient.ConnectAsync(_plcConfig.IpAddress, _plcConfig.Port);
                bool connectSuccess = connectTask.Wait(_plcConfig.Timeout);

                // 连接成功清空发送队列，避免断线前指令乱发
                if (connectSuccess)
                {
                    while (_sendQueue.TryDequeue(out _)) { }
                    Logs.LogInfo("PLC连接成功，清空发送队列");
                }

                return connectSuccess;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 启动后台任务（单例，lock保证）
        /// </summary>
        private void StartBackgroundTasks()
        {
            lock (_taskLock)
            {
                if (_receiveTask == null || _receiveTask.IsCompleted)
                    _receiveTask = Task.Factory.StartNew(ReceiveTask, _cancellationTokenSource.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
                if (_sendTask == null || _sendTask.IsCompleted)
                    _sendTask = Task.Factory.StartNew(SendTask, _cancellationTokenSource.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
                if (_heartbeatTask == null || _heartbeatTask.IsCompleted)
                    _heartbeatTask = Task.Factory.StartNew(HeartbeatTask, _cancellationTokenSource.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
                if (_reconnectTask == null || _reconnectTask.IsCompleted)
                    _reconnectTask = Task.Factory.StartNew(ReconnectTask, _cancellationTokenSource.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            }
        }
        #endregion
        #region 后台任务：Receive/Send/Heartbeat/Reconnect（核心，和机械臂分工一致）
        /// <summary>
        /// 接收任务：持续读取PLC返回的Fins响应帧，入队解析
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

                    // 检查是否有数据可读，避免阻塞
                    if (_tcpClient.Available <= 0)
                    {
                        Task.Delay(1).Wait();
                        continue;
                    }

                    // 读取Fins响应帧
                    int receiveLength = _tcpClient.Client.Receive(_receiveBuffer);
                    if (receiveLength <= 0)
                    {
                        Task.Delay(1).Wait();
                        continue;
                    }

                    // 截取有效数据，入队
                    byte[] validData = new byte[receiveLength];
                    Array.Copy(_receiveBuffer, 0, validData, 0, receiveLength);
                    _receiveQueue.Enqueue(validData);
                    // 触发响应等待信号
                    _responseWaitEvent.Set();
                }
                catch (Exception ex)
                {
                    if (!_cancellationTokenSource.IsCancellationRequested)
                    {
                        Connected = false;
                        PLCState = PLCState.Disconnecting;
                        string errorMsg = $"PLC数据接收异常：{ex.Message}，触发断线重连";
                        Message = errorMsg;
                        Logs.LogError(errorMsg);
                    }
                    Task.Delay(100).Wait();
                }
            }
            Logs.LogInfo("PLC接收任务已终止");
        }

        /// <summary>
        /// 发送任务：持续从队列取Fins指令，发送给PLC
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

                    // 取Fins指令帧
                    if (_sendQueue.TryDequeue(out byte[] finsFrame))
                    {
                        // 发送指令
                        _tcpClient.Client.Send(finsFrame);
                        Logs.LogInfo($"PLC Fins指令发送成功：{ByteToHex(finsFrame)}（字节长度：{finsFrame.Length}）");
                        // 指令发送间隔，适配PLC处理能力
                        if (_plcConfig.SendInterval > 0)
                            Task.Delay(_plcConfig.SendInterval).Wait();
                    }
                }
                catch (Exception ex)
                {
                    if (!_cancellationTokenSource.IsCancellationRequested)
                    {
                        Connected = false;
                        PLCState = PLCState.Disconnecting;
                        string errorMsg = $"PLC Fins指令发送异常：{ex.Message}，触发断线重连";
                        Message = errorMsg;
                        Logs.LogError(errorMsg);
                    }
                    Task.Delay(100).Wait();
                }
            }
            Logs.LogInfo("PLC发送任务已终止");
        }

        /// <summary>
        /// 心跳任务：发送Fins标准节点确认指令，检测PLC连接有效性
        /// </summary>
        private void HeartbeatTask()
        {
            while (!_cancellationTokenSource.IsCancellationRequested)
            {
                try
                {
                    // 状态+配置校验
                    if (!Initialized || !Connected || _plcConfig == null || _plcConfig.HeartbeatInterval <= 0)
                    {
                        Task.Delay(1000).Wait();
                        continue;
                    }

                    // 封装Fins标准心跳指令帧（01 01 节点确认）
                    byte[] heartbeatFrame = PackFinsHeartbeatFrame();
                    // 入队发送
                    _sendQueue.Enqueue(heartbeatFrame);
                    Logs.LogInfo($"PLC心跳包发送：{ByteToHex(heartbeatFrame)}");

                    // 心跳间隔
                    Task.Delay(_plcConfig.HeartbeatInterval).Wait();
                }
                catch (Exception ex)
                {
                    string errorMsg = $"PLC心跳任务异常：{ex.Message}";
                    Message = errorMsg;
                    Logs.LogError(ex);
                    Task.Delay(1000).Wait();
                }
            }
            Logs.LogInfo("PLC心跳任务已终止");
        }
        /// <summary>
        /// 重连任务：断线自动重连，和机械臂逻辑完全一致
        /// </summary>
        private void ReconnectTask()
        {
            while (!_cancellationTokenSource.IsCancellationRequested)
            {
                try
                {
                    if (Connected || !Initialized || _plcConfig == null)
                    {
                        _reconnectCount = 0;
                        Task.Delay(1000).Wait();
                        continue;
                    }

                    // 重连次数超限
                    if (_plcConfig.MaxReconnectCount > 0 && _reconnectCount >= _plcConfig.MaxReconnectCount)
                    {
                        string errorMsg = $"PLC重连次数超限（最大：{_plcConfig.MaxReconnectCount}），停止重连";
                        Message = errorMsg;
                        Logs.LogError(errorMsg);
                        Initialized = false;
                        PLCState = PLCState.UnInitialized;
                        Task.Delay(1000).Wait();
                        continue;
                    }

                    // 执行重连
                    _reconnectCount++;
                    PLCState = PLCState.Reconnecting;
                    Message = $"PLC重连中（第{_reconnectCount}次）：{_plcConfig.IpAddress}:{_plcConfig.Port}";
                    Logs.LogError(Message);
                    bool reconnectSuccess = TryConnect();
                    if (reconnectSuccess)
                    {
                        Connected = true;
                        PLCState = PLCState.Connected;
                        Message = $"PLC重连成功（第{_reconnectCount}次）：{_plcConfig.IpAddress}:{_plcConfig.Port}";
                        Logs.LogInfo(Message);
                        _reconnectCount = 0;
                        // 重启后台任务
                        StartBackgroundTasks();
                    }
                    else
                    {
                        Message = $"PLC重连失败（第{_reconnectCount}次）：{_plcConfig.IpAddress}:{_plcConfig.Port}";
                        Logs.LogError(Message);
                        Task.Delay(_plcConfig.ReconnectDelay).Wait();
                    }
                }
                catch (Exception ex)
                {
                    string errorMsg = $"PLC重连任务异常：{ex.Message}";
                    Message = errorMsg;
                    Logs.LogError(ex);
                    Task.Delay(_plcConfig.ReconnectDelay).Wait();
                }
            }
            Logs.LogInfo("PLC重连任务已终止");
        }
        #endregion

        #region Fins协议核心方法：帧封装/解析/响应判断（欧姆龙Fins TCP核心）
        /// <summary>
        /// 封装Fins标准心跳指令帧（节点确认：01 01）
        /// </summary>
        /// <returns>Fins指令帧</returns>
        private byte[] PackFinsHeartbeatFrame()
        {
            // Fins TCP帧结构：报头(8字节) + 命令码(2字节) + 参数(2字节)
            List<byte> frame = new List<byte>
            {
                0x46,0x49,0x4E,0x53, // FINS固定报头
                0x00,0x00,0x00,0x0C, // 帧长度（固定12）
                _plcConfig.NetworkNo, _plcConfig.UnitNo, _plcConfig.PlcNode, // 目标地址
                _plcConfig.LocalNode, 0x00, 0x00, // 源地址
                0x01,0x01, // 命令码：节点确认
                0x00,0x00  // 参数：无
            };
            return frame.ToArray();
        }

        /// <summary>
        /// 封装Fins读寄存器指令帧
        /// </summary>
        private byte[] PackFinsReadFrame(PLCRegisterType registerType, ushort address, ushort count)
        {
            // 获取寄存器Fins码
            byte areaCode = GetRegisterAreaCode(registerType);
            // 地址拆分（高8位+低8位）
            byte addrH = (byte)(address >> 8);
            byte addrL = (byte)(address & 0xFF);
            // 个数拆分
            byte countH = (byte)(count >> 8);
            byte countL = (byte)(count & 0xFF);

            // Fins读指令帧结构
            List<byte> frame = new List<byte>
            {
                0x46,0x49,0x4E,0x53, // FINS报头
                0x00,0x00,0x00,0x10, // 帧长度
                _plcConfig.NetworkNo, _plcConfig.UnitNo, _plcConfig.PlcNode, // 目标
                _plcConfig.LocalNode, 0x00, 0x00, // 源
                0x01,0x02, // 命令码：内存区读取
                areaCode,0x00, // 寄存器区码
                addrH,addrL,0x00,0x00, // 起始地址
                countH,countL  // 读取个数
            };
            return frame.ToArray();
        }

        /// <summary>
        /// 封装Fins写寄存器指令帧
        /// </summary>
        private byte[] PackFinsWriteFrame(PLCRegisterType registerType, ushort startAddress, List<int> values, bool isBCD)
        {
            // 获取寄存器Fins码
            byte areaCode = GetRegisterAreaCode(registerType);
            // 地址拆分
            byte addrH = (byte)(startAddress >> 8);
            byte addrL = (byte)(startAddress & 0xFF);
            // 个数拆分
            byte countH = (byte)(values.Count >> 8);
            byte countL = (byte)(values.Count & 0xFF);

            // 初始化帧
            List<byte> frame = new List<byte>
            {
                0x46,0x49,0x4E,0x53, // FINS报头
                0x00,0x00,0x00,0x00, // 帧长度（后续计算）
                _plcConfig.NetworkNo, _plcConfig.UnitNo, _plcConfig.PlcNode, // 目标
                _plcConfig.LocalNode, 0x00, 0x00, // 源
                0x01,0x02, // 命令码：内存区写入
                areaCode,0x00, // 寄存器区码
                addrH,addrL,0x00,0x00, // 起始地址
                countH,countL  // 写入个数
            };

            // 追加写入值（大端序，字单位）
            foreach (int val in values)
            {
                // 数据格式转换（BCD/二进制）
                ushort writeValue = ConvertToPlcValue(val, isBCD);
                // 大端序：高8位在前，低8位在后
                frame.Add((byte)(writeValue >> 8));
                frame.Add((byte)(writeValue & 0xFF));
            }

            // 计算并更新帧长度（Fins帧长度为总字节数-4）
            byte[] lengthBytes = BitConverter.GetBytes((ushort)(frame.Count - 4));
            Array.Reverse(lengthBytes); // 大端序
            frame[4] = lengthBytes[0];
            frame[5] = lengthBytes[1];
            frame[6] = 0x00;
            frame[7] = 0x00;

            return frame.ToArray();
        }

        /// <summary>
        /// 解析Fins单地址读响应
        /// </summary>
        private int ParseFinsReadResponse(byte[] response, bool isBCD)
        {
            if (response == null || response.Length < 20) return -1;
            // 数据区从第20字节开始，单字为2字节
            ushort rawValue = (ushort)((response[20] << 8) | response[21]);
            // 转换为十进制（BCD/二进制）
            return ConvertFromPlcValue(rawValue, isBCD);
        }

        /// <summary>
        /// 解析Fins批量读响应
        /// </summary>
        private List<int> ParseFinsBatchReadResponse(byte[] response, ushort count, bool isBCD)
        {
            List<int> result = new List<int>();
            if (response == null || response.Length < 20 + count * 2) return result;

            // 从第20字节开始，逐个解析字数据
            for (int i = 0; i < count; i++)
            {
                int index = 20 + i * 2;
                ushort rawValue = (ushort)((response[index] << 8) | response[index + 1]);
                result.Add(ConvertFromPlcValue(rawValue, isBCD));
            }
            return result;
        }

        /// <summary>
        /// 判断Fins响应是否成功（响应码0000为成功）
        /// </summary>
        private bool IsFinsResponseSuccess(byte[] response)
        {
            if (response == null || response.Length < 18) return false;
            // 响应码在第16、17字节，00 00为成功
            return response[16] == 0x00 && response[17] == 0x00;
        }

        /// <summary>
        /// 获取Fins错误信息
        /// </summary>
        private string GetFinsErrorMsg(byte[] response)
        {
            if (response == null || response.Length < 18)
                return "响应帧为空或格式错误";
            // 转换响应码为十六进制
            string errorCode = $"{response[16]:X2}{response[17]:X2}";
            return errorCode == "0000" ? "执行成功" : $"响应码：{errorCode}（参考欧姆龙Fins协议手册）";
        }

        private byte GetRegisterAreaCode(PLCRegisterType registerType)
        {
            switch (registerType)
            {
                case PLCRegisterType.D:
                    return 0x82;
                case PLCRegisterType.W:
                    return 0x30;
                case PLCRegisterType.X:
                    return 0x00;
                case PLCRegisterType.Y:
                    return 0x01;
                case PLCRegisterType.C:
                    return 0x84;
                case PLCRegisterType.T:
                    return 0x85;
                default:
                    return 0x82; // 默认D区，和原逻辑一致
            }
        }

        /// <summary>
        /// 转换值为PLC可识别的格式（十进制→二进制/BCD）（.NET4.7.2兼容，移除Math.Clamp）
        /// </summary>
        private ushort ConvertToPlcValue(int value, bool isBCD)
        {
            // 替代Math.Clamp：限制值范围为0-65535（PLC16位字的取值范围）
            int clampedValue = value;
            if (clampedValue < 0)
            {
                clampedValue = 0;
            }
            else if (clampedValue > 65535)
            {
                clampedValue = 65535;
            }

            if (isBCD)
            {
                // BCD码仅支持0-9999，再次手动限制范围（替代Math.Clamp）
                if (clampedValue < 0)
                {
                    clampedValue = 0;
                }
                else if (clampedValue > 9999)
                {
                    clampedValue = 9999;
                }
                // BCD码转换逻辑不变
                return (ushort)(((clampedValue / 1000) << 12) | ((clampedValue / 100 % 10) << 8) | ((clampedValue / 10 % 10) << 4) | (clampedValue % 10));
            }
            else
            {
                // 二进制直接转换，逻辑不变
                return (ushort)clampedValue;
            }
        }

        /// <summary>
        /// 从PLC值转换为十进制（二进制/BCD→十进制）
        /// </summary>
        private int ConvertFromPlcValue(ushort rawValue, bool isBCD)
        {
            if (isBCD)
            {
                // BCD码→十进制
                return ((rawValue >> 12) & 0x0F) * 1000 +
                       ((rawValue >> 8) & 0x0F) * 100 +
                       ((rawValue >> 4) & 0x0F) * 10 +
                       (rawValue & 0x0F);
            }
            else
            {
                // 二进制直接转换
                return rawValue;
            }
        }
        #endregion

        #region 私有辅助方法：基础状态校验/字节转十六进制/发送并等待响应
        /// <summary>
        /// PLC基础状态校验（初始化+连接）
        /// </summary>
        private bool CheckBaseState()
        {
            if (!Initialized || !Connected || _plcConfig == null || _tcpClient == null || !_tcpClient.Connected)
            {
                Message = "PLC未初始化或未连接，操作失败";
                Logs.LogError(Message);
                return false;
            }
            return true;
        }

        /// <summary>
        /// 字节数组转十六进制字符串（便于日志/调试）
        /// </summary>
        private string ByteToHex(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) return string.Empty;
            return string.Join(" ", bytes.Select(b => b.ToString("X2")));
        }

        /// <summary>
        /// 发送Fins指令并等待响应（同步，带超时）
        /// </summary>
        private byte[] SendAndWaitResponse(byte[] finsFrame)
        {
            // 清空历史响应
            _latestResponse = null;
            // 指令入队
            if (_sendQueue.Count >= _plcConfig.MaxSendQueueLength)
            {
                Message = "PLC指令发送队列已满，操作失败";
                Logs.LogError(Message);
                return null;
            }
            _sendQueue.Enqueue(finsFrame);

            // 等待响应（带超时）
            bool waitSuccess = _responseWaitEvent.WaitOne(_plcConfig.Timeout);
            if (!waitSuccess)
            {
                Message = "PLC指令发送后超时未收到响应";
                Logs.LogError(Message);
                return null;
            }

            // 解析接收队列中的最新响应
            if (_receiveQueue.TryDequeue(out byte[] response))
            {
                _latestResponse = response;
                // 更新接收帧（十六进制字符串，便于外部查看）
                ReceiveFrame = ByteToHex(response);
                return response;
            }

            return null;
        }
        #endregion
    }
}