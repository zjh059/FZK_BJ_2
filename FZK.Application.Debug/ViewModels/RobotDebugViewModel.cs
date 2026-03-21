using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace FZK.Application.Debug.ViewModels
{
    /// <summary>
    /// 布尔转可见性转换器（适配.NET 4.7.2）
    /// </summary>
    public class BooleanToVisibilityConverter : IValueConverter
    {
        public static readonly BooleanToVisibilityConverter Instance = new BooleanToVisibilityConverter();

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return (bool)value ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// 机器人调试工具ViewModel（保留ReactiveUI，适配.NET 4.7.2）
    /// </summary>
    public class RobotDebugViewModel : ReactiveObject
    {
        #region 基础配置属性
        [Reactive]
        public bool IsServerMode { get; set; } = true;

        [Reactive]
        public bool IsClientMode { get; set; } = false;

        [Reactive]
        public string SelectedProtocol { get; set; } = "TCP";

        [Reactive]
        public string ListenIp { get; set; } = "0.0.0.0";

        [Reactive]
        public string TargetIp { get; set; } = "127.0.0.1";

        [Reactive]
        public string Port { get; set; } = "8888";

        [Reactive]
        public string ConnectionStatusText { get; set; } = "未运行";

        [Reactive]
        public Brush ConnectionStatusColor { get; set; } = Brushes.Red;

        [Reactive]
        public string StartButtonText { get; set; } = "启动/连接";

        [Reactive]
        public string StopButtonText { get; set; } = "停止/断开";

        [Reactive]
        public bool CanStart { get; set; } = true;

        [Reactive]
        public bool CanStop { get; set; } = false;
        #endregion

        #region 客户端列表属性
        [Reactive]
        public ObservableCollection<string> ConnectedClients { get; set; } = new ObservableCollection<string>();

        public string ClientCountText => $"当前连接数：{ConnectedClients.Count}";
        #endregion

        #region 发送数据属性
        [Reactive]
        public bool IsHexSend { get; set; } = false;

        [Reactive]
        public bool IsAutoNewLine { get; set; } = false;

        [Reactive]
        public bool IsClearAfterSend { get; set; } = false;

        [Reactive]
        public string SelectedEncoding { get; set; } = "UTF-8";

        [Reactive]
        public string SendData { get; set; } = string.Empty;

        [Reactive]
        public List<string> QuickCommands { get; set; } = new List<string>
        {
            "常用指令1：查询状态",
            "常用指令2：复位",
            "常用指令3：心跳包"
        };

        [Reactive]
        public string SelectedQuickCommand { get; set; }

        [Reactive]
        public bool CanSend { get; set; } = false;

        [Reactive]
        public string TimedSendButtonText { get; set; } = "定时发送";

        [Reactive]
        public bool IsTimedSendEnabled { get; set; } = false;
        #endregion

        #region 日志相关属性
        [Reactive]
        public bool ShowTimestamp { get; set; } = true;

        [Reactive]
        public bool ShowSendRecvFlag { get; set; } = true;

        [Reactive]
        public bool IsHexDisplay { get; set; } = false;

        [Reactive]
        public long RecvByteCount { get; set; } = 0;

        [Reactive]
        public long SendByteCount { get; set; } = 0;

        public string ByteCountText => $"接收字节数：{RecvByteCount} | 发送字节数：{SendByteCount}";

        [Reactive]
        public string LogText { get; set; } = string.Empty;

        [Reactive]
        public string StatisticsText { get; set; } = "总连接数：0 | 最大并发：0 | 累计收发：0 字节";
        #endregion

        #region 网络通信核心对象
        private TcpListener _tcpListener;
        private UdpClient _udpClient;
        private TcpClient _tcpClient;
        private Dictionary<string, TcpClient> _connectedTcpClients = new Dictionary<string, TcpClient>();
        private CancellationTokenSource _cts;
        private Timer _timedSendTimer;
        private int _totalConnectionCount = 0;
        private int _maxConcurrentCount = 0;
        #endregion

        #region 命令定义
        public IReactiveCommand StartCommand { get; }
        public IReactiveCommand StopCommand { get; }
        public IReactiveCommand ClearClientListCommand { get; }
        public IReactiveCommand DisconnectClientCommand { get; }
        public IReactiveCommand LoadCommandLibraryCommand { get; }
        public IReactiveCommand SendDataCommand { get; }
        public IReactiveCommand ToggleTimedSendCommand { get; }
        public IReactiveCommand ClearLogCommand { get; }
        public IReactiveCommand SaveLogCommand { get; }
        public IReactiveCommand ExportConfigCommand { get; }
        public IReactiveCommand ImportConfigCommand { get; }
        #endregion

        /// <summary>
        /// 构造函数
        /// </summary>
        public RobotDebugViewModel()
        {
            // 初始化命令
            StartCommand = ReactiveCommand.CreateFromTask(OnStartAsync, this.WhenAnyValue(x => x.CanStart));
            StopCommand = ReactiveCommand.Create(OnStop, this.WhenAnyValue(x => x.CanStop));
            ClearClientListCommand = ReactiveCommand.Create(OnClearClientList);
            DisconnectClientCommand = ReactiveCommand.Create<string>(OnDisconnectClient);
            LoadCommandLibraryCommand = ReactiveCommand.Create(OnLoadCommandLibrary);
            SendDataCommand = ReactiveCommand.CreateFromTask(OnSendDataAsync, this.WhenAnyValue(x => x.CanSend));
            ToggleTimedSendCommand = ReactiveCommand.Create(OnToggleTimedSend);
            ClearLogCommand = ReactiveCommand.Create(OnClearLog);
            SaveLogCommand = ReactiveCommand.Create(OnSaveLog);
            ExportConfigCommand = ReactiveCommand.Create(OnExportConfig);
            ImportConfigCommand = ReactiveCommand.Create(OnImportConfig);

            // 监听模式切换
            this.WhenAnyValue(x => x.IsServerMode)
                .Subscribe(isServer =>
                {
                    IsClientMode = !isServer;
                    UpdateStatistics();
                });

            // 监听快捷指令选择
            this.WhenAnyValue(x => x.SelectedQuickCommand)
                .Where(cmd => !string.IsNullOrEmpty(cmd))
                .Subscribe(cmd =>
                {
                    var cmdContent = cmd.Split('：').Last();
                    SendData = cmdContent;
                });
        }

        #region 核心业务逻辑
        /// <summary>
        /// 启动/连接（适配.NET 4.7.2异步方法）
        /// </summary>
        private async Task OnStartAsync()
        {
            try
            {
                if (!int.TryParse(Port, out int port))
                {
                    AddLog("端口号格式错误，请输入有效的数字", LogType.Error);
                    return;
                }

                _cts = new CancellationTokenSource();
                CanStart = false;
                CanStop = true;
                CanSend = true;

                if (IsServerMode)
                {
                    // 服务器模式 - 适配.NET 4.7.2无CancellationToken重载的异步方法
                    if (SelectedProtocol == "TCP")
                    {
                        var ip = IPAddress.Parse(ListenIp);
                        _tcpListener = new TcpListener(ip, port);
                        _tcpListener.Start();
                        AddLog($"TCP服务器已启动，监听 {ListenIp}:{port}", LogType.Info);

                        // 异步监听客户端连接（改用Task.Run + 循环检测取消令牌）
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                while (!_cts.Token.IsCancellationRequested)
                                {
                                    // .NET 4.7.2的AcceptTcpClientAsync无CancellationToken参数
                                    var acceptTask = _tcpListener.AcceptTcpClientAsync();
                                    var completedTask = await Task.WhenAny(acceptTask, Task.Delay(100, _cts.Token));

                                    // 替换IsCompletedSuccessfully，改用Status判断
                                    if (completedTask == acceptTask && acceptTask.Status == TaskStatus.RanToCompletion)
                                    {
                                        var client = acceptTask.Result;
                                        var clientEndPoint = client.Client.RemoteEndPoint.ToString();

                                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                                        {
                                            ConnectedClients.Add(clientEndPoint);
                                            _connectedTcpClients[clientEndPoint] = client;
                                            _totalConnectionCount++;
                                            _maxConcurrentCount = Math.Max(_maxConcurrentCount, ConnectedClients.Count);
                                            UpdateStatistics();
                                            this.RaisePropertyChanged(nameof(ClientCountText));
                                        });

                                        AddLog($"客户端 {clientEndPoint} 已连接", LogType.Info);
                                        _ = Task.Run(async () => await HandleTcpClientDataAsync(client, clientEndPoint), _cts.Token);
                                    }
                                }
                            }
                            catch (OperationCanceledException)
                            {
                                AddLog("TCP服务器监听已取消", LogType.Info);
                            }
                            catch (Exception ex)
                            {
                                AddLog($"TCP服务器异常：{ex.Message}", LogType.Error);
                            }
                        }, _cts.Token);
                    }
                    else
                    {
                        // UDP服务器模式
                        _udpClient = new UdpClient(port);
                        AddLog($"UDP服务器已启动，监听 {port} 端口", LogType.Info);

                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                while (!_cts.Token.IsCancellationRequested)
                                {
                                    // .NET 4.7.2的ReceiveAsync无CancellationToken参数
                                    var receiveTask = _udpClient.ReceiveAsync();
                                    var completedTask = await Task.WhenAny(receiveTask, Task.Delay(100, _cts.Token));

                                    // 替换IsCompletedSuccessfully，改用Status判断
                                    if (completedTask == receiveTask && receiveTask.Status == TaskStatus.RanToCompletion)
                                    {
                                        var result = receiveTask.Result;
                                        var clientEndPoint = result.RemoteEndPoint.ToString();
                                        var data = result.Buffer;

                                        RecvByteCount += data.Length;
                                        AddLogData(data, clientEndPoint, isRecv: true);
                                    }
                                }
                            }
                            catch (OperationCanceledException)
                            {
                                AddLog("UDP服务器接收已取消", LogType.Info);
                            }
                            catch (Exception ex)
                            {
                                AddLog($"UDP服务器异常：{ex.Message}", LogType.Error);
                            }
                        }, _cts.Token);
                    }
                }
                else
                {
                    // 客户端模式
                    if (SelectedProtocol == "TCP")
                    {
                        _tcpClient = new TcpClient();
                        await _tcpClient.ConnectAsync(TargetIp, port);
                        AddLog($"已连接到TCP服务器 {TargetIp}:{port}", LogType.Info);
                        _ = Task.Run(async () => await HandleTcpClientDataAsync(_tcpClient, "服务器"), _cts.Token);
                    }
                    else
                    {
                        _udpClient = new UdpClient();
                        AddLog($"UDP客户端已初始化，目标 {TargetIp}:{port}", LogType.Info);
                    }
                }

                ConnectionStatusText = "运行中";
                ConnectionStatusColor = Brushes.Green;
            }
            catch (Exception ex)
            {
                AddLog($"启动失败：{ex.Message}", LogType.Error);
                ConnectionStatusText = "启动失败";
                ConnectionStatusColor = Brushes.OrangeRed;
                CanStart = true;
                CanStop = false;
                CanSend = false;
            }
        }

        /// <summary>
        /// 停止/断开
        /// </summary>
        private void OnStop()
        {
            try
            {
                _cts?.Cancel();

                if (_timedSendTimer != null)
                {
                    _timedSendTimer.Dispose();
                    _timedSendTimer = null;
                    IsTimedSendEnabled = false;
                    TimedSendButtonText = "定时发送";
                }

                if (_tcpListener != null)
                {
                    _tcpListener.Stop();
                    _tcpListener = null;
                }

                if (_tcpClient != null)
                {
                    _tcpClient.Close();
                    _tcpClient = null;
                }

                if (_udpClient != null)
                {
                    _udpClient.Close();
                    _udpClient = null;
                }

                foreach (var client in _connectedTcpClients.Values)
                {
                    client.Close();
                }
                _connectedTcpClients.Clear();
                ConnectedClients.Clear();
                this.RaisePropertyChanged(nameof(ClientCountText));

                ConnectionStatusText = "未运行";
                ConnectionStatusColor = Brushes.Red;
                CanStart = true;
                CanStop = false;
                CanSend = false;

                AddLog("已停止运行/断开连接", LogType.Info);
            }
            catch (Exception ex)
            {
                AddLog($"停止失败：{ex.Message}", LogType.Error);
            }
        }

        /// <summary>
        /// 处理TCP客户端数据（适配.NET 4.7.2）
        /// </summary>
        private async Task HandleTcpClientDataAsync(TcpClient client, string clientId)
        {
            try
            {
                var stream = client.GetStream();
                var buffer = new byte[4096];

                while (!_cts.Token.IsCancellationRequested && client.Connected)
                {
                    // .NET 4.7.2的ReadAsync重载适配
                    var readTask = stream.ReadAsync(buffer, 0, buffer.Length);
                    var completedTask = await Task.WhenAny(readTask, Task.Delay(100, _cts.Token));

                    // 修复CS1061：替换IsCompletedSuccessfully为Task.Status判断
                    if (completedTask == readTask && readTask.Status == TaskStatus.RanToCompletion)
                    {
                        var bytesRead = readTask.Result;
                        if (bytesRead == 0) break;

                        var data = new byte[bytesRead];
                        Array.Copy(buffer, data, bytesRead);

                        RecvByteCount += bytesRead;
                        AddLogData(data, clientId, isRecv: true);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                AddLog($"[{clientId}] 数据监听已取消", LogType.Info);
            }
            catch (Exception ex)
            {
                AddLog($"[{clientId}] 数据接收异常：{ex.Message}", LogType.Error);
            }
            finally
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    if (ConnectedClients.Contains(clientId))
                    {
                        ConnectedClients.Remove(clientId);
                        this.RaisePropertyChanged(nameof(ClientCountText));
                    }
                    if (_connectedTcpClients.ContainsKey(clientId))
                    {
                        _connectedTcpClients.Remove(clientId);
                    }
                    UpdateStatistics();
                });

                client.Close();
                AddLog($"[{clientId}] 连接已断开", LogType.Info);
            }
        }

        /// <summary>
        /// 发送数据
        /// </summary>
        private async Task OnSendDataAsync()
        {
            if (string.IsNullOrEmpty(SendData))
            {
                AddLog("发送数据不能为空", LogType.Warning);
                return;
            }

            try
            {
                byte[] sendData;

                if (IsHexSend)
                {
                    sendData = HexStringToBytes(SendData);
                }
                else
                {
                    var encoding = GetEncoding(SelectedEncoding);
                    sendData = encoding.GetBytes(IsAutoNewLine ? $"{SendData}\r\n" : SendData);
                }

                if (IsServerMode)
                {
                    if (SelectedProtocol == "TCP")
                    {
                        // 修复CS8130：替换解构赋值为普通KeyValuePair遍历
                        foreach (KeyValuePair<string, TcpClient> kvp in _connectedTcpClients)
                        {
                            string clientId = kvp.Key;
                            TcpClient client = kvp.Value;

                            if (client.Connected)
                            {
                                await client.GetStream().WriteAsync(sendData, 0, sendData.Length);
                                AddLogData(sendData, clientId, isRecv: false);
                            }
                        }
                    }
                    else
                    {
                        if (_udpClient != null && ConnectedClients.Any())
                        {
                            var lastClient = ConnectedClients.Last();
                            var ipPort = lastClient.Split(':');
                            var ip = IPAddress.Parse(ipPort[0]);
                            var port = int.Parse(ipPort[1]);
                            var endPoint = new IPEndPoint(ip, port);

                            await _udpClient.SendAsync(sendData, sendData.Length, endPoint);
                            AddLogData(sendData, lastClient, isRecv: false);
                        }
                        else
                        {
                            AddLog("无UDP客户端连接，无法发送数据", LogType.Warning);
                            return;
                        }
                    }
                }
                else
                {
                    if (SelectedProtocol == "TCP" && _tcpClient?.Connected == true)
                    {
                        await _tcpClient.GetStream().WriteAsync(sendData, 0, sendData.Length);
                        AddLogData(sendData, "服务器", isRecv: false);
                    }
                    else if (SelectedProtocol == "UDP" && _udpClient != null)
                    {
                        var ip = IPAddress.Parse(TargetIp);
                        var port = int.Parse(Port);
                        var endPoint = new IPEndPoint(ip, port);

                        await _udpClient.SendAsync(sendData, sendData.Length, endPoint);
                        AddLogData(sendData, $"{TargetIp}:{port}", isRecv: false);
                    }
                }

                SendByteCount += sendData.Length;

                if (IsClearAfterSend)
                {
                    SendData = string.Empty;
                }

                AddLog("数据发送成功", LogType.Info);
            }
            catch (Exception ex)
            {
                AddLog($"数据发送失败：{ex.Message}", LogType.Error);
            }
        }

        /// <summary>
        /// 切换定时发送
        /// </summary>
        private void OnToggleTimedSend()
        {
            IsTimedSendEnabled = !IsTimedSendEnabled;

            if (IsTimedSendEnabled)
            {
                TimedSendButtonText = "停止定时";
                _timedSendTimer = new Timer(async _ => await OnSendDataAsync(), null, 0, 1000);
                AddLog("定时发送已启动（间隔：1秒）", LogType.Info);
            }
            else
            {
                TimedSendButtonText = "定时发送";
                _timedSendTimer?.Dispose();
                _timedSendTimer = null;
                AddLog("定时发送已停止", LogType.Info);
            }
        }
        #endregion

        #region 辅助方法
        private void AddLog(string message, LogType logType = LogType.Info)
        {
            var timestamp = ShowTimestamp ? $"[{DateTime.Now:HH:mm:ss.fff}] " : "";
            string logPrefix = string.Empty;

            // 替换新式switch表达式为普通switch语句
            switch (logType)
            {
                case LogType.Info:
                    logPrefix = "[信息] ";
                    break;
                case LogType.Warning:
                    logPrefix = "[警告] ";
                    break;
                case LogType.Error:
                    logPrefix = "[错误] ";
                    break;
                default:
                    logPrefix = "";
                    break;
            }

            var logLine = $"{timestamp}{logPrefix}{message}\r\n";
            LogText += logLine;
        }

        private void AddLogData(byte[] data, string target, bool isRecv)
        {
            var timestamp = ShowTimestamp ? $"[{DateTime.Now:HH:mm:ss.fff}] " : "";
            var flag = ShowSendRecvFlag ? (isRecv ? "[接收] " : "[发送] ") : "";
            var dataStr = IsHexDisplay ? BytesToHexString(data) : GetEncoding(SelectedEncoding).GetString(data);

            var logLine = $"{timestamp}{flag}[{target}] {dataStr}\r\n";
            LogText += logLine;
        }

        private void OnClearClientList()
        {
            ConnectedClients.Clear();
            this.RaisePropertyChanged(nameof(ClientCountText));
            AddLog("客户端列表已清空", LogType.Info);
        }

        private void OnDisconnectClient(string clientId)
        {
            if (_connectedTcpClients.TryGetValue(clientId, out var client))
            {
                client.Close();
                ConnectedClients.Remove(clientId);
                _connectedTcpClients.Remove(clientId);
                this.RaisePropertyChanged(nameof(ClientCountText));
                AddLog($"已断开客户端 {clientId}", LogType.Info);
                UpdateStatistics();
            }
        }

        private void OnLoadCommandLibrary()
        {
            AddLog("指令库加载成功（示例）", LogType.Info);
            QuickCommands.Add("常用指令4：自定义指令");
            this.RaisePropertyChanged(nameof(QuickCommands));
        }

        private void OnClearLog()
        {
            LogText = string.Empty;
            AddLog("日志已清空", LogType.Info);
        }

        private void OnSaveLog()
        {
            try
            {
                var saveFileDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "文本文件 (*.txt)|*.txt|所有文件 (*.*)|*.*",
                    FileName = $"NetDebugLog_{DateTime.Now:yyyyMMddHHmmss}.txt",
                    DefaultExt = ".txt"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    File.WriteAllText(saveFileDialog.FileName, LogText, Encoding.UTF8);
                    AddLog($"日志已保存到：{saveFileDialog.FileName}", LogType.Info);
                }
            }
            catch (Exception ex)
            {
                AddLog($"日志保存失败：{ex.Message}", LogType.Error);
            }
        }

        private void OnExportConfig()
        {
            try
            {
                var saveFileDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "配置文件 (*.config)|*.config|所有文件 (*.*)|*.*",
                    FileName = "NetDebugConfig.config",
                    DefaultExt = ".config"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    // 简单配置保存（如需JSON可引用Newtonsoft.Json）
                    var configLines = new List<string>
                    {
                        $"IsServerMode={IsServerMode}",
                        $"SelectedProtocol={SelectedProtocol}",
                        $"ListenIp={ListenIp}",
                        $"TargetIp={TargetIp}",
                        $"Port={Port}",
                        $"IsHexSend={IsHexSend}",
                        $"SelectedEncoding={SelectedEncoding}"
                    };

                    File.WriteAllLines(saveFileDialog.FileName, configLines, Encoding.UTF8);
                    AddLog($"配置已导出到：{saveFileDialog.FileName}", LogType.Info);
                }
            }
            catch (Exception ex)
            {
                AddLog($"配置导出失败：{ex.Message}", LogType.Error);
            }
        }

        private void OnImportConfig()
        {
            try
            {
                var openFileDialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "配置文件 (*.config)|*.config|所有文件 (*.*)|*.*",
                    DefaultExt = ".config"
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    var configLines = File.ReadAllLines(openFileDialog.FileName, Encoding.UTF8);
                    foreach (var line in configLines)
                    {
                        if (string.IsNullOrEmpty(line) || !line.Contains('=')) continue;

                        var parts = line.Split('=');
                        var key = parts[0];
                        var value = parts[1];

                        // 替换新式switch表达式为普通switch语句
                        switch (key)
                        {
                            case "IsServerMode":
                                IsServerMode = bool.Parse(value);
                                break;
                            case "SelectedProtocol":
                                SelectedProtocol = value;
                                break;
                            case "ListenIp":
                                ListenIp = value;
                                break;
                            case "TargetIp":
                                TargetIp = value;
                                break;
                            case "Port":
                                Port = value;
                                break;
                            case "IsHexSend":
                                IsHexSend = bool.Parse(value);
                                break;
                            case "SelectedEncoding":
                                SelectedEncoding = value;
                                break;
                        }
                    }

                    AddLog($"配置已从：{openFileDialog.FileName} 导入", LogType.Info);
                }
            }
            catch (Exception ex)
            {
                AddLog($"配置导入失败：{ex.Message}", LogType.Error);
            }
        }

        private void UpdateStatistics()
        {
            var totalBytes = RecvByteCount + SendByteCount;
            StatisticsText = $"总连接数：{_totalConnectionCount} | 最大并发：{_maxConcurrentCount} | 累计收发：{totalBytes} 字节";
        }

        private Encoding GetEncoding(string encodingName)
        {
            // 替换新式switch表达式为普通switch语句
            switch (encodingName)
            {
                case "GB2312":
                    return Encoding.GetEncoding("GB2312");
                case "ASCII":
                    return Encoding.ASCII;
                default:
                    return Encoding.UTF8;
            }
        }

        private byte[] HexStringToBytes(string hex)
        {
            hex = hex.Replace(" ", "").Replace("-", "");
            var bytes = new byte[hex.Length / 2];
            for (int i = 0; i < hex.Length; i += 2)
            {
                bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
            }
            return bytes;
        }

        private string BytesToHexString(byte[] bytes)
        {
            return BitConverter.ToString(bytes).Replace("-", " ");
        }
        #endregion

        /// <summary>
        /// 日志类型
        /// </summary>
        private enum LogType
        {
            Info,
            Warning,
            Error
        }
    }
}