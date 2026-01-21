using FZK.Application.Share.Login;
using FZK.Database.Base.Models;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace FZK.Application.Login.Services
{
    class Session : ReactiveObject, ISession
    {
        [Reactive]
        public UserEntity CurrentUser { get; set; }

        public Window MainWindow => System.Windows.Application.Current.MainWindow;

        [Reactive]
        public Visibility Visibility { get; private set; } = Visibility.Collapsed;

        /// <summary>
        /// 弹窗提示
        /// </summary>
        /// <param name="message"></param>
        /// <param name="messageBoxButton"></param>
        /// <returns></returns>
        public bool MessageBox(string message, MessageBoxButton messageBoxButton = MessageBoxButton.OK)
        {
            bool result = false;
            System.Windows.Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                Visibility = Visibility.Visible;
                MessageBoxResult messageBoxResult = System.Windows.MessageBox.Show(
                    message,
                    "确认",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                Visibility = Visibility.Collapsed;
                result = messageBoxResult == MessageBoxResult.Yes;
            }));
            return result;
        }
    }
}
