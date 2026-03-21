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
    /// 康耐视 DM260 扫码器驱动（TCP 协议），最终优化版
    /// </summary>
    public class ScannerCognexDM260 : ReactiveObject, IScanner, IDisposable
    {
        // ========== 属性（使用 [Reactive] 简化基础属性） ==========
        [Reactive]
        public bool Initialized { get; private set; }

        [Reactive]
        public bool Connected { get; private set; }

        // Message 和 Content 需要手动推送 Observable
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

        // ========== 多码结果（新增） ==========
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
        private string _codeDelimiter = "\r\n";

        // ========== 通信资源 ==========
        private TcpClient _tcpClient;
        private readonly SemaphoreSlim _syncLock = new SemaphoreSlim(1, 1); // 保护 TCP 操作

        // ========== 后台任务控制 ==========
        private CancellationTokenSource _cts; // 每次初始化时创建，关闭时销毁
        private Task _receiveTask;
        private Task _analysisTask;
        private readonly ConcurrentQueue<byte[]> _receiveQueue = new ConcurrentQueue<byte[]>();
        private const int MaxQueueSize = 1000;

        // ========== 多码解析缓冲区 ==========
        private readonly StringBuilder _messageBuffer = new StringBuilder();
        private readonly object _bufferLock = new object(); // 独立锁对象
        private const int MaxBufferLength = 4096; // 防止缓冲区无限增长

        // ========== 公共方法 ==========
        public bool Init(ScannerConfig scannerConfig)
        {
            try
            {
               
                return Task.Run(async () => await InitAsync(scannerConfig).ConfigureAwait(false)).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Logs.LogError(ex,"解析数据异常:");
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

            // 新增：每个实例初始化前随机延迟100-300ms，分散并发压力
            var random = new Random(Guid.NewGuid().GetHashCode()); // 避免多实例随机数种子相同
            await Task.Delay(random.Next(100, 300)).ConfigureAwait(false);

            await CloseAsync().ConfigureAwait(false);
            _scannerConfig = scannerConfig;

            // 编码配置...
            if (!string.IsNullOrEmpty(_scannerConfig.EncodingName))
            {
                try
                {
                    _encoding = Encoding.GetEncoding(_scannerConfig.EncodingName);
                }
                catch (Exception ex)
                {
                    Logs.LogWarn(ex, $"编码 {_scannerConfig.EncodingName} 无效，使用默认 UTF8");
                }
            }



            await _syncLock.WaitAsync().ConfigureAwait(false);
            try
            {
                _cts = new CancellationTokenSource();
                var token = _cts.Token;

                _tcpClient = new TcpClient();
                var connectTask = _tcpClient.ConnectAsync(_scannerConfig.IpAddress, _scannerConfig.Port);
                var timeoutTask = Task.Delay(_scannerConfig.DelayTime, token);
                var completedTask = await Task.WhenAny(connectTask, timeoutTask).ConfigureAwait(false);

                if (completedTask == timeoutTask)
                {
                    // 超时：关闭底层 Socket 以强制取消连接
                    try
                    {
                        _tcpClient.Client?.Close(); // 这会使得 ConnectAsync 抛出异常
                    }
                    catch { }

                    // 等待 connectTask 完成（此时它将抛出异常）
                    try
                    {
                        await connectTask; // 预期会抛出 SocketException
                    }
                    catch (Exception ex)
                    {
                        Logs.LogError($"连接取消异常: {ex.Message}"); // 记录日志，不影响最终结果
                    }

                    Message = $"TCP 连接超时！目标：{_scannerConfig.IpAddress}:{_scannerConfig.Port}";
                    CloseTcpClient();
                    return false;
                }

                // 如果 completedTask 是 connectTask，等待它完成并检查异常
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

                // 检查是否真的连接上
                if (!_tcpClient.Connected)
                {
                    Message = $"连接失败：目标 {_scannerConfig.IpAddress}:{_scannerConfig.Port} 未就绪";
                    CloseTcpClient();
                    return false;
                }

                // 启动后台任务...
                _receiveTask = Task.Run(() => ReceiveTask(token), token)
                .ContinueWith(t =>
                {
                    if (t.Exception != null)
                    {
                        Logs.LogError(t.Exception, "接收任务异常退出");
                        Message = $"接收任务异常：{t.Exception.InnerException?.Message}";
                    }
                }, TaskContinuationOptions.OnlyOnFaulted);

                // 启动解析任务，添加异常处理
                _analysisTask = Task.Run(() => AnalysisTask(token), token)
                    .ContinueWith(t =>
                    {
                        if (t.Exception != null)
                        {
                            Logs.LogError(t.Exception, "解析任务异常退出");
                            Message = $"解析任务异常：{t.Exception.InnerException?.Message}";
                        }
                    }, TaskContinuationOptions.OnlyOnFaulted);

                Initialized = true;
                Connected = true;
                Message = "初始化成功";
                return true;
            }
            catch (Exception ex)
            {
                Logs.LogError(ex);
                Message = ex.Message;
                CloseTcpClient();
                _cts?.Dispose();
                _cts = null;
                return false;
            }
            finally
            {
                _syncLock.Release();
            }
        }
        public async Task<bool> TriggerAsync()
        {
            bool lockHeld = false; // 标记是否持有锁
          
            try
            {
                await _syncLock.WaitAsync().ConfigureAwait(false);
                lockHeld = true; // 成功获取锁，标记为持有
                if (!await CheckConnectionInternalAsync().ConfigureAwait(false))
                    return false;

                // 延迟发送：为避免长时间占用锁，释放锁后等待再重新获取
                if (_scannerConfig.DelayTime > 0)
                {
                    _syncLock.Release(); // 先释放锁
                    lockHeld = false; // 释放后标记为未持有
                    await Task.Delay(_scannerConfig.DelayTime, _cts?.Token ?? CancellationToken.None).ConfigureAwait(false);
                    await _syncLock.WaitAsync().ConfigureAwait(false); // 重新获取锁
                    lockHeld = true; // 重新获取后标记为持有
                }
                // 再次检查连接状态，防止延迟期间连接被关闭
                if (!await CheckConnectionInternalAsync().ConfigureAwait(false))
                {
                    Message = "触发失败：连接已断开";
                    return false;
                }
                var commandBytes = _encoding.GetBytes(_scannerConfig.TriggerCommand);
                await _tcpClient.GetStream().WriteAsync(commandBytes, 0, commandBytes.Length, _cts?.Token ?? CancellationToken.None).ConfigureAwait(false);
                return true;
            }
            catch (Exception ex)
            {
                Logs.LogError(ex);
                Message = $"触发失败：{ex.Message}";
                return false;
            }
            finally
            {
                if (lockHeld)
                {
                    _syncLock.Release();
                }
                // 确保锁被释放（如果已释放则不会再次释放）
                //try { _syncLock.Release(); } catch (SemaphoreFullException) { /* 兜底：防止极端场景重复释放 */ }

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
                Logs.LogError(ex, "关闭扫码器异常");
            }
        }

        public async Task CloseAsync()
        {
            await _syncLock.WaitAsync().ConfigureAwait(false);
            try
            {
                // 取消并销毁 CTS（核心修复）
                if (_cts != null)
                {
                    _cts.Cancel();
                    _cts.Dispose();
                    _cts = null;
                }

                // 等待后台任务结束（带超时）
                var tasks = new[] { _receiveTask, _analysisTask }.Where(t => t != null).ToArray();
                if (tasks.Any())
                {
                    try
                    {
                        await Task.WhenAll(tasks).TimeoutAfter(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
                    }
                    catch (TimeoutException)
                    {
                        // 超时后不再等待，继续清理
                    }
                }

                CloseTcpClient();

                // 清空队列
                while (_receiveQueue.TryDequeue(out _)) { }

                // 清空解析缓冲区（使用独立锁）
                lock (_bufferLock)
                {
                    _messageBuffer.Clear();
                }

                // 重置状态
                Initialized = false;
                Connected = false;
                Content = string.Empty;
                MultiCodes = new List<string>();
                Message = "已关闭";

                // 任务引用置空
                _receiveTask = null;
                _analysisTask = null;
            }
            finally
            {
                try { _syncLock.Release(); }
                catch (SemaphoreFullException) { /* 兜底：防止极端场景重复释放 */ }
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
                Logs.LogError(ex, "检查连接异常");
                return false;
            }
        }

        public async Task<bool> CheckConnectionAsync()
        {
            await _syncLock.WaitAsync().ConfigureAwait(false);
            try
            {
                return await CheckConnectionInternalAsync().ConfigureAwait(false);
            }
            finally
            {
                _syncLock.Release();
            }
        }

        // ========== 私有辅助方法 ==========
        private async Task<bool> CheckConnectionInternalAsync()
        {
            if (_scannerConfig == null) return false;

            // 检查后台任务状态...
            // 检查后台任务状态，若已终止则尝试重启（前提是未取消）
            if (_cts != null && !_cts.IsCancellationRequested && !_disposed)
            {
                if (_receiveTask != null && _receiveTask.IsCompleted)
                {
                    Logs.LogWarn("接收任务已终止，尝试重启...");
                    _receiveTask = Task.Run(() => ReceiveTask(_cts.Token), _cts.Token)
                        .ContinueWith(t =>
                        {
                            if (t.Exception != null)
                                Logs.LogError(t.Exception, "重启后的接收任务异常");
                        }, TaskContinuationOptions.OnlyOnFaulted);
                }
                if (_analysisTask != null && _analysisTask.IsCompleted)
                {
                    Logs.LogWarn("解析任务已终止，尝试重启...");
                    _analysisTask = Task.Run(() => AnalysisTask(_cts.Token), _cts.Token)
                        .ContinueWith(t =>
                        {
                            if (t.Exception != null)
                                Logs.LogError(t.Exception, "重启后的解析任务异常");
                        }, TaskContinuationOptions.OnlyOnFaulted);
                }
            }

            // 使用 Socket.Poll 检测连接活性
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
                CloseTcpClient(); // 确保旧资源释放
                _tcpClient = new TcpClient();
                var connectTask = _tcpClient.ConnectAsync(_scannerConfig.IpAddress, _scannerConfig.Port);
                var timeoutTask = Task.Delay(1000, _cts?.Token ?? CancellationToken.None);
                var completedTask = await Task.WhenAny(connectTask, timeoutTask).ConfigureAwait(false);

                if (completedTask == timeoutTask)
                {
                    // 超时：关闭 Socket 中止连接
                    try
                    {
                        _tcpClient.Client?.Close();
                    }
                    catch { }
                    try
                    {
                        await connectTask; // 等待异常
                    }
                    catch (Exception ex)
                    {
                        Logs.LogError($"重连取消异常: {ex.Message}");
                    }
                    Message = $"重连超时：{_scannerConfig.IpAddress}:{_scannerConfig.Port}";
                    CloseTcpClient();
                    return false;
                }

                // 等待连接完成
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

                Connected = true;
                Message = "重连成功";
                return true;
            }
            catch (Exception ex)
            {
                Logs.LogError(ex);
                Message = $"重连异常：{ex.Message}";
                CloseTcpClient();
                return false;
            }
        }

        private void CloseTcpClient()
        {
            if (_tcpClient != null)
            {
                try
                {
                    _tcpClient.Client?.Shutdown(SocketShutdown.Both);
                }
                catch { }
                try
                {
                    _tcpClient.Close();
                }
                catch { }
                _tcpClient = null;
            }
            Connected = false;
        }

        // ========== 后台任务 ==========
        private async Task ReceiveTask(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                var client = _tcpClient; // 捕获当前引用
                if (client == null || !client.Connected)
                {
                    await Task.Delay(100, token).ConfigureAwait(false);
                    continue;
                }

                try
                {
                    // 先无锁检查数据可用性
                    if (client.Available == 0)
                    {
                        await Task.Delay(1, token).ConfigureAwait(false);
                        continue;
                    }

                    // 有数据时获取锁读取
                    await _syncLock.WaitAsync(token).ConfigureAwait(false);
                    try
                    {
                        // 再次检查，防止连接状态变化
                        if (_tcpClient == null || !_tcpClient.Connected)
                            continue;

                        var stream = _tcpClient.GetStream();
                        byte[] buffer = new byte[4096];
                        int length = await stream.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false);
                        if (length > 0)
                        {
                            var received = buffer.Take(length).ToArray();
                            // 队列限流
                            if (_receiveQueue.Count < MaxQueueSize)
                            {
                                _receiveQueue.Enqueue(received);
                            }
                            else
                            {
                                _receiveQueue.TryDequeue(out _);
                                _receiveQueue.Enqueue(received);
                                Logs.LogWarn("接收队列已满，丢弃旧数据");
                            }
                        }
                        else
                        {
                            // 对端关闭连接
                            Message = "连接被对端关闭";
                            CloseTcpClient();
                        }
                    }
                    finally
                    {
                        _syncLock.Release();
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Logs.LogError(ex);
                    Message = $"接收异常：{ex.Message}";
                    CloseTcpClient();
                    await Task.Delay(100, token).ConfigureAwait(false);
                }
            }
        }

        //private async Task AnalysisTask(CancellationToken token)
        //{
        //    while (!token.IsCancellationRequested)
        //    {
        //        if (_receiveQueue.TryDequeue(out byte[] data))
        //        {
        //            try
        //            {
        //                string chunk = _encoding.GetString(data);
        //                List<string> parsedCodes = new List<string>();

        //                lock (_bufferLock)
        //                {
        //                    _messageBuffer.Append(chunk);

        //                    // 缓冲区长度限制，防止无限增长
        //                    if (_messageBuffer.Length > MaxBufferLength)
        //                    {
        //                        Logs.LogWarn("解析缓冲区超出最大长度，重置");
        //                        _messageBuffer.Clear();
        //                        continue;
        //                    }

        //                    string currentBuffer = _messageBuffer.ToString();
        //                    int delimiterIndex;

        //                    // 解析出所有完整条码
        //                    while ((delimiterIndex = currentBuffer.IndexOf(_codeDelimiter, StringComparison.Ordinal)) >= 0)
        //                    {
        //                        string code = currentBuffer.Substring(0, delimiterIndex).Trim();
        //                        if (!string.IsNullOrWhiteSpace(code))
        //                        {
        //                            parsedCodes.Add(code);
        //                        }
        //                        // 移除已处理部分（包括分隔符）
        //                        _messageBuffer.Remove(0, delimiterIndex + _codeDelimiter.Length);
        //                        currentBuffer = _messageBuffer.ToString();
        //                    }
        //                }

        //                // 如果解析出了新条码，更新属性并推送
        //                if (parsedCodes.Any())
        //                {
        //                    // 更新 Content 为拼接字符串（兼容旧代码）
        //                    Content = string.Join("|", parsedCodes);
        //                    // 更新 MultiCodes 列表
        //                    MultiCodes = new List<string>(parsedCodes);
        //                    _multiCodesSubject.OnNext(MultiCodes);
        //                }
        //            }
        //            catch (Exception ex)
        //            {
        //                Logs.LogError( ex,"解析数据异常:");
        //                lock (_bufferLock)
        //                {
        //                    _messageBuffer.Clear(); // 清空缓冲区避免持续错误
        //                }
        //            }
        //        }
        //        else
        //        {
        //            await Task.Delay(1, token).ConfigureAwait(false);
        //        }
        //    }
        //}
        private async Task AnalysisTask(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                if (_receiveQueue.TryDequeue(out byte[] data))
                {
                    try
                    {
                        string code = _encoding.GetString(data).Trim();
                        if (!string.IsNullOrWhiteSpace(code))
                        {
                            // 更新 Content 为单个码值
                            Content = code;
                            // 兼容旧代码，MultiCodes 设置为只包含一个元素的列表
                            MultiCodes = new List<string> { code };
                            _multiCodesSubject.OnNext(MultiCodes);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logs.LogError(ex, "解析数据异常:");
                    }
                }
                else
                {
                    await Task.Delay(1, token).ConfigureAwait(false);
                }
            }
        }
        // ========== IDisposable 实现 ==========

        // 新增字段标记是否已释放
        private bool _disposed = false;
        // 重写 Dispose 逻辑
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
                _syncLock.Dispose();
                _messageSubject.Dispose();
                _contentSubject.Dispose();
                _multiCodesSubject.Dispose();
            }

            // 释放非托管资源：仅做最简化操作，不执行网络调用
            if (_tcpClient != null)
            {
                try { _tcpClient.Client?.Shutdown(SocketShutdown.Both); } catch { }
                try { _tcpClient.Close(); } catch { }
                _tcpClient = null;
            }

            _disposed = true;
        }
    }

    // 扩展方法：为 Task 添加超时功能
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
                await task.ConfigureAwait(false); // 传播异常
                cts.Cancel();
            }
        }
    }
}