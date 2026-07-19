using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
namespace NgLogViewer
{
    /// <summary>宽松日志解析器：字段提取失败时仍保留原始消息，便于兼容后续日志措辞变化。</summary>
    public sealed class LogParser
    {
        private static readonly Regex TimeAtStart = new Regex(@"^\s*(?<time>\d{4}[-/]\d{1,2}[-/]\d{1,2}[ T]\d{1,2}:\d{2}:\d{2}(?:[\.,]\d{1,7})?)", RegexOptions.Compiled);
        // NG 必须是独立结果词。条码可能合法包含“NG”（例如 DR8HV9A02NG0001...），
        // 因此左右紧挨字母或数字时不能当成故障，否则会把实际 OK 的扫码/MES 请求误报为 NG。
        private static readonly Regex StandaloneNg = new Regex(@"(?<![A-Za-z0-9])NG(?![A-Za-z0-9])", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex IntermediateWeldingScanNg = new Regex(@"焊接扫码第\s*\d+\s*次.*识别结果\s*[：:]\s*NG(?![A-Za-z])", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex JigPattern = new Regex(@"(?:治具|JIG)\s*[-_：:]?\s*(?<v>[12]|\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex BottomPattern = new Regex(@"(?:底板码|底板|Bottom(?:Code)?)\s*[：:=]?\s*(?<v>[A-Za-z0-9][A-Za-z0-9_\-./]{2,})", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex TopPattern = new Regex(@"(?:导向板码|导向板|顶部码|Top(?:Code)?)\s*[：:=]?\s*(?<v>[A-Za-z0-9][A-Za-z0-9_\-./]{2,})", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex MainPattern = new Regex(@"(?:主板码|主板|SP码|MainBoard(?:Code)?|SPCode)\s*[：:=]?\s*(?<v>[A-Za-z0-9][A-Za-z0-9_\-./+]{2,})", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex GenericCodePattern = new Regex(@"(?:码号|code_no)\s*[：:=]\s*[""']?(?<v>[A-Za-z0-9][A-Za-z0-9_\-./+]{5,})", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly string[] DateFormats = { "yyyy-MM-dd HH:mm:ss.ffff", "yyyy-MM-dd HH:mm:ss.fff", "yyyy-MM-dd HH:mm:ss", "yyyy/MM/dd HH:mm:ss.ffff", "yyyy/MM/dd HH:mm:ss.fff", "yyyy/MM/dd HH:mm:ss" };

        public LogEntry Parse(string line, string sourceFile, long lineNumber, IList<string> keywords)
        {
            RawLogLine raw = ParseAny(line, sourceFile, lineNumber);
            if (raw == null || !IsNg(raw.Message, raw.Level, keywords)) return null;
            var entry = new LogEntry { Time = raw.Time, Level = raw.Level, Message = raw.Message, Reason = raw.Message, SourceFile = sourceFile, LineNumber = lineNumber, Context = String.Empty };
            FillFields(entry, raw.Message); entry.Category = Classify(raw.Message, raw.Level);
            return entry;
        }

        internal RawLogLine ParseAny(string line, string sourceFile, long lineNumber)
        {
            if (String.IsNullOrWhiteSpace(line)) return null;
            Match tm = TimeAtStart.Match(line); DateTime time;
            if (!tm.Success || !TryParseTime(tm.Groups["time"].Value, out time)) return null;
            string[] parts = line.Split(new[] { '|' }, 4);
            string level = parts.Length > 1 ? parts[1].Trim() : InferLevel(sourceFile);
            string message = parts.Length > 3 ? parts[3].Trim() : line.Substring(tm.Length).Trim(' ', '|');
            return new RawLogLine { Time = time, Level = level, Message = message, SourceFile = sourceFile, LineNumber = lineNumber };
        }

        /// <summary>把NG前后相关日志合并到上下文，并从上下文补取主板码等字段。</summary>
        internal void Enrich(LogEntry entry, IEnumerable<RawLogLine> nearby)
        {
            var useful = new List<string>();
            if (!String.IsNullOrWhiteSpace(entry.Context)) useful.AddRange(entry.Context.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries));
            foreach (RawLogLine x in nearby)
            {
                if (x == null || Math.Abs((x.Time - entry.Time).TotalSeconds) > 120 || !IsContextRelevant(x.Message)) continue;
                string contextMessage = x.Message.Length > 700 ? x.Message.Substring(0, 700) + "..." : x.Message;
                useful.Add(x.Time.ToString("HH:mm:ss.fff") + " [第" + x.LineNumber + "行] " + contextMessage);
                FillFields(entry, x.Message);
                if (IsProductCodeMissing(x.Message))
                {
                    // MES 的响应体可能使用简体或繁体。只要明确返回产品码不存在，
                    // 就以 MES 原因为准，避免被 NG 行中的“底板码”误分到扫码比对分类。
                    entry.Category = "焊接/MES";
                    entry.Reason = "MES返回：产品码不存在；NG行：" + entry.Message;
                }
                else if (IsMoreSpecificReason(x.Message) && !entry.Reason.Contains(x.Message)) entry.Reason = x.Message + "；NG行：" + entry.Message;
            }
            entry.Context = String.Join(Environment.NewLine, useful.Distinct().Take(10));
        }

        private static void FillFields(LogEntry entry, string message)
        {
            if (String.IsNullOrEmpty(entry.Jig)) entry.Jig = Group(JigPattern, message);
            if (String.IsNullOrEmpty(entry.BottomCode)) entry.BottomCode = Group(BottomPattern, message);
            if (String.IsNullOrEmpty(entry.TopCode)) entry.TopCode = Group(TopPattern, message);
            if (String.IsNullOrEmpty(entry.MainBoardCode)) entry.MainBoardCode = Group(MainPattern, message);
            if (String.IsNullOrEmpty(entry.MainBoardCode)) entry.MainBoardCode = Group(GenericCodePattern, message);
        }
        private static bool IsProductCodeMissing(string m) { return ContainsAny(m, "产品码不存在", "產品碼不存在"); }
        private static bool IsMoreSpecificReason(string m) { return IsProductCodeMissing(m) || ContainsAny(m, "工站不匹配", "站点不匹配", "工位不匹配", "接口返回", "SP码为空", "码号为空", "比对失败", "校验失败", "报站失败", "请求失败", "返回失败", "超时", "Exception"); }
        private static bool IsContextRelevant(string m)
        {
            if (IsMoreSpecificReason(m)) return true;
            return ContainsAny(m, "MES校验结束", "焊接完成", "数据库更新结束", "获取码信息", "响应体", "码号=", "SP码", "底板码", "导向板码", "主板码");
        }

        private static bool IsNg(string message, string level, IList<string> keywords)
        {
            // “焊接扫码第N次=NG”只是一次重试过程，不代表整件产品最终失败。
            // 最终真失败仍会由“MES校验结果=NG”“工站不匹配”“重试耗尽”等终态日志识别。
            // 在这里直接排除中间尝试，也能避免实时监控在下一次扫码成功前先弹出一条假NG。
            if (IntermediateWeldingScanNg.IsMatch(message)) return false;
            if (StandaloneNg.IsMatch(message)) return true; // 防止把 PING 或产品码内嵌 NG 误判成故障。
            if (keywords != null) foreach (string k in keywords)
            {
                if (String.Equals(k, "NG", StringComparison.OrdinalIgnoreCase)) continue;
                if (message.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return String.Equals(level, "ERROR", StringComparison.OrdinalIgnoreCase) || String.Equals(level, "FATAL", StringComparison.OrdinalIgnoreCase);
        }
        private static string Classify(string m, string level)
        {
            if (ContainsAny(m, "工站不匹配", "站点不匹配", "工位不匹配", "接口返回", "报站")) return "工站/接口";
            if (ContainsAny(m, "比对", "底板", "导向板", "顶部扫码")) return "底板/导向板比对";
            if (ContainsAny(m, "焊接", "MES", "报站", "SFC")) return "焊接/MES";
            if (ContainsAny(m, "扫码", "Scanner", "条码", "未读到", "超时")) return "扫码异常";
            if (ContainsAny(m, "PLC", "机械臂", "Robot", "连接", "通信", "Socket")) return "设备/通信";
            if (String.Equals(level, "ERROR", StringComparison.OrdinalIgnoreCase) || m.IndexOf("Exception", StringComparison.OrdinalIgnoreCase) >= 0) return "系统异常";
            return "其他NG";
        }
        private static bool ContainsAny(string s, params string[] values) { foreach (string v in values) if (s.IndexOf(v, StringComparison.OrdinalIgnoreCase) >= 0) return true; return false; }
        private static string Group(Regex r, string s) { Match m = r.Match(s); return m.Success ? m.Groups["v"].Value.TrimEnd('，', ',', '。', ';', '；') : String.Empty; }
        private static string InferLevel(string path) { string n = Path.GetFileName(path) ?? ""; if (n.StartsWith("error", StringComparison.OrdinalIgnoreCase)) return "ERROR"; if (n.StartsWith("debug", StringComparison.OrdinalIgnoreCase)) return "DEBUG"; return "INFO"; }
        private static bool TryParseTime(string s, out DateTime value)
        {
            s = s.Replace(',', '.');
            return DateTime.TryParseExact(s, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out value) || DateTime.TryParse(s, CultureInfo.CurrentCulture, DateTimeStyles.AssumeLocal, out value);
        }
    }
}
