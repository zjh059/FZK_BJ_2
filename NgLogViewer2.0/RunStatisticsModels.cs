using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace NgLogViewer
{
    public sealed class WeldingRecord
    {
        [DisplayName("焊接时间")]
        public DateTime Time { get; set; }
        [DisplayName("治具")]
        public string Jig { get; set; }
        [DisplayName("结果")]
        public string Result { get; set; }
        [DisplayName("底板码")]
        public string BottomCode { get; set; }
        [DisplayName("SP码")]
        public string SpCode { get; set; }
        [DisplayName("结果/NG原因")]
        public string Reason { get; set; }
        [Browsable(false)] public string ProductionMode { get; set; }
        [Browsable(false)] public string Context { get; set; }
        [Browsable(false)] public string SourceFile { get; set; }
        [Browsable(false)] public long LineNumber { get; set; }
    }

    public sealed class BottomNgSummary
    {
        [DisplayName("底板码")]
        public string BottomCode { get; set; }
        [DisplayName("NG数量")]
        public int NgCount { get; set; }
        [DisplayName("涉及治具")]
        public string Jigs { get; set; }
        [DisplayName("最后NG时间")]
        public string LastNgTime { get; set; }
    }

    public sealed class RunStatistics
    {
        [Browsable(false)] public DateTime StartTime { get; set; }
        [Browsable(false)] public DateTime EndTime { get; set; }
        [Browsable(false)] public string ModeUrl { get; set; }
        [Browsable(false)] public bool HasModeConflict { get; set; }
        [Browsable(false)] public int DailySequence { get; set; }
        [Browsable(false)] public List<WeldingRecord> Records { get; set; } = new List<WeldingRecord>();
        [DisplayName("当天批次")] public string SequenceName { get { return "第 " + DailySequence + " 批"; } }
        [DisplayName("生产模式")] public string ProductionMode { get { return HasModeConflict ? "异常（同批混用）" : (String.IsNullOrEmpty(ModeUrl) ? "未识别" : (ModeUrl.Contains("10.176.152.159:28080") ? "量试" : "量产")); } }
        [DisplayName("焊接总数")] public int TotalCount { get { return Records.Count; } }
        [DisplayName("OK数量")] public int OkCount { get { return Records.FindAll(x => x.Result == "OK").Count; } }
        [DisplayName("NG数量")] public int NgCount { get { return Records.FindAll(x => x.Result == "NG").Count; } }
        [DisplayName("批次开始")] public string BatchName { get { return StartTime.ToString("yyyy-MM-dd HH:mm:ss"); } }
        [DisplayName("最后焊接")]
        public string EndName { get { return Records.Count == 0 ? "-" : Records.Max(x => x.Time).ToString("yyyy-MM-dd HH:mm:ss"); } }
        [DisplayName("有效焊接时长")]
        public string Duration
        {
            get
            {
                return FormatMinutes(EffectiveMinutes);
            }
        }
        [Browsable(false)] public double EffectiveMinutes
        {
            get
            {
                List<DateTime> times = Records.Select(x => x.Time).OrderBy(x => x).ToList(); double minutes = 0;
                for (int i = 1; i < times.Count; i++) { double gap = (times[i] - times[i - 1]).TotalMinutes; if (gap <= 5) minutes += gap; }
                return minutes;
            }
        }
        [Browsable(false)] public DateTime FirstWeldTime { get { return Records.Count == 0 ? StartTime : Records.Min(x => x.Time); } }
        [Browsable(false)] public DateTime LastWeldTime { get { return Records.Count == 0 ? EndTime : Records.Max(x => x.Time); } }
        private static string FormatMinutes(double minutes) { int total = Math.Max(0, (int)Math.Round(minutes)); return total >= 60 ? (total / 60) + "小时" + (total % 60) + "分钟" : total + "分钟"; }
    }
}
