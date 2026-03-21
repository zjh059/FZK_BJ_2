using FZK.Application.Share.Config;
using FZK.Application.Share.Init;
using FZK.Hardware.Scanner.Base;
using FZK.Logger;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace FZK.Application.Debug.ViewModels
{
    internal class ScannerDebugViewModel : ReactiveObject
    {
        /// <summary>
        /// 扫码器IP地址
        /// </summary>
        [Reactive]
        public string IPAdress { get; set; }

        /// <summary>
        /// 扫码器端口
        /// </summary>
        [Reactive]
        public string Port { get; set; }

        /// <summary>
        /// 是否已连接
        /// </summary>
        [Reactive]
        public bool Connected { get; set; }

        /// <summary>
        /// 连接状态文本
        /// </summary>
        [Reactive]
        public string StatusText { get; set; } = "未连接";

        /// <summary>
        /// 调试日志内容
        /// </summary>
        [Reactive]
        public string LogText { get; set; } = string.Empty;

        /// <summary>
        /// 要发送的指令内容
        /// </summary>
        [Reactive]
        public string SendData { get; set; } = string.Empty;

        /// <summary>
        /// 解析后的扫码数据
        /// </summary>
        [Reactive]
        public string ParsedData { get; set; } = string.Empty;

        /// <summary>
        /// 是否自动解析数据
        /// </summary>
        [Reactive]
        public bool IsAutoParse { get; set; } = true;

        /// <summary>
        /// 自动解析按钮文本
        /// </summary>
        [Reactive]
        public string AutoParseButtonText { get; set; } = "关闭自动解析";

        #region 命令定义
        public ICommand ConnectScannerCommand { get; }
        public ICommand DisconnectScannerCommand { get; }
        public ICommand SendDataCommand { get; }
        public ICommand ClearLogCommand { get; }
        public ICommand ToggleAutoParseCommand { get; }
        #endregion

        #region 依赖注入对象
        public ISystemConfigManager SystemConfigManager { get; }
        public IHardwareManager HardwareManager { get; }
        #endregion

        /// <summary>
        /// 扫码器硬件实例
        /// </summary>
        private readonly IScanner _scanner;

        /// <summary>
        /// 扫码器配置对象
        /// </summary>
        private ScannerConfig _scannerConfig;

        public ScannerDebugViewModel(
            ISystemConfigManager systemConfigManager,
            IHardwareManager hardwareManager)
        {
            // 注入依赖
            SystemConfigManager = systemConfigManager;
            HardwareManager = hardwareManager;

            // 初始化扫码器实例（从硬件管理器获取，或新建实例）
            _scanner = hardwareManager.SPScanner; // 如果硬件管理器有扫码器属性，可替换为 hardwareManager.Scanner

            // 初始化配置（从系统配置读取，若无则使用默认值）
            InitScannerConfig();

            // 初始化命令
            ConnectScannerCommand = ReactiveCommand.Create(OnConnectScannerCommand);
            DisconnectScannerCommand = ReactiveCommand.Create(OnDisconnectScannerCommand);
            SendDataCommand = ReactiveCommand.Create(OnSendDataCommand);
            ClearLogCommand = ReactiveCommand.Create(OnClearLogCommand);
            ToggleAutoParseCommand = ReactiveCommand.Create(OnToggleAutoParseCommand);

            // 初始化状态
            Connected = _scanner.Connected;
            UpdateStatusText();

            // 启动数据监听
            StartDataListening();
        }

        #region 初始化方法
        /// <summary>
        /// 初始化扫码器配置
        /// </summary>
        private void InitScannerConfig()
        {
          
                IPAdress = "192.168.1.100";
                Port = "8080";
                _scannerConfig = new ScannerConfig
                {
                    IpAddress = IPAdress,
                    Port = int.Parse(Port),
                    DelayTime = 100,
                    TriggerCommand = string.Empty
                };
            
        }
        #endregion

        #region 命令执行方法
        /// <summary>
        /// 连接扫码器命令执行
        /// </summary>
        private void OnConnectScannerCommand()
        {
            if (!Connected)
            {
                try
                {
                    // 更新配置
                    _scannerConfig.IpAddress = IPAdress;
                    _scannerConfig.Port = int.TryParse(Port, out int port) ? port : 8080;

                    // 初始化并连接
                    bool result = _scanner.Init(_scannerConfig);
                    Connected = _scanner.Connected;
                    UpdateStatusText();

                    if (result)
                    {
                        Logs.LogInfo($"扫码器连接成功：{IPAdress}:{Port}");
                        Logs.LogInfo("调试：扫码器已连接...");
                        MessageBox.Show("扫码器连接成功！");
                    }
                    else
                    {
                        Logs.LogInfo($"扫码器连接失败：{_scanner.Message}");
                        MessageBox.Show($"扫码器连接失败：{_scanner.Message}");
                    }
                }
                catch (Exception ex)
                {
                    Connected = false;
                    UpdateStatusText();
                    Logs.LogInfo($"连接扫码器异常：{ex.Message}");
                    Logs.LogError(ex);
                    MessageBox.Show($"连接扫码器异常：{ex.Message}");
                }
            }
        }

        /// <summary>
        /// 断开扫码器命令执行
        /// </summary>
        private void OnDisconnectScannerCommand()
        {
            if (Connected)
            {
                try
                {
                    _scanner.Close();
                    Connected = _scanner.Connected;
                    UpdateStatusText();
                    Logs.LogInfo("扫码器已断开连接");
                    Logs.LogInfo("调试：扫码器已断开...");
                    MessageBox.Show("扫码器已断开连接！");
                }
                catch (Exception ex)
                {
                    Logs.LogInfo($"断开扫码器异常：{ex.Message}");
                    Logs.LogError(ex);
                    MessageBox.Show($"断开扫码器异常：{ex.Message}");
                }
            }
        }

        /// <summary>
        /// 发送指令命令执行
        /// </summary>
        private void OnSendDataCommand()
        {
            if (Connected)
            {
                if (string.IsNullOrWhiteSpace(SendData))
                {
                    MessageBox.Show("发送指令不能为空！");
                    return;
                }

                try
                {
                    // 检查连接状态
                    bool isConnected = _scanner.Connected;
                    if (!isConnected)
                    {
                        Connected = false;
                        UpdateStatusText();
                        MessageBox.Show("扫码器连接已断开，发送失败！");
                        return;
                    }
                    _scanner.TriggerAsync();
                    Logs.LogInfo($"发送指令成功：{SendData}");
                    MessageBox.Show($"扫码器发送指令：{SendData}");
                }
                catch (Exception ex)
                {
                    Logs.LogInfo($"发送指令异常：{ex.Message}");
                    Logs.LogError(ex);
                    MessageBox.Show($"发送指令异常：{ex.Message}");
                }
            }
            else
            {
                MessageBox.Show("扫码器未连接，发送失败！");
            }
        }

        /// <summary>
        /// 清空日志命令执行
        /// </summary>
        private void OnClearLogCommand()
        {
            LogText = string.Empty;
            Logs.LogInfo("日志已清空");
        }

        /// <summary>
        /// 切换自动解析命令执行
        /// </summary>
        private void OnToggleAutoParseCommand()
        {
            IsAutoParse = !IsAutoParse;
            AutoParseButtonText = IsAutoParse ? "关闭自动解析" : "开启自动解析";
            Logs.LogInfo(IsAutoParse ? "已开启自动解析" : "已关闭自动解析");

            // 关闭自动解析时清空解析结果
            if (!IsAutoParse)
            {
                ParsedData = string.Empty;
            }
        }
        #endregion

        #region 辅助方法
        /// <summary>
        /// 更新连接状态文本
        /// </summary>
        private void UpdateStatusText()
        {
            StatusText = Connected ? "已连接" : "未连接";
        }



        /// <summary>
        /// 启动数据监听
        /// </summary>
        private void StartDataListening()
        {
            // 后台轮询监听扫码器数据
            Task.Run(async () =>
            {
                while (true)
                {
                    await Task.Delay(50); // 50ms轮询一次

                    if (Connected && !string.IsNullOrEmpty(_scanner.Content))
                    {
                        string receivedData = _scanner.Content;
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            // 添加接收日志
                            Logs.LogInfo($"接收数据：{receivedData}");

                            // 自动解析数据
                            if (IsAutoParse)
                            {
                                ParseScannerData(receivedData);
                            }
                            _scanner.Close();
                        });
                    }
                }
            });
        }

        /// <summary>
        /// 解析扫码器数据
        /// </summary>
        /// <param name="data">原始数据</param>
        private void ParseScannerData(string data)
        {
            try
            {
                // 基础解析逻辑，可根据实际扫码器返回格式修改
                ParsedData = $"解析结果：{Environment.NewLine}{data}";

                // 示例：如果是JSON格式
                // if (data.StartsWith("{"))
                // {
                //     var jsonObj = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(data);
                //     ParsedData = $"条码：{jsonObj.barcode}{Environment.NewLine}时间：{jsonObj.time}";
                // }
            }
            catch (Exception ex)
            {
                ParsedData = $"解析失败：{ex.Message}";
                Logs.LogInfo($"解析数据异常：{ex.Message}");
            }
        }
        #endregion
    }


}