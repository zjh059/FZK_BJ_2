using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
namespace NgLogViewer
{
    public sealed partial class MainForm : Form
    {
        private readonly LogQueryService service = new LogQueryService();
        private readonly BindingList<LogEntry> rows = new BindingList<LogEntry>();
        private readonly Dictionary<string, long> livePositions = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> liveSeenEvents = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private AppSettings settings;
        private CancellationTokenSource queryCts;
        private QuerySession pausedQuery;
        private string lastSortProperty = "Time";
        private ListSortDirection lastSortDirection = ListSortDirection.Descending;
        private bool liveReading;

        public MainForm()
        {
            settings = AppSettings.Load();
            InitializeComponent();
            // 日期、数据绑定属于运行时状态，不写死在设计器文件里。
            startDatePicker.Value = DateTime.Today;
            startTimePicker.Value = DateTime.Today;
            endDatePicker.Value = DateTime.Today;
            endTimePicker.Value = DateTime.Now;
            grid.DataSource = rows;
            try { using (Stream iconStream = typeof(MainForm).Assembly.GetManifestResourceStream("NgLogViewer.FI-app-icon.ico")) if (iconStream != null) Icon = new Icon(iconStream); } catch { }
            ApplySettings();
            // “焊接统计”是独立页，嵌入标准 StatisticsForm，避免再从 NG 查询页按钮进入。
            var statistics = new StatisticsForm(CurrentLogRoot(), Icon) { TopLevel = false, FormBorderStyle = FormBorderStyle.None, Dock = DockStyle.Fill };
            statisticsHost.Controls.Add(statistics);
            statistics.Show();
        }

        // 下面这些事件处理方法由 MainForm.Designer.cs 中的控件事件连接。
        // 界面布局放在 Designer，业务动作留在本文件，方便以后拖动控件和阅读逻辑。
        private async void QueryButton_Click(object sender, EventArgs e) { await QueryAsync(false); }
        private void CancelButton_Click(object sender, EventArgs e) { if (queryCts != null) queryCts.Cancel(); }
        private async void ContinueButton_Click(object sender, EventArgs e) { await ContinueQueryAsync(); }
        private void ClearButton_Click(object sender, EventArgs e)
        {
            pausedQuery = null; continueButton.Enabled = false; rows.Clear(); UpdateSummary();
        }
        private void CopyButton_Click(object sender, EventArgs e) { CopySelectedRows(); }
        private void OpenLogsButton_Click(object sender, EventArgs e) { OpenFolder(CurrentLogRoot()); }
        private void OpenSettingsFolderButton_Click(object sender, EventArgs e) { OpenFolder(pathBox.Text.Trim()); }
        private void Grid_CellDoubleClick(object sender, DataGridViewCellEventArgs e) { ShowDetail(e.RowIndex); }
        private void CopyCurrentCellMenuItem_Click(object sender, EventArgs e) { CopyCurrentCell(); }
        private void CopySelectedRowsMenuItem_Click(object sender, EventArgs e) { CopySelectedRows(); }
        private void CopyReasonMenuItem_Click(object sender, EventArgs e) { LogEntry x = SelectedEntry(); if (x != null) SafeCopy(x.Reason ?? ""); }
        private void CopyMainBoardMenuItem_Click(object sender, EventArgs e) { LogEntry x = SelectedEntry(); if (x != null) SafeCopy(x.MainBoardCode ?? ""); }
        private void OpenSourceMenuItem_Click(object sender, EventArgs e) { OpenSourceFile(SelectedEntry()); }
        private void LocateSourceMenuItem_Click(object sender, EventArgs e) { OpenSelectedSource(); }
        private async Task QueryAsync(bool fromLive)
        {
            if (queryCts != null) return; QueryOptions o = BuildOptions();
            if (o.Start > o.End) { MessageBox.Show("开始时间不能晚于结束时间，请重新选择日期和时间。", "时间范围错误", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            if (!Directory.Exists(o.LogRoot)) { MessageBox.Show("日志目录不存在，请在‘设置与安全清理’中选择正确目录。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            if (!o.IncludeInfo && !o.IncludeError && !o.IncludeDebug) { MessageBox.Show("请至少勾选一种日志类型。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            // 点击“开始查询”始终建立新会话；旧的暂停进度会被明确丢弃。
            pausedQuery = service.CreateSession(o);
            await RunQuerySessionAsync(pausedQuery);
        }

        private async Task ContinueQueryAsync()
        {
            if (queryCts != null || pausedQuery == null) return;
            await RunQuerySessionAsync(pausedQuery);
        }

        private async Task RunQuerySessionAsync(QuerySession session)
        {
            queryCts = new CancellationTokenSource(); queryButton.Enabled = false; cancelButton.Enabled = true; continueButton.Enabled = false; statusLabel.Text = "正在后台低优先级只读查询...";
            try
            {
                var progress = new Progress<Tuple<int, string>>(x => statusLabel.Text = "读取进度 " + x.Item1 + "%：" + x.Item2);
                CancellationToken token = queryCts.Token;
                QueryResult r = await Task.Run(() => RunLowPriority(() => service.ContinueQuery(session, token, (n, f) => ((IProgress<Tuple<int, string>>)progress).Report(Tuple.Create(n, f)))));
                ReplaceRows(r.Entries);
                UpdateSummary(r);
                if (r.Stopped)
                {
                    pausedQuery = session; continueButton.Enabled = true;
                    statusLabel.Text = "查询已中止：已处理 " + (r.TotalFiles - r.RemainingFiles) + "/" + r.TotalFiles + " 个文件，剩余 " + r.RemainingFiles + " 个。当前结果已保留，可点“继续剩余”。";
                }
                else
                {
                    pausedQuery = null; continueButton.Enabled = false;
                    statusLabel.Text = r.TotalFiles == 0
                        ? "没有找到符合日期和日志类型的文件。可选择 logs 根目录，也可直接选择 info、error 或 debug 文件夹。"
                        : "查询完成。只读了 " + r.FilesRead + "/" + r.TotalFiles + " 个文件" + (r.FailedFiles > 0 ? "，安全跳过 " + r.FailedFiles + " 个占用/无权限文件" : "") + "。";
                }
            }
            catch (Exception ex) { MessageBox.Show("查询失败：" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            finally
            {
                queryCts.Dispose(); queryCts = null; queryButton.Enabled = true; cancelButton.Enabled = false;
                continueButton.Enabled = pausedQuery != null && pausedQuery.NextFileIndex < pausedQuery.Files.Count;
            }
        }

        private void ReplaceRows(IEnumerable<LogEntry> entries)
        {
            rows.RaiseListChangedEvents = false; rows.Clear(); foreach (LogEntry e in entries) rows.Add(e); rows.RaiseListChangedEvents = true; rows.ResetBindings();
        }

        private QueryOptions BuildOptions()
        {
            DateTime start = startDatePicker.Value.Date.Add(startTimePicker.Value.TimeOfDay);
            DateTime end = endDatePicker.Value.Date.Add(endTimePicker.Value.TimeOfDay);
            return new QueryOptions { LogRoot = CurrentLogRoot(), Start = start, End = end, Category = Convert.ToString(categoryBox.SelectedItem), Jig = Convert.ToString(jigBox.SelectedItem), Keyword = keywordBox.Text.Trim(), Code = codeBox.Text.Trim(), IncludeInfo = infoCheck.Checked, IncludeError = errorCheck.Checked, IncludeDebug = debugCheck.Checked, MaxResults = settings.MaxResults, NgKeywords = settings.KeywordList };
        }

        private string CurrentLogRoot() { return pathBox == null ? settings.LogRoot : pathBox.Text.Trim(); }
        private void LiveChanged(object sender, EventArgs e)
        {
            if (liveCheck.Checked)
            {
                QueryOptions o = BuildOptions(); o.Start = DateTime.Today; o.End = DateTime.Now.AddMinutes(1); service.InitializePositions(o, livePositions); liveSeenEvents.Clear(); foreach (LogEntry x in rows) liveSeenEvents.Add(LogQueryService.EventKey(x)); liveTimer.Interval = settings.RefreshSeconds * 1000; liveTimer.Start(); statusLabel.Text = "实时监控已开启：后台低优先级、每次只读取新增内容。";
            }
            else { liveTimer.Stop(); livePositions.Clear(); liveSeenEvents.Clear(); statusLabel.Text = "实时监控已关闭。"; }
        }
        private async void LiveTick(object sender, EventArgs e)
        {
            if (!liveCheck.Checked || liveReading) return; liveReading = true;
            try
            {
                QueryOptions o = BuildOptions(); o.Start = DateTime.Today.AddDays(-1); o.End = DateTime.Now.AddMinutes(1);
                List<LogEntry> appended = await Task.Run(() => RunLowPriority(() => service.ReadAppended(o, livePositions)));
                if (IsDisposed || Disposing || !liveCheck.Checked) return;
                foreach (LogEntry entry in appended.Reverse<LogEntry>())
                {
                    if (!liveSeenEvents.Add(LogQueryService.EventKey(entry))) continue;
                    rows.Insert(0, entry); while (rows.Count > settings.MaxResults) rows.RemoveAt(rows.Count - 1);
                }
                UpdateSummary(); statusLabel.Text = "实时监控中，最近检查：" + DateTime.Now.ToString("HH:mm:ss") + "（仅增量读取）";
            }
            catch { statusLabel.Text = "本轮实时读取失败，已安全跳过；下轮自动重试。"; }
            finally { liveReading = false; }
        }
        private void UpdateSummary(QueryResult r = null)
        {
            int total = r == null ? rows.Count : r.MatchedCount; var top = rows.GroupBy(x => x.Category).OrderByDescending(x => x.Count()).Take(4).Select(x => x.Key + " " + x.Count() + "条");
            summaryLabel.Text = "NG 总数：" + total + "　当前显示：" + rows.Count + "　" + String.Join("　", top) +
                (r != null && r.Truncated ? "　（结果过多，已限制显示条数）" : "") +
                (r != null && r.Stopped ? "　（已中止，剩余 " + r.RemainingFiles + " 个文件）" : "");
        }
        private void GridFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            if (grid.Columns[e.ColumnIndex].Name == "OpenSourceAction") return;
            // 所有查询结果统一显示为红色；表头使用独立样式，仍保持蓝底白字。
            // 字体加粗只在 Designer 中设置一次，避免逐单元格 new Font 造成资源泄漏。
            e.CellStyle.ForeColor = Color.FromArgb(190, 0, 0);
            e.CellStyle.SelectionForeColor = Color.FromArgb(190, 0, 0);
        }
        private void GridCellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0 || grid.Columns[e.ColumnIndex].Name != "OpenSourceAction") return;
            LogEntry entry = grid.Rows[e.RowIndex].DataBoundItem as LogEntry;
            OpenSourceFile(entry);
        }
        private void GridCellToolTipTextNeeded(object sender, DataGridViewCellToolTipTextNeededEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0 || grid.Columns[e.ColumnIndex].DataPropertyName != "SourceDisplayName") return;
            LogEntry entry = grid.Rows[e.RowIndex].DataBoundItem as LogEntry;
            if (entry != null) e.ToolTipText = entry.SourceFile;
        }
        private void ShowDetail(int row)
        {
            if (row < 0 || row >= rows.Count) return; LogEntry e = rows[row];
            using (var f = new LogDetailForm(FormatEntry(e), Icon)) f.ShowDialog(this);
        }
        private void ExportCsv(object sender, EventArgs e)
        {
            if (rows.Count == 0) { MessageBox.Show("当前没有可导出的记录。"); return; }
            using (var d = new SaveFileDialog { Filter = "CSV文件（WPS/Excel兼容）|*.csv", FileName = "NG查询_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".csv" }) if (d.ShowDialog() == DialogResult.OK)
            using (var sw = new StreamWriter(d.FileName, false, new UTF8Encoding(true)))
            {
                // sep=, 帮助部分WPS/Excel正确分列；时间与条码强制按文本，防止变成57:46.1或科学计数法。
                sw.WriteLine("sep=,"); sw.WriteLine("时间,级别,分类,生产模式,治具,底板码,导向板码,主板码,原因,关联上下文,来源文件,行号");
                foreach (LogEntry x in rows) sw.WriteLine(String.Join(",", ExcelText(x.Time.ToString("yyyy-MM-dd HH:mm:ss.ffff")), Csv(x.Level), Csv(x.Category), Csv(x.ProductionMode), ExcelText(x.Jig), ExcelText(x.BottomCode), ExcelText(x.TopCode), ExcelText(x.MainBoardCode), Csv(x.Reason), Csv(x.Context), Csv(x.SourceFile), x.LineNumber.ToString()));
            }
        }
        private void SaveSettings(object s, EventArgs e) { settings.LogRoot = pathBox.Text.Trim(); settings.Keywords = customKeywordsBox.Text.Trim(); settings.RefreshSeconds = (int)refreshNumber.Value; settings.MaxResults = (int)maxNumber.Value; settings.CleanupDays = (int)cleanupNumber.Value; settings.Save(); liveTimer.Interval = settings.RefreshSeconds * 1000; MessageBox.Show("设置已保存到EXE同目录。", "成功"); }
        private void CleanupOldLogs(object s, EventArgs e)
        {
            int days = (int)cleanupNumber.Value; if (MessageBox.Show("将删除“" + settings.LogRoot + "”中早于 " + days + " 天的历史 .log 文件。\r\n今天的日志会被强制保护。\r\n\r\n建议只在机台停机或低负载时操作。确定继续？", "高风险操作二次确认", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            List<string> failures; int count = service.DeleteOldFiles(settings.LogRoot, days, out failures); MessageBox.Show("已删除 " + count + " 个过期文件。" + (failures.Count > 0 ? "\r\n失败 " + failures.Count + " 个。" : ""), "清理完成");
        }
        private void BrowseFolder(object s, EventArgs e) { using (var d = new FolderBrowserDialog { Description = "可选择 logs 根目录，也可直接选择 info、error 或 debug 子目录", SelectedPath = pathBox.Text }) if (d.ShowDialog() == DialogResult.OK) pathBox.Text = d.SelectedPath; }
        private void OpenFolder(string path)
        {
            if (!Directory.Exists(path)) { MessageBox.Show("文件夹不存在：\r\n" + path, "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            try { Process.Start("explorer.exe", "\"" + path + "\""); } catch (Exception ex) { MessageBox.Show("打开失败：" + ex.Message); }
        }
        private void OpenSelectedSource()
        {
            LogEntry e = SelectedEntry(); if (e == null || !File.Exists(e.SourceFile)) { MessageBox.Show("请先选择一条有效记录。"); return; }
            try { Process.Start("explorer.exe", "/select,\"" + e.SourceFile + "\""); } catch (Exception ex) { MessageBox.Show("打开失败：" + ex.Message); }
        }
        private void OpenSourceFile(LogEntry entry)
        {
            if (entry == null || !File.Exists(entry.SourceFile)) { MessageBox.Show("原日志文件不存在或已被移动。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            try
            {
                // 使用系统默认的文本程序打开；查询工具本身仍然不会写入日志。
                Process.Start(new ProcessStartInfo { FileName = entry.SourceFile, UseShellExecute = true });
            }
            catch (Exception ex) { MessageBox.Show("打开原日志失败：" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }
        private LogEntry SelectedEntry() { return grid.CurrentRow == null ? null : grid.CurrentRow.DataBoundItem as LogEntry; }
        private void CopyCurrentCell() { if (grid.CurrentCell != null && grid.CurrentCell.Value != null) SafeCopy(Convert.ToString(grid.CurrentCell.FormattedValue)); }
        private void CopySelectedRows()
        {
            var selected = grid.SelectedRows.Cast<DataGridViewRow>().OrderBy(x => x.Index).Select(x => x.DataBoundItem as LogEntry).Where(x => x != null).ToList();
            if (selected.Count == 0) { LogEntry one = SelectedEntry(); if (one != null) selected.Add(one); }
            if (selected.Count == 0) { MessageBox.Show("请先选择记录。"); return; }
            if (SafeCopy(String.Join(Environment.NewLine + Environment.NewLine, selected.Select(FormatEntry)))) statusLabel.Text = "已复制 " + selected.Count + " 条记录，可直接粘贴到微信、记事本或日志搜索框。";
        }
        private static string FormatEntry(LogEntry e)
        {
            return "时间：" + e.Time.ToString("yyyy-MM-dd HH:mm:ss.ffff") + "\r\n级别：" + e.Level + "\r\n分类：" + e.Category + "\r\n生产模式：" + e.ProductionMode + "\r\n治具：" + e.Jig + "\r\n底板码：" + e.BottomCode + "\r\n导向板码：" + e.TopCode + "\r\n主板码：" + e.MainBoardCode + "\r\nNG原因：" + e.Reason + "\r\n来源：" + e.SourceFile + "（第" + e.LineNumber + "行）\r\n关联上下文：\r\n" + e.Context;
        }
        private bool SafeCopy(string text)
        {
            try { Clipboard.SetText(text ?? ""); return true; }
            catch (System.Runtime.InteropServices.ExternalException) { MessageBox.Show("剪贴板正被微信、WPS或其他程序占用，请稍后再试。", "复制失败", MessageBoxButtons.OK, MessageBoxIcon.Warning); return false; }
        }
        private static T RunLowPriority<T>(Func<T> work)
        {
            Thread thread = Thread.CurrentThread; ThreadPriority old = thread.Priority;
            try { thread.Priority = ThreadPriority.BelowNormal; return work(); }
            finally { try { thread.Priority = old; } catch { } }
        }
        private void GridColumnHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.ColumnIndex < 0) return; string property = grid.Columns[e.ColumnIndex].DataPropertyName; if (String.IsNullOrEmpty(property)) return;
            ListSortDirection direction = property == lastSortProperty && lastSortDirection == ListSortDirection.Ascending ? ListSortDirection.Descending : ListSortDirection.Ascending;
            var prop = typeof(LogEntry).GetProperty(property); if (prop == null) return;
            IEnumerable<LogEntry> sorted = direction == ListSortDirection.Ascending ? rows.OrderBy(x => prop.GetValue(x, null)) : rows.OrderByDescending(x => prop.GetValue(x, null));
            var values = sorted.ToList(); rows.RaiseListChangedEvents = false; rows.Clear(); foreach (LogEntry x in values) rows.Add(x); rows.RaiseListChangedEvents = true; rows.ResetBindings();
            foreach (DataGridViewColumn c in grid.Columns) c.HeaderCell.SortGlyphDirection = SortOrder.None;
            grid.Columns[e.ColumnIndex].HeaderCell.SortGlyphDirection = direction == ListSortDirection.Ascending ? SortOrder.Ascending : SortOrder.Descending;
            lastSortProperty = property; lastSortDirection = direction;
        }
        private void ApplySettings() { pathBox.Text = settings.LogRoot; customKeywordsBox.Text = settings.Keywords; refreshNumber.Value = settings.RefreshSeconds; maxNumber.Value = settings.MaxResults; cleanupNumber.Value = settings.CleanupDays; liveTimer.Interval = settings.RefreshSeconds * 1000; }
        private void OnClosing(object s, FormClosingEventArgs e) { liveTimer.Stop(); if (queryCts != null) queryCts.Cancel(); }
        private static string Csv(string s) { return "\"" + (s ?? "").Replace("\"", "\"\"").Replace("\r", " ").Replace("\n", " ") + "\""; }
        private static string ExcelText(string s) { return Csv("=\"" + (s ?? "").Replace("\"", "\"\"") + "\""); }
    }
}
