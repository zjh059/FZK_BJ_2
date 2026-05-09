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
using static Unity.Storage.RegistrationSet;

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
        private readonly IHardwareManager _hardwareManager;
        private const int MaxRetries = 3; // 写入PLC最大重试次数

        /// <summary>
        /// 构造函数注入已初始化的HardwareManager
        /// </summary>
        public HardwareService(IHardwareManager hardwareManager)
        {
            _hardwareManager = hardwareManager ?? throw new ArgumentNullException(nameof(hardwareManager));
            // 硬件初始化已在应用启动时完成，此处不再重复初始化
            Logs.LogInfo("[HardwareService] 服务初始化完成，复用硬件实例");
        }

        #region IHardwareService接口实现

        /// <summary>
        /// 初始化（触发硬件重连，避免阻塞调用线程）
        /// </summary>
        public void Init()
        {
            Logs.LogInfo("[HardwareService] 开始尝试重连硬件...");
            Task.Run(async () =>
            {
                try
                {
                    var result = await _hardwareManager.InitAsync();
                    if (result.Success)
                    {
                        Logs.LogInfo("[HardwareService] 硬件重连成功");
                    }
                    else
                    {
                        Logs.LogError($"[HardwareService] 硬件重连失败：{result.Message}");
                    }
                }
                catch (Exception ex)
                {
                    Logs.LogError(ex, "[HardwareService] 硬件重连过程中发生异常");
                }
            });
        }

        /// <summary>
        /// 读取PLC多个寄存器值（适配欧姆龙PLC的BatchRead方法）
        /// </summary>
        public async Task<Dictionary<int, int>> ReadPlcRegisters(List<int> addresses)
        {
            var result = new Dictionary<int, int>();
            if (addresses == null || addresses.Count == 0)
            {
                Logs.LogWarn("[PLC] 读取寄存器时地址列表为空");
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
                        Logs.LogError("[PLC] 连接异常，重连失败，读取寄存器中止");
                        return result;
                    }
                    Logs.LogInfo("[PLC] 重连成功，继续读取寄存器");
                }

                // 批量读取（默认读取DM寄存器）
                int minAddr = addresses.Min();
                int maxAddr = addresses.Max();
                long range = (long)maxAddr - minAddr + 1;
                if (range > ushort.MaxValue)
                {
                    Logs.LogError($"[PLC] 地址范围过大：{minAddr}-{maxAddr}，超出PLC支持的最大连续读取长度 {ushort.MaxValue}");
                    return result;
                }

                ushort startAddress = (ushort)minAddr;
                ushort count = (ushort)range;
                List<int> plcValues = await Task.Run(() =>
                    _hardwareManager.OmronPLC.BatchRead(PLCRegisterType.DM, startAddress, count));

                if (plcValues == null || plcValues.Count < count)
                {
                    Logs.LogError($"[PLC] 批量读取返回数据不完整，期望{count}个，实际{plcValues?.Count ?? 0}个");
                    return result;
                }

                // 映射到地址-值字典
                for (int i = 0; i < addresses.Count; i++)
                {
                    int address = addresses[i];
                    int index = address - startAddress;
                    if (index >= 0 && index < plcValues.Count)
                    {
                        result[address] = plcValues[index];
                    }
                    else
                    {
                        Logs.LogError($"[PLC] 地址{address}计算索引{index}超出范围");
                    }
                }

                Logs.LogDebug($"[PLC] 读取DM寄存器成功 | 地址：{string.Join(",", addresses)} | 值：{string.Join(",", result.Values)}");
                await Task.Delay(100); // 适当延时，避免过于频繁
            }
            catch (Exception ex)
            {
                Logs.LogError(ex, "[PLC] 读取寄存器失败");
                await Task.Delay(500);
            }

            return result;
        }

        /// <summary>
        /// 写入PLC单个寄存器值（带重试机制）
        /// </summary>
        public async Task WritePlcRegister(int address, int value)
        {
            for (int retry = 0; retry < MaxRetries; retry++)
            {
                try
                {
                    // 校验PLC连接状态
                    if (!_hardwareManager.OmronPLC.Connected)
                    {
                        bool reconnectSuccess = _hardwareManager.OmronPLC.CheckConnection();
                        if (!reconnectSuccess)
                        {
                            throw new Exception("PLC连接异常，重连失败");
                        }
                        Logs.LogInfo("[PLC] 重连成功，继续写入");
                    }

                    bool writeSuccess = await Task.Run(() =>
                        _hardwareManager.OmronPLC.Write(PLCRegisterType.DM, (ushort)address, value));

                    if (!writeSuccess)
                    {
                        throw new Exception($"写入DM{address} = {value} 返回失败");
                    }

                    Logs.LogDebug($"[PLC] 写入DM{address} = {value} 成功");
                    return; // 成功返回
                }
                catch (Exception ex)
                {
                    if (retry == MaxRetries - 1)
                    {
                        Logs.LogError(ex, $"[PLC] 写入DM{address} = {value} 失败，已重试{MaxRetries}次");
                        throw; // 最后一次失败，抛出异常
                    }
                    Logs.LogWarn(ex, $"[PLC] 写入DM{address} = {value} 失败（尝试{retry + 1}/{MaxRetries}），将重试");
                    await Task.Delay(100 * (retry + 1)); // 递增延迟
                }
            }
        }

        /// <summary>
        /// 触发扫码枪扫码（适配康耐视扫码枪的TriggerAsync方法）
        /// </summary>
        public async Task<string> TriggerScanner(ScannerType scannerType)
        {
            string scanCode = string.Empty;
            IScanner targetScanner = null;

            switch (scannerType)
            {
                case ScannerType.治具1下:
                    targetScanner = _hardwareManager.LeftDownScanner;
                    break;
                case ScannerType.治具1上:
                    targetScanner = _hardwareManager.LeftUpScanner;
                    break;
                case ScannerType.机械臂:
                    targetScanner = _hardwareManager.SPScanner;
                    break;
                case ScannerType.治具2上:
                    targetScanner = _hardwareManager.RightUpScanner;
                    break;
                case ScannerType.治具2下:
                    targetScanner = _hardwareManager.RightDownScanner;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(scannerType), "不支持的扫码类型");
            }

            try
            {
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
                    Logs.LogInfo($"[Scanner] {scannerType}扫码枪重连成功");
                }

                bool triggerSuccess = await targetScanner.TriggerAsync();
                if (triggerSuccess)
                {
                    int retry = 0;
                    while (string.IsNullOrEmpty(targetScanner.Content) && retry < 10)
                    {
                        await Task.Delay(50);
                        retry++;
                    }
                    scanCode = targetScanner.Content?.Trim();
                    if (string.IsNullOrEmpty(scanCode))
                    {
                        Logs.LogWarn($"[Scanner] {scannerType}扫码枪触发成功但未返回有效码值");
                    }
                    else
                    {
                        Logs.LogDebug($"[Scanner] {scannerType}扫码成功：{scanCode}");
                    }
                }
                else
                {
                    throw new Exception($"{scannerType}扫码枪触发失败");
                }
            }
            catch (Exception ex)
            {
                Logs.LogError(ex, $"[Scanner] 触发{scannerType}扫码枪失败");
                // 失败时返回空字符串，由上层根据业务逻辑处理
            }

            return scanCode;
        }
        string robotCmd = "";
        /// <summary>
        /// 获取机械臂指令（适配EPSON机械臂的接收数据）
        /// </summary>
        public async Task<string> GetRobotCommand()
        {
            try
            {
                if (!_hardwareManager.EpsonRobot.Connected)
                {
                    bool reconnectSuccess = _hardwareManager.EpsonRobot.CheckConnection();
                    if (!reconnectSuccess)
                    {
                        Logs.LogError("[Robot] EPSON机械臂连接异常，重连失败，无法读取指令");
                        return string.Empty;
                    }
                    Logs.LogInfo("[Robot] EPSON机械臂重连成功");
                }
                robotCmd = "";
                robotCmd = await Task.Run(() => _hardwareManager.EpsonRobot.ReceiveContent?.Trim());
                _hardwareManager.EpsonRobot.ClearReceiveContent();
                if (string.IsNullOrEmpty(robotCmd))
                {
                    Logs.LogDebug("[Robot] 未获取到机械臂指令");
                }
                else
                {
                    Logs.LogDebug($"[Robot] 获取机械臂指令：{robotCmd}");
                }
                return robotCmd ?? string.Empty;
            }
            catch (Exception ex)
            {
                Logs.LogError(ex, "[Robot] 读取EPSON机械臂指令异常");
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
                if (!_hardwareManager.EpsonRobot.Connected)
                {
                    bool reconnectSuccess = _hardwareManager.EpsonRobot.CheckConnection();
                    if (!reconnectSuccess)
                    {
                        throw new Exception("EPSON机械臂连接异常，重连失败");
                    }
                    Logs.LogInfo("[Robot] EPSON机械臂重连成功");
                }

                string responseCmd = success ? "OK" : "NG";
                bool sendSuccess = await Task.Run(() =>
                    _hardwareManager.EpsonRobot.SendCommand(responseCmd));

                if (!sendSuccess)
                {
                    throw new Exception($"发送机械臂响应{responseCmd}失败");
                }

                Logs.LogInfo($"[Robot] 发送机械臂响应：{(success ? "成功(OK)" : "失败(NG)")}");
            }
            catch (Exception ex)
            {
                Logs.LogError(ex, "[Robot] 发送机械臂响应失败");
                throw;
            }
        }

        #endregion

        #region 扩展方法（可选）

        /// <summary>
        /// 批量触发多个扫码枪
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
        public async Task<int> ReadPlcRegister(int address)
        {
            try
            {
                if (!_hardwareManager.OmronPLC.Connected)
                {
                    bool reconnectSuccess = _hardwareManager.OmronPLC.CheckConnection();
                    if (!reconnectSuccess)
                    {
                        Logs.LogError($"[PLC] 读取DM{address}失败：连接异常，重连失败");
                        return -1;
                    }
                    Logs.LogInfo("[PLC] 重连成功，继续读取");
                }

                List<int> values = await Task.Run(() =>
                    _hardwareManager.OmronPLC.BatchRead(PLCRegisterType.DM, (ushort)address, 1));

                if (values != null && values.Count == 1)
                {
                    Logs.LogDebug($"[PLC] 读取DM{address} = {values[0]}");
                    return values[0];
                }
                else
                {
                    Logs.LogError($"[PLC] 读取DM{address}失败：返回数据无效");
                    return -1;
                }
            }
            catch (Exception ex)
            {
                Logs.LogError(ex, $"[PLC] 读取DM{address}异常");
                return -1;
            }
        }

        /// <summary>
        /// 批量写入PLC寄存器
        /// </summary>
        public async Task<bool> BatchWritePlcRegisters(Dictionary<int, int> addressValues)
        {
            if (addressValues == null || addressValues.Count == 0)
            {
                Logs.LogWarn("[PLC] 批量写入：地址-值列表为空");
                return false;
            }

            // 检查地址是否连续
            var sorted = addressValues.Keys.OrderBy(k => k).ToList();
            bool isContinuous = true;
            for (int i = 1; i < sorted.Count; i++)
            {
                if (sorted[i] != sorted[i - 1] + 1)
                {
                    isContinuous = false;
                    break;
                }
            }

            try
            {
                if (isContinuous && addressValues.Count > 1)
                {
                    ushort startAddr = (ushort)sorted.Min();
                    var values = new List<int>();
                    for (int i = 0; i < addressValues.Count; i++)
                    {
                        int addr = startAddr + i;
                        values.Add(addressValues[addr]); // 按顺序取值
                    }
                    bool success = await Task.Run(() =>
                        _hardwareManager.OmronPLC.BatchWrite(PLCRegisterType.DM, startAddr, values));
                    if (success)
                    {
                        Logs.LogInfo($"[PLC] 批量写入DM寄存器成功 | 地址范围：{startAddr}-{startAddr + values.Count - 1}");
                        return true;
                    }
                    else
                    {
                        Logs.LogWarn("[PLC] 批量写入失败，降级为逐个写入");
                        // 降级到逐个写入
                    }
                }

                // 不连续或批量失败时逐个写入
                bool allSuccess = true;
                foreach (var kv in addressValues)
                {
                    try
                    {
                        await WritePlcRegister(kv.Key, kv.Value);
                    }
                    catch (Exception ex)
                    {
                        Logs.LogError(ex, $"[PLC] 逐个写入DM{kv.Key} = {kv.Value} 失败");
                        allSuccess = false;
                    }
                }
                return allSuccess;
            }
            catch (Exception ex)
            {
                Logs.LogError(ex, "[PLC] 批量写入寄存器失败");
                return false;
            }
        }

        /// <summary>
        /// 停止硬件（释放资源）
        /// </summary>
        public void Stop()
        {
            Logs.LogInfo("[HardwareService] 开始停止硬件...");
            Task.Run(async () =>
            {
                try
                {
                    var result = await _hardwareManager.Stop();
                    if (result.Success)
                    {
                        Logs.LogInfo("[HardwareService] 硬件停止成功");
                    }
                    else
                    {
                        Logs.LogError($"[HardwareService] 硬件停止失败：{result.Message}");
                    }
                }
                catch (Exception ex)
                {
                    Logs.LogError(ex, "[HardwareService] 硬件停止过程中发生异常");
                }
            });
        }

        #endregion
    }
}