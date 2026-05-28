using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FZK.Application.Run.Service
{
    public class CodeInfoRequest
    {
        [JsonProperty("code_no")]
        public string CodeNo { get; set; }

        [JsonProperty("equipment_ip")]
        public string EquipmentIp { get; set; }

        [JsonProperty("resv1")]
        public string Resv1 { get; set; } = "";

        [JsonProperty("resv2")]
        public string Resv2 { get; set; } = "";
    }
    // 获取码信息响应（完整字段）
    public class CodeInfoResponse
    {
        [JsonProperty("code_no")]
        public string CodeNo { get; set; }

        [JsonProperty("code_sn")]
        public string CodeSn { get; set; }

        [JsonProperty("client_code")]
        public string ClientCode { get; set; }

        [JsonProperty("bg")]
        public string Bg { get; set; }

        [JsonProperty("sp")]
        public string Sp { get; set; }

        [JsonProperty("pvd")]
        public string Pvd { get; set; }

        [JsonProperty("iqc_top_c")]
        public string IqcTopC { get; set; }

        [JsonProperty("iqc_top_u")]
        public string IqcTopU { get; set; }

        [JsonProperty("iqc_right_rail")]
        public string IqcRightRail { get; set; }

        [JsonProperty("iqc_bottom_c")]
        public string IqcBottomC { get; set; }

        [JsonProperty("iqc_bottom_u")]
        public string IqcBottomU { get; set; }

        [JsonProperty("iqc_left_rail")]
        public string IqcLeftRail { get; set; }

        [JsonProperty("vi_glue_code")]
        public string ViGlueCode { get; set; }

        [JsonProperty("bill_no")]
        public string BillNo { get; set; }

        [JsonProperty("product_no")]
        public string ProductNo { get; set; }

        [JsonProperty("part_no")]
        public string PartNo { get; set; }

        [JsonProperty("phase_no")]
        public string PhaseNo { get; set; }

        [JsonProperty("color")]
        public string Color { get; set; }

        [JsonProperty("bill_type")]
        public string BillType { get; set; }

        [JsonProperty("bg_bin")]
        public string BgBin { get; set; }

        [JsonProperty("config")]
        public string Config { get; set; }

        [JsonProperty("trace_config")]
        public string TraceConfig { get; set; }

        [JsonProperty("line_type")]
        public string LineType { get; set; }

        [JsonProperty("doe")]
        public string Doe { get; set; }

        [JsonProperty("bg_glass_count")]
        public string BgGlassCount { get; set; }

        // 最后过站制程（与配置比对）
        [JsonProperty("process_dopass")]
        public string ProcessDoPass { get; set; }

        // 最后过站工站（与配置比对）
        [JsonProperty("station_dopass")]
        public string StationDoPass { get; set; }

        [JsonProperty("pad1")]
        public string Pad1 { get; set; }

        [JsonProperty("pad2")]
        public string Pad2 { get; set; }

        [JsonProperty("pad3")]
        public string Pad3 { get; set; }

        [JsonProperty("pad4")]
        public string Pad4 { get; set; }

        [JsonProperty("pad5")]
        public string Pad5 { get; set; }

        [JsonProperty("pad6")]
        public string Pad6 { get; set; }

        [JsonProperty("pad7")]
        public string Pad7 { get; set; }

        [JsonProperty("pad8")]
        public string Pad8 { get; set; }

        // 返回码
        [JsonProperty("rc")]
        public string Rc { get; set; }

        // 返回消息
        [JsonProperty("rm")]
        public string Rm { get; set; }
    }
    // 封装获取码信息的业务结果
    public class CodeInfoResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public CodeInfoResponse Data { get; set; }

        public static CodeInfoResult Ok(CodeInfoResponse data) => new CodeInfoResult
        {
            Success = true,
            Message = "OK",
            Data = data
        };

        public static CodeInfoResult Fail(string message) => new CodeInfoResult
        {
            Success = false,
            Message = message
        };
    }
}
