using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace NgLogViewer
{
    partial class LogDetailForm
    {
        private IContainer components = null;
        private TextBox detailTextBox;
        private Button copyAllButton;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null)) components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.detailTextBox = new TextBox();
            this.copyAllButton = new Button();
            this.SuspendLayout();
            this.detailTextBox.Dock = DockStyle.Fill;
            this.detailTextBox.Font = new Font("Consolas", 10F);
            this.detailTextBox.Location = new Point(0, 0);
            this.detailTextBox.Multiline = true;
            this.detailTextBox.Name = "detailTextBox";
            this.detailTextBox.ReadOnly = true;
            this.detailTextBox.ScrollBars = ScrollBars.Both;
            this.detailTextBox.Size = new Size(884, 535);
            this.detailTextBox.WordWrap = false;
            this.copyAllButton.BackColor = Color.FromArgb(105, 65, 150);
            this.copyAllButton.Cursor = Cursors.Hand;
            this.copyAllButton.Dock = DockStyle.Bottom;
            this.copyAllButton.FlatAppearance.BorderSize = 0;
            this.copyAllButton.FlatStyle = FlatStyle.Flat;
            this.copyAllButton.ForeColor = Color.White;
            this.copyAllButton.Location = new Point(0, 535);
            this.copyAllButton.Name = "copyAllButton";
            this.copyAllButton.Size = new Size(884, 46);
            this.copyAllButton.Text = "复制全部";
            this.copyAllButton.UseVisualStyleBackColor = false;
            this.copyAllButton.Click += new System.EventHandler(this.CopyAllButton_Click);
            this.AutoScaleDimensions = new SizeF(7F, 17F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.ClientSize = new Size(884, 581);
            this.Controls.Add(this.detailTextBox);
            this.Controls.Add(this.copyAllButton);
            this.Font = new Font("Microsoft YaHei UI", 9F);
            this.MinimumSize = new Size(700, 450);
            this.Name = "LogDetailForm";
            this.StartPosition = FormStartPosition.CenterParent;
            this.Text = "NG详情（文本可直接选择和复制）";
            this.ResumeLayout(false);
            this.PerformLayout();
        }
    }
}
