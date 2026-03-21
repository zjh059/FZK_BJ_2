using System;
using System.Globalization;
using System.Windows.Data;

namespace FZK.Application.UI.Converters
{
    /// <summary>
    /// 布尔值转文本转换器（True=已连接，False=未连接）
    /// </summary>
    public class BoolToTextConverter : IValueConverter
    {
        /// <summary>
        /// 为True时显示的文本
        /// </summary>
        public string TrueText { get; set; } = "已连接";

        /// <summary>
        /// 为False时显示的文本
        /// </summary>
        public string FalseText { get; set; } = "未连接";

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? TrueText : FalseText;
            }
            return FalseText;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string text)
            {
                return text.Equals(TrueText, StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }
    }
}