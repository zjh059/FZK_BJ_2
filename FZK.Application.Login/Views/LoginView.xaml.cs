using FZK.Core.Ioc;
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

namespace FZK.Application.Login.Views
{
    /// <summary>
    /// LoginView.xaml 的交互逻辑
    /// </summary>
    public partial class LoginView : UserControl
    {
        public LoginView()
        {
            InitializeComponent();
            //this.Loaded += (s, e) =>
            //{
            //    CNS.Checked += Checked;
            //    CNT.Checked += Checked;
            //    English.Checked += Checked;
            //};
        }

        //    private void Checked(object sender, RoutedEventArgs e)
        //    {
        //        if (sender is RadioButton button)
        //        {
        //            switch (button.Name)
        //            {
        //                case "CNS":
        //                    PrismProvider.LanguageManager.Set(Core.Enums.LanguageType.CNS);
        //                    break;
        //                case "CNT":
        //                    PrismProvider.LanguageManager.Set(Core.Enums.LanguageType.CNT);
        //                    break;
        //                case "English":
        //                    PrismProvider.LanguageManager.Set(Core.Enums.LanguageType.English);
        //                    break;
        //                default:
        //                    break;
        //            }
        //        }
        //    }
        //}
    }
}
