using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace FZK.Application.UI.Converters
{
    /// <summary>
    /// 状态转颜色转换器（0=未开始(灰色)/1=进行中(蓝色)/2=成功(绿色)/3=失败(红色)）
    /// </summary>
    public class StatusToColorConverter : IValueConverter
    {
        /// <summary>
        /// 核心转换方法（输入状态，输出SolidColorBrush）
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // 1. 空值/非预期类型处理（避免转换异常）
            if (value == null)
            {
                // 返回默认透明背景
                return Brushes.Transparent;
            }

            // 2. 根据你的业务场景，将状态转换为对应的SolidColorBrush
            // 示例：假设状态是字符串类型（如"正常"/"异常"/"警告"）
            //string status = value.ToString().Trim().ToLower();
            //return status switch
            //{
            //    "正常" or "connected" => new SolidColorBrush(Color.FromRgb(0, 255, 0)), // 绿色
            //    "异常" or "disconnected" => new SolidColorBrush(Color.FromRgb(255, 0, 0)), // 红色
            //    "警告" or "warning" => new SolidColorBrush(Color.FromRgb(255, 255, 0)), // 黄色
            //    _ => Brushes.Transparent // 默认透明
            //};

            // 如果你使用C#7.3及以下（不支持switch expression），替换为：
            string status = value.ToString().Trim().ToLower();
            switch (status)
            {
                case "正常":
                    return new SolidColorBrush(Color.FromRgb(40, 167, 69));
                case "connected":
                    return new SolidColorBrush(Color.FromRgb(40, 167, 69));
                case "true":
                    return new SolidColorBrush(Color.FromRgb(40, 167, 69));
                case "异常":
                case "disconnected":
                    return new SolidColorBrush(Color.FromRgb(255, 0, 0));
                case "警告":
                case "warning":
                    return new SolidColorBrush(Color.FromRgb(255, 255, 0));
                case "false":
                    return new SolidColorBrush(Color.FromRgb(255, 0, 0));

                default:
                    return Brushes.Transparent;
            }
        }

        /// <summary>
        /// 反向转换（Background一般不需要，返回UnsetValue即可）
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return DependencyProperty.UnsetValue;
        }
    }
}