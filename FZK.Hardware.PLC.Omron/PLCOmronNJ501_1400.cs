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
        private Task _sendNoRespTask;
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
            Logs.LogTrace("[PLC] 驱动实例已创建");
        }
        #endregion

        #region Init
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

                _finsConfig = new FinsHelper.FinsConfig
                {
                    SourceNode = _plcConfig.LocalNode,
                    TargetNode = _plcConfig.PlcNode,
                    NetworkNo = _plcConfig.NetworkNo,
                    SID = 0
                };

                PLCState = PLCState.Connecting;
                Logs.LogInfo($"[PLC] 正在连接 {config.IpAddress}:{config.Port}");

                bool connectSuccess = TryConnectAndHandshake();
                if (!connectSuccess)
                {
                    string errorMsg = $"首次连接PLC失败：{config.IpAddress}:{config.Port}";
                    Message = errorMsg;
                    Logs.LogError($"[PLC] {errorMsg}");
                    PLCState = PLCState.UnInitialized;
                    return false;
                }

                Initialized = true;
                Connected = true;
                PLCState = PLCState.Connected;
                Message = $"PLC连接成功：{config.IpAddress}:{config.Port}（Fins TCP，节点：{_plcNegotiatedNode}）";
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

                Logs.LogDebug($"[PLC] 读取{registerType}{address}成功 | 值={value}");
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
                string msg = count > 100 ? "PLC批量读取个数不能超过100" : "PLC基础状态校验不通过";
                Logs.LogWarn($"[PLC] {msg}");
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

                Logs.LogDebug($"[PLC] 批量读取{registerType}{startAddress}-{startAddress + count - 1}成功 | 数量={count}");
                return values;
            }
            catch (Exception ex)
            {
                Logs.LogError(ex, $"[PLC] 批量读取寄存器异常 | {registerType}{startAddress}-{startAddress + count - 1}");
                return new List<int>();
            }
        }

        public bool Write(PLCRegisterType registerType, ushort address, int value, bool isBCD = false, bool Require=true)
        {
            if (!CheckBaseState()) return false;

            try
            {
                ushort writeValue = ConvertToPlcValue(value, isBCD);
                byte areaCode = GetRegisterAreaCode(registerType);
                byte[] response = ExecuteCommand(
                    config => FinsHelper.BuildWriteUInt16Command(config, areaCode, address, writeValue, Require),
                    $"Write_{registerType}{address}");

                if (response == null || !FinsHelper.ParseWriteUInt16Response(response))
                {
                    Logs.LogError($"[PLC] 写入{registerType}{address}失败 | 值={value}");
                    return false;
                }

                Logs.LogDebug($"[PLC] 写入{registerType}{address}成功 | 值={value}");
                return true;
            }
            catch (Exception ex)
            {
                Logs.LogError(ex, $"[PLC] 写入寄存器异常 | {registerType}{address} | 值={value}");
                return false;
            }
        }

        public bool BatchWrite(PLCRegisterType registerType, ushort startAddress, List<int> values, bool isBCD = false, bool Require = true)
        {
            if (!CheckBaseState() || values == null || values.Count == 0 || values.Count > 100)
            {
                string msg = values.Count > 100 ? "PLC批量写入个数不能超过100" : "PLC基础状态/写入值校验不通过";
                Logs.LogWarn($"[PLC] {msg}");
                return false;
            }

            try
            {
                ushort[] writeValues = values.Select(v => ConvertToPlcValue(v, isBCD)).ToArray();
                byte areaCode = GetRegisterAreaCode(registerType);
                byte[] response = ExecuteCommand(
                    config => FinsHelper.BuildBatchWriteCommand(config, areaCode, startAddress, writeValues, Require),
                    $"BatchWrite_{registerType}{startAddress}_{values.Count}");

                if (response == null || !FinsHelper.ParseBatchWriteResponse(response))
                {
                    Logs.LogError($"[PLC] 批量写入{registerType}{startAddress}失败 | 数量={values.Count}");
                    return false;
                }

                Logs.LogDebug($"[PLC] 批量写入{registerType}{startAddress}成功 | 数量={values.Count}");
                return true;
            }
            catch (Exception ex)
            {
                Logs.LogError(ex, $"[PLC] 批量写入寄存器异常 | {registerType}{startAddress}");
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

                Logs.LogWarn("[PLC] 连接异常，尝试重连");
                PLCState = PLCState.Reconnecting;
                return TryConnectAndHandshake();
            }
        }

        public void Close()
        {
            try
            {
                PLCState = PLCState.Disconnecting;
                Logs.LogInfo("[PLC] 正在关闭连接，释放资源...");

                // 1. 终止所有后台任务
                if (!_cancellationTokenSource.IsCancellationRequested)
                    _cancellationTokenSource.Cancel();

                // 2. 等待任务结束
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

                // 7. 重置状态
                Initialized = false;
                Connected = false;
                HandshakeCompleted = false;
                _reconnectCount = 0;
                _plcNegotiatedNode = 0;

                PLCState = PLCState.UnInitialized;
                Logs.LogInfo("[PLC] 连接已关闭，资源释放完成");
            }
            catch (Exception ex)
            {
                Logs.LogError(ex, "[PLC] 关闭连接异常");
            }
        }
        #endregion

        #region 私有核心方法：TCP连接 + FINS握手
        private bool TryConnectAndHandshake()
        {
            try
            {
                // 先关闭旧连接
                _tcpClient?.Close();

                // 创建新实例
                var newClient = new TcpClient();
                newClient.ReceiveTimeout = _plcConfig?.Timeout ?? 3000;
                newClient.SendTimeout = _plcConfig?.Timeout ?? 3000;

                // 异步连接
                var connectTask = newClient.ConnectAsync(_plcConfig.IpAddress, _plcConfig.Port);
                bool connectSuccess = connectTask.Wait(_plcConfig.Timeout);

                if (!connectSuccess)
                {
                    Logs.LogError($"[PLC] TCP连接超时 | {_plcConfig.IpAddress}:{_plcConfig.Port}");
                    newClient.Close();
                    return false;
                }

                Logs.LogDebug("[PLC] TCP连接成功，开始FINS握手...");

                byte[] handshakeFrame = FinsHelper.BuildHandshakeCommand(_plcConfig.LocalNode);
                newClient.Client.Send(handshakeFrame);
                Logs.LogTrace($"[PLC] 发送握手帧: {ByteToHex(handshakeFrame)}");

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
                    Logs.LogError($"[PLC] FINS握手失败 | 收到长度={totalRead} 数据={ByteToHex(response)}");
                    newClient.Close();
                    return false;
                }

                // 握手成功，原子替换连接对象
                var oldClient = Interlocked.Exchange(ref _tcpClient, newClient);
                oldClient?.Close();

                _plcNegotiatedNode = response[19];
                _finsConfig.TargetNode = _plcNegotiatedNode;
                HandshakeCompleted = true;
                Connected = true;
                Initialized = true;
                Logs.LogInfo($"[PLC] FINS握手成功 | PLC节点={_plcNegotiatedNode}");
                return true;
            }
            catch (Exception ex)
            {
                Logs.LogError(ex, "[PLC] 连接/握手异常");
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
                Logs.LogError($"[PLC] SID {sid} 已存在，命令 {commandDesc} 注册失败");
                return null;
            }

            try
            {
                if (_sendQueue.Count >= _plcConfig.MaxSendQueueLength)
                {
                    Logs.LogWarn($"[PLC] 指令发送队列已满 | 队列长度={_plcConfig.MaxSendQueueLength}");
                    return null;
                }

                _sendQueue.Enqueue(frame);
                Logs.LogTrace($"[PLC] 命令 {commandDesc} (SID={sid}) 已入队 | 帧={ByteToHex(frame)}");

                if (!tcs.Task.Wait(_plcConfig.Timeout))
                {
                    Logs.LogError($"[PLC] 命令 {commandDesc} (SID={sid}) 执行超时");
                    return null;
                }

                byte[] response = tcs.Task.Result;
                ReceiveFrame = ByteToHex(response);
                return response;
            }
            catch (Exception ex)
            {
                Logs.LogError(ex, $"[PLC] 命令 {commandDesc} (SID={sid}) 执行异常");
                return null;
            }
            finally
            {
                _waitingCommands.TryRemove(sid, out _);
            }
        }
        #endregion

        #region 后台任务

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
                    Logs.LogError("[PLC] 接收任务：TcpClient 内部状态异常（NullReferenceException）");
                }
                Thread.Sleep(100);
                return false;
            }
            catch
            {
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
                    currentClient = _tcpClient;
                    if (currentClient == null || !IsClientConnected(currentClient) || !HandshakeCompleted)
                    {
                        Thread.Sleep(10);
                        continue;
                    }

                    if (currentClient.Available <= 0)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    int receiveLength = currentClient.Client.Receive(_receiveBuffer);
                    if (receiveLength <= 0)
                    {
                        Connected = false;
                        HandshakeCompleted = false;
                        Logs.LogWarn("[PLC] 接收返回0字节，连接可能已关闭");
                        Thread.Sleep(100);
                        continue;
                    }

                    byte[] receivedData = new byte[receiveLength];
                    Array.Copy(_receiveBuffer, receivedData, receiveLength);
                    Logs.LogTrace($"[PLC] 收到原始数据: {ByteToHex(receivedData)}");

                    if (receivedData.Length >= MinResponseLength)
                    {
                        byte sid = receivedData[TcpHeaderLength + FinsHeaderSidOffset];
                        if (sid == HeartbeatSid)
                        {
                            // 心跳响应，忽略
                            continue;
                        }

                        Logs.LogTrace($"[PLC] 接收到响应，SID={sid} | 等待列表：{string.Join(",", _waitingCommands.Keys)}");

                        if (_waitingCommands.TryRemove(sid, out var tcs) && !tcs.Task.IsCompleted)
                        {
                            tcs.TrySetResult(receivedData);
                            Logs.LogTrace($"[PLC] SID={sid} 匹配成功，响应已设置");
                            if (receivedData.Length >= 16 + 10 + 2 + 2)
                            {
                                byte mainCode = receivedData[16 + 10 + 2];
                                byte subCode = receivedData[16 + 10 + 2 + 1];
                                Logs.LogTrace($"[PLC] 响应错误码：主码=0x{mainCode:X2}, 副码=0x{subCode:X2}");
                            }
                        }
                        else
                        {
                            Logs.LogTrace($"[PLC] 收到未匹配的SID：{sid} | 数据={ByteToHex(receivedData)}");
                        }
                    }
                }
                catch (ObjectDisposedException)
                {
                    if (!token.IsCancellationRequested)
                    {
                        Connected = false;
                        HandshakeCompleted = false;
                        Logs.LogTrace("[PLC] 接收任务：TcpClient 已被释放");
                    }
                    Thread.Sleep(100);
                }
                catch (SocketException ex)
                {
                    if (!token.IsCancellationRequested)
                    {
                        Connected = false;
                        HandshakeCompleted = false;
                        Logs.LogError(ex, $"[PLC] 接收任务 Socket 异常 | {ex.SocketErrorCode}");
                    }
                    Thread.Sleep(100);
                }
                catch (NullReferenceException)
                {
                    if (!_cancellationTokenSource.IsCancellationRequested)
                    {
                        Connected = false;
                        HandshakeCompleted = false;
                        Logs.LogError("[PLC] 接收任务：TcpClient 内部状态异常（NullReferenceException）");
                    }
                    Thread.Sleep(100);
                }
                catch (Exception ex)
                {
                    if (!token.IsCancellationRequested)
                    {
                        Connected = false;
                        HandshakeCompleted = false;
                        Logs.LogError(ex, "[PLC] 接收任务未知异常");
                    }
                    Thread.Sleep(100);
                }
            }
            Logs.LogTrace("[PLC] 接收任务已终止");
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
                        if (_plcConfig.SendInterval > 0)
                            Thread.Sleep(_plcConfig.SendInterval);
                    }
                    else
                    {
                        Thread.Sleep(1);
                    }
                }
                catch (Exception ex)
                {
                    if (!_cancellationTokenSource.IsCancellationRequested)
                    {
                        Connected = false;
                        HandshakeCompleted = false;
                        Logs.LogError(ex, "[PLC] 发送任务异常");
                    }
                    Thread.Sleep(100);
                }
            }
            Logs.LogTrace("[PLC] 发送任务已终止");
        }

        private void SendNoRespTask()
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

                    if (_sendQueueNoResponse.TryDequeue(out byte[] frame))
                    {
                        SendFrame(currentClient, frame);
                    }
                    else
                    {
                        Thread.Sleep(1);
                    }
                }
                catch (Exception ex)
                {
                    if (!_cancellationTokenSource.IsCancellationRequested)
                    {
                        Connected = false;
                        HandshakeCompleted = false;
                        Logs.LogError(ex, "[PLC] 无响应发送任务异常");
                    }
                    Thread.Sleep(100);
                }
            }
            Logs.LogTrace("[PLC] 无响应发送任务已终止");
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
                        await Task.Delay(1000, token);
                        continue;
                    }

                    if (_sendQueue.Count >= _plcConfig.MaxSendQueueLength ||
                        _sendQueueNoResponse.Count >= _plcConfig.MaxSendQueueLength)
                    {
                        Logs.LogWarn("[PLC] 心跳发送队列已满，跳过本次心跳");
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
                        writeValue,false);

                    _sendQueueNoResponse.Enqueue(frame);
                    Logs.LogTrace($"[PLC] 心跳指令 (SID={heartbeatConfig.SID}) 已入队 | 帧={ByteToHex(frame)}");

                    isSetOn = !isSetOn;
                    await Task.Delay(_plcConfig.HeartbeatInterval, token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Logs.LogError(ex, "[PLC] 心跳任务异常");
                    Connected = false;
                    HandshakeCompleted = false;
                    await Task.Delay(1000, token);
                }
            }
            Logs.LogTrace("[PLC] 心跳任务已终止");
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
                        Logs.LogError($"[PLC] {errorMsg}");
                        Initialized = false;
                        HandshakeCompleted = false;
                        PLCState = PLCState.UnInitialized;
                        Thread.Sleep(1000);
                        continue;
                    }

                    Thread.Sleep(1000);
                    _reconnectCount++;
                    PLCState = PLCState.Reconnecting;
                    Logs.LogInfo($"[PLC] 重连中（第{_reconnectCount}次）...");

                    bool reconnectSuccess = TryConnectAndHandshake();

                    if (reconnectSuccess)
                    {
                        Connected = true;
                        HandshakeCompleted = true;
                        PLCState = PLCState.Connected;
                        Logs.LogInfo($"[PLC] 重连成功（第{_reconnectCount}次）");
                        _reconnectCount = 0;
                        StartBackgroundTasks();
                    }
                    else
                    {
                        Logs.LogWarn($"[PLC] 重连失败（第{_reconnectCount}次）");
                        Thread.Sleep(_plcConfig.ReconnectDelay);
                    }
                }
                catch (Exception ex)
                {
                    Logs.LogError(ex, "[PLC] 重连任务异常");
                    Thread.Sleep(_plcConfig.ReconnectDelay);
                }
            }
            Logs.LogTrace("[PLC] 重连任务已终止");
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
                if (_sendNoRespTask == null || _sendNoRespTask.IsCompleted)
                    _sendNoRespTask = Task.Factory.StartNew(SendNoRespTask, _cancellationTokenSource.Token,
                        TaskCreationOptions.LongRunning, TaskScheduler.Default);
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

                var tasks = new[] { _receiveTask, _sendTask, _sendNoRespTask, _heartbeatTask, _reconnectTask }
                    .Where(t => t != null && !t.IsCompleted).ToArray();

                if (tasks.Length > 0)
                    Task.WaitAll(tasks, TimeSpan.FromSeconds(5));

                _cancellationTokenSource.Dispose();
                _cancellationTokenSource = new CancellationTokenSource();

                _receiveTask = _sendTask = _heartbeatTask = _reconnectTask = _sendNoRespTask = null;
            }
        }

        private bool CheckBaseState()
        {
            if (!Initialized || !Connected || !HandshakeCompleted ||
                _plcConfig == null || _tcpClient == null || !_tcpClient.Connected)
            {
                // 不在此处记录错误，由调用者负责记录
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