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
            protected static readonly Lazy<SqlServerDbContext> LazyDb =
                new Lazy<SqlServerDbContext>();

            protected static SqlServerDbContext db => LazyDb.Value;
        }
    }
}
