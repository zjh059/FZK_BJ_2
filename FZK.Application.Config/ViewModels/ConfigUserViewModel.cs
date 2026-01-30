using FZK.Database.Base.Models;
using FZK.Database.Base.Repositories;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace FZK.Application.Config.ViewModels
{
    internal class ConfigUserViewModel: ReactiveObject
    {

        [Reactive]
        public List<UserEntity> Users { get; set; }

        public ICommand LoadedCommand { get; }
        public ICommand UpdateUserCommand { get; }

        private IUserRepository UserRepository { get; }
        public ConfigUserViewModel(IUserRepository userRepository)
        {
            UserRepository = userRepository;
            LoadedCommand = ReactiveCommand.Create(OnLoadedCommand);
            UpdateUserCommand = ReactiveCommand.Create(OnUpdateUserCommand);
        }

        private void OnUpdateUserCommand()
        {
            UserRepository.SaveChanged();
        }

        private void OnLoadedCommand()
        {
            Users = UserRepository.GetAll();
        }
    }
}
