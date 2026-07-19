using System;
using System.Drawing;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace NgLogViewer
{
    public sealed partial class StatisticsForm : Form
    {
        private string logRoot;
        private readonly RunStatisticsService service = new RunStatisticsService();
        private RunStatistics current;
        private RunStatistics selectedRun;
        private string resultFilter = "ALL";
        private List<RunStatistics> displayedRuns = new List<RunStatistics>();
        private List<WeldingRecord> displayedRecords = new List<WeldingRecord>();
        private string batchSortProperty = "BatchName";
        private SortOrder batchSortOrder = SortOrder.Descending;
        private string recordSortProperty = "Time";
        private SortOrder recordSortOrder = SortOrder.Descending;
        private bool changingBatchSource;
        private readonly Timer liveTimer = new Timer();

        public StatisticsForm(string logRoot, Icon icon)
        {
            this.logRoot = logRoot;
            InitializeComponent();
            logPathBox.Text = logRoot;
            if (icon != null) Icon = icon;
            historyStartPicker.Value = DateTime.Today.AddDays(-7);
            historyEndPicker.Value = DateTime.Today;
            liveTimer.Interval = 10000;
            liveTimer.Tick += async (s, e) => { if (liveCheck.Checked) await RefreshCurrentAsync(false); };
        }

        private async void StatisticsForm_Load(object sender, EventArgs e) { liveTimer.Start(); await RefreshCurrentAsync(true); await QueryHistoryAsync(); }
        private async void RefreshButton_Click(object sender, EventArgs e) { await RefreshCurrentAsync(true); }
        private async void QueryHistoryButton_Click(object sender, EventArgs e) { await QueryHistoryAsync(); }
        private async void SelectInfoFileButton_Click(object sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog { Filter = "info日志文件|info*.log|日志文件|*.log|所有文件|*.*", Title = "选择要统计的 info 日志" })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                logRoot = dialog.FileName; logPathBox.Text = logRoot;
                // 从标准文件名自动带出日期，用户选完桌面日志后可直接点查询。
                Match date = Regex.Match(Path.GetFileName(dialog.FileName), @"\d{4}-\d{2}-\d{2}"); DateTime value;
                if (date.Success && DateTime.TryParse(date.Value, out value)) { historyStartPicker.Value = value; historyEndPicker.Value = value; }
                batchGrid.DataSource = null; weldingGrid.DataSource = null;
                await RefreshCurrentAsync(true); await QueryHistoryAsync();
            }
        }

        private async void SelectLogFolderButton_Click(object sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog { Description = "选择包含 info 日志的文件夹，或 logs 根目录" })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                logRoot = dialog.SelectedPath; logPathBox.Text = logRoot;
                await RefreshCurrentAsync(true); await QueryHistoryAsync();
            }
        }

        private async Task RefreshCurrentAsync(bool showErrors)
        {
            refreshButton.Enabled = false;
            statusLabel.Text = "正在只读分析 info 日志...";
            try
            {
                current = await Task.Run(() => service.ReadCurrentRun(logRoot));
                // 用户正在查看历史批次时不强行跳回当前批次；查看当前批次时刷新数字。
                if (selectedRun == null || selectedRun.StartTime == current.StartTime) ShowRunDetails(current);
                statusLabel.Text = "当前批次已刷新（" + DateTime.Now.ToString("HH:mm:ss") + "）。每 10 秒自动读取一次。";
            }
            catch (Exception ex) { statusLabel.Text = "当前批次刷新失败"; if (showErrors) MessageBox.Show(ex.Message, "统计失败", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            finally { refreshButton.Enabled = true; }
        }

        private async Task QueryHistoryAsync()
        {
            queryHistoryButton.Enabled = false; statusLabel.Text = "正在按日期切分历史运行批次...";
            try
            {
                DateTime start = historyStartPicker.Value.Date;
                DateTime end = historyEndPicker.Value.Date.AddDays(1).AddTicks(-1);
                List<RunStatistics> runs = await Task.Run(() => service.ReadRuns(logRoot, start, end));
                runs = runs.Where(x => x.TotalCount > 0).ToList(); // 隐藏“刚启动又立即初始化”形成的 0 件空批次。
                resultFilter = "ALL";
                displayedRuns = runs.OrderByDescending(x => x.StartTime).ToList();
                BindBatchRows();
                statusLabel.Text = "历史查询完成，共 " + runs.Count + " 个有效批次。单击批次查看；双击 NG 数量可打开 NG 汇总。";
                if (batchGrid.Rows.Count > 0) { batchGrid.Rows[0].Selected = true; ShowSelectedBatch(); }
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "历史统计失败", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            finally { queryHistoryButton.Enabled = true; }
        }

        private void BatchGrid_SelectionChanged(object sender, EventArgs e) { if (!changingBatchSource) ShowSelectedBatch(); }
        private void BatchGrid_ColumnHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.ColumnIndex < 0) return; string property = batchGrid.Columns[e.ColumnIndex].DataPropertyName;
            if (String.IsNullOrEmpty(property)) return;
            batchSortOrder = property == batchSortProperty && batchSortOrder == SortOrder.Ascending ? SortOrder.Descending : SortOrder.Ascending;
            batchSortProperty = property; SortBatchRows();
        }
        private void WeldingGrid_ColumnHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.ColumnIndex < 0) return; string property = weldingGrid.Columns[e.ColumnIndex].DataPropertyName;
            if (String.IsNullOrEmpty(property)) return;
            recordSortOrder = property == recordSortProperty && recordSortOrder == SortOrder.Ascending ? SortOrder.Descending : SortOrder.Ascending;
            recordSortProperty = property; SortWeldingRows();
        }
        private void BatchGrid_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            if (batchGrid.Columns[e.ColumnIndex].DataPropertyName == "NgCount") { resultFilter = "NG"; ShowSelectedBatch(); OpenNgSummary(); }
        }
        private void ShowAllButton_Click(object sender, EventArgs e) { resultFilter = "ALL"; ShowSelectedBatch(); }
        private void ShowOkButton_Click(object sender, EventArgs e) { resultFilter = "OK"; ShowSelectedBatch(); }
        private void ShowNgButton_Click(object sender, EventArgs e) { resultFilter = "NG"; ShowSelectedBatch(); OpenNgSummary(); }
        private void ShowSelectedBatch()
        {
            if (batchGrid.CurrentRow == null) return;
            RunStatistics run = batchGrid.CurrentRow.DataBoundItem as RunStatistics;
            if (run != null) ShowRunDetails(run);
        }
        private void ShowRunDetails(RunStatistics run)
        {
            selectedRun = run;
            bool isCurrent = current != null && current.StartTime == run.StartTime;
            totalTitleLabel.Text = isCurrent ? "当前焊接总数：" : run.SequenceName + "焊接总数：";
            totalValueLabel.Text = run.TotalCount.ToString(); okValueLabel.Text = run.OkCount.ToString(); ngValueLabel.Text = run.NgCount.ToString();
            modeValueLabel.Text = run.ProductionMode;
            modeValueLabel.ForeColor = run.HasModeConflict ? Color.Red : (run.ProductionMode == "量试" ? Color.FromArgb(180, 100, 0) : Color.FromArgb(0, 105, 145));
            runTimeLabel.Text = (isCurrent ? "当前" : run.SequenceName) + "：首件 " + run.FirstWeldTime.ToString("HH:mm:ss") + "　末件 " + run.LastWeldTime.ToString("HH:mm:ss") + "　有效焊接时长 " + run.Duration + "（超过5分钟无焊接的停顿不计）";
            selectedBatchLabel.Text = "所选：" + run.StartTime.ToString("yyyy-MM-dd") + " " + run.SequenceName + "　" + run.ProductionMode + "　总数 " + run.TotalCount + "　OK " + run.OkCount + "　NG " + run.NgCount;
            var records = resultFilter == "ALL" ? run.Records : run.Records.Where(x => x.Result == resultFilter).ToList();
            displayedRecords = records.ToList(); SortWeldingRows();
            filterHintLabel.Text = resultFilter == "ALL" ? "当前显示：全部 " + records.Count + " 条" : "当前只显示：" + resultFilter + " " + records.Count + " 条";
        }

        private void BindBatchRows()
        {
            changingBatchSource = true; DateTime selectedStart = selectedRun == null ? DateTime.MinValue : selectedRun.StartTime;
            batchGrid.DataSource = null; batchGrid.DataSource = displayedRuns;
            PrepareManualSort(batchGrid, batchSortProperty, batchSortOrder);
            foreach (DataGridViewRow row in batchGrid.Rows)
            {
                RunStatistics run = row.DataBoundItem as RunStatistics;
                if (run != null && run.StartTime == selectedStart) { row.Selected = true; batchGrid.CurrentCell = row.Cells[0]; break; }
            }
            changingBatchSource = false;
        }

        private void SortBatchRows()
        {
            bool asc = batchSortOrder == SortOrder.Ascending; IEnumerable<RunStatistics> sorted;
            switch (batchSortProperty)
            {
                case "SequenceName": sorted = asc ? displayedRuns.OrderBy(x => x.DailySequence) : displayedRuns.OrderByDescending(x => x.DailySequence); break;
                case "ProductionMode": sorted = asc ? displayedRuns.OrderBy(x => x.ProductionMode) : displayedRuns.OrderByDescending(x => x.ProductionMode); break;
                case "TotalCount": sorted = asc ? displayedRuns.OrderBy(x => x.TotalCount) : displayedRuns.OrderByDescending(x => x.TotalCount); break;
                case "OkCount": sorted = asc ? displayedRuns.OrderBy(x => x.OkCount) : displayedRuns.OrderByDescending(x => x.OkCount); break;
                case "NgCount": sorted = asc ? displayedRuns.OrderBy(x => x.NgCount) : displayedRuns.OrderByDescending(x => x.NgCount); break;
                case "EndName": sorted = asc ? displayedRuns.OrderBy(x => x.LastWeldTime) : displayedRuns.OrderByDescending(x => x.LastWeldTime); break;
                case "Duration": sorted = asc ? displayedRuns.OrderBy(x => x.EffectiveMinutes) : displayedRuns.OrderByDescending(x => x.EffectiveMinutes); break;
                default: sorted = asc ? displayedRuns.OrderBy(x => x.StartTime) : displayedRuns.OrderByDescending(x => x.StartTime); break;
            }
            displayedRuns = sorted.ToList(); BindBatchRows();
        }

        private void SortWeldingRows()
        {
            bool asc = recordSortOrder == SortOrder.Ascending; IEnumerable<WeldingRecord> sorted;
            switch (recordSortProperty)
            {
                case "Jig": sorted = asc ? displayedRecords.OrderBy(x => x.Jig) : displayedRecords.OrderByDescending(x => x.Jig); break;
                case "Result": sorted = asc ? displayedRecords.OrderBy(x => x.Result) : displayedRecords.OrderByDescending(x => x.Result); break;
                case "BottomCode": sorted = asc ? displayedRecords.OrderBy(x => x.BottomCode) : displayedRecords.OrderByDescending(x => x.BottomCode); break;
                case "SpCode": sorted = asc ? displayedRecords.OrderBy(x => x.SpCode) : displayedRecords.OrderByDescending(x => x.SpCode); break;
                case "Reason": sorted = asc ? displayedRecords.OrderBy(x => x.Reason) : displayedRecords.OrderByDescending(x => x.Reason); break;
                default: sorted = asc ? displayedRecords.OrderBy(x => x.Time) : displayedRecords.OrderByDescending(x => x.Time); break;
            }
            displayedRecords = sorted.ToList(); weldingGrid.DataSource = null; weldingGrid.DataSource = displayedRecords;
            PrepareManualSort(weldingGrid, recordSortProperty, recordSortOrder);
        }

        private static void PrepareManualSort(DataGridView grid, string property, SortOrder order)
        {
            foreach (DataGridViewColumn column in grid.Columns)
            {
                column.SortMode = DataGridViewColumnSortMode.Programmatic;
                column.HeaderCell.SortGlyphDirection = column.DataPropertyName == property ? order : SortOrder.None;
            }
        }

        private void OpenNgSummary()
        {
            if (selectedRun == null) return;
            using (var form = new NgSummaryForm(selectedRun, service.SummarizeBottomNg(selectedRun), Icon)) form.ShowDialog(this);
        }

        private void BatchGrid_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            if (batchGrid.Columns[e.ColumnIndex].DataPropertyName == "NgCount") e.CellStyle.ForeColor = Color.FromArgb(205, 25, 35);
            if (batchGrid.Columns[e.ColumnIndex].DataPropertyName == "ProductionMode" && Convert.ToString(e.Value).Contains("异常")) e.CellStyle.ForeColor = Color.Red;
        }

        private void WeldingGrid_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0) return;
            WeldingRecord record = weldingGrid.Rows[e.RowIndex].DataBoundItem as WeldingRecord;
            if (record != null && record.Result == "NG") e.CellStyle.ForeColor = Color.FromArgb(205, 25, 35);
        }

        private void WeldingGrid_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            WeldingRecord record = weldingGrid.Rows[e.RowIndex].DataBoundItem as WeldingRecord;
            if (record == null) return;
            string detail = "时间：" + record.Time.ToString("yyyy-MM-dd HH:mm:ss.ffff") + Environment.NewLine +
                "治具：" + record.Jig + Environment.NewLine + "结果：" + record.Result + Environment.NewLine +
                "底板码：" + record.BottomCode + Environment.NewLine + "SP码：" + record.SpCode + Environment.NewLine +
                "原因：" + record.Reason + Environment.NewLine + "来源：" + record.SourceFile + "（第" + record.LineNumber + "行）" +
                Environment.NewLine + Environment.NewLine + "关联日志：" + Environment.NewLine + record.Context;
            using (var form = new LogDetailForm(detail, Icon)) form.ShowDialog(this);
        }
    }
}
