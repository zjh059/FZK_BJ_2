using ControlzEx.Standard;
using FZK.Application.Share.Config;
using FZK.Application.Share.Run;
using FZK.Logger;
using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FZK.Application.Run.Service
{
    public class MesService : IMesService
    {
        private readonly HttpClient _httpClient = new HttpClient();
        private readonly string _testResultUrl;
        private readonly string _reportStationUrl;
        private readonly string _stationCode;
        private readonly string _equipmentIp;          // 新增
        private readonly string _processDoPassConfig;  // 新增：配置中的制程
        private readonly string _stationDoPassConfig;  // 新增：配置中的工站
        private readonly bool _isDebug;  // 新增：配置中的工站

        public MesService(ISystemConfigManager systemConfigManager)
        {
            var config = systemConfigManager.SoftwareConfig
                         ?? throw new ArgumentNullException(nameof(systemConfigManager.SoftwareConfig), "软件配置未找到");

            _reportStationUrl = config.ReportStationUrl
                                ?? throw new ArgumentNullException(nameof(config.ReportStationUrl), "ReportStationUrl未配置");
            _stationCode = config.StationCode
                           ?? throw new ArgumentNullException(nameof(config.StationCode), "StationCode未配置");
            _isDebug = config.IsDebug;
            // 新增配置读取
            _testResultUrl = config.TestResultUrl
                           ?? throw new ArgumentNullException(nameof(config.TestResultUrl), "TestResultUrl未配置");
            _equipmentIp = config.EquipmentIp
                           ?? throw new ArgumentNullException(nameof(config.EquipmentIp), "EquipmentIp未配置");
            _processDoPassConfig = config.Process_dopass ?? string.Empty;
            _stationDoPassConfig = config.Station_dopass ?? string.Empty;
        }

        /// <summary>
        /// 从 MES 查询主板码测试结果（保持原有功能）
        /// </summary>
        public async Task<bool> GetMesTestResult(string spCode)
        {
            var response = null;
            if (_isDebug)
            {
                 response = await GetCodeInfoAsync("DRC1054A8CSPQY0A1AA");
            }
            else
            {
                if (string.IsNullOrEmpty(spCode))
                {
                    Logs.LogWarn("SP码为空，无需查询MES");
                    return false;
                }
            }
            response = await GetCodeInfoAsync(spCode);
            return response.Success;
        }

        /// <summary>
        /// 向 MES 报站（上报扫码结果）（保持原有功能）
        /// </summary>
        public async Task<bool> ReportStation(string spCode)
        {
            if (string.IsNullOrEmpty(spCode))
            {
                Logs.LogWarn("SP码为空，无需报站");
                return false;
            }

            var reportDto = new MesReportDto
            {
                productCode = spCode
            };

            var content = new StringContent(
                JsonConvert.SerializeObject(reportDto),
                Encoding.UTF8,
                "application/json"
            );

            var response = await SendRequestAsync<MesReportResponseDto>(
                async () => await _httpClient.PostAsync(_reportStationUrl, content), spCode, "报站").ConfigureAwait(false);

            return response != null && string.Equals(response.Result, "OK", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 获取码信息（新增接口）
        /// 功能：调用获取码信息接口，检查返回码 rc，并校验制程与工站是否匹配配置。
        /// </summary>
        /// <param name="codeNo">码号</param>
        /// <param name="equipmentIp">设备IP，可选，默认使用配置中的IP</param>
        /// <returns>包含校验结果的 CodeInfoResult</returns>
        public async Task<CodeInfoResult> GetCodeInfoAsync(string codeNo, string equipmentIp = null)
        {
            if (string.IsNullOrEmpty(codeNo))
            {
                Logs.LogWarn("码号为空，无法获取码信息");
                return CodeInfoResult.Fail("码号为空");
            }

            // 构建请求体
            var requestDto = new CodeInfoRequest
            {
                CodeNo = codeNo,
                EquipmentIp = equipmentIp ?? _equipmentIp,
                Resv1 = "",
                Resv2 = ""
            };

            var content = new StringContent(
                JsonConvert.SerializeObject(requestDto),
                Encoding.UTF8,
                "application/json"
            );

            // 发送请求并获取原始响应
            var responseDto = await SendRequestAsync<CodeInfoResponse>(
                async () => await _httpClient.PostAsync(_testResultUrl, content), codeNo, "获取码信息").ConfigureAwait(false);

            if (responseDto == null)
            {
                return CodeInfoResult.Fail("网络或服务异常，未能获取到码信息响应");
            }

            // 检查 rc 字段
            if (!string.Equals(responseDto.Rc, "000", StringComparison.Ordinal))
            {
                var errorMsg = !string.IsNullOrEmpty(responseDto.Rm) ? responseDto.Rm : $"接口返回失败 rc={responseDto.Rc}";
                Logs.LogError($"获取码信息失败（码号={codeNo}）：{errorMsg}");
                return CodeInfoResult.Fail(errorMsg);
            }

            // 校验制程（process_dopass）
            if (!string.IsNullOrEmpty(_processDoPassConfig) &&
                !string.Equals(responseDto.ProcessDoPass, _processDoPassConfig, StringComparison.OrdinalIgnoreCase))
            {
                var msg = $"制程不匹配：接口返回 [{responseDto.ProcessDoPass}]，配置要求 [{_processDoPassConfig}]";
                Logs.LogWarn($"码号={codeNo}：{msg}");
                return CodeInfoResult.Fail(msg);
            }

            // 校验工站（station_dopass）
            if (!string.IsNullOrEmpty(_stationDoPassConfig) &&
                !string.Equals(responseDto.StationDoPass, _stationDoPassConfig, StringComparison.OrdinalIgnoreCase))
            {
                var msg = $"工站不匹配：接口返回 [{responseDto.StationDoPass}]，配置要求 [{_stationDoPassConfig}]";
                Logs.LogWarn($"码号={codeNo}：{msg}");
                return CodeInfoResult.Fail(msg);
            }

            Logs.LogInfo($"获取码信息成功（码号={codeNo}），制程={responseDto.ProcessDoPass}，工站={responseDto.StationDoPass}");
            return CodeInfoResult.Ok(responseDto);
        }

        // ========== 以下为内部工具方法，与原来保持一致 ==========

        private async Task<T> SendRequestAsync<T>(Func<Task<HttpResponseMessage>> requestFunc, string spCode, string operationName)
            where T : class
        {
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
            {
                try
                {
                    var response = await requestFunc().WithCancellation(cts.Token).ConfigureAwait(false);

                    if (response.IsSuccessStatusCode)
                    {
                        var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        var result = JsonConvert.DeserializeObject<T>(json);
                        Logs.LogInfo($"MES {operationName} 成功：SP码={spCode}");
                        return result;
                    }

                    Logs.LogError($"MES {operationName} 失败：HTTP {response.StatusCode}，SP码={spCode}");
                    return null;
                }
                catch (OperationCanceledException) when (cts.Token.IsCancellationRequested)
                {
                    Logs.LogError($"MES {operationName} 超时（10秒），SP码={spCode}");
                    return null;
                }
                catch (HttpRequestException ex)
                {
                    Logs.LogError($"MES {operationName} 网络异常（SP码={spCode}）：{ex.Message}");
                    return null;
                }
                catch (Exception ex)
                {
                    Logs.LogError($"MES {operationName} 未知异常（SP码={spCode}）：{ex.Message}");
                    return null;
                }
            }
        }
    }
    // Task 扩展方法保持不变
    internal static class TaskExtensions
    {
        public static async Task<T> WithCancellation<T>(this Task<T> task, CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<bool>();
            using (cancellationToken.Register(s => ((TaskCompletionSource<bool>)s).TrySetResult(true), tcs))
            {
                if (task != await Task.WhenAny(task, tcs.Task).ConfigureAwait(false))
                {
                    throw new OperationCanceledException(cancellationToken);
                }
            }
            return await task.ConfigureAwait(false);
        }
    }
}