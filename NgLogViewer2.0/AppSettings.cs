using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
namespace NgLogViewer
{
    public sealed class AppSettings
    {
        private static readonly string FilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "NgLogViewer.settings.ini");
        public string LogRoot { get; set; }
        public string Keywords { get; set; }
        public int RefreshSeconds { get; set; }
        public int MaxResults { get; set; }
        public int CleanupDays { get; set; }
        public AppSettings()
        {
            LogRoot = @"D:\A_BJ_HJ_LH\6.30_XC\镀金片焊接\FZK.Shell\bin\Debug\logs";
            Keywords = "失败;异常;超时;耗尽;不符;不匹配;接口返回;未读到;为空;错误;Error;Exception;NG";
            RefreshSeconds = 5; MaxResults = 3000; CleanupDays = 90;
        }
        public IList<string> KeywordList { get { return Keywords.Split(new[] { ';', '；', ',' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList(); } }
        public static AppSettings Load()
        {
            var s = new AppSettings();
            try
            {
                if (!File.Exists(FilePath)) return s;
                foreach (string raw in File.ReadAllLines(FilePath))
                {
                    int p = raw.IndexOf('='); if (p < 1) continue;
                    string key = raw.Substring(0, p).Trim(), value = raw.Substring(p + 1).Trim(); int n;
                    if (key.Equals("LogRoot", StringComparison.OrdinalIgnoreCase)) s.LogRoot = value;
                    else if (key.Equals("Keywords", StringComparison.OrdinalIgnoreCase)) s.Keywords = value;
                    else if (key.Equals("RefreshSeconds", StringComparison.OrdinalIgnoreCase) && int.TryParse(value, out n)) s.RefreshSeconds = Math.Max(3, Math.Min(60, n));
                    else if (key.Equals("MaxResults", StringComparison.OrdinalIgnoreCase) && int.TryParse(value, out n)) s.MaxResults = Math.Max(100, Math.Min(20000, n));
                    else if (key.Equals("CleanupDays", StringComparison.OrdinalIgnoreCase) && int.TryParse(value, out n)) s.CleanupDays = Math.Max(7, Math.Min(3650, n));
                }
            }
            catch { }
            // 版本升级兼容：用户已有settings.ini时自动补充关键现场规则，不覆盖其自定义内容。
            if (s.Keywords.IndexOf("不匹配", StringComparison.OrdinalIgnoreCase) < 0) s.Keywords += ";不匹配";
            if (s.Keywords.IndexOf("接口返回", StringComparison.OrdinalIgnoreCase) < 0) s.Keywords += ";接口返回";
            return s;
        }
        public void Save()
        {
            File.WriteAllLines(FilePath, new[] { "# NG日志查询工具配置", "LogRoot=" + LogRoot, "Keywords=" + Keywords, "RefreshSeconds=" + RefreshSeconds, "MaxResults=" + MaxResults, "CleanupDays=" + CleanupDays });
        }
    }
}
