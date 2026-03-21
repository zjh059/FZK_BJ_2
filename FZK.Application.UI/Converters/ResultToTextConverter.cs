using System;
using System.Globalization;
using System.Windows.Data;

namespace FZK.Application.UI.Converters
{
    /// <summary>
    /// 结果转文本转换器（适配低版本C#，普通switch写法）
    /// </summary>
    public class ResultToTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // 处理空值
            if (value == null)
                return "未知";

            // 转换为字符串
            string result = value.ToString();

            // 普通switch语句（兼容所有C#版本）
            switch (result)
            {
                case "1":
                    return "OK";
                case "2":
                    return "NG";
                case "0":
                    return "未测试";
                default:
                    return result; // 非预设值返回原值
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // 反向转换不需要实现
            throw new NotImplementedException("结果转文本转换器不支持反向转换");
        }
    }
}