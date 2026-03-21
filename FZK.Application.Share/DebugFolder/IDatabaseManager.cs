using FZK.Database.Base.Models;
using FZK.Database.Base.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FZK.Application.Share.DebugFolder
{
    public interface IDatabaseManager
    {
        List<BTEntity> BTEntities { get; }
        List<CodeEntity> CodeEntities { get; }
        List<UserEntity> UserEntities { get; }
        IBTRepository BTRepository { get; }
        ICodeRepository CodeRepository { get; }
        IUserRepository UserRepository { get; }
        void GetAll();
        void SaveChanged();
    }
}
