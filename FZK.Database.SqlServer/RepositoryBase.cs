using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FZK.Database.SqlServer
{
    using System;

    namespace FZK.Database.SqlServer
    {
        public abstract class RepositoryBase
        {
            //protected static readonly Lazy<SqlServerDbContext> LazyDb =
            //    new Lazy<SqlServerDbContext>();

            //protected static SqlServerDbContext db => LazyDb.Value;
            // 提供一个工厂方法，由子类在每个方法里调用
            protected SqlServerDbContext CreateContext()
            {
                return new SqlServerDbContext();
            }
        }
    }
}
