using FZK.Application.Share.Config;
using FZK.Application.Share.Run;
using FZK.Logger;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace FZK.Application.Run.Service
{
    public class MesService : IMesService
    {
        private readonly HttpClient _httpClient;
        private readonly string _mesBaseUrl; // 从配置读取
        private readonly string _mesToken;   // 从配置读取
        // 构造函数注入HttpClient和配置（可替换为你的配置管理器）
        public MesService(HttpClient httpClient, ISystemConfigManager systemConfigManager)
        {
            _httpClient = httpClient;
            _mesBaseUrl = systemConfigManager.SoftwareConfig.BaseUrl;
            _mesToken = systemConfigManager.SoftwareConfig.Token;

            // 设置MES接口默认请求头
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_mesToken}");
            _httpClient.Timeout = TimeSpan.FromSeconds(10); // 超时时间
        }
        /// <summary>
        /// 从MES查询主板码测试结果
        /// </summary>
        public async Task<bool> GetMesTestResult(string spCode)
        {
            if (string.IsNullOrEmpty(spCode))
            {
                Logs.LogWarn("SP码为空，无需查询MES");
                return false;
            }

            try
            {
                var response = await _httpClient.GetAsync($"{_mesBaseUrl}/api/TestResult?spCode={spCode}");
                if (response.IsSuccessStatusCode)
                {
                    string result = await response.Content.ReadAsStringAsync();
                    var mesResult = JsonConvert.DeserializeObject<MesTestResultDto>(result);
                    Logs.LogInfo($"MES查询成功：SP码={spCode}，结果={(mesResult.IsOk ? "OK" : "NG")}");
                    return mesResult.IsOk;
                }
                else
                {
                    Logs.LogError($"MES查询失败：{response.StatusCode}，SP码={spCode}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Logs.LogError($"MES查询异常（SP码={spCode}）：{ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 向MES报站（上报扫码结果）
        /// </summary>
        public async Task<bool> ReportStation(string spCode)
        {
            if (string.IsNullOrEmpty(spCode))
            {
                Logs.LogWarn("SP码为空，无需报站");
                return false;
            }

            try
            {
                var reportDto = new MesReportDto
                {
                    SPCode = spCode,
                    StationCode = "WELD_01", // 焊接工位编码（从配置读取）
                    ReportTime = DateTime.Now
                };

                var content = new StringContent(
                    JsonConvert.SerializeObject(reportDto),
                    Encoding.UTF8,
                    "application/json"
                );

                var response = await _httpClient.PostAsync($"{_mesBaseUrl}/api/ReportStation", content);

                if (response.IsSuccessStatusCode)
                {
                    Logs.LogInfo($"MES报站成功：SP码={spCode}");
                    return true;
                }
                else
                {
                    Logs.LogError($"MES报站失败：{response.StatusCode}，SP码={spCode}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Logs.LogError($"MES报站异常（SP码={spCode}）：{ex.Message}");
                return false;
            }
        }

     
        private class MesTestResultDto
        {
            public string SPCode { get; set; }
            public bool IsOk { get; set; }
            public string ErrorMsg { get; set; }
        }

        private class MesReportDto
        {
            public string SPCode { get; set; }
            public string StationCode { get; set; }
            public DateTime ReportTime { get; set; }
        }
    }
}

