using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace FZK.Application.Run.Views
{
    /// <summary>
    /// RunView.xaml 的交互逻辑
    /// </summary>
    public partial class RunView : UserControl
    {
        public RunView()
        {
            InitializeComponent();
        }
        private void DataGrid_Loaded(object sender, RoutedEventArgs e)
        {
            var dataGrid = sender as DataGrid;
            if (dataGrid == null || dataGrid.Columns.Count == 0) return;

            // 临时关闭再重新启用 Auto 宽度，触发重新测量
            foreach (var column in dataGrid.Columns)
            {
                var oldWidth = column.Width;
                column.Width = 0;
                column.Width = oldWidth;
                // 或者直接设置为 Auto
                // column.Width = DataGridLength.Auto;
            }
        }
    }
}
