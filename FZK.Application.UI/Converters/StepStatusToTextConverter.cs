using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace FZK.Application.UI.Converters
{
    public class StepStatusToTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            string status = (string)value;
            switch (status)
            {
                case "Completed":
                    return "已完成";
                case "Running":
                    return "执行中";
                case "Failed":
                    return "失败";
                case "Waiting":
                    return "等待中";
                default:
                    return "未执行";
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
