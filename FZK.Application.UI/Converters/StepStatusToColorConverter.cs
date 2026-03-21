using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Media;

namespace FZK.Application.UI.Converters
{
    public class StepStatusToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            string status = (string)value;
            switch (status)
            {
                case "Completed":
                    return new SolidColorBrush(Color.FromRgb(0, 180, 42)); // 绿色-完成
                case "Running":
                    return new SolidColorBrush(Color.FromRgb(22, 93, 255)); // 蓝色-执行中
                case "Failed":
                    return new SolidColorBrush(Color.FromRgb(245, 63, 63)); // 红色-失败
                default:
                    return new SolidColorBrush(Color.FromRgb(201, 205, 212)); // 灰色-未执行/等待
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
