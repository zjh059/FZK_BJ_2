using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FZK.Application.Share.Run
{
    public interface IRobotCoordinator
    {
        Task ProcessCommandAsync();
    }
}
