using FZK.Application.Share.Config;
using FZK.Application.Share.Init;
using FZK.Application.Share.Run;
using FZK.Hardware.Scanner.Base;
using FZK.Logger;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace FZK.Application.Run.Service
{
    /// <summary>
    /// 单个治具的流程引擎（扫码比对、焊接结果处理、清零）
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

        // 用于记录当前底板码（清零时可能用到）
        private string _currentBottomCode;

        public JigFlowEngine(
            JigConfig config,
            IPlcService plc,
            IHardwareService hardware,
            IDatabaseService db,
            IMesService mes,
            RunConfig runConfig,
            ScannerConfig bottomScannerConfig,
            bool isNoHardwareMode,
            bool isSfcEnabled)
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
        }

        public async Task ProcessScanAsync()
        {
            Logs.LogInfo($"{_config.JigName} 开始扫码比对流程");
            await _plc.WriteRegisterAsync(_config.TriggerScanAddr, 0);

            string bottomCode = "", spCode = "", topCode = "";
            bool success = false;
            int retry = 0;

            while (retry < _runConfig.ScanRetryCount && !success)
            {
                if (retry > 0)
                    await Task.Delay(_runConfig.ScanRetryDelay);

                string bottomRaw = "";
                if (_isNoHardwareMode)
                {
                    // 模拟：底板码+主板码（用分隔符连接）
                    bottomRaw = $"H-B{DateTime.Now:yyyyMMddHHmmss}{_bottomScannerConfig.CodeDelimiter}SP{DateTime.Now:yyyyMMddHHmmss}";
                    topCode = $"T{DateTime.Now:yyyyMMddHHmmss}";
                }
                else
                {
                    var bottomTask = _hardware.TriggerScanner(_config.BottomScanner);
                    var topTask = _config.TopScanner.HasValue
                        ? _hardware.TriggerScanner(_config.TopScanner.Value)
                        : Task.FromResult("");
                    await Task.WhenAll(bottomTask, topTask);
                    bottomRaw = await bottomTask;
                    topCode = await topTask;
                }

                if (TryParseBottomAndSpCode(bottomRaw, _bottomScannerConfig.CodeDelimiter, out var parsedBottom, out var parsedSp))
                {
                    bottomCode = parsedBottom;
                    spCode = parsedSp;
                    success = !string.IsNullOrEmpty(bottomCode) &&
                              !string.IsNullOrEmpty(spCode) &&
                              !string.IsNullOrEmpty(topCode);
                }
                retry++;
            }

            if (success)
            {
                await _plc.WriteRegisterAsync(_config.ScanResultAddr, 1);
                bool verifyOk = await _db.VerifyBottomTopCodeAsync(bottomCode, topCode);
                await _plc.WriteRegisterAsync(_config.CompareResultAddr, verifyOk ? 1 : 2);
                if (verifyOk)
                {
                    await _db.UpdateOrAddCodeEntityAsync(bottomCode, topCode, spCode);
                    Logs.LogInfo($"{_config.JigName} 比对成功: {bottomCode} / {topCode}");
                }
                else
                {
                    Logs.LogWarn($"{_config.JigName} 比对失败: {bottomCode} != {topCode}");
                }
            }
            else
            {
                await _plc.WriteRegisterAsync(_config.CompareResultAddr, 2);
                Logs.LogWarn($"{_config.JigName} 扫码重试耗尽");
            }
        }

        public async Task ProcessWeldAsync()
        {
            Logs.LogInfo($"{_config.JigName} 开始焊接结果处理");
            await _plc.WriteRegisterAsync(_config.TriggerWeldAddr, 0);

            string bottomCode = "", spCode = "";
            bool success = false;
            int retry = 0;

            while (retry < _runConfig.ScanRetryCount && !success)
            {
                if (retry > 0)
                    await Task.Delay(_runConfig.ScanRetryDelay);

                string bottomRaw = _isNoHardwareMode
                    ? $"H-B{DateTime.Now:yyyyMMddHHmmss}{_bottomScannerConfig.CodeDelimiter}SP{DateTime.Now:yyyyMMddHHmmss}"
                    : await _hardware.TriggerScanner(_config.BottomScanner);

                if (TryParseBottomAndSpCode(bottomRaw, _bottomScannerConfig.CodeDelimiter, out var parsedBottom, out var parsedSp))
                {
                    bottomCode = parsedBottom;
                    spCode = parsedSp;
                    success = true;
                }
                retry++;
            }

            if (success)
            {
                await _plc.WriteRegisterAsync(_config.WeldResultAddr, 1);
                bool mesOk = !_isSfcEnabled || await _mes.GetMesTestResult(spCode);
                int weldResult = mesOk ? 1 : 2;
                await _db.UpdateTestResultAsync(spCode, weldResult);
                string newCount = await _db.IncrementCountAsync(bottomCode);
                if (int.TryParse(newCount, out int countVal))
                    await _plc.WriteRegisterAsync(_config.CountAddr, countVal);
                await _plc.WriteRegisterAsync(_config.WeldFinalAddr, weldResult);
                _currentBottomCode = bottomCode;
                Logs.LogInfo($"{_config.JigName} 焊接完成，底板码 {bottomCode}，MES={(mesOk ? "OK" : "NG")}，使用次数={newCount}");
            }
            else
            {
                await _plc.WriteRegisterAsync(_config.WeldFinalAddr, 2);
                Logs.LogWarn($"{_config.JigName} 焊接扫码重试耗尽");
            }
        }

        public async Task ProcessClearAsync()
        {
            Logs.LogInfo($"{_config.JigName} 开始清零流程");
            await _plc.WriteRegisterAsync(_config.TriggerClearAddr, 0);

            string bottomCode = "";
            bool success = false;

            if (_isNoHardwareMode)
            {
                bottomCode = $"B{DateTime.Now:yyyyMMddHHmmss}";
                success = true;
            }
            else
            {
                string raw = await _hardware.TriggerScanner(_config.BottomScanner);
                if (TryParseBottomAndSpCode(raw, _bottomScannerConfig.CodeDelimiter, out var parsedBottom, out _))
                {
                    bottomCode = parsedBottom;
                    success = !string.IsNullOrEmpty(bottomCode);
                }
            }

            if (success)
            {
                await _db.ClearCountAsync(bottomCode);
                await _plc.WriteRegisterAsync(_config.CountAddr, 0);
                Logs.LogInfo($"{_config.JigName} 清零成功，底板码 {bottomCode}");
            }
            else
            {
                Logs.LogWarn($"{_config.JigName} 清零扫码失败");
            }
        }

        /// <summary>
        /// 从原始扫码结果中解析底板码和主板码
        /// </summary>
        /// <param name="raw">原始字符串（可能包含分隔符）</param>
        /// <param name="delimiter">分隔符</param>
        /// <param name="bottom">解析出的底板码</param>
        /// <param name="sp">解析出的主板码</param>
        /// <returns>是否解析成功</returns>
        private bool TryParseBottomAndSpCode(string raw, string delimiter, out string bottom, out string sp)
        {
            bottom = null;
            sp = null;

            if (string.IsNullOrEmpty(raw)) return false;

            var codes = raw.Split(new[] { delimiter }, StringSplitOptions.RemoveEmptyEntries);
            if (codes.Length < 2) return false;

            // 遍历所有码，根据特征识别
            string possibleBottom = null;
            string possibleSp = null;

            foreach (var code in codes)
            {
                var trimmed = code.Trim();
                if (trimmed.StartsWith("H-")) // 底板码特征：以 H- 开头
                {
                    possibleBottom = trimmed;
                }
                else if (!trimmed.Contains("-") && trimmed.All(c => char.IsLetterOrDigit(c))) // 主板码特征：不含横线，纯字母数字
                {
                    possibleSp = trimmed;
                }
            }

            // 如果两者都找到，则成功
            if (!string.IsNullOrEmpty(possibleBottom) && !string.IsNullOrEmpty(possibleSp))
            {
                bottom = possibleBottom;
                sp = possibleSp;
                return true;
            }

            // 如果特征不明显，降级按顺序假设（第一个为底板，第二个为主板）
            if (codes.Length >= 2)
            {
                bottom = codes[0].Trim();
                sp = codes[1].Trim();
                Logs.LogWarn($"解析降级使用顺序：底板={bottom}, 主板={sp}");
                return true;
            }

            // 特殊处理：可能只有一个码，且长度为底板码长度（视为底板码，主板码留空）
            if (codes.Length == 1 && codes[0].Length == _bottomScannerConfig.SnLength)
            {
                bottom = codes[0].Trim();
                sp = "";
                Logs.LogWarn($"解析得到单个底板码：{bottom}");
                return true;
            }

            return false;
        }
    }
}