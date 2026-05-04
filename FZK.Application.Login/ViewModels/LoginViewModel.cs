using FZK.Application.Share.Language;
using FZK.Application.Share.Login;
using FZK.Application.Share.Models;
using FZK.Core.Enums;
using FZK.Database.Base.Models;
using FZK.Database.Base.Repositories;
using Prism.Events;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace FZK.Application.Login.ViewModels
{
    internal class LoginViewModel : ReactiveObject
    {
        [Reactive]
        public UserEntity User { get; set; } = new UserEntity();
        public ICommand LoginCommand { get; set; }
        private IUserRepository UserRepository { get; }
        private IEventAggregator EventAggregator { get; }
        private ISession Session { get; }

        public LoginViewModel(
            ISession session,
            IUserRepository userRepository,
            IEventAggregator eventAggregator)
        {
            Session = session;
            UserRepository = userRepository;
            EventAggregator = eventAggregator;

#if DEBUG
            User.UserName = "admin";
            User.Password = "123456";
#endif
            LoginCommand = ReactiveCommand.Create(OnLoginCommand);
        }

        private Task OnLoginCommand()
        {
            return Task.Run(() =>
            {
                var userEntity = UserRepository.Select(User.UserName);
                if (userEntity != null)
                {
                    if (userEntity.Password != User.Password)
                    {
                        MessageBox.Show(MultiLang.LoginFailed);
                    }
                    else
                    {
                        Session.CurrentUser = userEntity;
                        EventAggregator.GetEvent<LoginEvent>().Publish(userEntity);
                    }
                }
                else
                {
                    userEntity = new UserEntity();
                    userEntity.UserName = User.UserName;
                    userEntity.Password = User.Password;
                    userEntity.InsertDate = DateTime.Now;
                    userEntity.Role = (int)RoleType.操作员;

                    if (userEntity.UserName == "admin")
                    {
                        userEntity.Role = (int)RoleType.管理员;
                    }

                    var count = UserRepository.Insert(userEntity);
                    if (count > 0)
                    {
                        MessageBox.Show(string.Format(MultiLang.RegisterSuccess, userEntity.UserName, userEntity.Password));
                        Session.CurrentUser = userEntity;
                        EventAggregator.GetEvent<LoginEvent>().Publish(userEntity);
                    }
                }
            });
        }
    }
}