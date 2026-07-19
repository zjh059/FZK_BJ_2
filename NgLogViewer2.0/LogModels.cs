using System;
using System.Collections.Generic;
namespace NgLogViewer
{
    public sealed class LogEntry
    {
        public DateTime Time { get; set; }
        public string Level { get; set; }
        public string Category { get; set; }
        public string ProductionMode { get; set; }
        public string Jig { get; set; }
        public string BottomCode { get; set; }
        public string TopCode { get; set; }
        public string MainBoardCode { get; set; }
        public string Reason { get; set; }
        /// <summary>NG行附近的关联日志，用于补全接口返回、工站不匹配、码信息等原因。</summary>
        public string Context { get; set; }
        public string Message { get; set; }
        public string SourceFile { get; set; }
        public long LineNumber { get; set; }

        /// <summary>
        /// 表格只显示精简后的文件名，完整路径仍保存在 SourceFile 中，
        /// 鼠标悬停、复制、导出和“打开”按钮都继续使用完整路径。
        /// </summary>
        public string SourceDisplayName
        {
            get
            {
                string name = System.IO.Path.GetFileName(SourceFile ?? String.Empty);
                if (name.Length <= 38) return name;
                return name.Substring(0, 19) + "…" + name.Substring(name.Length - 16);
            }
        }
    }
    internal sealed class RawLogLine
    {
        public DateTime Time { get; set; }
        public string Level { get; set; }
        public string Message { get; set; }
        public string SourceFile { get; set; }
        public long LineNumber { get; set; }
    }
    public sealed class QueryOptions
    {
        public string LogRoot { get; set; }
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public string Category { get; set; }
        public string Keyword { get; set; }
        public string Code { get; set; }
        public string Jig { get; set; }
        public bool IncludeInfo { get; set; }
        public bool IncludeError { get; set; }
        public bool IncludeDebug { get; set; }
        public int MaxResults { get; set; }
        public IList<string> NgKeywords { get; set; }
    }
    public sealed class QueryResult
    {
        public List<LogEntry> Entries { get; set; } = new List<LogEntry>();
        public int MatchedCount { get; set; }
        public int FilesRead { get; set; }
        public int FailedFiles { get; set; }
        public bool Truncated { get; set; }
        public bool Stopped { get; set; }
        public int TotalFiles { get; set; }
        public int RemainingFiles { get; set; }
    }

    /// <summary>
    /// 保存一次查询已经完成到哪个文件。用户中止后保留此对象，
    /// 下次可从未完成的文件继续，不必重新扫描前面的日志。
    /// </summary>
    public sealed class QuerySession
    {
        public QueryOptions Options { get; set; }
        public List<string> Files { get; set; } = new List<string>();
        public int NextFileIndex { get; set; }
        internal HashSet<string> SeenEvents { get; private set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        internal List<LogEntry> Entries { get; private set; } = new List<LogEntry>();
        internal int MatchedCount { get; set; }
        internal int FilesRead { get; set; }
        internal int FailedFiles { get; set; }
        internal bool Truncated { get; set; }
    }
}
