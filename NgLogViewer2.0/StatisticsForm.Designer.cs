using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace NgLogViewer
{
    partial class StatisticsForm
    {
        private IContainer components;
        private TableLayoutPanel rootPanel;
        private FlowLayoutPanel sourcePanel;
        private TextBox logPathBox;
        private Button selectInfoFileButton;
        private Button selectLogFolderButton;
        private FlowLayoutPanel headerPanel;
        private Label totalTitleLabel;
        private Label totalValueLabel;
        private Label okTitleLabel;
        private Label okValueLabel;
        private Label ngTitleLabel;
        private Label ngValueLabel;
        private Label modeTitleLabel;
        private Label modeValueLabel;
        private Button refreshButton;
        private CheckBox liveCheck;
        private Label runTimeLabel;
        private FlowLayoutPanel historyFilterPanel;
        private DateTimePicker historyStartPicker;
        private DateTimePicker historyEndPicker;
        private Button queryHistoryButton;
        private DataGridView batchGrid;
        private Label selectedBatchLabel;
        private FlowLayoutPanel selectedBatchPanel;
        private Button showAllButton;
        private Button showOkButton;
        private Button showNgButton;
        private Label filterHintLabel;
        private SplitContainer splitContainer;
        private GroupBox weldingGroup;
        private DataGridView weldingGrid;
        private GroupBox bottomGroup;
        private DataGridView bottomGrid;
        private Label statusLabel;

        protected override void Dispose(bool disposing) { if (disposing && components != null) components.Dispose(); base.Dispose(disposing); }

        private void InitializeComponent()
        {
            this.components = new Container();
            this.rootPanel = new TableLayoutPanel(); this.sourcePanel = new FlowLayoutPanel(); this.logPathBox = new TextBox(); this.selectInfoFileButton = new Button(); this.selectLogFolderButton = new Button(); this.headerPanel = new FlowLayoutPanel();
            this.totalTitleLabel = new Label(); this.totalValueLabel = new Label(); this.okTitleLabel = new Label(); this.okValueLabel = new Label(); this.ngTitleLabel = new Label(); this.ngValueLabel = new Label(); this.modeTitleLabel = new Label(); this.modeValueLabel = new Label(); this.refreshButton = new Button();
            this.liveCheck = new CheckBox(); this.runTimeLabel = new Label(); this.historyFilterPanel = new FlowLayoutPanel(); this.historyStartPicker = new DateTimePicker(); this.historyEndPicker = new DateTimePicker(); this.queryHistoryButton = new Button(); this.batchGrid = new DataGridView(); this.selectedBatchPanel = new FlowLayoutPanel(); this.selectedBatchLabel = new Label(); this.showAllButton = new Button(); this.showOkButton = new Button(); this.showNgButton = new Button(); this.filterHintLabel = new Label(); this.splitContainer = new SplitContainer(); this.weldingGroup = new GroupBox(); this.weldingGrid = new DataGridView(); this.bottomGroup = new GroupBox(); this.bottomGrid = new DataGridView(); this.statusLabel = new Label();
            this.SuspendLayout();
            this.rootPanel.BackColor = Color.FromArgb(242, 246, 250); this.rootPanel.ColumnCount = 1; this.rootPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F)); this.rootPanel.Dock = DockStyle.Fill; this.rootPanel.Padding = new Padding(14, 10, 14, 10); this.rootPanel.RowCount = 8;
            this.rootPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F)); this.rootPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 54F)); this.rootPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F)); this.rootPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F)); this.rootPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 40F)); this.rootPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F)); this.rootPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 60F)); this.rootPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
            this.sourcePanel.BackColor = Color.White; this.sourcePanel.Dock = DockStyle.Fill; this.sourcePanel.Padding = new Padding(6, 0, 0, 0); this.sourcePanel.WrapContents = false; this.sourcePanel.Controls.Add(new Label { AutoSize = true, Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold), Margin = new Padding(3, 10, 4, 0), Text = "统计日志：" }); this.logPathBox.ReadOnly = true; this.logPathBox.Width = 570; this.logPathBox.Margin = new Padding(3, 6, 4, 3); this.sourcePanel.Controls.Add(this.logPathBox); this.selectInfoFileButton.AutoSize = true; this.selectInfoFileButton.Text = "选择单个 info 日志"; this.selectInfoFileButton.Margin = new Padding(4, 4, 3, 3); this.selectInfoFileButton.Click += new System.EventHandler(this.SelectInfoFileButton_Click); this.sourcePanel.Controls.Add(this.selectInfoFileButton); this.selectLogFolderButton.AutoSize = true; this.selectLogFolderButton.Text = "选择日志文件夹"; this.selectLogFolderButton.Margin = new Padding(4, 4, 3, 3); this.selectLogFolderButton.Click += new System.EventHandler(this.SelectLogFolderButton_Click); this.sourcePanel.Controls.Add(this.selectLogFolderButton);
            this.headerPanel.Dock = DockStyle.Fill; this.headerPanel.Padding = new Padding(6, 8, 0, 0); this.headerPanel.WrapContents = false;
            SetupTitle(this.totalTitleLabel, "焊接总数"); SetupValue(this.totalValueLabel, Color.FromArgb(25, 91, 155)); SetupTitle(this.okTitleLabel, "OK"); SetupValue(this.okValueLabel, Color.FromArgb(26, 135, 84)); SetupTitle(this.ngTitleLabel, "NG"); SetupValue(this.ngValueLabel, Color.FromArgb(190, 0, 0));
            SetupTitle(this.modeTitleLabel, "模式"); this.modeValueLabel.AutoSize = true; this.modeValueLabel.Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Bold); this.modeValueLabel.Margin = new Padding(0, 7, 12, 0); this.modeValueLabel.Text = "未识别";
            this.refreshButton.AutoSize = true; this.refreshButton.BackColor = Color.FromArgb(0, 120, 110); this.refreshButton.FlatStyle = FlatStyle.Flat; this.refreshButton.ForeColor = Color.White; this.refreshButton.Margin = new Padding(30, 2, 3, 3); this.refreshButton.Padding = new Padding(12, 4, 12, 4); this.refreshButton.Text = "刷新统计"; this.refreshButton.Click += new System.EventHandler(this.RefreshButton_Click);
            this.liveCheck.AutoSize = true; this.liveCheck.Checked = true; this.liveCheck.Margin = new Padding(14, 10, 0, 0); this.liveCheck.Text = "当前批次实时刷新";
            this.headerPanel.BackColor = Color.White; this.headerPanel.Controls.AddRange(new Control[] { this.totalTitleLabel, this.totalValueLabel, this.okTitleLabel, this.okValueLabel, this.ngTitleLabel, this.ngValueLabel, this.modeTitleLabel, this.modeValueLabel, this.refreshButton, this.liveCheck });
            this.runTimeLabel.Dock = DockStyle.Fill; this.runTimeLabel.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold); this.runTimeLabel.ForeColor = Color.FromArgb(50, 70, 90); this.runTimeLabel.Text = "正在确定本次跑线时间...";
            this.historyFilterPanel.Dock = DockStyle.Fill; this.historyFilterPanel.Controls.Add(new Label { AutoSize = true, Margin = new Padding(3, 9, 3, 0), Text = "第1步：选择历史批次日期" }); this.historyStartPicker.Format = DateTimePickerFormat.Short; this.historyStartPicker.Width = 120; this.historyStartPicker.Margin = new Padding(3, 5, 3, 3); this.historyFilterPanel.Controls.Add(this.historyStartPicker); this.historyFilterPanel.Controls.Add(new Label { AutoSize = true, Margin = new Padding(4, 9, 3, 0), Text = "至" }); this.historyEndPicker.Format = DateTimePickerFormat.Short; this.historyEndPicker.Width = 120; this.historyEndPicker.Margin = new Padding(3, 5, 3, 3); this.historyFilterPanel.Controls.Add(this.historyEndPicker); this.queryHistoryButton.AutoSize = true; this.queryHistoryButton.Text = "第2步：查询历史批次"; this.queryHistoryButton.Margin = new Padding(12, 4, 3, 3); this.queryHistoryButton.Click += new System.EventHandler(this.QueryHistoryButton_Click); this.historyFilterPanel.Controls.Add(this.queryHistoryButton); this.historyFilterPanel.Controls.Add(new Label { AutoSize = true, ForeColor = Color.DimGray, Margin = new Padding(14, 9, 0, 0), Text = "第3步：单击批次查看；点击任意表头可排序；双击 NG 数量打开汇总" });
            SetupGrid(this.batchGrid); this.batchGrid.SelectionChanged += new System.EventHandler(this.BatchGrid_SelectionChanged); this.batchGrid.CellDoubleClick += new DataGridViewCellEventHandler(this.BatchGrid_CellDoubleClick); this.batchGrid.CellFormatting += new DataGridViewCellFormattingEventHandler(this.BatchGrid_CellFormatting); this.batchGrid.ColumnHeaderMouseClick += new DataGridViewCellMouseEventHandler(this.BatchGrid_ColumnHeaderMouseClick);
            this.selectedBatchPanel.Dock = DockStyle.Fill; this.selectedBatchPanel.WrapContents = false; this.selectedBatchLabel.AutoSize = true; this.selectedBatchLabel.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold); this.selectedBatchLabel.ForeColor = Color.FromArgb(35, 63, 92); this.selectedBatchLabel.Margin = new Padding(3, 5, 10, 0); this.selectedBatchLabel.Text = "请选择一个运行批次"; this.selectedBatchPanel.Controls.Add(this.selectedBatchLabel);
            SetupSmallButton(this.showAllButton, "显示全部", this.ShowAllButton_Click); SetupSmallButton(this.showOkButton, "只看 OK", this.ShowOkButton_Click); SetupSmallButton(this.showNgButton, "只看 NG", this.ShowNgButton_Click); this.showNgButton.ForeColor = Color.FromArgb(190, 0, 0); this.selectedBatchPanel.Controls.Add(this.showAllButton); this.selectedBatchPanel.Controls.Add(this.showOkButton); this.selectedBatchPanel.Controls.Add(this.showNgButton); this.filterHintLabel.AutoSize = true; this.filterHintLabel.ForeColor = Color.DimGray; this.filterHintLabel.Margin = new Padding(12, 6, 0, 0); this.selectedBatchPanel.Controls.Add(this.filterHintLabel);
            this.splitContainer.Dock = DockStyle.Fill; this.splitContainer.Orientation = Orientation.Horizontal; this.splitContainer.Panel2Collapsed = true;
            this.weldingGroup.Dock = DockStyle.Fill; this.weldingGroup.Text = "每次焊接记录（双击查看原因和关联日志）"; this.weldingGroup.Controls.Add(this.weldingGrid);
            SetupGrid(this.weldingGrid); this.weldingGrid.CellDoubleClick += new DataGridViewCellEventHandler(this.WeldingGrid_CellDoubleClick); this.weldingGrid.CellFormatting += new DataGridViewCellFormattingEventHandler(this.WeldingGrid_CellFormatting); this.weldingGrid.ColumnHeaderMouseClick += new DataGridViewCellMouseEventHandler(this.WeldingGrid_ColumnHeaderMouseClick);
            this.bottomGroup.Dock = DockStyle.Fill; this.bottomGroup.Text = "所选批次按底板码统计 NG"; this.bottomGroup.Controls.Add(this.bottomGrid); SetupGrid(this.bottomGrid);
            this.splitContainer.Panel1.Controls.Add(this.weldingGroup); this.splitContainer.Panel2.Controls.Add(this.bottomGroup);
            this.statusLabel.Dock = DockStyle.Fill; this.statusLabel.ForeColor = Color.DimGray; this.statusLabel.Text = "准备统计...";
            this.rootPanel.Controls.Add(this.sourcePanel, 0, 0); this.rootPanel.Controls.Add(this.headerPanel, 0, 1); this.rootPanel.Controls.Add(this.runTimeLabel, 0, 2); this.rootPanel.Controls.Add(this.historyFilterPanel, 0, 3); this.rootPanel.Controls.Add(this.batchGrid, 0, 4); this.rootPanel.Controls.Add(this.selectedBatchPanel, 0, 5); this.rootPanel.Controls.Add(this.splitContainer, 0, 6); this.rootPanel.Controls.Add(this.statusLabel, 0, 7);
            this.AutoScroll = true; this.ClientSize = new Size(1180, 760); this.Controls.Add(this.rootPanel); this.Font = new Font("Microsoft YaHei UI", 9F); this.MinimumSize = new Size(560, 360); this.StartPosition = FormStartPosition.CenterParent; this.Text = "本次机台运行焊接统计"; this.Load += new System.EventHandler(this.StatisticsForm_Load);
            this.ResumeLayout(false);
        }

        private static void SetupTitle(Label label, string text) { label.AutoSize = true; label.Font = new Font("Microsoft YaHei UI", 11F, FontStyle.Bold); label.Margin = new Padding(12, 7, 4, 0); label.Text = text + "："; }
        private static void SetupValue(Label label, Color color) { label.AutoSize = true; label.Font = new Font("Microsoft YaHei UI", 18F, FontStyle.Bold); label.ForeColor = color; label.Margin = new Padding(0, 0, 12, 0); label.Text = "0"; }
        private static void SetupGrid(DataGridView grid) { grid.AllowUserToAddRows = false; grid.AllowUserToDeleteRows = false; grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(247, 250, 253); grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill; grid.BackgroundColor = Color.White; grid.BorderStyle = BorderStyle.FixedSingle; grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(35, 63, 92); grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White; grid.ColumnHeadersDefaultCellStyle.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold); grid.ColumnHeadersHeight = 34; grid.Dock = DockStyle.Fill; grid.EnableHeadersVisualStyles = false; grid.GridColor = Color.FromArgb(220, 228, 235); grid.ReadOnly = true; grid.RowHeadersVisible = false; grid.RowTemplate.Height = 28; grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect; }
        private static void SetupSmallButton(Button button, string text, System.EventHandler click) { button.AutoSize = true; button.Margin = new Padding(3, 1, 3, 1); button.Padding = new Padding(5, 0, 5, 0); button.Text = text; button.Click += click; }
    }
}
