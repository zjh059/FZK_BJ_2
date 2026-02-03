using FZK.Hardware.Scanner.Base;
using FZK.Logger;
using ReactiveUI;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Media.Media3D;

namespace FZK.Hardware.Scanner.Cognex
{
    class ScannerCognexDM260 : ReactiveObject, IScanner
    {

        public bool Initialized { get; set; }

        public bool Connected { get; set; }

        public string Message { get; set; }

        public IObservable<string> MessageObservable { get; set; }

        public string Content { get; set; }

        public IObservable<string> ContentObservable { get; set; }
        protected TcpClient tcpClient = null;
        protected CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        ScannerConfig _scannerConfig = null;
        protected ConcurrentQueue<byte[]> receiveQueue = new ConcurrentQueue<byte[]>();

        public void Close()
        {
            cancellationTokenSource.Cancel();

            Task.Factory.StartNew(() =>
            {
                if (tcpClient != null)
                {
                    tcpClient.Client.Shutdown(SocketShutdown.Both); // 先关闭读写
                    tcpClient.Close();
                    tcpClient = null;
                    while (receiveQueue.TryDequeue(out _)) { } // 清空未解析数据
                    cancellationTokenSource.Dispose();
                    // 5. 重置状态
                    Initialized = false;
                    Connected = false;
                    Content = string.Empty;
                    Message = string.Empty;
                }
            });
        }

        public bool Init(ScannerConfig scannerConfig)
        {
            try
            {
                if (cancellationTokenSource.IsCancellationRequested)
                {
                    cancellationTokenSource = new CancellationTokenSource();
                }
                _scannerConfig = scannerConfig;

                if (Initialized)
                    return true;
                bool result = false;
                if (scannerConfig == null)
                {
                    Logs.LogError("扫码器配置文件为空");
                    return false;
                }
                if (tcpClient == null)
                {
                    tcpClient = new System.Net.Sockets.TcpClient();
                    result = tcpClient.ConnectAsync(scannerConfig.IpAddress, scannerConfig.Port).Wait(1000);
                }
                if (!result)
                {
                    Message = $"TCP连接超时！目标：{scannerConfig.IpAddress}:{scannerConfig.Port}，超时时间：1000ms";
                }
                if (tcpClient.Connected)
                {
                    Initialized = true;
                    Connected = tcpClient.Connected;
                    Task.Factory.StartNew(ReceiveTask);
                    Task.Factory.StartNew(AnalysisTask);

                }
                else
                {
                    tcpClient = null;
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                Logs.LogError(ex);
                Message = ex.Message;
                return false;
            }
        }

        private void AnalysisTask()
        {
            while (!cancellationTokenSource.IsCancellationRequested)
            {
                var result = receiveQueue.TryDequeue(out byte[] receiveBytes);
                if (result)
                {
                    DoAnalysis(receiveBytes);
                }
                else
                {
                    Task.Delay(1).Wait();
                }
            }
        }

        private void DoAnalysis(byte[] receiveBytes)
        {
            if (receiveBytes == null) return;
            if (receiveBytes.Length == 0) return;
            Content = Encoding.UTF8.GetString(receiveBytes);
        }

        /// <summary>
        /// 检查连接状态
        /// </summary>
        /// <returns></returns>
        public bool CheckConnection()
        {
            if (tcpClient == null)
            {
                tcpClient = new TcpClient(_scannerConfig.IpAddress, _scannerConfig.Port);
            }

            if (!tcpClient.Connected)
            {
                tcpClient.Connect(_scannerConfig.IpAddress, _scannerConfig.Port);
            }

            Connected = tcpClient.Connected;

            return Connected;
        }
        public async Task<bool> TriggerAsync()
        {
            bool result = CheckConnection();
            try
            {
                while (!cancellationTokenSource.IsCancellationRequested)
                {
                    await Task.Delay(_scannerConfig.DelayTime);

                    if (result)
                    {
                        tcpClient.Client.Send(Encoding.UTF8.GetBytes(_scannerConfig.TriggerCommand));
                    }
                }
               
            }
            catch (Exception ex)
            {
                result = false;
                Message = ex.ToString();
            }
            return result;
        }
        private void ReceiveTask()
        {
            while (!cancellationTokenSource.IsCancellationRequested)
            {
                try
                {
                    if (tcpClient == null) break;
                    if (tcpClient.Available <= 0)
                    {
                        Task.Delay(1).Wait();
                        continue;
                    }

                    byte[] buffer = new byte[4096];//接收缓冲区
                    int length = tcpClient.Client.Receive(buffer);//阻塞
                    byte[] receiveBytes = buffer.Take(length).ToArray();//实际长度
                    receiveQueue.Enqueue(receiveBytes);//入队
                }
                catch (Exception ex)
                {
                    Logs.LogError(ex);
                }

            }
        }
    }
}
