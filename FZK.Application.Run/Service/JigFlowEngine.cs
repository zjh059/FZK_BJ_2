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
using FZK.Application.Run.Models;
using Newtonsoft.Json.Linq;
using System.Net;
using System.Diagnostics;

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
        private readonly ScannerConfig _topScannerConfig;
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
            ScannerConfig topScannerConfig,
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
            _topScannerConfig = topScannerConfig;
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

                    var bottomTask = _hardware.TriggerScannerMultiCodesAsync(_config.BottomScanner);
                    var topTask = _config.TopScanner.HasValue
                        ? _hardware.TriggerScannerAndValidateAsync(
                            _config.TopScanner.Value,
                            expectedLength: 0,
                            enableDebug: _isDebug)
                        : Task.FromResult(new ScanValidationResult { IsValid = true, Codes = new List<string> { "" } });

                    await Task.WhenAll(bottomTask, topTask);
                    bottomCodes = await bottomTask;
                    topResult = await topTask;

                    if (TryIdentifyCodes(bottomCodes, BottomCodePrefix, out bottomCode, out spCode))
                    {
                        topCode = topResult.Codes.FirstOrDefault() ?? "";

                        success = !string.IsNullOrEmpty(bottomCode)
                                  && topResult.IsValid
                                  && (_config.TopScanner.HasValue ? !string.IsNullOrEmpty(topCode) : true);
                    }
                    retry++;
                }

                await _plc.WriteRegisterAsync(_config.ScanFinishAddr, _config.FinishFlag);
                _onLogged?.Invoke($"{_config.JigName} 扫码完成: {bottomCode} / {topCode}");

                if (success)
                {
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
                        //await _db.UpdateOrAddCodeEntityAsync(bottomCode, topCode, spCode);
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

        public async Task<int> ProcessWeldAsync()
        {
            try
            {
                Logs.LogInfo($"{_config.JigName} 开始焊接结果处理，时间: {DateTime.Now:HH:mm:ss.fff}");
                _onLogged?.Invoke($"{_config.JigName} 开始焊接结果处理");

                string bottomCode = "", spCode = "";
                bool success = false;
                int retry = 0;

                while (retry < _runConfig.ScanRetryCount && !success)
                {
                    if (retry > 0)
                    {
                        Logs.LogInfo($"{_config.JigName} 焊接扫码第{retry + 1}次前等待 {_runConfig.ScanRetryDelay}ms");
                        await Task.Delay(_runConfig.ScanRetryDelay);
                    }
                    int attempt = retry + 1;
                    var bottomCodes = await _hardware.TriggerScannerMultiCodesAsync(_config.BottomScanner);
                    success = TryIdentifyCodes(bottomCodes, BottomCodePrefix, out bottomCode, out spCode, true);
                    Logs.LogInfo($"{_config.JigName} 焊接扫码第{attempt}次，识别结果: {(success ? "OK" : "NG")}，码数量: {bottomCodes?.Count ?? 0}");
                    retry++;
                }

                // 先写焊接完成信号，告诉 PLC：上位机焊接结果处理到了完成阶段。
                await WritePlcWithTraceAsync(_config.WeldFinishAddr, _config.FinishFlag, "焊接完成信号");

                if (success)
                {
                    var mesWatch = Stopwatch.StartNew();
                    bool mesOk = !_isSfcEnabled || await _mes.GetMesTestResult(spCode);
                  

                    int weldResult = mesOk ? 1 : 2;


                    await _db.UpdateTestResultAsync(spCode, weldResult);
                    string newCount = await _db.IncrementCountAsync(bottomCode);

                    int countVal = 0;
                    if (int.TryParse(newCount, out countVal))
                        await WritePlcWithTraceAsync(_config.CountsAddr, countVal, "焊接使用次数");

                    await WritePlcWithTraceAsync(_config.WeldResultAddr, weldResult == 1 ? _config.OKFlag : _config.NGFlag, "焊接结果");
                    _currentBottomCode = bottomCode;
                    mesWatch.Stop();
                    Logs.LogInfo($"{_config.JigName} MES校验结束，耗时: {mesWatch.ElapsedMilliseconds}ms，结果: {(mesOk ? "OK" : "NG")}，SP码: {spCode}");
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
                    return countVal;
                }
                else
                {                   
                    await WritePlcWithTraceAsync(_config.WeldResultAddr, _config.NGFlag, "焊接扫码重试耗尽-结果NG");
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
                    return 0;
                }
            }
            catch (Exception ex)
            {
                _onLogged?.Invoke($"{_config.JigName} 焊接流程异常：{ex.Message}");

                try
                {
                    await WritePlcWithTraceAsync(_config.WeldFinishAddr, _config.FinishFlag, "焊接异常-完成信号");
                    await WritePlcWithTraceAsync(_config.WeldResultAddr, _config.NGFlag, "焊接异常-结果NG");
                }
                catch (Exception plcEx)
                {
                    Logs.LogError(plcEx, $"{_config.JigName} 焊接异常时写入PLC失败");
                    _onLogged?.Invoke($"{_config.JigName} 异常时PLC通信失败：{plcEx.Message}");
                    return 0;
                }

                _onRecordAdded?.Invoke(new ScanRecord
                {
                    CreateTime = DateTime.Now,
                    JigNo = _config.JigName,
                    ScanType = "焊接结果扫码",
                    Result = "2",
                    Remark = $"系统异常：{ex.Message}"
                });

                return 0;
            }
        }

        public async Task ProcessClearAsync()
        {
            try
            {
                Logs.LogInfo($"{_config.JigName} 开始清零流程");
                _onLogged?.Invoke($"{_config.JigName} 开始清零流程");

                string bottomCode = "",spCode="";
                bool success = false;
                int retry = 0;

                while (retry < _runConfig.ScanRetryCount && !success)
                {
                    if (retry > 0)
                        await Task.Delay(_runConfig.ScanRetryDelay);                  
                    var codes = await _hardware.TriggerScannerMultiCodesAsync(_config.BottomScanner);
                    success = TryIdentifyCodes(codes, BottomCodePrefix, out bottomCode, out spCode,false); 
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
                    await _plc.WriteRegisterAsync(_config.WeldFinishAddr, _config.FinishFlag);
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



        //(6)
        private async Task WritePlcWithTraceAsync(int address, int value, string action)
        {
            // 这个方法只是统一给“业务层写 PLC”加日志。
            // 真正和 PLC 通信的地方仍然是 _plc.WriteRegisterAsync -> HardwareService.WritePlcRegister。
            var writeWatch = Stopwatch.StartNew();
            Logs.LogInfo($"{_config.JigName} [PLC] {action} 开始写入 DM{address} = {value}，时间: {DateTime.Now:HH:mm:ss.fff}");

            try
            {
                await _plc.WriteRegisterAsync(address, value);
                writeWatch.Stop();
                Logs.LogInfo($"{_config.JigName} [PLC] {action} 写入完成 DM{address} = {value}，耗时: {writeWatch.ElapsedMilliseconds}ms");
            }
            catch (Exception ex)
            {
                writeWatch.Stop();
                Logs.LogWarn(ex, $"{_config.JigName} [PLC] {action} 写入失败 DM{address} = {value}，耗时: {writeWatch.ElapsedMilliseconds}ms");
                throw;
            }
        }



        /// <summary>
        /// 从硬件驱动返回的多码列表中识别底板码和主板码
        /// 逻辑：前缀匹配底板码，第一个非底板码为主板码
        /// 比对的时候需要底板码,获取mes结果的时候需要主板码
        /// </summary>
        /// <param name="codes">硬件驱动已解析的多码列表</param>
        /// <param name="bottomPrefix">底板码前缀</param>
        /// <param name="bottomCode">输出底板码</param>
        /// <param name="spCode">输出主板码</param>
        /// <returns>是否同时识别到底板码和主板码</returns>
        private bool TryIdentifyCodes(List<string> codes, string bottomPrefix,
            out string bottomCode, out string spCode,
            bool isSpCode = false)
        {
            bottomCode = null;
            spCode = null;

            if (codes == null || codes.Count == 0)
                return false;

            var cleanedCodes = new List<string>();
            foreach (var code in codes)
            {
                if (string.IsNullOrWhiteSpace(code))
                    continue;
                var cleaned = code.Trim();
                cleaned = cleaned.Replace("\r", "").Replace("\n", "");
                if (!string.IsNullOrEmpty(cleaned))
                    cleanedCodes.Add(cleaned);
            }
            foreach (var code in cleanedCodes)
            {
                // 主板码(SP码)特征：包含"+"号
                if (code.Contains("+"))
                {                  
                        spCode = code;
                }
                else if(code.Length == 26 && code.StartsWith("L-"))
                {
                    if (string.IsNullOrEmpty(bottomCode))
                        bottomCode = code;
                }
            }
            if (isSpCode)
            {
                //需要同时有主板和底板码
                if (string.IsNullOrEmpty(spCode)|| string.IsNullOrEmpty(bottomCode))
                {
                    return false;
                }
                else
                {
                    return true;
                }
            }
            else
            {
                if (string.IsNullOrEmpty(bottomCode))
                {
                    return false;
                }
                else
                {
                    return true;
                }
            }
        }
    }
}
