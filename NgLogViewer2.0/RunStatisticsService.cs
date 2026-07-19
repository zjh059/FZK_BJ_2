using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace NgLogViewer
{
    /// <summary>只读分析 info 日志，并以最后一次“硬件初始化完成”为本次跑线的起点。</summary>
    public sealed class RunStatisticsService
    {
        public const string TrialUrl = "http://10.176.152.159:28080/NewSFCV2-center/NewSFCV2/getcodeinfo";
        public const string ProductionUrl = "http://10.197.246.63:8080/NewSFCV2/getcodeinfo";
        private static readonly Regex TimePattern = new Regex(@"^(?<v>\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d+)", RegexOptions.Compiled);
        private static readonly Regex FinalPattern = new Regex(@"治具(?<j>\d+)\s+MES校验结束.*?结果\s*[:：=]\s*(?<r>OK|NG).*?SP码\s*[:：=]\s*(?<sp>[^,，\s]+)(?:[,，]底板码\s*[:：=]\s*(?<bottom>[^,，\s]+))?", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex BottomPattern = new Regex(@"治具(?<j>\d+).*?底板码\s*[:：]?\s*(?<v>[A-Za-z0-9_./-]+)", RegexOptions.Compiled);

        public RunStatistics ReadCurrentRun(string logRoot)
        {
            List<RunStatistics> runs = ReadRuns(logRoot, DateTime.MinValue, DateTime.MaxValue);
            if (runs.Count == 0) throw new InvalidDataException("日志中没有找到“硬件初始化完成”，无法确定运行批次。");
            return runs[runs.Count - 1];
        }

        /// <summary>把每次“硬件初始化完成”到下一次初始化之前切分成一个独立跑线批次。</summary>
        public List<RunStatistics> ReadRuns(string logRoot, DateTime start, DateTime end)
        {
            List<string> files = FindInfoFiles(logRoot).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
            if (files.Count == 0) throw new FileNotFoundException("所选目录中没有找到 info 日志。");

            var all = new List<LineInfo>();
            foreach (string file in files)
            {
                long number = 0;
                foreach (string line in ReadLinesShared(file))
                {
                    number++;
                    DateTime time;
                    if (TryTime(line, out time)) all.Add(new LineInfo { Time = time, Text = line, File = file, Number = number });
                }
            }
            all = all.OrderBy(x => x.Time).ThenBy(x => x.File).ThenBy(x => x.Number).ToList();
            var runs = new List<RunStatistics>();
            RunStatistics result = null;
            var bottomByJig = new Dictionary<string, string>();
            for (int i = 0; i < all.Count; i++)
            {
                LineInfo item = all[i];
                if (item.Text.Contains("硬件初始化完成"))
                {
                    if (result != null) { result.EndTime = item.Time; runs.Add(result); }
                    result = new RunStatistics { StartTime = item.Time, EndTime = item.Time };
                    bottomByJig.Clear();
                    continue;
                }
                if (result == null) continue;
                result.EndTime = item.Time;
                UpdateMode(result, item.Text);
                Match bottom = BottomPattern.Match(Message(item.Text));
                if (bottom.Success) bottomByJig[bottom.Groups["j"].Value] = bottom.Groups["v"].Value;

                Match final = FinalPattern.Match(Message(item.Text));
                if (!final.Success) continue; // 只认最终 MES 结果，扫码重试中的临时 NG 不重复计数。
                string jig = final.Groups["j"].Value;
                string bottomCode = final.Groups["bottom"].Value;
                if (String.IsNullOrWhiteSpace(bottomCode)) bottomByJig.TryGetValue(jig, out bottomCode);
                string context = BuildContext(all, i);
                result.Records.Add(new WeldingRecord
                {
                    Time = item.Time, Jig = "治具" + jig, Result = final.Groups["r"].Value.ToUpperInvariant(),
                    BottomCode = bottomCode ?? String.Empty, SpCode = final.Groups["sp"].Value,
                    Reason = final.Groups["r"].Value.Equals("NG", StringComparison.OrdinalIgnoreCase) ? FindReason(all, i) : "焊接完成，最终结果 OK",
                    Context = context, SourceFile = item.File, LineNumber = item.Number, ProductionMode = result.ProductionMode
                });
            }
            if (result != null) runs.Add(result);
            // “今天第几批”只统计真正发生过焊接的运行段；启动后立刻再次初始化形成的 0 件空段不占序号。
            foreach (var day in runs.Where(x => x.TotalCount > 0).GroupBy(x => x.StartTime.Date))
            {
                int sequence = 0;
                foreach (RunStatistics run in day.OrderBy(x => x.StartTime)) run.DailySequence = ++sequence;
            }
            // 时间范围按批次开始时间筛选；这样一个批次不会因为跨过午夜而被拆散。
            return runs.Where(x => x.StartTime >= start && x.StartTime <= end).OrderBy(x => x.StartTime).ToList();
        }

        private static void UpdateMode(RunStatistics run, string line)
        {
            string url = line.IndexOf(TrialUrl, StringComparison.OrdinalIgnoreCase) >= 0 ? TrialUrl :
                (line.IndexOf(ProductionUrl, StringComparison.OrdinalIgnoreCase) >= 0 ? ProductionUrl : String.Empty);
            if (url.Length == 0) return;
            if (String.IsNullOrEmpty(run.ModeUrl)) run.ModeUrl = url;
            else if (!String.Equals(run.ModeUrl, url, StringComparison.OrdinalIgnoreCase)) run.HasModeConflict = true;
        }

        public List<BottomNgSummary> SummarizeBottomNg(RunStatistics run)
        {
            return run.Records.Where(x => x.Result == "NG").GroupBy(x => String.IsNullOrWhiteSpace(x.BottomCode) ? "（未识别到底板码）" : x.BottomCode)
                .Select(g => new BottomNgSummary { BottomCode = g.Key, NgCount = g.Count(), Jigs = String.Join("、", g.Select(x => x.Jig).Distinct()), LastNgTime = g.Max(x => x.Time).ToString("yyyy-MM-dd HH:mm:ss") })
                .OrderByDescending(x => x.NgCount).ThenBy(x => x.BottomCode).ToList();
        }

        private static string FindReason(List<LineInfo> lines, int index)
        {
            // NG 前 15 行通常含 MES 返回体、工站不匹配或接口失败的明确说明。
            for (int i = index - 1; i >= Math.Max(0, index - 15); i--)
            {
                string m = Message(lines[i].Text);
                if (m.IndexOf("接口返回", StringComparison.OrdinalIgnoreCase) >= 0 || m.Contains("不匹配") || m.Contains("不存在") || m.Contains("失败") || m.Contains("异常")) return m;
            }
            return Message(lines[index].Text);
        }

        private static string BuildContext(List<LineInfo> lines, int index)
        {
            int from = Math.Max(0, index - 12);
            return String.Join(Environment.NewLine, lines.Skip(from).Take(index - from + 1).Select(x => x.Time.ToString("HH:mm:ss.fff") + " [第" + x.Number + "行] " + Message(x.Text)));
        }

        private static IEnumerable<string> FindInfoFiles(string root)
        {
            if (String.IsNullOrWhiteSpace(root)) return Enumerable.Empty<string>();
            // 统计页支持直接选择一份 info-日期.log，方便拿单个备份日志离线查询。
            if (File.Exists(root)) return new[] { root };
            if (!Directory.Exists(root)) return Enumerable.Empty<string>();
            string info = new DirectoryInfo(root).Name.Equals("info", StringComparison.OrdinalIgnoreCase) ? root : Path.Combine(root, "info");
            string folder = Directory.Exists(info) ? info : root;
            try { return Directory.GetFiles(folder, "info*.log", SearchOption.TopDirectoryOnly); } catch { return Enumerable.Empty<string>(); }
        }

        private static IEnumerable<string> ReadLinesShared(string file)
        {
            using (var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var reader = new StreamReader(stream, true)) while (!reader.EndOfStream) yield return reader.ReadLine();
        }

        private static bool TryTime(string line, out DateTime value)
        {
            value = DateTime.MinValue;
            Match m = TimePattern.Match(line ?? String.Empty);
            return m.Success && DateTime.TryParse(m.Groups["v"].Value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out value);
        }
        private static string Message(string line) { string[] p = (line ?? String.Empty).Split(new[] { '|' }, 4); return p.Length > 3 ? p[3].Trim() : line; }
        private sealed class LineInfo { public DateTime Time; public string Text; public string File; public long Number; }
    }
}
