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
        private readonly string _testResultUrl;      // 完整 URL，例如 http://mes-server/api/TestResult
        private readonly string _reportStationUrl;   // 完整 URL，例如 http://mes-server/api/ReportStation
        private readonly string _stationCode;        // 工位编码
        private readonly string _token;              // 认证令牌

        public MesService(ISystemConfigManager systemConfigManager)
        {
            // 从配置中获取 MES 相关配置（直接使用 SoftwareConfig 的属性）
            var config = systemConfigManager.SoftwareConfig
                         ?? throw new ArgumentNullException(nameof(systemConfigManager.SoftwareConfig), "软件配置未找到");

            _testResultUrl = config.TestResultUrl
                             ?? throw new ArgumentNullException(nameof(config.TestResultUrl), "TestResultUrl未配置");
            _reportStationUrl = config.ReportStationUrl
                                ?? throw new ArgumentNullException(nameof(config.ReportStationUrl), "ReportStationUrl未配置");
            _stationCode = config.StationCode
                           ?? throw new ArgumentNullException(nameof(config.StationCode), "StationCode未配置");
            //_token = config.Token
            //         ?? throw new ArgumentNullException(nameof(config.Token), "Token未配置");

            //// 设置默认认证头
            //_httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_token}");
        }

        /// <summary>
        /// 从 MES 查询主板码测试结果
        /// </summary>
        public async Task<bool> GetMesTestResult(string spCode)
        {
            if (string.IsNullOrEmpty(spCode))
            {
                Logs.LogWarn("SP码为空，无需查询MES");
                return false;
            }

            var uriBuilder = new UriBuilder(_testResultUrl) { Query = $"spCode={Uri.EscapeDataString(spCode)}" };
            var response = await SendRequestAsync<MesReportResponseDto>(
                async () => await _httpClient.GetAsync(uriBuilder.Uri), spCode, "查询测试结果").ConfigureAwait(false);

            // 根据实际返回的 result 字段判断是否成功
            return response != null && string.Equals(response.Result, "OK", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 向 MES 报站（上报扫码结果）
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

            // 根据实际返回的 result 字段判断是否成功
            return response != null && string.Equals(response.Result, "OK", StringComparison.OrdinalIgnoreCase);
        }


        /// <summary>
        /// 封装 HTTP 请求，统一处理异常、日志和超时
        /// </summary>
        private async Task<T> SendRequestAsync<T>(Func<Task<HttpResponseMessage>> requestFunc, string spCode, string operationName)
            where T : class
        {
            // 使用 CancellationToken 控制超时（10秒）
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
            {
                try
                {
                    // 执行请求（带取消令牌）
                    var response = await requestFunc().WithCancellation(cts.Token).ConfigureAwait(false);

                    if (response.IsSuccessStatusCode)
                    {
                        var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        var result = JsonConvert.DeserializeObject<T>(json);
                        Logs.LogInfo($"MES {operationName} 成功：SP码={spCode}");
                        return result;
                    }

                    // 处理非成功状态码
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

        // 查询结果 DTO
        private class MesTestResultDto
        {
            public string SPCode { get; set; }
            public bool IsOk { get; set; }
            public string ErrorMsg { get; set; }
        }

        // 报站请求 DTO
        private class MesReportDto
        {
            public string productCode { get; set; }
        }
        private class MesReportResponseDto
        {
            [JsonProperty("result")]
            public string Result { get; set; }

            [JsonProperty("nextStation")]
            public string NextStation { get; set; }

            [JsonProperty("message")]
            public string Message { get; set; }
        }
    }

    /// <summary>
    /// 扩展方法：为 Task 添加超时取消支持
    /// </summary>
    internal static class TaskExtensions
    {
        /// <summary>
        /// 将 Task 与 CancellationToken 关联，当 token 取消时抛出 OperationCanceledException
        /// </summary>
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