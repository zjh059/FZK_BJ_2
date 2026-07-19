using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace NgLogViewer
{
    partial class NgSummaryForm
    {
        private IContainer components;
        private TableLayoutPanel rootPanel;
        private Label titleLabel;
        private GroupBox bottomGroup;
        private DataGridView bottomGrid;
        private GroupBox ngGroup;
        private DataGridView ngGrid;
        private DataGridViewButtonColumn openSourceColumn;

        protected override void Dispose(bool disposing) { if (disposing && components != null) components.Dispose(); base.Dispose(disposing); }

        private void InitializeComponent()
        {
            this.components = new Container(); this.rootPanel = new TableLayoutPanel(); this.titleLabel = new Label(); this.bottomGroup = new GroupBox(); this.bottomGrid = new DataGridView(); this.ngGroup = new GroupBox(); this.ngGrid = new DataGridView(); this.openSourceColumn = new DataGridViewButtonColumn(); this.SuspendLayout();
            this.rootPanel.BackColor = Color.FromArgb(242, 246, 250); this.rootPanel.ColumnCount = 1; this.rootPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F)); this.rootPanel.Dock = DockStyle.Fill; this.rootPanel.Padding = new Padding(14); this.rootPanel.RowCount = 3; this.rootPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F)); this.rootPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 38F)); this.rootPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 62F));
            this.titleLabel.BackColor = Color.White; this.titleLabel.Dock = DockStyle.Fill; this.titleLabel.Font = new Font("Microsoft YaHei UI", 13F, FontStyle.Bold); this.titleLabel.ForeColor = Color.FromArgb(190, 0, 0); this.titleLabel.Padding = new Padding(12, 10, 0, 0); this.titleLabel.Text = "所选批次 NG 汇总";
            this.bottomGroup.Dock = DockStyle.Fill; this.bottomGroup.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold); this.bottomGroup.Text = "按底板码统计 NG"; this.bottomGroup.Controls.Add(this.bottomGrid); SetupGrid(this.bottomGrid);
            this.ngGroup.Dock = DockStyle.Fill; this.ngGroup.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold); this.ngGroup.Text = "NG 明细（双击查看完整原因；点击‘打开文件’打开所属日志）"; this.ngGroup.Controls.Add(this.ngGrid); SetupGrid(this.ngGrid); this.openSourceColumn.HeaderText = "日志文件"; this.openSourceColumn.Name = "OpenSourceAction"; this.openSourceColumn.ReadOnly = true; this.openSourceColumn.Text = "打开文件"; this.openSourceColumn.UseColumnTextForButtonValue = true; this.openSourceColumn.Width = 82; this.openSourceColumn.AutoSizeMode = DataGridViewAutoSizeColumnMode.None; this.ngGrid.Columns.Add(this.openSourceColumn); this.ngGrid.DefaultCellStyle.ForeColor = Color.FromArgb(200, 20, 30); this.ngGrid.CellDoubleClick += new DataGridViewCellEventHandler(this.NgGrid_CellDoubleClick); this.ngGrid.CellContentClick += new DataGridViewCellEventHandler(this.NgGrid_CellContentClick);
            this.rootPanel.Controls.Add(this.titleLabel, 0, 0); this.rootPanel.Controls.Add(this.bottomGroup, 0, 1); this.rootPanel.Controls.Add(this.ngGroup, 0, 2); this.Controls.Add(this.rootPanel);
            this.ClientSize = new Size(1180, 720); this.Font = new Font("Microsoft YaHei UI", 9F); this.MinimumSize = new Size(900, 600); this.StartPosition = FormStartPosition.CenterParent; this.Text = "批次 NG 与底板码统计"; this.ResumeLayout(false);
        }

        private static void SetupGrid(DataGridView grid) { grid.AllowUserToAddRows = false; grid.AllowUserToDeleteRows = false; grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(247, 250, 253); grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill; grid.BackgroundColor = Color.White; grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(35, 63, 92); grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White; grid.ColumnHeadersDefaultCellStyle.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold); grid.ColumnHeadersHeight = 34; grid.Dock = DockStyle.Fill; grid.EnableHeadersVisualStyles = false; grid.ReadOnly = true; grid.RowHeadersVisible = false; grid.RowTemplate.Height = 28; grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect; }
    }
}
