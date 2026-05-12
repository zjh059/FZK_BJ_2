using FZK.Application.Share.Config;
using FZK.Application.Share.Init;
using FZK.Application.Share.Language;
using FZK.Application.Share.Run;
using FZK.Application.Share.Models;
using FZK.Hardware.Scanner.Base;
using FZK.Logger;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static FZK.Application.Run.ViewModels.RunViewModel;

namespace FZK.Application.Run.Service
{
    /// <summary>
    /// 单个治具的流程引擎（扫码比对、焊接结果处理、清零）
    /// 修复版：解决无顶部扫码枪时数据库比对误判问题
    /// </summary>
    public class JigFlowEngine : IJigFlowEngine
    {
        private readonly JigConfig _config;
        private readonly IPlcService _plc;
        private readonly IHardwareService _hardware;
        private readonly IDatabaseService _db;
        private readonly IMesService _mes;
        private readonly RunConfig _runConfig;
        private readonly ScannerConfig _bottomScannerConfig;
        private readonly bool _isNoHardwareMode;
        private readonly bool _isSfcEnabled;
        private readonly bool _isDebug;
        private readonly Action<ScanRecord> _onRecordAdded;
        private readonly Action<string> _onLogged;
        // 底板码前缀（建议后续移至JigConfig配置化）
        private const string BottomCodePrefix = "H-";
        // 用于记录当前底板码（清零时可能用到）
        private string _currentBottomCode;
        private const int DebugNoNgFlag = 1;
        public JigFlowEngine(
            JigConfig config,
            IPlcService plc,
            IHardwareService hardware,
            IDatabaseService db,
            IMesService mes,
            RunConfig runConfig,
            ScannerConfig bottomScannerConfig,
            bool isNoHardwareMode,
            bool isSfcEnabled,
            bool isDebug,
            Action<ScanRecord> onRecordAdded,
            Action<string> onLogAdded)
        {
            _config = config;
            _plc = plc;
            _hardware = hardware;
            _db = db;
            _mes = mes;
            _runConfig = runConfig;
            _bottomScannerConfig = bottomScannerConfig;
            _isNoHardwareMode = isNoHardwareMode;
            _isSfcEnabled = isSfcEnabled;
            _isDebug = isDebug;
            //调试模式下,所有结果为OK
            if (_isDebug)
                _config.NGFlag = DebugNoNgFlag;

            _onRecordAdded = onRecordAdded;
            _onLogged = onLogAdded;
        }

        public async Task ProcessScanAsync()
        {
            try
            {
                Logs.LogInfo($"{_config.JigName} 开始扫码比对流程");
                _onLogged?.Invoke($"{_config.JigName} 开始扫码比对流程");

                string bottomCode = "", spCode = "", topCode = "";
                bool success = false;
                int retry = 0;

                while (retry < _runConfig.ScanRetryCount && !success)
                {
                    if (retry > 0)
                        await Task.Delay(_runConfig.ScanRetryDelay);

                    List<string> bottomCodes = new List<string>();
                    ScanValidationResult topResult = new ScanValidationResult();

                    if (_isNoHardwareMode)
                    {
                        // 模拟：直接返回多码列表，无需字符串拆分
                        bottomCodes = new List<string>
                        {
                            $"{BottomCodePrefix}B{DateTime.Now:yyyyMMddHHmmss}",
                            $"SP{DateTime.Now:yyyyMMddHHmmss}"
                        };

                        // 无硬件模式适配：未配置顶部扫码时返回空且有效
                        topResult = _config.TopScanner.HasValue
                            ? new ScanValidationResult
                            {
                                IsValid = true,
                                Codes = new List<string> { $"T{DateTime.Now:yyyyMMddHHmmss}" }
                            }
                            : new ScanValidationResult
                            {
                                IsValid = true,
                                Codes = new List<string> { "" }
                            };
                    }
                    else
                    {
                        // 并行触发底部多码扫码和顶部标准化扫码
                        var bottomTask = _hardware.TriggerScannerMultiCodesAsync(_config.BottomScanner);
                        var topTask = _config.TopScanner.HasValue
                            ? _hardware.TriggerScannerAndValidateAsync(
                                _config.TopScanner.Value,
                                expectedLength: 0, // 如需顶部码长度校验，改为配置值
                                enableDebug: _isDebug)
                            : Task.FromResult(new ScanValidationResult { IsValid = true, Codes = new List<string> { "" } });

                        await Task.WhenAll(bottomTask, topTask);
                        bottomCodes = await bottomTask;
                        topResult = await topTask;
                    }

                    // 从多码列表中识别底板码和主板码
                    if (TryIdentifyCodes(bottomCodes, BottomCodePrefix, out bottomCode, out spCode))
                    {
                        topCode = topResult.Codes.FirstOrDefault() ?? "";
                        // 未配置顶部扫码时，跳过topCode非空检查
                        success = !string.IsNullOrEmpty(bottomCode)
                                  && !string.IsNullOrEmpty(spCode)
                                  && topResult.IsValid
                                  && (_config.TopScanner.HasValue ? !string.IsNullOrEmpty(topCode) : true);
                    }
                    retry++;
                }

                await _plc.WriteRegisterAsync(_config.ScanFinishAddr, _config.FinishFlag);
                _onLogged?.Invoke($"{_config.JigName} 扫码完成: {bottomCode} / {topCode}");

                if (success)
                {
                    // ✅ 修复：未配置顶部扫码枪时，直接跳过数据库比对
                    bool verifyOk = true;
                    if (_config.TopScanner.HasValue)
                    {
                        verifyOk = await _db.VerifyBottomTopCodeAsync(bottomCode, topCode);
                    }
                    else
                    {
                        Logs.LogInfo($"{_config.JigName} 未配置顶部扫码枪，跳过比对流程");
                        _onLogged?.Invoke($"{_config.JigName} 未配置顶部扫码枪，跳过比对流程");
                    }

                    await _plc.WriteRegisterAsync(_config.ScanResultAddr, verifyOk ? _config.OKFlag : _config.NGFlag);

                    if (verifyOk)
                    {
                        // ✅ 修复：未配置顶部扫码枪时，传入空topCode更新数据库
                        // 数据库方法应支持空topCode的情况，若不支持可改为传入特殊标记如"NO_TOP_CODE"
                        await _db.UpdateOrAddCodeEntityAsync(bottomCode, topCode, spCode);
                        Logs.LogInfo($"{_config.JigName} 流程成功");
                        _onLogged?.Invoke($"{_config.JigName} 流程成功");
                    }
                    else
                    {
                        Logs.LogWarn($"{_config.JigName} 比对失败: {bottomCode} != {topCode}");
                        _onLogged?.Invoke($"{_config.JigName} 比对失败");
                    }

                    _onRecordAdded?.Invoke(new ScanRecord
                    {
                        CreateTime = DateTime.Now,
                        JigNo = _config.JigName,
                        ScanType = MultiLang.ScanTypeBottomTop,
                        BottomCode = bottomCode,
                        TopCode = topCode,
                        SPCode = spCode,
                        Result = verifyOk ? "1" : "2",
                        Remark = _config.TopScanner.HasValue
                            ? (verifyOk ? "比对成功" : "比对失败")
                            : "无顶部扫码，跳过比对"
                    });
                }
                else
                {
                    await _plc.WriteRegisterAsync(_config.ScanResultAddr, _config.NGFlag);
                    Logs.LogWarn($"{_config.JigName} 扫码重试耗尽");
                    _onLogged?.Invoke($"{_config.JigName} 扫码重试耗尽");

                    _onRecordAdded?.Invoke(new ScanRecord
                    {
                        CreateTime = DateTime.Now,
                        JigNo = _config.JigName,
                        ScanType = "底部+顶部扫码",
                        BottomCode = bottomCode,
                        TopCode = topCode,
                        SPCode = spCode,
                        Result = "2",
                        Remark = "扫码重试耗尽"
                    });
                }
            }
            catch (Exception ex)
            {
                Logs.LogError(ex, $"{_config.JigName} 扫码流程全局异常");
                _onLogged?.Invoke($"{_config.JigName} 扫码流程异常：{ex.Message}");

                // 异常时必须先通知PLC流程结束，避免机械手无限等待
                try
                {
                    await _plc.WriteRegisterAsync(_config.ScanFinishAddr, _config.FinishFlag);
                    await _plc.WriteRegisterAsync(_config.ScanResultAddr, _config.NGFlag);
                }
                catch (Exception plcEx)
                {
                    Logs.LogError(plcEx, $"{_config.JigName} 扫码异常时写入PLC失败");
                    _onLogged?.Invoke($"{_config.JigName} 异常时PLC通信失败：{plcEx.Message}");
                }

                _onRecordAdded?.Invoke(new ScanRecord
                {
                    CreateTime = DateTime.Now,
                    JigNo = _config.JigName,
                    ScanType = "底部+顶部扫码",
                    Result = "2",
                    Remark = $"系统异常：{ex.Message}"
                });
            }
        }

        public async Task ProcessWeldAsync()
        {
            try
            {
                Logs.LogInfo($"{_config.JigName} 开始焊接结果处理");
                _onLogged?.Invoke($"{_config.JigName} 开始焊接结果处理");

                string bottomCode = "", spCode = "";
                bool success = false;
                int retry = 0;

                while (retry < _runConfig.ScanRetryCount && !success)
                {
                    if (retry > 0)
                        await Task.Delay(_runConfig.ScanRetryDelay);

                    List<string> bottomCodes = new List<string>();

                    if (_isNoHardwareMode)
                    {
                        bottomCodes = new List<string>
                        {
                            $"{BottomCodePrefix}B{DateTime.Now:yyyyMMddHHmmss}",
                            $"SP{DateTime.Now:yyyyMMddHHmmss}"
                        };
                    }
                    else
                    {
                        bottomCodes = await _hardware.TriggerScannerMultiCodesAsync(_config.BottomScanner);
                    }

                    success = TryIdentifyCodes(bottomCodes, BottomCodePrefix, out bottomCode, out spCode);
                    retry++;
                }

                await _plc.WriteRegisterAsync(_config.WeldFinishAddr, _config.FinishFlag);

                if (success)
                {
                    bool mesOk = !_isSfcEnabled || await _mes.GetMesTestResult(spCode);
                    int weldResult = mesOk ? 1 : 2;
                    await _db.UpdateTestResultAsync(spCode, weldResult);
                    string newCount = await _db.IncrementCountAsync(bottomCode);

                    if (int.TryParse(newCount, out int countVal))
                        await _plc.WriteRegisterAsync(_config.CountsAddr, countVal);

                    await _plc.WriteRegisterAsync(_config.WeldResultAddr, weldResult == 1 ? _config.OKFlag : _config.NGFlag);
                    _currentBottomCode = bottomCode;

                    Logs.LogInfo($"{_config.JigName} 焊接完成，底板码 {bottomCode}，MES={(mesOk ? "OK" : "NG")}，使用次数={newCount}");
                    _onLogged?.Invoke($"{_config.JigName} 焊接完成，底板码 {bottomCode}，MES={(mesOk ? "OK" : "NG")}，使用次数={newCount}");

                    _onRecordAdded?.Invoke(new ScanRecord
                    {
                        CreateTime = DateTime.Now,
                        JigNo = _config.JigName,
                        ScanType = "焊接结果扫码",
                        BottomCode = bottomCode,
                        SPCode = spCode,
                        Result = weldResult.ToString(),
                        Remark = mesOk ? "MES OK" : "MES NG"
                    });
                }
                else
                {
                    await _plc.WriteRegisterAsync(_config.WeldResultAddr, _config.NGFlag);
                    Logs.LogWarn($"{_config.JigName} 焊接扫码重试耗尽");
                    _onLogged?.Invoke($"{_config.JigName} 焊接扫码重试耗尽");

                    _onRecordAdded?.Invoke(new ScanRecord
                    {
                        CreateTime = DateTime.Now,
                        JigNo = _config.JigName,
                        ScanType = "焊接结果扫码",
                        BottomCode = bottomCode,
                        SPCode = spCode,
                        Result = "2",
                        Remark = "扫码重试耗尽"
                    });
                }
            }
            catch (Exception ex)
            {
                Logs.LogError(ex, $"{_config.JigName} 焊接流程全局异常");
                _onLogged?.Invoke($"{_config.JigName} 焊接流程异常：{ex.Message}");

                try
                {
                    await _plc.WriteRegisterAsync(_config.WeldFinishAddr, _config.FinishFlag);
                    await _plc.WriteRegisterAsync(_config.WeldResultAddr, _config.NGFlag);
                }
                catch (Exception plcEx)
                {
                    Logs.LogError(plcEx, $"{_config.JigName} 焊接异常时写入PLC失败");
                    _onLogged?.Invoke($"{_config.JigName} 异常时PLC通信失败：{plcEx.Message}");
                }

                _onRecordAdded?.Invoke(new ScanRecord
                {
                    CreateTime = DateTime.Now,
                    JigNo = _config.JigName,
                    ScanType = "焊接结果扫码",
                    Result = "2",
                    Remark = $"系统异常：{ex.Message}"
                });
            }
        }

        public async Task ProcessClearAsync()
        {
            try
            {
                Logs.LogInfo($"{_config.JigName} 开始清零流程");
                _onLogged?.Invoke($"{_config.JigName} 开始清零流程");

                string bottomCode = "";
                bool success = false;
                int retry = 0;

                while (retry < _runConfig.ScanRetryCount && !success)
                {
                    if (retry > 0)
                        await Task.Delay(_runConfig.ScanRetryDelay);

                    if (_isNoHardwareMode)
                    {
                        bottomCode = $"{BottomCodePrefix}B{DateTime.Now:yyyyMMddHHmmss}";
                        success = true;
                    }
                    else
                    {
                        // 清零只需底板码，使用多码接口确保准确识别
                        var codes = await _hardware.TriggerScannerMultiCodesAsync(_config.BottomScanner);
                        bottomCode = codes.FirstOrDefault(c =>
                            c.Trim().StartsWith(BottomCodePrefix, StringComparison.OrdinalIgnoreCase))?.Trim();
                        success = !string.IsNullOrEmpty(bottomCode);
                    }
                    retry++;
                }

                if (success)
                {
                    await _db.ClearCountAsync(bottomCode);
                    await _plc.WriteRegisterAsync(_config.CountsAddr, 0);
                    Logs.LogInfo($"{_config.JigName} 清零成功，底板码 {bottomCode}");
                    _onLogged?.Invoke($"{_config.JigName} 清零成功，底板码 {bottomCode}");

                    _onRecordAdded?.Invoke(new ScanRecord
                    {
                        CreateTime = DateTime.Now,
                        JigNo = _config.JigName,
                        ScanType = "清零",
                        BottomCode = bottomCode,
                        Result = "1",
                        Remark = "清零成功"
                    });
                }
                else
                {
                    Logs.LogWarn($"{_config.JigName} 清零扫码失败");
                    _onLogged?.Invoke($"{_config.JigName} 清零扫码失败");

                    _onRecordAdded?.Invoke(new ScanRecord
                    {
                        CreateTime = DateTime.Now,
                        JigNo = _config.JigName,
                        ScanType = "清零",
                        BottomCode = bottomCode,
                        Result = "2",
                        Remark = "清零扫码失败"
                    });
                }
            }
            catch (Exception ex)
            {
                Logs.LogError(ex, $"{_config.JigName} 清零流程全局异常");
                _onLogged?.Invoke($"{_config.JigName} 清零流程异常：{ex.Message}");

                // 清零流程异常时无需写入结果地址，但必须记录
                _onRecordAdded?.Invoke(new ScanRecord
                {
                    CreateTime = DateTime.Now,
                    JigNo = _config.JigName,
                    ScanType = "清零",
                    Result = "2",
                    Remark = $"系统异常：{ex.Message}"
                });
            }
        }

        /// <summary>
        /// 从硬件驱动返回的多码列表中识别底板码和主板码
        /// 逻辑：前缀匹配底板码，第一个非底板码为主板码
        /// </summary>
        /// <param name="codes">硬件驱动已解析的多码列表</param>
        /// <param name="bottomPrefix">底板码前缀</param>
        /// <param name="bottomCode">输出底板码</param>
        /// <param name="spCode">输出主板码</param>
        /// <returns>是否同时识别到底板码和主板码</returns>
        private bool TryIdentifyCodes(List<string> codes, string bottomPrefix, out string bottomCode, out string spCode)
        {
            bottomCode = null;
            spCode = null;

            if (codes == null || codes.Count == 0)
                return false;

            foreach (var code in codes)
            {
                var trimmed = code.Trim();
                if (string.IsNullOrEmpty(trimmed))
                    continue;

                if (trimmed.StartsWith(bottomPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    bottomCode = trimmed;
                }
                else if (string.IsNullOrEmpty(spCode)) // 只取第一个非底板码作为主板码
                {
                    spCode = trimmed;
                }
            }

            return !string.IsNullOrEmpty(bottomCode) && !string.IsNullOrEmpty(spCode);
        }
    }
}