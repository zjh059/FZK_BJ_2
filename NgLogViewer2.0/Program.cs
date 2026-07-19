using System;
using System.Diagnostics;
using System.Threading;
using System.Windows.Forms;
namespace NgLogViewer
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            // 整个工具进程和当前线程都低于普通优先级，尽量减少与机台主程序争抢CPU。
            try { Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.BelowNormal; } catch { }
            try { Thread.CurrentThread.Priority = ThreadPriority.BelowNormal; } catch { }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
