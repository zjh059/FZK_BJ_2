using FZK.Application.Config.Services;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace FZK.Application.Config.ViewModels
{
    internal class ConfigMarkViewModel : ReactiveObject
    {
        public ICommand FindMarkCommad { get; }
        public ICommand SaveCommand { get; }
        public ICommand LoadedCommand { get; }
        public CalibrationManager CalibrationManager { get; }

        public ConfigMarkViewModel(CalibrationManager calibrationManager)
        {
            CalibrationManager = calibrationManager;
            FindMarkCommad = ReactiveCommand.CreateFromTask(OnFindMarkCommad);
            SaveCommand = ReactiveCommand.CreateFromTask(OnSaveCommand);
            LoadedCommand = ReactiveCommand.CreateFromTask(OnLoadedCommand);
        }

        private async Task OnLoadedCommand()
        {
         
        }

        private async Task OnSaveCommand()
        {
            
        }

        private async Task OnFindMarkCommad()
        {
         
        }
    }
}
