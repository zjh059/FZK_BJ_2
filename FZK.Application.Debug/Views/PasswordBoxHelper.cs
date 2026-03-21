// 注意：命名空间要和View的命名空间一致（FZK.Application.Debug.Views）
// 文件路径：FZK.Application.Debug/Views/PasswordBoxHelper.cs
using System;
using System.Windows;
using System.Windows.Controls;

namespace FZK.Application.Debug.Views
{
    /// <summary>
    /// PasswordBox绑定辅助类（必须是public，否则XAML无法访问）
    /// </summary>
    public static class PasswordBoxHelper
    {
        // 定义依赖属性（用于双向绑定密码）
        public static readonly DependencyProperty BindPasswordProperty =
            DependencyProperty.RegisterAttached(
                "BindPassword",
                typeof(string),
                typeof(PasswordBoxHelper),
                new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnBindPasswordChanged));

        // 获取属性值的方法（必须）
        public static string GetBindPassword(DependencyObject obj)
        {
            return (string)obj.GetValue(BindPasswordProperty);
        }

        // 设置属性值的方法（必须）
        public static void SetBindPassword(DependencyObject obj, string value)
        {
            obj.SetValue(BindPasswordProperty, value);
        }

        // 属性变更回调（同步PasswordBox的密码）
        private static void OnBindPasswordChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (!(d is  PasswordBox passwordBox)) return;

            // 先移除旧的事件绑定，避免重复触发
            passwordBox.PasswordChanged -= PasswordBox_PasswordChanged;

            // 同步新密码到PasswordBox
            if (e.NewValue != null && passwordBox.Password != e.NewValue.ToString())
            {
                passwordBox.Password = e.NewValue.ToString();
            }

            // 绑定密码变更事件
            passwordBox.PasswordChanged += PasswordBox_PasswordChanged;
        }

        // PasswordBox密码变更时，同步到绑定属性
        private static void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (sender is PasswordBox passwordBox)
            {
                SetBindPassword(passwordBox, passwordBox.Password);
            }
        }
    }

  
}