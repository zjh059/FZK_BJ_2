using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
namespace NgLogViewer
{
    /// <summary>只读查询服务。使用FileShare.ReadWrite/Delete，不锁住机台主程序正在写的日志。</summary>
    public sealed class LogQueryService
    {
        private readonly LogParser parser = new LogParser();
        private static readonly Regex FileDatePattern = new Regex(@"(?<!\d)(?<date>\d{4}-\d{2}-\d{2})(?!\d)", RegexOptions.Compiled);

        /// <summary>新建可暂停、可继续的查询会话；文件清单只在开始查询时生成一次。</summary>
        public QuerySession CreateSession(QueryOptions options)
        {
            return new QuerySession { Options = options, Files = SelectFiles(options).ToList(), NextFileIndex = 0 };
        }

        /// <summary>兼容一次性查询调用。</summary>
        public QueryResult Query(QueryOptions o, CancellationToken token, Action<int, string> progress)
        {
            return ContinueQuery(CreateSession(o), token, progress);
        }

        /// <summary>
        /// 从 QuerySession.NextFileIndex 继续读取。中止发生在文件中间时，
        /// 当前文件不计为完成，下次从该文件重读；已经完成的文件不会重复扫描。
        /// </summary>
        public QueryResult ContinueQuery(QuerySession session, CancellationToken token, Action<int, string> progress)
        {
            if (session == null || session.Options == null) throw new ArgumentNullException("session");
            QueryOptions o = session.Options;
            while (session.NextFileIndex < session.Files.Count)
            {
                if (token.IsCancellationRequested) return Snapshot(session, true);
                string file = session.Files[session.NextFileIndex];
                if (progress != null) progress(session.Files.Count == 0 ? 100 : session.NextFileIndex * 100 / session.Files.Count, Path.GetFileName(file));
                try
                {
                    List<LogEntry> fileEntries = ReadOneFile(file, o, token);
                    // 完整读完当前文件后才提交结果，确保中止后继续时不会产生半个文件的重复记录。
                    foreach (LogEntry e in fileEntries)
                    {
                        if (e.Time < o.Start || e.Time > o.End || !Matches(e, o)) continue;
                        // 日志目录里可能存在“副本”或重复归档；相同事件跨文件只统计一次。
                        if (!session.SeenEvents.Add(EventKey(e))) continue;
                        session.MatchedCount++;
                        if (session.Entries.Count < o.MaxResults) session.Entries.Add(e); else session.Truncated = true;
                    }
                    session.FilesRead++;
                    session.NextFileIndex++;
                }
                catch (OperationCanceledException) { return Snapshot(session, true); }
                catch (IOException) { session.FailedFiles++; session.NextFileIndex++; }
                catch (UnauthorizedAccessException) { session.FailedFiles++; session.NextFileIndex++; }
            }
            return Snapshot(session, false);
        }

        private List<LogEntry> ReadOneFile(string file, QueryOptions o, CancellationToken token)
        {
            var fileEntries = new List<LogEntry>();
            using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 32768, FileOptions.SequentialScan))
            using (var sr = new StreamReader(fs, new UTF8Encoding(false, false), true, 32768))
            {
                string line; long lineNo = 0; var context = new Queue<RawLogLine>(); string currentMode = "未识别";
                while ((line = sr.ReadLine()) != null)
                {
                    lineNo++;
                    if ((lineNo & 2047) == 0) { token.ThrowIfCancellationRequested(); Thread.Sleep(1); }
                    RawLogLine raw = parser.ParseAny(line, file, lineNo);
                    currentMode = DetectProductionMode(line, currentMode);
                    LogEntry e = parser.Parse(line, file, lineNo, o.NgKeywords);
                    if (e != null) { e.ProductionMode = currentMode; parser.Enrich(e, context); }
                    // 原因、MES结果和底板码可能分布在NG行前后；只补最近的少量NG事件，控制内存和CPU。
                    if (raw != null)
                    {
                        foreach (LogEntry pending in fileEntries.Skip(Math.Max(0, fileEntries.Count - 8)))
                            if ((raw.Time - pending.Time).TotalSeconds >= 0 && (raw.Time - pending.Time).TotalSeconds <= 3) parser.Enrich(pending, new[] { raw });
                    }
                    if (raw != null) { context.Enqueue(raw); while (context.Count > 24) context.Dequeue(); }
                    if (e != null) MergeOrAdd(fileEntries, e);
                }
            }
            token.ThrowIfCancellationRequested();
            return fileEntries;
        }

        private static QueryResult Snapshot(QuerySession session, bool stopped)
        {
            return new QueryResult
            {
                Entries = session.Entries.OrderByDescending(x => x.Time).ToList(),
                MatchedCount = session.MatchedCount,
                FilesRead = session.FilesRead,
                FailedFiles = session.FailedFiles,
                Truncated = session.Truncated,
                Stopped = stopped && session.NextFileIndex < session.Files.Count,
                TotalFiles = session.Files.Count,
                RemainingFiles = Math.Max(0, session.Files.Count - session.NextFileIndex)
            };
        }

        public IEnumerable<string> SelectFiles(QueryOptions o)
        {
            if (String.IsNullOrWhiteSpace(o.LogRoot) || !Directory.Exists(o.LogRoot)) return Enumerable.Empty<string>();
            var selectedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string folder in ResolveSearchFolders(o))
            {
                string[] folderFiles; try { folderFiles = Directory.GetFiles(folder, "*.log", SearchOption.TopDirectoryOnly); } catch { continue; }
                foreach (string file in folderFiles)
                    if (IsSelectedLevel(file, o) && CouldContainDate(file, o.Start.Date, o.End.Date)) selectedFiles.Add(file);
            }
            // 多天查询时优先读取较新的日志，中止后也能先看到最近结果。
            return selectedFiles.OrderByDescending(FileSortDate).ThenByDescending(x => Path.GetFileName(x), StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static IEnumerable<string> ResolveSearchFolders(QueryOptions o)
        {
            string root = Path.GetFullPath(o.LogRoot.Trim());
            string leaf = new DirectoryInfo(root).Name;
            var folders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 支持用户直接选择 ...\logs\info、error 或 debug，不再错误拼成 info\info。
            if (IsKnownLevelName(leaf))
            {
                if (IsFolderLevelEnabled(leaf, o)) folders.Add(root);
                return folders;
            }

            if (o.IncludeInfo && Directory.Exists(Path.Combine(root, "info"))) folders.Add(Path.Combine(root, "info"));
            if (o.IncludeError && Directory.Exists(Path.Combine(root, "error"))) folders.Add(Path.Combine(root, "error"));
            if (o.IncludeDebug && Directory.Exists(Path.Combine(root, "debug"))) folders.Add(Path.Combine(root, "debug"));

            // 兼容后续前辈把日志直接放在所选目录中的情况。
            try { if (Directory.GetFiles(root, "*.log", SearchOption.TopDirectoryOnly).Length > 0) folders.Add(root); } catch { }
            return folders;
        }

        private static bool IsKnownLevelName(string value)
        {
            return value.Equals("info", StringComparison.OrdinalIgnoreCase) || value.Equals("warn", StringComparison.OrdinalIgnoreCase) ||
                   value.Equals("error", StringComparison.OrdinalIgnoreCase) || value.Equals("debug", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsFolderLevelEnabled(string folderName, QueryOptions o)
        {
            if (folderName.Equals("error", StringComparison.OrdinalIgnoreCase)) return o.IncludeError;
            if (folderName.Equals("debug", StringComparison.OrdinalIgnoreCase)) return o.IncludeDebug;
            return o.IncludeInfo;
        }

        private static bool IsSelectedLevel(string file, QueryOptions o)
        {
            string folderName = new DirectoryInfo(Path.GetDirectoryName(file)).Name;
            if (IsKnownLevelName(folderName)) return IsFolderLevelEnabled(folderName, o);
            string name = Path.GetFileName(file) ?? String.Empty;
            if (name.StartsWith("error", StringComparison.OrdinalIgnoreCase)) return o.IncludeError;
            if (name.StartsWith("debug", StringComparison.OrdinalIgnoreCase)) return o.IncludeDebug;
            if (name.StartsWith("info", StringComparison.OrdinalIgnoreCase) || name.StartsWith("warn", StringComparison.OrdinalIgnoreCase)) return o.IncludeInfo;
            return o.IncludeInfo || o.IncludeError || o.IncludeDebug;
        }

        private static bool CouldContainDate(string file, DateTime start, DateTime end)
        {
            DateTime d;
            if (TryGetDateFromFileName(file, out d)) return d >= start && d <= end;
            // 没有日期的特殊文件才使用修改日期，并严格限制在用户选择的日期内，不再前后放宽一天。
            try { DateTime w = File.GetLastWriteTime(file).Date; return w >= start && w <= end; } catch { return false; }
        }

        private static bool TryGetDateFromFileName(string file, out DateTime value)
        {
            value = DateTime.MinValue;
            Match match = FileDatePattern.Match(Path.GetFileNameWithoutExtension(file) ?? String.Empty);
            return match.Success && DateTime.TryParseExact(match.Groups["date"].Value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out value);
        }

        private static DateTime FileSortDate(string file)
        {
            DateTime value;
            if (TryGetDateFromFileName(file, out value))
            {
                try { return value.Date.Add(File.GetLastWriteTime(file).TimeOfDay); } catch { return value; }
            }
            try { return File.GetLastWriteTime(file); } catch { return DateTime.MinValue; }
        }
        private static bool Matches(LogEntry e, QueryOptions o)
        {
            if (!String.IsNullOrEmpty(o.Category) && o.Category != "全部" && e.Category != o.Category) return false;
            if (!String.IsNullOrEmpty(o.Jig) && o.Jig != "全部" && (e.Jig ?? "").IndexOf(o.Jig.Replace("治具", ""), StringComparison.OrdinalIgnoreCase) < 0) return false;
            if (!Contains(e.Message, o.Keyword)) return false;
            if (!String.IsNullOrWhiteSpace(o.Code) && !Contains((e.BottomCode ?? "") + " " + (e.TopCode ?? "") + " " + (e.MainBoardCode ?? "") + " " + e.Message, o.Code)) return false;
            return true;
        }
        private static bool Contains(string source, string value) { return String.IsNullOrWhiteSpace(value) || (source ?? "").IndexOf(value.Trim(), StringComparison.OrdinalIgnoreCase) >= 0; }
        public static string EventKey(LogEntry e)
        {
            return e.Time.ToString("yyyyMMddHHmmssffff") + "|" + (e.MainBoardCode ?? "") + "|" + (e.BottomCode ?? "") + "|" + (e.Reason ?? e.Message ?? "");
        }

        /// <summary>同一主板码在数秒内出现“工站不匹配”和“MES=NG”时合并为一个NG事件，避免重复计数。</summary>
        private static void MergeOrAdd(List<LogEntry> list, LogEntry entry)
        {
            LogEntry same = list.LastOrDefault(x => !String.IsNullOrEmpty(x.MainBoardCode) && !String.IsNullOrEmpty(entry.MainBoardCode) &&
                String.Equals(x.MainBoardCode, entry.MainBoardCode, StringComparison.OrdinalIgnoreCase) && Math.Abs((entry.Time - x.Time).TotalSeconds) <= 3);
            if (same == null) { list.Add(entry); return; }
            if (String.IsNullOrEmpty(same.Jig)) same.Jig = entry.Jig;
            if (String.IsNullOrEmpty(same.BottomCode)) same.BottomCode = entry.BottomCode;
            if (String.IsNullOrEmpty(same.TopCode)) same.TopCode = entry.TopCode;
            if (String.IsNullOrEmpty(same.ProductionMode) || same.ProductionMode == "未识别") same.ProductionMode = entry.ProductionMode;
            if (entry.Reason.IndexOf("不匹配", StringComparison.OrdinalIgnoreCase) >= 0 || entry.Reason.IndexOf("接口返回", StringComparison.OrdinalIgnoreCase) >= 0) same.Reason = entry.Reason;
            if (!same.Message.Contains(entry.Message)) same.Message += " | " + entry.Message;
            if (!String.IsNullOrEmpty(entry.Context) && !same.Context.Contains(entry.Context)) same.Context = (same.Context + Environment.NewLine + entry.Context).Trim();
            if (entry.Category == "工站/接口") same.Category = entry.Category;
        }

        /// <summary>实时模式只读取上次位置之后新增的字节，不反复扫描历史。</summary>
        public List<LogEntry> ReadAppended(QueryOptions o, Dictionary<string, long> positions)
        {
            var list = new List<LogEntry>();
            foreach (string file in SelectFiles(o))
            {
                try
                {
                    long old; if (!positions.TryGetValue(file, out old)) { positions[file] = new FileInfo(file).Length; continue; }
                    using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 8192, FileOptions.SequentialScan))
                    {
                        if (fs.Length < old) old = 0; if (fs.Length == old) continue; fs.Position = old;
                        using (var sr = new StreamReader(fs, new UTF8Encoding(false, false), true, 8192))
                        {
                            string line; long n = 0; var context = new Queue<RawLogLine>(); var appendedEntries = new List<LogEntry>(); string currentMode = "未识别";
                            while ((line = sr.ReadLine()) != null)
                            {
                                n++; RawLogLine raw = parser.ParseAny(line, file, n); currentMode = DetectProductionMode(line, currentMode); LogEntry e = parser.Parse(line, file, n, o.NgKeywords);
                                if (e != null) { e.ProductionMode = currentMode; parser.Enrich(e, context); }
                                if (raw != null) foreach (LogEntry pending in appendedEntries.Skip(Math.Max(0, appendedEntries.Count - 8)))
                                    if ((raw.Time - pending.Time).TotalSeconds >= 0 && (raw.Time - pending.Time).TotalSeconds <= 3) parser.Enrich(pending, new[] { raw });
                                if (raw != null) { context.Enqueue(raw); while (context.Count > 24) context.Dequeue(); }
                                if (e != null) MergeOrAdd(appendedEntries, e);
                            }
                            foreach (LogEntry e in appendedEntries) if (e.Time >= o.Start && e.Time <= o.End && Matches(e, o)) list.Add(e);
                            positions[file] = fs.Position;
                        }
                    }
                }
                catch { /* 实时读取失败时跳过本轮，绝不影响机台进程。 */ }
            }
            return list.OrderByDescending(x => x.Time).ToList();
        }

        public void InitializePositions(QueryOptions o, Dictionary<string, long> positions)
        {
            positions.Clear(); foreach (string f in SelectFiles(o)) try { positions[f] = new FileInfo(f).Length; } catch { }
        }

        private static string DetectProductionMode(string line, string current)
        {
            if ((line ?? String.Empty).IndexOf(RunStatisticsService.TrialUrl, StringComparison.OrdinalIgnoreCase) >= 0) return "量试";
            if ((line ?? String.Empty).IndexOf(RunStatisticsService.ProductionUrl, StringComparison.OrdinalIgnoreCase) >= 0) return "量产";
            return current;
        }

        /// <summary>仅删除指定天数以前的历史文件；今天及正在写入的文件永不删除。</summary>
        public int DeleteOldFiles(string root, int days, out List<string> failures)
        {
            failures = new List<string>(); int count = 0; DateTime cutoff = DateTime.Now.Date.AddDays(-Math.Max(7, days));
            if (!Directory.Exists(root)) return 0;
            foreach (string f in Directory.GetFiles(root, "*.log", SearchOption.AllDirectories))
            {
                try { if (File.GetLastWriteTime(f).Date < cutoff && File.GetLastWriteTime(f).Date < DateTime.Now.Date) { File.Delete(f); count++; } }
                catch (Exception ex) { failures.Add(Path.GetFileName(f) + "：" + ex.Message); }
            }
            return count;
        }
    }
}
