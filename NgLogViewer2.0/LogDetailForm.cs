using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace NgLogViewer
{
    /// <summary>
    /// 单条 NG 的完整详情窗体。布局放在 Designer 文件，复制逻辑留在这里。
    /// </summary>
    public sealed partial class LogDetailForm : Form
    {
        public LogDetailForm(string detailText, Icon ownerIcon)
        {
            InitializeComponent();
            detailTextBox.Text = detailText ?? String.Empty;
            if (ownerIcon != null) Icon = ownerIcon;
        }

        private void CopyAllButton_Click(object sender, EventArgs e)
        {
            try
            {
                Clipboard.SetText(detailTextBox.Text ?? String.Empty);
            }
            catch (ExternalException)
            {
                MessageBox.Show("剪贴板正被其他程序占用，请稍后再试。", "复制失败",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}
