using FZK.Application.Share.Login;
using FZK.Application.Share.Models;
using FZK.Core.Enums;
using FZK.Database.Base.Models;
using FZK.Database.Base.Repositories;
using Prism.Commands;
using Prism.Events;
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
            //  userRepository.Insert(User);
            //  LoginCommand = new DelegateCommand(OnLoginCommand);
        }

        private Task OnLoginCommand()
        {
            return Task.Run(() =>
            {
                var userEntity = UserRepository.Select(User.UserName);
                if (userEntity != null)
                {
                    //比较密码
                    if (userEntity.Password != User.Password)
                    {
                        MessageBox.Show("用户名或密码错误");
                    }
                    else
                    {
                        Session.CurrentUser = userEntity;
                        EventAggregator.GetEvent<LoginEvent>().Publish(userEntity);//跳转页面
                    }
                }
                else
                {
                    //添加新用户
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
                        MessageBox.Show($"注册新用户成功\r\n用户名:{userEntity.UserName}\r\n密码:{userEntity.Password}");
                        Session.CurrentUser = userEntity;
                        EventAggregator.GetEvent<LoginEvent>().Publish(userEntity);//跳转页面
                   }

                }
            });
           

        }
    }
}
