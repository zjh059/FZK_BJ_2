using FZK.Hardware.PLC.Base;
using FZK.Logger;
using ReactiveUI;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;

namespace FZK.Hardware.PLC.Omron
{
    internal class PLCOmronNJ501_1400 : ReactiveObject, IPLC
    {
        // ===================== 常量定义 =====================
        private const int TcpHeaderLength = 16;
        private const int FinsHeaderLength = 10;
        private const int HandshakeLength = 20;
        private const int ReceiveBufferSize = 8192;
        private const int MinResponseLength = TcpHeaderLength + FinsHeaderLength + 4; // 4 = 2字节指令码 + 2字节错误码
        private const int FinsHeaderSidOffset = 9;  // SID在FINS头中的偏移（0-based）
        // ====================================================

        public bool Initialized { get; set; }

        // ===================== 线程安全状态封装 =====================
        private readonly object _stateLock = new object();
        private bool _connected;
        public bool Connected
        {
            get { lock (_stateLock) return _connected; }
            set { lock (_stateLock) _connected = value; }
        }

        private bool _handshakeCompleted;
        public bool HandshakeCompleted
        {
            get { lock (_stateLock) return _handshakeCompleted; }
            private set { lock (_stateLock) _handshakeCompleted = value; }
        }
        // =============================================================

        public PLCState PLCState { get; set; }

        // ===================== 可观察属性实现 =====================
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

        // ===========================================================

        #region 私有核心字段
        private TcpClient _tcpClient;
        private CancellationTokenSource _cancellationTokenSource;
        private PLCConfig _plcConfig;
        private readonly ConcurrentQueue<byte[]> _sendQueue = new ConcurrentQueue<byte[]>();
        private readonly ConcurrentDictionary<byte, TaskCompletionSource<byte[]>> _waitingCommands
            = new ConcurrentDictionary<byte, TaskCompletionSource<byte[]>>();
        private readonly ConcurrentQueue<byte[]> _sendQueueNoResponse = new ConcurrentQueue<byte[]>();

        private Task _receiveTask;
        private Task _sendTask;
        private Task _heartbeatTask;
        private Task _reconnectTask;
        private readonly object _taskLock = new object();

        private int _reconnectCount;
        private readonly byte[] _receiveBuffer = new byte[ReceiveBufferSize];

        private FinsHelper.FinsConfig _finsConfig;
        private byte _plcNegotiatedNode = 0;
        #endregion

        #region 构造函数
        public PLCOmronNJ501_1400()
        {
            PLCState = PLCState.UnInitialized;
            _cancellationTokenSource = new CancellationTokenSource();
            _tcpClient = new TcpClient();
            Message = "PLC驱动已实例化，未初始化连接";
            Logs.LogTrace(Message);
        }
        #endregion

        #region 核心接口实现：Init
        public bool Init(PLCConfig config)
        {
            try
            {
                if (Initialized)
                {
                    Message = "PLC已完成初始化，无需重复执行";
                    Logs.LogTrace(Message);
                    return true;
                }

                if (config == null || string.IsNullOrWhiteSpace(config.IpAddress))
                {
                    string errorMsg = "PLC配置为空或IP地址无效，初始化失败";
                    Message = errorMsg;
                    Logs.LogError(errorMsg);
                    return false;
                }

                _plcConfig = config;

                _finsConfig = new FinsHelper.FinsConfig
                {
                    SourceNode = _plcConfig.LocalNode,
                    TargetNode = _plcConfig.PlcNode,
                    NetworkNo = _plcConfig.NetworkNo,
                    SID = 0
                };

                PLCState = PLCState.Connecting;
                Message = $"正在连接欧姆龙PLC：{config.IpAddress}:{config.Port}";
                Logs.LogTrace(Message);

                bool connectSuccess = TryConnectAndHandshake();
                if (!connectSuccess)
                {
                    string errorMsg = $"首次连接PLC失败：{config.IpAddress}:{config.Port}";
                    Message = errorMsg;
                    Logs.LogError(errorMsg);
                    PLCState = PLCState.UnInitialized;
                    return false;
                }

                Initialized = true;
                Connected = true;
                PLCState = PLCState.Connected;
                Message = $"PLC连接成功：{config.IpAddress}:{config.Port}（Fins TCP，节点：{_plcNegotiatedNode}）";
                Logs.LogTrace(Message);
                _reconnectCount = 0;

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

        #region 核心接口实现：寄存器读写
        public int Read(PLCRegisterType registerType, ushort address, bool isBCD = false)
        {
            if (!CheckBaseState()) return -1;

            try
            {
                byte areaCode = GetRegisterAreaCode(registerType);
                byte[] response = ExecuteCommand(
                    config => FinsHelper.BuildReadUInt16Command(config, areaCode, address),
                    $"Read_{registerType}{address}");

                if (response == null) return -1;

                ushort rawValue = FinsHelper.ParseReadUInt16Response(response);
                int value = ConvertFromPlcValue(rawValue, isBCD);

                Message = $"读取PLC{registerType}{address}成功，值：{value}";
                Logs.LogTrace(Message);
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

        public List<int> BatchRead(PLCRegisterType registerType, ushort startAddress, ushort count, bool isBCD = false)
        {
            if (!CheckBaseState() || count <= 0 || count > 100)
            {
                Message = count > 100 ? "PLC批量读取个数不能超过100" : "PLC基础状态校验不通过";
                Logs.LogError(Message);
                return new List<int>();
            }

            try
            {
                byte areaCode = GetRegisterAreaCode(registerType);
                byte[] response = ExecuteCommand(
                    config => FinsHelper.BuildBatchReadCommand(config, areaCode, startAddress, count),
                    $"BatchRead_{registerType}{startAddress}_{count}");

                if (response == null) return new List<int>();

                ushort[] rawValues = FinsHelper.ParseBatchReadResponse(response, count);
                List<int> values = rawValues.Select(v => ConvertFromPlcValue(v, isBCD)).ToList();

                Message = $"批量读取PLC{registerType}{startAddress}-{startAddress + count - 1}成功";
                Logs.LogTrace(Message);
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

        public bool Write(PLCRegisterType registerType, ushort address, int value, bool isBCD = false)
        {
            if (!CheckBaseState()) return false;

            try
            {
                ushort writeValue = ConvertToPlcValue(value, isBCD);
                byte areaCode = GetRegisterAreaCode(registerType);
                byte[] response = ExecuteCommand(
                    config => FinsHelper.BuildWriteUInt16Command(config, areaCode, address, writeValue),
                    $"Write_{registerType}{address}");

                if (response == null || !FinsHelper.ParseWriteUInt16Response(response))
                {
                    Message = $"写入PLC{registerType}{address}失败";
                    Logs.LogError(Message);
                    return false;
                }

                Message = $"写入PLC{registerType}{address}成功，值：{value}";
                Logs.LogTrace(Message);
                return true;
            }
            catch (Exception ex)
            {
                string errorMsg = $"写入PLC寄存器异常：{ex.Message}";
                Message = errorMsg;
                Logs.LogError(ex);
                return false;
            }
        }

        public bool BatchWrite(PLCRegisterType registerType, ushort startAddress, List<int> values, bool isBCD = false)
        {
            if (!CheckBaseState() || values == null || values.Count == 0 || values.Count > 100)
            {
                Message = values.Count > 100 ? "PLC批量写入个数不能超过100" : "PLC基础状态/写入值校验不通过";
                Logs.LogError(Message);
                return false;
            }

            try
            {
                ushort[] writeValues = values.Select(v => ConvertToPlcValue(v, isBCD)).ToArray();
                byte areaCode = GetRegisterAreaCode(registerType);
                byte[] response = ExecuteCommand(
                    config => FinsHelper.BuildBatchWriteCommand(config, areaCode, startAddress, writeValues),
                    $"BatchWrite_{registerType}{startAddress}_{values.Count}");

                if (response == null || !FinsHelper.ParseBatchWriteResponse(response))
                {
                    Message = $"批量写入PLC{registerType}{startAddress}失败";
                    Logs.LogError(Message);
                    return false;
                }

                Message = $"批量写入PLC{registerType}{startAddress}成功，共{values.Count}个值";
                Logs.LogTrace(Message);
                return true;
            }
            catch (Exception ex)
            {
                string errorMsg = $"批量写入PLC寄存器异常：{ex.Message}";
                Message = errorMsg;
                Logs.LogError(ex);
                return false;
            }
        }
        #endregion

        #region 核心接口实现：CheckConnection + Close
        private readonly object _reconnectLock = new object();
        public bool CheckConnection()
        {
            lock (_reconnectLock)
            {
                if (Connected && _tcpClient != null && _tcpClient.Connected && HandshakeCompleted)
                    return true;

                PLCState = PLCState.Reconnecting;
                Message = "PLC连接异常，正在重连";
                Logs.LogError(Message);
                return TryConnectAndHandshake();
            }
        }

        public void Close()
        {
            try
            {
                PLCState = PLCState.Disconnecting;
                Message = "正在关闭PLC连接，释放资源...";
                Logs.LogTrace(Message);

                // 1. 终止所有后台任务
                if (!_cancellationTokenSource.IsCancellationRequested)
                    _cancellationTokenSource.Cancel();

                // 2. 等待任务结束（使用StopBackgroundTasks完成）
                StopBackgroundTasks();

                // 3. 取消所有等待的命令
                foreach (var kv in _waitingCommands)
                {
                    kv.Value.TrySetCanceled();
                }
                _waitingCommands.Clear();

                // 4. 清空收发队列
                while (_sendQueue.TryDequeue(out _)) { }
                while (_sendQueueNoResponse.TryDequeue(out _)) { }

                // 5. 关闭TCP连接
                if (_tcpClient != null)
                {
                    try
                    {
                        if (_tcpClient.Connected)
                            _tcpClient.Client.Shutdown(SocketShutdown.Both);
                    }
                    catch { }
                    _tcpClient.Close();
                    _tcpClient = new TcpClient();
                }

                // 6. 释放资源
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = new CancellationTokenSource();

                // 7. 重置状态（包括重连计数和节点号）
                Initialized = false;
                Connected = false;
                HandshakeCompleted = false;
                _reconnectCount = 0;
                _plcNegotiatedNode = 0;

                PLCState = PLCState.UnInitialized;
                Message = "PLC连接已关闭，所有资源释放完成";
                Logs.LogTrace(Message);
            }

            catch (Exception ex)
            {
                string errorMsg = $"关闭PLC连接异常：{ex.Message}";
                Message = errorMsg;
                Logs.LogError(ex);
            }
        }
        #endregion

        #region 私有核心方法：TCP连接 + FINS握手
        private bool TryConnectAndHandshake()
        {
            try
            {
                // 先关闭旧连接（如有）
                _tcpClient?.Close();

                // 创建新实例（局部变量，不受字段变化影响）
                var newClient = new TcpClient();
                newClient.ReceiveTimeout = _plcConfig?.Timeout ?? 3000;
                newClient.SendTimeout = _plcConfig?.Timeout ?? 3000;

                // 使用局部变量进行异步连接
                var connectTask = newClient.ConnectAsync(_plcConfig.IpAddress, _plcConfig.Port);
                bool connectSuccess = connectTask.Wait(_plcConfig.Timeout);

                if (!connectSuccess)
                {
                    Logs.LogError("TCP连接失败");
                    newClient.Close();
                    return false;
                }

                Logs.LogTrace("TCP连接成功，开始FINS握手...");

                byte[] handshakeFrame = FinsHelper.BuildHandshakeCommand(_plcConfig.LocalNode);
                newClient.Client.Send(handshakeFrame);
                Logs.LogError($"发送:{ByteToHex(handshakeFrame)}");

                byte[] response = new byte[HandshakeLength];
                int totalRead = 0;
                DateTime startTime = DateTime.Now;

                while (totalRead < HandshakeLength && (DateTime.Now - startTime).TotalMilliseconds < _plcConfig.Timeout)
                {
                    if (newClient.Available > 0)
                    {
                        int read = newClient.Client.Receive(response, totalRead, HandshakeLength - totalRead, SocketFlags.None);
                        if (read > 0)
                            totalRead += read;
                    }
                    else
                    {
                        Thread.Sleep(10);
                    }
                }

                if (totalRead < HandshakeLength || !FinsHelper.ParseHandshakeResponse(response))
                {
                    Logs.LogError($"收到:{ByteToHex(response)}");
                    Logs.LogError("FINS握手失败");
                    newClient.Close();
                    return false;
                }

                // 握手成功，原子地替换字段（确保其他线程看到的是完整初始化的对象）
                var oldClient = Interlocked.Exchange(ref _tcpClient, newClient);
                oldClient?.Close(); // 理论上 oldClient 已关闭，但再次确保

                _plcNegotiatedNode = response[19];
                _finsConfig.TargetNode = _plcNegotiatedNode;
                HandshakeCompleted = true;
                Logs.LogTrace($"FINS握手成功，PLC节点号：{_plcNegotiatedNode}");
                Connected = true;
                Initialized = true;
                return true;
            }
            catch (Exception ex)
            {
                Logs.LogError($"连接/握手异常：{ex.Message}");
                return false;
            }
        }
        #endregion

        #region 私有核心方法：命令执行（使用SID精确匹配）
        private byte[] ExecuteCommand(Func<FinsHelper.FinsConfig, byte[]> buildFrameFunc, string commandDesc)
        {
            var requestConfig = _finsConfig.CloneForRequest();
            byte[] frame = buildFrameFunc(requestConfig);
            byte sid = requestConfig.SID;

            var tcs = new TaskCompletionSource<byte[]>();

            if (!_waitingCommands.TryAdd(sid, tcs))
            {
                Logs.LogError($"SID {sid} 已存在，命令{commandDesc}注册失败");
                return null;
            }

            try
            {
                if (_sendQueue.Count >= _plcConfig.MaxSendQueueLength)
                {
                    Message = "PLC指令发送队列已满";
                    Logs.LogError(Message);
                    return null;
                }

                _sendQueue.Enqueue(frame);
                Logs.LogTrace($"命令{commandDesc} (SID={sid}) 已入队：{ByteToHex(frame)}");

                if (!tcs.Task.Wait(_plcConfig.Timeout))
                {
                    Message = $"命令{commandDesc}执行超时";
                    Logs.LogError(Message);
                    return null;
                }

                byte[] response = tcs.Task.Result;
                ReceiveFrame = ByteToHex(response);
                return response;
            }
            catch (Exception ex)
            {
                Logs.LogError($"命令{commandDesc}执行异常：{ex.Message}");
                return null;
            }
            finally
            {
                _waitingCommands.TryRemove(sid, out _);
            }
        }
        #endregion

        #region 后台任务（优化版）

        private bool IsClientConnected(TcpClient client)
        {
            if (client == null) return false;
            try
            {
                return client.Connected;
            }
            catch (NullReferenceException)
            {
                if (!_cancellationTokenSource.IsCancellationRequested)
                {
                    Connected = false;
                    HandshakeCompleted = false;
                    Logs.LogError("接收任务：TcpClient 内部状态异常（NullReferenceException）");
                }
                Thread.Sleep(100);
                return false;
            }
            catch
            {
                // 任何异常均视为未连接
                return false;
            }
        }
        private void ReceiveTask()
        {
            var token = _cancellationTokenSource.Token;
            while (!token.IsCancellationRequested)
            {
                TcpClient currentClient = null;
                try
                {
                    // 获取当前客户端引用
                    currentClient = _tcpClient;
                    if (currentClient == null || !IsClientConnected(currentClient) || !HandshakeCompleted)
                    {
                        Thread.Sleep(10);
                        continue;
                    }

                    // 再次检查 Available，避免在检查后连接断开
                    if (currentClient.Available <= 0)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    // 执行接收
                    int receiveLength = currentClient.Client.Receive(_receiveBuffer);
                    if (receiveLength <= 0)
                    {
                        // 接收0字节表示连接关闭
                        Connected = false;
                        HandshakeCompleted = false;
                        Logs.LogWarn("接收返回0字节，连接可能已关闭");
                        Thread.Sleep(100);
                        continue;
                    }

                    byte[] receivedData = new byte[receiveLength];
                    Array.Copy(_receiveBuffer, receivedData, receiveLength);
                    Logs.LogTrace($"收到原始数据：{ByteToHex(receivedData)}");

                    if (receivedData.Length >= MinResponseLength)
                    {
                        byte sid = receivedData[TcpHeaderLength + FinsHeaderSidOffset];
                        if (sid == HeartbeatSid)
                        {
                            // 心跳响应，直接忽略，不进行任何匹配
                            continue;
                        }

                        Logs.LogTrace($"接收到响应，SID={sid}，等待列表：{string.Join(",", _waitingCommands.Keys)}");

                        if (_waitingCommands.TryRemove(sid, out var tcs) && !tcs.Task.IsCompleted)
                        {
                            tcs.TrySetResult(receivedData);
                            Logs.LogTrace($"SID={sid} 匹配成功，响应已设置");
                            if (receivedData.Length >= 16 + 10 + 2 + 2) // 确保足够长
                            {
                                byte mainCode = receivedData[16 + 10 + 2];
                                byte subCode = receivedData[16 + 10 + 2 + 1];
                                Logs.LogTrace($"响应错误码：主码=0x{mainCode:X2}, 副码=0x{subCode:X2}");
                            }
                        }
                        else
                        {
                            Logs.LogTrace($"收到未匹配的SID：{sid}，数据：{ByteToHex(receivedData)}");
                        }
                    }
                }
                catch (ObjectDisposedException)
                {
                    // 对象已释放，说明连接已被主动关闭
                    if (!token.IsCancellationRequested)
                    {
                        Connected = false;
                        HandshakeCompleted = false;
                        Logs.LogTrace("接收任务：TcpClient 已被释放");
                    }
                    Thread.Sleep(100);
                }
                catch (SocketException ex)
                {
                    // 网络错误，标记断开
                    if (!token.IsCancellationRequested)
                    {
                        Connected = false;
                        HandshakeCompleted = false;
                        Logs.LogError($"接收任务 Socket 异常：{ex.SocketErrorCode} - {ex.Message}");
                    }
                    Thread.Sleep(100);
                }
                catch (NullReferenceException)
                {
                    if (!_cancellationTokenSource.IsCancellationRequested)
                    {
                        Connected = false;
                        HandshakeCompleted = false;
                        Logs.LogError("接收任务：TcpClient 内部状态异常（NullReferenceException）");
                    }
                    Thread.Sleep(100);
                }
                catch (Exception ex)
                {
                    // 其他未知异常
                    if (!token.IsCancellationRequested)
                    {
                        Connected = false;
                        HandshakeCompleted = false;
                        Logs.LogError($"接收任务未知异常：{ex.Message}");
                    }
                    Thread.Sleep(100);
                }

            }
            Logs.LogTrace("接收任务已终止");
        }

        private void SendTask()
        {
            while (!_cancellationTokenSource.IsCancellationRequested)
            {
                try
                {
                    TcpClient currentClient = _tcpClient;
                    if (currentClient == null || !IsClientConnected(currentClient) || !HandshakeCompleted)
                    {
                        Thread.Sleep(10);
                        continue;
                    }

                    if (_sendQueue.TryDequeue(out byte[] frame))
                    {
                        SendFrame(currentClient, frame);
                        continue;
                    }

                    if (_sendQueueNoResponse.TryDequeue(out byte[] frameNoResp))
                    {
                        SendFrame(currentClient, frameNoResp);
                        continue;
                    }

                    Thread.Sleep(1);

                }
                catch (Exception ex)
                {
                    if (!_cancellationTokenSource.IsCancellationRequested)
                    {
                        Connected = false;
                        HandshakeCompleted = false;
                        Logs.LogError($"发送异常：{ex.Message}");
                    }
                    Thread.Sleep(100);
                }
            }
            Logs.LogTrace("发送任务已终止");
        }
        private void SendFrame(TcpClient client, byte[] frame)
        {
            byte[] lengthHeader = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(frame.Length));
            client.Client.Send(lengthHeader);
            client.Client.Send(frame);
            if (_plcConfig.SendInterval > 0)
                Thread.Sleep(_plcConfig.SendInterval);
        }
        private async Task HeartbeatTaskAsync()
        {
            var token = _cancellationTokenSource.Token;
            bool isSetOn = true;

            while (!_cancellationTokenSource.IsCancellationRequested)
            {
                try
                {
                    if (!HandshakeCompleted || _plcConfig == null || _plcConfig.HeartbeatInterval <= 0)
                    {
                        Thread.Sleep(1000);
                        continue;
                    }
                    // 队列保护
                    if (_sendQueue.Count >= _plcConfig.MaxSendQueueLength ||
    _sendQueueNoResponse.Count >= _plcConfig.MaxSendQueueLength)
                    {
                        Logs.LogWarn("心跳发送队列已满，跳过本次心跳");
                        await Task.Delay(_plcConfig.HeartbeatInterval, token);
                        continue;
                    }

                    int heartbeatValue = isSetOn ? 1 : 0;
                    ushort writeValue = ConvertToPlcValue(heartbeatValue, false);
                    byte areaCode = GetRegisterAreaCode(_plcConfig.HeartbeatRegisterType);

                    var heartbeatConfig = _finsConfig.HeartRequest();
                    byte[] frame = FinsHelper.BuildWriteUInt16Command(
                        heartbeatConfig,
                        areaCode,
                        _plcConfig.HeartbeatAddress,
                        writeValue);

                    _sendQueueNoResponse.Enqueue(frame);
                    Logs.LogTrace($"心跳指令 (SID={heartbeatConfig.SID}) 已入队：{ByteToHex(frame)}");

                    isSetOn = !isSetOn;
                    await Task.Delay(_plcConfig.HeartbeatInterval, _cancellationTokenSource.Token);
                }
                catch (OperationCanceledException)
                {
                    // 正常取消，退出循环
                    break;
                }
                catch (Exception ex)
                {
                    Logs.LogError($"心跳异常：{ex.Message}");
                    Connected = false;
                    HandshakeCompleted = false;
                    Thread.Sleep(1000);
                }
            }
            Logs.LogTrace("心跳任务已终止");
        }

        private void ReconnectTask()
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
                        string errorMsg = $"PLC重连次数超限（最大：{_plcConfig.MaxReconnectCount}）";
                        Message = errorMsg;
                        Logs.LogError(errorMsg);
                        Initialized = false;
                        HandshakeCompleted = false;
                        PLCState = PLCState.UnInitialized;
                        Thread.Sleep(1000);
                        continue;
                    }
                    Thread.Sleep(1000);
                    _reconnectCount++;
                    PLCState = PLCState.Reconnecting;
                    Message = $"PLC重连中（第{_reconnectCount}次）：{_plcConfig.IpAddress}:{_plcConfig.Port}";
                    Logs.LogError(Message);

                    bool reconnectSuccess = TryConnectAndHandshake();

                    if (reconnectSuccess)
                    {
                        Connected = true;
                        HandshakeCompleted = true;
                        PLCState = PLCState.Connected;
                        Message = $"PLC重连成功（第{_reconnectCount}次）";
                        Logs.LogTrace(Message);
                        _reconnectCount = 0;
                        StartBackgroundTasks();
                    }
                    else
                    {
                        Message = $"PLC重连失败（第{_reconnectCount}次）";
                        Logs.LogError(Message);
                        Thread.Sleep(_plcConfig.ReconnectDelay);
                    }
                }
                catch (Exception ex)
                {
                    Logs.LogError($"重连任务异常：{ex.Message}");
                    Thread.Sleep(_plcConfig.ReconnectDelay);
                }
            }
            Logs.LogTrace("重连任务已终止");
        }
        #endregion

        #region 辅助方法
        private void StartBackgroundTasks()
        {
            lock (_taskLock)
            {
                if (_receiveTask == null || _receiveTask.IsCompleted)
                    _receiveTask = Task.Factory.StartNew(ReceiveTask, _cancellationTokenSource.Token,
                        TaskCreationOptions.LongRunning, TaskScheduler.Default);
                if (_sendTask == null || _sendTask.IsCompleted)
                    _sendTask = Task.Factory.StartNew(SendTask, _cancellationTokenSource.Token,
                        TaskCreationOptions.LongRunning, TaskScheduler.Default);
                if (_plcConfig.HeartbeatIsOpen)
                {
                    if (_heartbeatTask == null || _heartbeatTask.IsCompleted)
                        _heartbeatTask = Task.Run(HeartbeatTaskAsync, _cancellationTokenSource.Token);
                }
                if (_reconnectTask == null || _reconnectTask.IsCompleted)
                    _reconnectTask = Task.Factory.StartNew(ReconnectTask, _cancellationTokenSource.Token,
                        TaskCreationOptions.LongRunning, TaskScheduler.Default);
            }
        }
        private void StopBackgroundTasks()
        {
            lock (_taskLock)
            {
                _cancellationTokenSource.Cancel();

                var tasks = new[] { _receiveTask, _sendTask, _heartbeatTask, _reconnectTask }
                    .Where(t => t != null && !t.IsCompleted).ToArray();

                // 等待任务退出（最多5秒）
                if (tasks.Length > 0)
                    Task.WaitAll(tasks, TimeSpan.FromSeconds(5));

                // 重建 TokenSource
                _cancellationTokenSource.Dispose();
                _cancellationTokenSource = new CancellationTokenSource();

                // 清空任务引用（可选）
                _receiveTask = _sendTask = _heartbeatTask = _reconnectTask = null;
            }
        }
        private bool CheckBaseState()
        {
            if (!Initialized || !Connected || !HandshakeCompleted ||
                _plcConfig == null || _tcpClient == null || !_tcpClient.Connected)
            {
                Message = "PLC未初始化、未连接或未完成握手，操作失败";
                Logs.LogError(Message);
                return false;
            }
            return true;
        }

        private string ByteToHex(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) return string.Empty;
            return BitConverter.ToString(bytes).Replace('-', ' ');
        }

        private ushort ConvertToPlcValue(int value, bool isBCD)
        {
            int clampedValue = value;
            if (clampedValue < 0) clampedValue = 0;
            else if (clampedValue > 65535) clampedValue = 65535;

            if (isBCD)
            {
                if (clampedValue > 9999) clampedValue = 9999;
                return (ushort)(((clampedValue / 1000) << 12) |
                               ((clampedValue / 100 % 10) << 8) |
                               ((clampedValue / 10 % 10) << 4) |
                               (clampedValue % 10));
            }
            return (ushort)clampedValue;
        }

        private int ConvertFromPlcValue(ushort rawValue, bool isBCD)
        {
            if (isBCD)
            {
                return ((rawValue >> 12) & 0x0F) * 1000 +
                       ((rawValue >> 8) & 0x0F) * 100 +
                       ((rawValue >> 4) & 0x0F) * 10 +
                       (rawValue & 0x0F);
            }
            return rawValue;
        }

        private byte GetRegisterAreaCode(PLCRegisterType registerType)
        {
            switch (registerType)
            {
                case PLCRegisterType.DM: return FinsHelper.AreaCode_DM;
                case PLCRegisterType.CIO: return FinsHelper.AreaCode_CIO;
                case PLCRegisterType.TIM: return FinsHelper.AreaCode_TIM;
                case PLCRegisterType.CNTR: return FinsHelper.AreaCode_CNTR;
                default: return FinsHelper.AreaCode_DM;
            }
        }
        #endregion
    }
}