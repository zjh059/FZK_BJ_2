using FZK.Database.Base.Repositories;
using Prism.Ioc;
using Prism.Modularity;

namespace FZK.Database.SqlServer
{
    public class SqlServerModule : IModule
    {
        public void OnInitialized(IContainerProvider containerProvider)
        {
        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterSingleton<IUserRepository, UserRepository>();
            containerRegistry.RegisterSingleton<IBTRepository, BTRepository>();
            containerRegistry.RegisterSingleton<ICodeRepository, CodeRepository>();
        }
    }
}