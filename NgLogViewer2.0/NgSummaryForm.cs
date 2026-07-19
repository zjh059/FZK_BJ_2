using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

namespace NgLogViewer
{
    /// <summary>独立展示所选批次的底板 NG 汇总和每一条 NG，避免主统计页被小表格挤占。</summary>
    public sealed partial class NgSummaryForm : Form
    {
        public NgSummaryForm(RunStatistics run, IList<BottomNgSummary> summaries, Icon icon)
        {
            InitializeComponent();
            if (icon != null) Icon = icon;
            titleLabel.Text = run.StartTime.ToString("yyyy-MM-dd") + "　" + run.SequenceName + "　" + run.ProductionMode +
                "　焊接 " + run.TotalCount + "　OK " + run.OkCount + "　NG " + run.NgCount;
            bottomGrid.DataSource = summaries.ToList();
            ngGrid.DataSource = run.Records.Where(x => x.Result == "NG").OrderByDescending(x => x.Time).ToList();
        }

        private void NgGrid_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || (e.ColumnIndex >= 0 && ngGrid.Columns[e.ColumnIndex].Name == "OpenSourceAction")) return;
            WeldingRecord record = ngGrid.Rows[e.RowIndex].DataBoundItem as WeldingRecord;
            if (record == null) return;
            string detail = "时间：" + record.Time.ToString("yyyy-MM-dd HH:mm:ss.ffff") + Environment.NewLine +
                "模式：" + record.ProductionMode + Environment.NewLine + "治具：" + record.Jig + Environment.NewLine +
                "底板码：" + record.BottomCode + Environment.NewLine + "SP码：" + record.SpCode + Environment.NewLine +
                "NG原因：" + record.Reason + Environment.NewLine + "来源：" + record.SourceFile + "（第" + record.LineNumber + "行）" +
                Environment.NewLine + Environment.NewLine + "关联日志：" + Environment.NewLine + record.Context;
            using (var form = new LogDetailForm(detail, Icon)) form.ShowDialog(this);
        }

        private void NgGrid_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0 || ngGrid.Columns[e.ColumnIndex].Name != "OpenSourceAction") return;
            WeldingRecord record = ngGrid.Rows[e.RowIndex].DataBoundItem as WeldingRecord;
            if (record == null || String.IsNullOrWhiteSpace(record.SourceFile) || !File.Exists(record.SourceFile))
            {
                MessageBox.Show("该条记录的日志文件不存在或已被移动。", "无法打开", MessageBoxButtons.OK, MessageBoxIcon.Warning); return;
            }
            try { Process.Start(new ProcessStartInfo { FileName = record.SourceFile, UseShellExecute = true }); }
            catch (Exception ex) { MessageBox.Show("打开日志失败：" + ex.Message, "无法打开", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }
    }
}
