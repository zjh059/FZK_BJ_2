using FZK.Database.Base.Models;
using FZK.Database.Base.Repositories;
using Prism.Commands;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace FZK.Application.Login.ViewModels
{
    internal class LoginViewModel : ReactiveObject
    {
        [Reactive]
        public UserEntity User { get; set; } = new UserEntity();
        public ICommand LoginCommand { get; set; }
        private IUserRepository UserRepository { get; }
        public LoginViewModel(IUserRepository userRepository)
        {
            UserRepository = userRepository;

#if DEBUG
            User.UserName = "admin";
            User.Password = "123456";
#endif
            LoginCommand = ReactiveCommand.Create(OnLoginCommand);
            userRepository.Insert(User);
            //   LoginCommand = new DelegateCommand(OnLoginCommand);
        }

        private void OnLoginCommand()

        {
            MessageBox.Show("OK");
        }
    }
}
