using FZK.Application.Share.Init;
using FZK.Application.Share.Models;
using FZK.Application.Share.Run;
using FZK.Hardware.PLC.Base;
using FZK.Hardware.Robot.Base;
using FZK.Hardware.Scanner.Base;
using FZK.Logger;
using Prism.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace FZK.Application.Run.Service
{
    /// <summary>
    /// 硬件服务实现类（适配真实硬件：欧姆龙PLC+康耐视扫码枪+EPSON机械臂）
    /// 核心：复用HardwareManager已初始化的硬件实例，不重复Init
    /// 兼容：.NET Framework 4.7.2 + C# 7.3
    /// </summary>
    public class HardwareService : IHardwareService
    {
        private readonly IHardwareManager _hardwareManager;
        private const int MaxRetries = 3; // 写入PLC最大重试次数        
        private const int ScannerTimeoutMs = 1000; // 扫码超时时间（毫秒）
        private readonly IEventAggregator _eventAggregator;
       
        public HardwareService(
            IHardwareManager hardwareManager,
            IEventAggregator eventAggregator
            )
        {
           
            _hardwareManager = hardwareManager ?? throw new ArgumentNullException(nameof(hardwareManager));
            _eventAggregator = eventAggregator;
            Logs.LogInfo("[HardwareService] 服务初始化完成");
        }

        #region IHardwareService接口实现

        /// <summary>
        /// 初始化（触发硬件重连，避免阻塞调用线程）
        /// </summary>
        public void Init()
        {
            Task.Run(async () =>
            {
                try
                {
                    var result = await _hardwareManager.InitAsync();
                    if (result.Success)
                    {
                        Logs.LogInfo("[HardwareService] 硬件重连成功");
                        _eventAggregator.GetEvent<UILogEvent>().Publish("硬件重连成功");
                    }
                    else
                    {
                        Logs.LogError($"[HardwareService] 硬件重连失败：{result.Message}");
                        _eventAggregator.GetEvent<UILogEvent>().Publish($"硬件重连失败：{result.Message}");
                    }
                }
                catch (Exception ex)
                {
                    Logs.LogError(ex, "[HardwareService] 硬件重连过程中发生异常");
                    _eventAggregator.GetEvent<UILogEvent>().Publish($"硬件重连异常：{ex.Message}");
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

            if (_hardwareManager.OmronPLC == null)
            {
                Logs.LogError("[PLC] 欧姆龙PLC实例未初始化");
                return result;
            }

            try
            {
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
                Logs.LogInfo($"[PLC] 读取DM寄存器成功 | 地址：{string.Join(",", addresses)} | 值：{string.Join(",", result.Values)}");
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
            if (_hardwareManager.OmronPLC == null)
            {
                Logs.LogError("[PLC] 欧姆龙PLC实例未初始化");
                throw new InvalidOperationException("欧姆龙PLC实例未初始化");
            }

            for (int retry = 0; retry < MaxRetries; retry++)
            {
                try
                {
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
                    _eventAggregator.GetEvent<UILogEvent>().Publish($"[PLC] 写入DM{address} = {value} 成功");
                    Logs.LogDebug($"[PLC] 写入DM{address} = {value} 成功");
                    return;
                }
                catch (Exception ex)
                {
                    if (retry == MaxRetries - 1)
                    {
                        Logs.LogError(ex, $"[PLC] 写入DM{address} = {value} 失败，已重试{MaxRetries}次");
                        throw;
                    }
                    Logs.LogWarn(ex, $"[PLC] 写入DM{address} = {value} 失败（尝试{retry + 1}/{MaxRetries}），将重试");
                    await Task.Delay(100 * (retry + 1));
                }
            }
        }

        /// <summary>
        /// 触发扫码枪扫码（返回单码，向后兼容）
        /// </summary>
        public async Task<string> TriggerScanner(ScannerType scannerType)
        {
            var codes = await TriggerScannerMultiCodesAsync(scannerType);
            return codes.FirstOrDefault() ?? string.Empty;
        }

        /// <summary>
        /// 触发扫码枪扫码（返回多码列表，基于Observable主动推送）
        /// </summary>
        public async Task<List<string>> TriggerScannerMultiCodesAsync(ScannerType scannerType)
        {
            try
            {
                IScanner targetScanner = GetScanner(scannerType);
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

                // 触发前清空旧码
                targetScanner.ClearContent();

                // 创建任务源，用于接收第一个有效多码列表
                var tcs = new TaskCompletionSource<List<string>>();

                // 订阅MultiCodesObservable，只取第一个非空列表
                var subscription = targetScanner.MultiCodesObservable
                    .Where(codes => codes != null && codes.Count > 0)
                    .Take(1)
                    .Subscribe(
                        codes => tcs.TrySetResult(codes),
                        ex => tcs.TrySetException(ex)
                    );

                try
                {
                    bool triggerSuccess = await targetScanner.TriggerAsync();
                    if (!triggerSuccess)
                    {
                        throw new Exception($"{scannerType}扫码枪触发失败");
                    }

                    // 等待结果，超时500ms
                    var timeoutTask = Task.Delay(ScannerTimeoutMs);
                    var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

                    if (completedTask == tcs.Task)
                    {
                        var codes = await tcs.Task;
                        _eventAggregator.GetEvent<UILogEvent>().Publish(
                            $"[Scanner] {scannerType}扫码成功：{string.Join(" | ", codes)}");

                        Logs.LogDebug($"[Scanner] {scannerType}扫码成功：{string.Join(" | ", codes)}");
                        return codes;
                    }
                    else
                    {
                        Logs.LogWarn($"[Scanner] {scannerType}扫码超时（{ScannerTimeoutMs}ms）或未检测到条码");
                        _eventAggregator.GetEvent<UILogEvent>().Publish(
                            $"[Scanner] {scannerType}扫码失败：超时或未检测到条码");
                        return new List<string>();
                    }
                }
                finally
                {
                    // 确保订阅被释放，防止内存泄漏
                    subscription.Dispose();
                }
            }
            catch (Exception ex)
            {
                Logs.LogError(ex, $"[Scanner] 触发{scannerType}扫码枪失败");
                _eventAggregator.GetEvent<UILogEvent>().Publish(
                    $"[Scanner] {scannerType}扫码异常：{ex.Message}");
                return new List<string>();
            }
        }

        /// <summary>
        /// 获取机械臂指令（适配EPSON机械臂的接收数据）
        /// </summary>
        public async Task<string> GetRobotCommand()
        {
            try
            {
                if (_hardwareManager.EpsonRobot == null)
                {
                    Logs.LogError("[Robot] EPSON机械臂实例未初始化");
                    return string.Empty;
                }

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

                string robotCmd = _hardwareManager.EpsonRobot.ReceiveContent?.Trim();
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
                if (_hardwareManager.EpsonRobot == null)
                {
                    Logs.LogError("[Robot] EPSON机械臂实例未初始化");
                    throw new InvalidOperationException("EPSON机械臂实例未初始化");
                }

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
                bool sendSuccess = _hardwareManager.EpsonRobot.SendCommand(responseCmd);

                if (!sendSuccess)
                {
                    throw new Exception($"发送机械臂响应{responseCmd}失败");
                }
                _eventAggregator.GetEvent<UILogEvent>().Publish(
                    $"[Robot] 发送机械臂响应：{(success ? "成功(OK)" : "失败(NG)")}");
                Logs.LogInfo($"[Robot] 发送机械臂响应：{(success ? "成功(OK)" : "失败(NG)")}");
            }
            catch (Exception ex)
            {
                Logs.LogError(ex, "[Robot] 发送机械臂响应失败");
                _eventAggregator.GetEvent<UILogEvent>().Publish(
                    $"[Robot] 发送响应失败：{ex.Message}");
                throw;
            }
        }

        #endregion



        #region 产品码处理流程

        /// <summary>
        /// 触发扫码并根据配置进行长度校验、MES 校验，返回最终结果
        /// </summary>
        /// <param name="scannerType">扫码枪类型</param>
        /// <param name="expectedLength">期望的条码长度（0 表示不校验）</param>
        /// <param name="enableDebug">调试模式（跳过 MES 校验）</param>          
        /// <returns>true：校验通过；false：失败</returns>
        public async Task<ScanValidationResult> TriggerScannerAndValidateAsync(
          ScannerType scannerType,
          int expectedLength,
          bool enableDebug)
        {
            var result = new ScanValidationResult();

            // 1. 触发扫码（获得条码列表）
            var codes = await TriggerScannerMultiCodesAsync(scannerType);
            result.Codes = codes ?? new List<string>();

            if (codes == null || codes.Count == 0)
            {
                Logs.LogWarn($"[Scanner] {scannerType} 未读到条码");
                result.IsValid = false;
                return result;
            }

            // 2. 取第一个条码
            string scanCode = codes[0]?.Trim();
            if (string.IsNullOrEmpty(scanCode))
            {
                Logs.LogWarn($"[Scanner] {scannerType} 条码为空");
                result.IsValid = false;
                return result;
            }

            // 3. 调试模式通过
            if (enableDebug)
            {
                Logs.LogDebug($"[Scanner] {scannerType} 调试模式，跳过后续校验，条码：{scanCode}");
                result.IsValid = true;
                return result;
            }

            // 4. 长度校验（先于调试，保证物理码正确）
            if (expectedLength > 0 && scanCode.Length != expectedLength)
            {
                Logs.LogWarn($"[Scanner] {scannerType} 条码长度不符（期望{expectedLength}，实际{scanCode.Length}）");
                result.IsValid = false;
                return result;
            }



            // 没有进一步校验，默认通过
            result.IsValid = true;
            return result;
        }
        #endregion



        #region 私有辅助方法

        /// <summary>
        /// 根据扫码枪类型获取对应的硬件实例（兼容C# 7.3）
        /// </summary>
        private IScanner GetScanner(ScannerType scannerType)
        {
            switch (scannerType)
            {
                case ScannerType.治具1下:
                    return _hardwareManager.LeftDownScanner;
                case ScannerType.治具1上:
                    return _hardwareManager.LeftUpScanner;
                case ScannerType.机械臂:
                    return _hardwareManager.SPScanner;
                case ScannerType.治具2上:
                    return _hardwareManager.RightUpScanner;
                case ScannerType.治具2下:
                    return _hardwareManager.RightDownScanner;
                default:
                    throw new ArgumentOutOfRangeException(nameof(scannerType), scannerType, "不支持的扫码类型");
            }
        }

        #endregion

        #region 扩展方法（可选）

        /// <summary>
        /// 批量触发多个扫码枪（返回多码列表）
        /// </summary>
        public async Task<Dictionary<ScannerType, List<string>>> BatchTriggerScannersMultiCodesAsync(
            List<ScannerType> scannerTypes)
        {
            var result = new Dictionary<ScannerType, List<string>>();
            foreach (var type in scannerTypes)
            {
                var codes = await TriggerScannerMultiCodesAsync(type);
                result[type] = codes;
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
                if (_hardwareManager.OmronPLC == null)
                {
                    Logs.LogError($"[PLC] 读取DM{address}失败：欧姆龙PLC实例未初始化");
                    return -1;
                }

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

            if (_hardwareManager.OmronPLC == null)
            {
                Logs.LogError("[PLC] 欧姆龙PLC实例未初始化");
                return false;
            }

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
                        values.Add(addressValues[addr]);
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
                    }
                }

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
                        _eventAggregator.GetEvent<UILogEvent>().Publish("硬件已停止");
                    }
                    else
                    {
                        Logs.LogError($"[HardwareService] 硬件停止失败：{result.Message}");
                        _eventAggregator.GetEvent<UILogEvent>().Publish($"硬件停止失败：{result.Message}");
                    }
                }
                catch (Exception ex)
                {
                    Logs.LogError(ex, "[HardwareService] 硬件停止过程中发生异常");
                    _eventAggregator.GetEvent<UILogEvent>().Publish($"硬件停止异常：{ex.Message}");
                }
            });
        }

        #endregion
    }
}