using FZK.Hardware.Scanner.Base;
using FZK.Logger;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Reactive.Subjects;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FZK.Hardware.Scanner.Cognex
{
    /// <summary>
    /// 康耐视 DM260 扫码器驱动（TCP 协议），最终生产版
    /// 修复历史：
    /// 1. 修复锁内异步IO导致的死锁问题
    /// 2. 修复Interlocked置空导致的空引用异常
    /// 3. 修复BlockingCollection CompleteAdding导致的重连失败问题
    /// 4. 修复ClearContent未清空解析缓冲区导致的条码拼接错误
    /// </summary>
    public class ScannerCognexDM260 : ReactiveObject, IScanner, IDisposable
    {
        // ========== 属性 ==========
        [Reactive] public bool Initialized { get; private set; }
        [Reactive] public bool Connected { get; private set; }

        private string _message;
        public string Message
        {
            get => _message;
            private set
            {
                this.RaiseAndSetIfChanged(ref _message, value);
                _messageSubject.OnNext(value);
            }
        }

        private string _content;
        public string Content
        {
            get => _content;
            private set
            {
                this.RaiseAndSetIfChanged(ref _content, value);
                _contentSubject.OnNext(value);
            }
        }

        private List<string> _multiCodes = new List<string>();
        public List<string> MultiCodes
        {
            get => _multiCodes;
            private set => this.RaiseAndSetIfChanged(ref _multiCodes, value);
        }

        // ========== Observable 接口 ==========
        private readonly Subject<string> _messageSubject = new Subject<string>();
        public IObservable<string> MessageObservable => _messageSubject;

        private readonly Subject<string> _contentSubject = new Subject<string>();
        public IObservable<string> ContentObservable => _contentSubject;

        private readonly Subject<List<string>> _multiCodesSubject = new Subject<List<string>>();
        public IObservable<List<string>> MultiCodesObservable => _multiCodesSubject;

        // ========== 配置 ==========
        private ScannerConfig _scannerConfig;
        private Encoding _encoding = Encoding.UTF8;
        private string _codeDelimiter = ",";
        private string _endOfMessageDelimiter = "\r\n";

        // ========== 通信资源 ==========
        private volatile TcpClient _tcpClient;
        private readonly SemaphoreSlim _syncLock = new SemaphoreSlim(1, 1);
        private readonly ManualResetEventSlim _tasksStarted = new ManualResetEventSlim(false);
        private readonly ManualResetEventSlim _analysisStarted = new ManualResetEventSlim(false);

        // ========== 后台任务控制 ==========
        private CancellationTokenSource _cts;
        private Task _receiveTask;
        private Task _analysisTask;

        // 【核心设计】队列全局唯一，永不重新创建，永不调用CompleteAdding
        // 彻底解决队列引用不一致和永久关闭问题
        private readonly BlockingCollection<byte[]> _receiveQueue = new BlockingCollection<byte[]>(MaxQueueSize);
        private const int MaxQueueSize = 2000;

        // ========== 多码解析缓冲区 ==========
        private readonly StringBuilder _messageBuffer = new StringBuilder();
        private readonly object _bufferLock = new object();
        private const int MaxBufferLength = 4096;

        private bool _disposed = false;

        // ========== 公共方法 ==========
        public bool Init(ScannerConfig scannerConfig)
        {
            try
            {
                return Task.Run(async () => await InitAsync(scannerConfig).ConfigureAwait(false)).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Logs.LogError(ex, "初始化异常:");
                Message = $"初始化失败：{ex.Message}";
                return false;
            }
        }

        public async Task<bool> InitAsync(ScannerConfig scannerConfig)
        {
            if (scannerConfig == null)
            {
                Logs.LogError("扫码器配置文件为空");
                return false;
            }

            // 随机延迟分散并发初始化压力
            var random = new Random(Guid.NewGuid().GetHashCode());
            await Task.Delay(random.Next(100, 300)).ConfigureAwait(false);

            // 先关闭旧连接和任务
            await CloseAsync().ConfigureAwait(false);
            _scannerConfig = scannerConfig;

            // 加载编码配置
            if (!string.IsNullOrEmpty(_scannerConfig.EncodingName))
            {
                try
                {
                    _encoding = Encoding.GetEncoding(_scannerConfig.EncodingName);
                }
                catch (Exception ex)
                {
                    Logs.LogWarn(ex, $"[{GetDeviceId()}] 编码 {_scannerConfig.EncodingName} 无效，使用默认 UTF8");
                }
            }

            // 加载分隔符配置
            if (!string.IsNullOrEmpty(_scannerConfig.CodeDelimiter))
                _codeDelimiter = _scannerConfig.CodeDelimiter;
            if (!string.IsNullOrEmpty(_scannerConfig.EndOfMessageDelimiter))
                _endOfMessageDelimiter = _scannerConfig.EndOfMessageDelimiter;

            await _syncLock.WaitAsync().ConfigureAwait(false);
            try
            {
                _cts = new CancellationTokenSource();
                var token = _cts.Token;

                // 注册取消回调，立即中断所有IO操作
                token.Register(() => CloseTcpClient());

                _tcpClient = new TcpClient();
                var connectTask = _tcpClient.ConnectAsync(_scannerConfig.IpAddress, _scannerConfig.Port);
                var timeoutTask = Task.Delay(_scannerConfig.DelayTime, token);
                var completedTask = await Task.WhenAny(connectTask, timeoutTask).ConfigureAwait(false);

                if (completedTask == timeoutTask)
                {
                    try { _tcpClient.Client?.Close(); } catch { }
                    try { await connectTask; } catch { }
                    Message = $"TCP 连接超时！目标：{_scannerConfig.IpAddress}:{_scannerConfig.Port}";
                    CloseTcpClient();
                    return false;
                }

                try
                {
                    await connectTask.ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Message = $"连接失败: {ex.Message}";
                    CloseTcpClient();
                    return false;
                }

                if (!_tcpClient.Connected)
                {
                    Message = $"连接失败：目标 {_scannerConfig.IpAddress}:{_scannerConfig.Port} 未就绪";
                    CloseTcpClient();
                    return false;
                }

                // 启用TCP保活机制
                EnableTcpKeepAlive(_tcpClient.Client, 5000, 2000);

                // 启动后台任务
                _receiveTask = Task.Run(() => ReceiveTask(token), token)
                    .ContinueWith(t =>
                    {
                        if (t.Exception != null)
                            Logs.LogError(t.Exception, $"[{GetDeviceId()}] 接收任务异常退出");
                    }, TaskContinuationOptions.OnlyOnFaulted);

                _analysisTask = Task.Run(() => AnalysisTask(token), token)
                    .ContinueWith(t =>
                    {
                        if (t.Exception != null)
                            Logs.LogError(t.Exception, $"[{GetDeviceId()}] 解析任务异常退出");
                    }, TaskContinuationOptions.OnlyOnFaulted);

                // 等待任务启动就绪
                if (!_tasksStarted.Wait(TimeSpan.FromSeconds(2)))
                {
                    Logs.LogWarn($"[{GetDeviceId()}] 接收任务启动超时，可能影响首次触发");
                }
                else
                {
                    Logs.LogDebug($"[{GetDeviceId()}] 接收任务已就绪");
                }
                if (!_analysisStarted.Wait(TimeSpan.FromSeconds(2)))
                {
                    Logs.LogWarn($"[{GetDeviceId()}] 解析任务启动超时，可能影响首次触发");
                }
                else
                {
                    Logs.LogDebug($"[{GetDeviceId()}] 解析任务已就绪");
                }

                Initialized = true;
                Connected = true;
                Message = "初始化成功";
                return true;
            }
            catch (Exception ex)
            {
                Logs.LogError(ex, $"[{GetDeviceId()}] 初始化异常");
                Message = ex.Message;
                CloseTcpClient();
                _cts?.Dispose();
                _cts = null;
                _tasksStarted.Reset();
                _analysisStarted.Reset();
                return false;
            }
            finally
            {
                _syncLock.Release();
            }
        }

        /// <summary>
        /// 触发扫码
        /// </summary>
        public async Task<bool> TriggerAsync()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ScannerCognexDM260));

            bool lockHeld = false;
            try
            {
                await _syncLock.WaitAsync().ConfigureAwait(false);
                lockHeld = true;

                if (!await CheckConnectionInternalNoLockAsync().ConfigureAwait(false))
                    return false;

                // 锁内捕获局部引用，防止被Interlocked.Exchange置空
                TcpClient client = _tcpClient;
                if (client == null)
                {
                    Message = "触发失败：连接已关闭";
                    return false;
                }

                if (_scannerConfig.DelayTime > 0)
                {
                    _syncLock.Release();
                    lockHeld = false;
                    await Task.Delay(_scannerConfig.DelayTime, _cts?.Token ?? CancellationToken.None).ConfigureAwait(false);
                    await _syncLock.WaitAsync().ConfigureAwait(false);
                    lockHeld = true;

                    if (!await CheckConnectionInternalNoLockAsync().ConfigureAwait(false))
                    {
                        Message = "触发失败：连接已断开";
                        return false;
                    }

                    // 延迟后必须再次捕获局部引用
                    client = _tcpClient;
                    if (client == null)
                    {
                        Message = "触发失败：连接已关闭";
                        return false;
                    }
                }

                var commandBytes = _encoding.GetBytes(_scannerConfig.TriggerCommand);
                // 使用局部client发送，绝对安全
                await client.GetStream().WriteAsync(commandBytes, 0, commandBytes.Length, _cts?.Token ?? CancellationToken.None).ConfigureAwait(false);
                return true;
            }
            catch (Exception ex)
            {
                Logs.LogError(ex, $"[{GetDeviceId()}] 触发失败");
                Message = $"触发失败：{ex.Message}";
                return false;
            }
            finally
            {
                if (lockHeld)
                    _syncLock.Release();
            }
        }

        public void Close()
        {
            try
            {
                Task.Run(async () => await CloseAsync().ConfigureAwait(false)).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Logs.LogError(ex, $"[{GetDeviceId()}] 关闭扫码器异常");
            }
        }

        public async Task CloseAsync()
        {
            if (_disposed) return;

            bool statusResetDone = false;

            try
            {
                // 取消并销毁CTS（会触发注册的CloseTcpClient回调，停止生产）
                if (_cts != null)
                {
                    _cts.Cancel();
                    _cts.Dispose();
                    _cts = null;
                }

                await _syncLock.WaitAsync().ConfigureAwait(false);
                // 等待后台任务结束（带超时）
                var tasks = new[] { _receiveTask, _analysisTask }.Where(t => t != null).ToArray();
                if (tasks.Any())
                {
                    try
                    {
                        await Task.WhenAll(tasks).TimeoutAfter(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
                        Logs.LogDebug($"[{GetDeviceId()}] 后台任务正常退出");
                    }
                    catch (TimeoutException)
                    {
                        Logs.LogWarn($"[{GetDeviceId()}] 后台任务退出超时，强制清理队列");
                    }
                    catch (OperationCanceledException)
                    {
                        // 任务被取消，正常情况
                    }
                }

                // 【关键修复】永不调用CompleteAdding，仅清空队列
                // 队列可重复使用，下次InitAsync直接使用
                while (_receiveQueue.TryTake(out _)) { }

                // 清空解析缓冲区
                lock (_bufferLock)
                {
                    _messageBuffer.Clear();
                }

                // 重置事件
                _tasksStarted.Reset();
                _analysisStarted.Reset();

                // 重置状态
                Initialized = false;
                Connected = false;
                Content = string.Empty;
                MultiCodes = new List<string>();
                Message = "已关闭";

                _receiveTask = null;
                _analysisTask = null;

                statusResetDone = true;
            }
            finally
            {
                if (!statusResetDone)
                {
                    // 确保状态始终被重置
                    Initialized = false;
                    Connected = false;
                    Content = string.Empty;
                    MultiCodes = new List<string>();
                    Message = "已关闭";
                    _receiveTask = null;
                    _analysisTask = null;
                    _tasksStarted.Reset();
                    _analysisStarted.Reset();
                }

                try { _syncLock.Release(); }
                catch (SemaphoreFullException) { }
            }
        }

        public bool CheckConnection()
        {
            try
            {
                return Task.Run(async () => await CheckConnectionAsync().ConfigureAwait(false)).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Logs.LogError(ex, $"[{GetDeviceId()}] 检查连接异常");
                return false;
            }
        }

        public async Task<bool> CheckConnectionAsync()
        {
            if (_disposed) return false;
            await _syncLock.WaitAsync().ConfigureAwait(false);
            try
            {
                return await CheckConnectionInternalNoLockAsync().ConfigureAwait(false);
            }
            finally
            {
                _syncLock.Release();
            }
        }

        private string GetDeviceId() => _scannerConfig != null ? $"{_scannerConfig.IpAddress}:{_scannerConfig.Port}" : "Unknown";

        private async Task<bool> CheckConnectionInternalNoLockAsync()
        {
            // 假设调用者已持有锁
            if (_scannerConfig == null) return false;

            // 检查后台任务状态，若已终止则尝试重启
            if (_cts != null && !_cts.IsCancellationRequested && !_disposed)
            {
                if (_receiveTask != null && _receiveTask.IsCompleted)
                {
                    Logs.LogWarn($"[{GetDeviceId()}] 接收任务已终止，尝试重启...");
                    _receiveTask = Task.Run(() => ReceiveTask(_cts.Token), _cts.Token)
                        .ContinueWith(t => { if (t.Exception != null) Logs.LogError(t.Exception, $"[{GetDeviceId()}] 重启后的接收任务异常"); }, TaskContinuationOptions.OnlyOnFaulted);
                }
                if (_analysisTask != null && _analysisTask.IsCompleted)
                {
                    Logs.LogWarn($"[{GetDeviceId()}] 解析任务已终止，尝试重启...");
                    _analysisTask = Task.Run(() => AnalysisTask(_cts.Token), _cts.Token)
                        .ContinueWith(t => { if (t.Exception != null) Logs.LogError(t.Exception, $"[{GetDeviceId()}] 重启后的解析任务异常"); }, TaskContinuationOptions.OnlyOnFaulted);
                }
            }

            // 使用Socket.Poll检测连接活性
            bool isConnected = false;
            if (_tcpClient != null && _tcpClient.Client != null)
            {
                try
                {
                    isConnected = !(_tcpClient.Client.Poll(1, SelectMode.SelectRead) && _tcpClient.Client.Available == 0);
                }
                catch { isConnected = false; }
            }

            if (isConnected) return true;

            // 尝试重连
            try
            {
                Logs.LogInfo($"[{GetDeviceId()}] 连接断开，尝试重连...");
                CloseTcpClient();

                // 【关键修复】重连时仅清空队列，不创建新实例
                // AnalysisTask始终消费同一个队列，无需重启
                while (_receiveQueue.TryTake(out _)) { }

                _tcpClient = new TcpClient();
                var connectTask = _tcpClient.ConnectAsync(_scannerConfig.IpAddress, _scannerConfig.Port);
                var timeoutTask = Task.Delay(1000, _cts?.Token ?? CancellationToken.None);
                var completedTask = await Task.WhenAny(connectTask, timeoutTask).ConfigureAwait(false);

                if (completedTask == timeoutTask)
                {
                    try { _tcpClient.Client?.Close(); } catch { }
                    try { await connectTask; } catch { }
                    Message = $"重连超时：{_scannerConfig.IpAddress}:{_scannerConfig.Port}";
                    CloseTcpClient();
                    return false;
                }

                try
                {
                    await connectTask.ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Message = $"重连失败：{ex.Message}";
                    CloseTcpClient();
                    return false;
                }

                if (!_tcpClient.Connected)
                {
                    Message = $"重连失败：目标未就绪";
                    CloseTcpClient();
                    return false;
                }

                // 重连成功后重新启用KeepAlive
                EnableTcpKeepAlive(_tcpClient.Client, 5000, 2000);

                Connected = true;
                Message = "重连成功";
                Logs.LogInfo($"[{GetDeviceId()}] 重连成功");
                return true;
            }
            catch (Exception ex)
            {
                Logs.LogError(ex, $"[{GetDeviceId()}] 重连异常");
                Message = $"重连异常：{ex.Message}";
                CloseTcpClient();
                return false;
            }
        }

        private void CloseTcpClient()
        {
            // 原子性交换引用，确保只关闭一次
            var client = Interlocked.Exchange(ref _tcpClient, null);
            if (client != null)
            {
                try { client.Client?.Shutdown(SocketShutdown.Both); } catch { }
                try { client.Close(); } catch { }
            }
            Connected = false;
        }

        private async Task ReceiveTask(CancellationToken token)
        {
            _tasksStarted.Set();
            while (!token.IsCancellationRequested)
            {
                TcpClient client;
                await _syncLock.WaitAsync(token).ConfigureAwait(false);
                try
                {
                    client = _tcpClient;
                }
                finally
                {
                    _syncLock.Release();
                }

                if (client == null || !client.Connected)
                {
                    token.ThrowIfCancellationRequested();
                    await Task.Delay(100, token).ConfigureAwait(false);
                    continue;
                }

                try
                {
                    byte[] buffer = new byte[4096];
                    // 异步IO在锁外执行，不阻塞其他操作
                    int length = await client.GetStream().ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false);

                    if (length > 0)
                    {
                        var received = buffer.Take(length).ToArray();

                        // 防御性检查（理论上不会触发，因为我们永不调用CompleteAdding）
                        if (!_receiveQueue.IsAddingCompleted)
                        {
                            // 队列满时丢弃最旧的数据
                            if (!_receiveQueue.TryAdd(received, 0, token))
                            {
                                if (_receiveQueue.TryTake(out _))
                                {
                                    _receiveQueue.TryAdd(received, 0, token);
                                }
                                Logs.LogWarn($"[{GetDeviceId()}] 接收队列已满，丢弃旧数据");
                            }
                        }
                    }
                    else
                    {
                        // 对端正常关闭连接
                        Message = "连接被对端关闭";
                        CloseTcpClient();
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (InvalidOperationException ex)
                {
                    // 捕获队列已完成的异常（理论上不会触发）
                    Logs.LogDebug($"[{GetDeviceId()}] 队列已完成添加，接收任务正常退出");
                    break;
                }
                catch (Exception ex)
                {
                    Logs.LogError(ex, $"[{GetDeviceId()}] 接收异常");
                    Message = $"接收异常：{ex.Message}";
                    CloseTcpClient();
                    await Task.Delay(100, token).ConfigureAwait(false);
                }
            }
        }

        private async Task AnalysisTask(CancellationToken token)
        {
            _analysisStarted.Set();
            try
            {
                // 【核心优势】AnalysisTask只启动一次，始终消费同一个队列
                // 重连时无需重启，彻底解决队列引用不一致问题
                foreach (var data in _receiveQueue.GetConsumingEnumerable(token))
                {
                    try
                    {
                        string chunk = _encoding.GetString(data);
                        lock (_bufferLock)
                        {
                            _messageBuffer.Append(chunk);
                            if (_messageBuffer.Length > MaxBufferLength)
                            {
                                Logs.LogWarn($"[{GetDeviceId()}] 解析缓冲区超出最大长度，重置");
                                _messageBuffer.Clear();
                                continue;
                            }

                            string currentBuffer = _messageBuffer.ToString();
                            int endIndex;
                            // 按报文结束符提取完整报文
                            while ((endIndex = currentBuffer.IndexOf(_endOfMessageDelimiter, StringComparison.Ordinal)) >= 0)
                            {
                                string completeMessage = currentBuffer.Substring(0, endIndex);
                                // 移除已处理部分（包括结束符）
                                _messageBuffer.Remove(0, endIndex + _endOfMessageDelimiter.Length);
                                currentBuffer = _messageBuffer.ToString();

                                // 处理完整报文
                                if (!string.IsNullOrWhiteSpace(completeMessage))
                                {
                                    Content = completeMessage;
                                    // 拆分多码
                                    var codes = completeMessage.Split(new[] { _codeDelimiter }, StringSplitOptions.RemoveEmptyEntries)
                                        .Select(c => c.Trim())
                                        .Where(c => !string.IsNullOrEmpty(c))
                                        .ToList();
                                    MultiCodes = codes;
                                    _multiCodesSubject.OnNext(MultiCodes);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logs.LogError(ex, $"[{GetDeviceId()}] 解析数据异常");
                        lock (_bufferLock)
                        {
                            _messageBuffer.Clear();
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // 正常取消，忽略
            }
        }

        private static void EnableTcpKeepAlive(Socket socket, uint keepAliveTime = 5000, uint keepAliveInterval = 2000)
        {
            try
            {
                // 启用KeepAlive
                socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);

                // 设置KeepAlive详细参数（Windows平台）
                byte[] inArray = new byte[12];
                System.BitConverter.GetBytes(1u).CopyTo(inArray, 0);        // 启用
                System.BitConverter.GetBytes(keepAliveTime).CopyTo(inArray, 4);      // 空闲时间（ms）
                System.BitConverter.GetBytes(keepAliveInterval).CopyTo(inArray, 8);   // 重试间隔（ms）
                socket.IOControl(IOControlCode.KeepAliveValues, inArray, null);
            }
            catch (Exception ex)
            {
                // 某些平台可能不支持IOControl，记录警告但不影响主流程
                Logs.LogWarn($"启用 TCP KeepAlive 失败: {ex.Message}");
            }
        }

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
                // 释放托管资源
                Close();
                _syncLock?.Dispose();
                _messageSubject?.Dispose();
                _contentSubject?.Dispose();
                _multiCodesSubject?.Dispose();
                _receiveQueue?.Dispose();
                _tasksStarted?.Dispose();
                _analysisStarted?.Dispose();
            }

            // 释放非托管资源
            CloseTcpClient();
            _disposed = true;
        }

        /// <summary>
        /// 清空扫码结果缓存，防止读取到上一次的旧码
        /// </summary>
        public void ClearContent()
        {
            Content = string.Empty;
            MultiCodes = new List<string>();
            // 【关键修复】同时清空解析缓冲区
            // 彻底解决不完整报文残留导致的条码拼接错误
            lock (_bufferLock)
            {
                _messageBuffer.Clear();
            }
        }
    }

    // 扩展方法：为Task添加超时功能
    internal static class TaskExtensions
    {
        public static async Task TimeoutAfter(this Task task, TimeSpan timeout)
        {
            using (var cts = new CancellationTokenSource())
            {
                var delayTask = Task.Delay(timeout, cts.Token);
                var completedTask = await Task.WhenAny(task, delayTask).ConfigureAwait(false);
                if (completedTask == delayTask)
                    throw new TimeoutException("任务执行超时");
                await task.ConfigureAwait(false);
                cts.Cancel();
            }
        }
    }
}