using FZK.Application.Share.Init;
using FZK.Application.Share.Run;
using FZK.Hardware.PLC.Base;
using FZK.Hardware.Robot.Base;
using FZK.Hardware.Robot.Epson;
using FZK.Hardware.Scanner.Base;
using FZK.Logger;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FZK.Application.Run.Service
{
    /// <summary>
    /// 硬件服务实现类（适配真实硬件：欧姆龙PLC+康耐视扫码枪+EPSON机械臂）
    /// 核心：复用HardwareManager已初始化的硬件实例，不重复Init
    /// 适配：C# 7.3（移除switch expression，改用传统switch语句）
    /// </summary>
    public class HardwareService : IHardwareService
    {
        // 复用已初始化的硬件管理器
        private IHardwareManager _hardwareManager;

        /// <summary>
        /// 构造函数注入已初始化的HardwareManager
        /// </summary>
        public HardwareService(IHardwareManager hardwareManager)
        {
            _hardwareManager = hardwareManager ?? throw new ArgumentNullException(nameof(hardwareManager));

            // 校验硬件是否已初始化（根据实际业务决定是否启用）
            //if (!_hardwareManager.Initialized)
            //{
            //    throw new InvalidOperationException("硬件未完成初始化！请先调用HardwareManager.InitAsync()");
            //}
            //Logs.LogInfo("HardwareService初始化完成，复用已初始化的硬件实例");
        }

        #region IHardwareService接口实现
        /// <summary>
        /// 初始化（空实现，避免重复初始化）
        /// </summary>
        public void Init()
        {
            Logs.LogInfo("HardwareService.Init 被调用，尝试重新连接硬件...");

            // 异步执行重连，避免阻塞调用线程
            Task.Run(async () =>
            {
                try
                {
                    // 1. 重新连接 PLC
                    //if (_hardwareManager.OmronPLC != null && !_hardwareManager.OmronPLC.Connected)
                    //{

                    //    //Logs.LogInfo(plcReconnected ? "PLC 重连成功" : "PLC 重连失败");
                    //}

                    // 2. 重新连接扫码枪
                    //await ReconnectScannerAsync(_hardwareManager.LeftUpScanner, "左上");
                    //await ReconnectScannerAsync(_hardwareManager.LeftDownScanner, "左下");
                    //await ReconnectScannerAsync(_hardwareManager.RightUpScanner, "右上");
                    //await ReconnectScannerAsync(_hardwareManager.RightDownScanner, "右下");
                    //await ReconnectScannerAsync(_hardwareManager.SPScanner, "机械臂扫码");

                    // 3. 重新连接机械臂
                    //if (_hardwareManager.EpsonRobot != null && !_hardwareManager.EpsonRobot.Connected)
                    //{
                    //    bool robotReconnected = _hardwareManager.EpsonRobot.CheckConnection();
                    //    Logs.LogInfo(robotReconnected ? "机械臂重连成功" : "机械臂重连失败");
                    //}
                    Task<InitResult> result = _hardwareManager.InitAsync();
                    bool plcReconnected = result.Result.Success;
                    if (plcReconnected)
                        Logs.LogInfo("硬件重连操作完成");
                }
                catch (Exception ex)
                {
                    Logs.LogError(ex, "硬件重连过程中发生异常");
                }
            });
        }
        /// <summary>
        /// 辅助方法：重新连接单个扫码枪
        /// </summary>
        private async Task ReconnectScannerAsync(IScanner scanner, string name)
        {
            if (scanner == null) return;

            try
            {
                if (!scanner.Connected)
                {
                    bool success = await scanner.CheckConnectionAsync();
                    Logs.LogInfo(success ? $"{name}扫码枪重连成功" : $"{name}扫码枪重连失败");
                }
            }
            catch (Exception ex)
            {
                Logs.LogError(ex, $"{name}扫码枪重连异常");
            }
        }

        // 其他现有方法保持不变（Stop, ReadPlcRegisters, WritePlcRegister, TriggerScanner 等）


        /// <summary>
        /// 停止硬件（调用各硬件的Close方法释放资源）
        /// </summary>
     

        /// <summary>
        /// 读取PLC多个寄存器值（适配欧姆龙PLC的BatchRead方法）
        /// </summary>
        /// <param name="addresses">寄存器地址列表（DM/CIO等）</param>
        /// <returns>地址-值键值对</returns>
        public async Task<Dictionary<int, int>> ReadPlcRegisters(List<int> addresses)
        {
            var result = new Dictionary<int, int>();
            if (addresses == null || addresses.Count == 0)
            {
                Logs.LogWarn("读取PLC寄存器：地址列表为空");
                return result;
            }

            try
            {
                // 校验PLC连接状态
                if (!_hardwareManager.OmronPLC.Connected)
                {
                    bool reconnectSuccess = _hardwareManager.OmronPLC.CheckConnection();
                    if (!reconnectSuccess)
                    {
                        Logs.LogError("PLC连接异常，重连失败，读取寄存器失败");
                        await Task.Delay(100); // 延时100毫秒，避免立即重试
                        return result;
                    }
                }

                // 批量读取（默认读取DM寄存器，可根据业务调整为CIO/TIM等）
                int minAddr = addresses.Min();
                int maxAddr = addresses.Max();
                long range = (long)maxAddr - minAddr + 1;
                if (range > ushort.MaxValue)
                {
                    Logs.LogError($"地址范围过大：{minAddr}-{maxAddr}，超出PLC支持的最大连续读取长度 {ushort.MaxValue}");
                    return result;
                }

                ushort startAddress = (ushort)minAddr;
                ushort count = (ushort)range;
                List<int> plcValues = await Task.Run(() =>
                    _hardwareManager.OmronPLC.BatchRead(PLCRegisterType.DM, startAddress, count)
                );

                // 检查返回数据的完整性
                if (plcValues == null || plcValues.Count < count)
                {
                    Logs.LogError($"批量读取返回数据不完整，期望{count}个，实际{plcValues?.Count ?? 0}个");
                    return result;
                }

                // 映射到地址-值字典
                for (int i = 0; i < addresses.Count; i++)
                {
                    int address = addresses[i];
                    if (address >= startAddress && address <= startAddress + count - 1)
                    {
                        int index = address - startAddress;
                        if (index >= 0 && index < plcValues.Count)
                        {
                            result[address] = plcValues[index];
                        }
                        else
                        {
                            Logs.LogError($"地址{address}计算索引{index}超出范围");
                        }
                    }
                }

                Logs.LogInfo($"读取PLC DM寄存器成功，地址：{string.Join(",", addresses)}，值：{string.Join(",", result.Values)}");
                await Task.Delay(100); // 延时100毫秒，避免立即重试
            }
            catch (Exception ex)
            {
                Logs.LogError($"读取PLC寄存器失败：{ex.Message}");
                await Task.Delay(500); // 延时100毫秒，避免立即重试
            }

            return result;
        }

        /// <summary>
        /// 写入PLC单个寄存器值（适配欧姆龙PLC的Write方法）
        /// </summary>
        public async Task WritePlcRegister(int address, int value)
        {
            try
            {
                // 校验PLC连接状态
                if (!_hardwareManager.OmronPLC.Connected)
                {
                    bool reconnectSuccess = _hardwareManager.OmronPLC.CheckConnection();
                    if (!reconnectSuccess)
                    {
                        throw new Exception("PLC连接异常，重连失败，写入寄存器失败");
                    }
                }

                // 异步执行写入（默认写入DM寄存器，可根据业务调整）
                bool writeSuccess = await Task.Run(() =>
                    _hardwareManager.OmronPLC.Write(PLCRegisterType.DM, (ushort)address, value)
                );

                if (!writeSuccess)
                {
                    throw new Exception($"写入PLC DM{address} = {value} 失败");
                }

                Logs.LogInfo($"写入PLC DM{address} = {value} 成功");
            }
            catch (Exception ex)
            {
                Logs.LogError($"写入PLC寄存器失败：{ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 触发扫码枪扫码（适配康耐视扫码枪的TriggerAsync方法）
        /// </summary>
        /// <param name="scannerType">扫码类型（映射到5个扫码枪实例）</param>
        public async Task<string> TriggerScanner(ScannerType scannerType)
        {
            string scanCode = string.Empty;
            IScanner targetScanner = null;

            switch (scannerType)
            {
                case ScannerType.左下:
                    targetScanner = _hardwareManager.LeftDownScanner;
                    break;
                case ScannerType.左上:
                    targetScanner = _hardwareManager.LeftUpScanner;
                    break;
                case ScannerType.机械臂:
                    targetScanner = _hardwareManager.SPScanner;
                    break;
                case ScannerType.右上:
                    targetScanner = _hardwareManager.RightUpScanner;
                    break;
                case ScannerType.右下:
                    targetScanner = _hardwareManager.RightDownScanner;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(scannerType), "不支持的扫码类型");
            }

            try
            {
                // 校验扫码枪连接状态
                if (targetScanner == null)
                {
                    throw new Exception($"{scannerType}扫码枪实例未初始化");
                }

                if (!targetScanner.Connected)
                {
                    bool reconnectSuccess = await targetScanner.CheckConnectionAsync();
                    if (!reconnectSuccess)
                    {
                        throw new Exception($"{scannerType}扫码枪连接异常，重连失败");
                    }
                }

                // 触发扫码（调用康耐视扫码枪的TriggerAsync方法）
                bool triggerSuccess = await targetScanner.TriggerAsync();
                if (triggerSuccess)
                {
                    scanCode = targetScanner.Content?.Trim();
                    if (string.IsNullOrEmpty(scanCode))
                    {
                        Logs.LogFatal($"{scannerType}扫码枪未返回有效码值");
                    }
                    else
                    {
                        Logs.LogFatal($"{scannerType}扫码成功：{scanCode}");
                    }
                }
                else
                {
                    throw new Exception($"{scannerType}扫码枪触发失败");
                }
            }
            catch (Exception ex)
            {
                Logs.LogError($"触发{scannerType}扫码枪失败：{ex.Message}");
                // 失败时返回空字符串，由上层根据业务逻辑处理（例如记录失败次数）
            }

            return scanCode;
        }

        /// <summary>
        /// 获取机械臂指令（适配EPSON机械臂的接收数据）
        /// </summary>
        public async Task<string> GetRobotCommand()
        {
            string result = string.Empty; // 初始化默认值
            try
            {
                // 校验机械臂连接状态
                if (!_hardwareManager.EpsonRobot.Connected)
                {
                    bool reconnectSuccess = _hardwareManager.EpsonRobot.CheckConnection();
                    if (!reconnectSuccess)
                    {
                        Logs.LogError("EPSON机械臂连接异常，重连失败，读取指令失败");
                        return result;
                    }
                }

                // 异步获取机械臂最新接收的指令
                string robotCmd = await Task.Run(() => _hardwareManager.EpsonRobot.ReceiveContent?.Trim());
                // 修复：返回实际获取的指令
                return robotCmd ?? string.Empty;
            }
            catch (Exception ex)
            {
                Logs.LogError($"读取EPSON机械臂指令失败：{ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// 发送机械臂响应（适配EPSON机械臂的SendCommand方法）
        /// </summary>
        public async Task SendRobotResponse(bool success)
        {
            try
            {
                // 校验机械臂连接状态
                if (!_hardwareManager.EpsonRobot.Connected)
                {
                    bool reconnectSuccess = _hardwareManager.EpsonRobot.CheckConnection();
                    if (!reconnectSuccess)
                    {
                        throw new Exception("EPSON机械臂连接异常，重连失败，发送响应失败");
                    }
                }

                // 构造响应指令（根据你的机械臂协议调整）
                string responseCmd = success ? "OK" : "NG";
                bool sendSuccess = await Task.Run(() =>
                    _hardwareManager.EpsonRobot.SendCommand(responseCmd)
                );

                if (!sendSuccess)
                {
                    throw new Exception($"发送机械臂响应{responseCmd}失败");
                }

                Logs.LogInfo($"发送机械臂响应：{(success ? "成功(OK)" : "失败(NG)")}");
            }
            catch (Exception ex)
            {
                Logs.LogError($"发送机械臂响应失败：{ex.Message}");
                throw;
            }
        }
        #endregion

        #region 扩展方法（可选）
        /// <summary>
        /// 批量触发多个扫码枪（扩展业务方法）
        /// </summary>
        public async Task<Dictionary<ScannerType, string>> BatchTriggerScanners(List<ScannerType> scannerTypes)
        {
            var result = new Dictionary<ScannerType, string>();
            foreach (var type in scannerTypes)
            {
                string code = await TriggerScanner(type);
                result[type] = code;
            }
            return result;
        }
        /// <summary>
        /// 读取单个PLC寄存器值（默认读取DM区）
        /// </summary>
        /// <param name="address">寄存器地址（如110）</param>
        /// <returns>读取到的整数值，失败返回-1</returns>
        public async Task<int> ReadPlcRegister(int address)
        {
            try
            {
                // 校验PLC连接状态
                if (!_hardwareManager.OmronPLC.Connected)
                {
                    bool reconnectSuccess = _hardwareManager.OmronPLC.CheckConnection();
                    if (!reconnectSuccess)
                    {
                        Logs.LogError("PLC连接异常，重连失败，读取寄存器失败");
                        return -1;
                    }
                }

                // 异步执行读取单个寄存器（调用BatchRead读取1个值）
                List<int> values = await Task.Run(() =>
                    _hardwareManager.OmronPLC.BatchRead(PLCRegisterType.DM, (ushort)address, 1)
                );

                if (values != null && values.Count == 1)
                {
                    Logs.LogInfo($"读取PLC DM{address} = {values[0]}");
                    return values[0];
                }
                else
                {
                    Logs.LogError($"读取PLC DM{address}失败，返回数据无效");
                    return -1;
                }
            }
            catch (Exception ex)
            {
                Logs.LogError(ex, $"读取PLC寄存器DM{address}异常");
                return -1;
            }
        }
        /// <summary>
        /// 批量写入PLC寄存器（逐个写入，确保只写入指定地址）
        /// </summary>
        public async Task<bool> BatchWritePlcRegisters(Dictionary<int, int> addressValues)
        {
            if (addressValues == null || addressValues.Count == 0)
            {
                Logs.LogWarn("批量写入PLC寄存器：地址-值列表为空");
                return false;
            }

            try
            {
                bool allSuccess = true;
                foreach (var kv in addressValues)
                {
                    try
                    {
                        await WritePlcRegister(kv.Key, kv.Value);
                    }
                    catch (Exception ex)
                    {
                        Logs.LogError($"写入PLC DM{kv.Key} = {kv.Value} 失败：{ex.Message}");
                        allSuccess = false;
                    }
                }

                if (allSuccess)
                {
                    Logs.LogInfo($"批量写入PLC DM寄存器成功，地址：{string.Join(",", addressValues.Keys)}");
                }
                else
                {
                    Logs.LogWarn("批量写入PLC寄存器部分失败，请检查日志");
                }
                return allSuccess;
            }
            catch (Exception ex)
            {
                Logs.LogError($"批量写入PLC寄存器失败：{ex.Message}");
                return false;
            }
        }

        public void Stop()
        {
            Logs.LogInfo("HardwareService.Init 被调用，尝试重新连接硬件...");

            // 异步执行重连，避免阻塞调用线程
            Task.Run(async () =>
            {
                try
                {
                    Task<InitResult> result = _hardwareManager.Stop();
                    bool plcReconnected = result.Result.Success;
                    if (plcReconnected)
                        Logs.LogInfo("硬件重连操作完成");
                }
                catch (Exception ex)
                {
                    Logs.LogError(ex, "硬件重连过程中发生异常");
                }
            });
        }
        #endregion
    }
}