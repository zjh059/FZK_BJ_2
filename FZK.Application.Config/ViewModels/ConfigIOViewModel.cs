using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace FZK.Application.Config.ViewModels
{
    internal class ConfigIOViewModel : ReactiveObject
    {
        public ICommand SetOutCommand { get; }
        public ConfigIOViewModel( )
        {
            
        }
        
    }
}
