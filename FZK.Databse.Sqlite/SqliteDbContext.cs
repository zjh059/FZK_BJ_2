using FZK.Database.Base.Models;
using SQLite.CodeFirst;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.ModelConfiguration.Conventions;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FZK.Databse.Sqlite
{
    public class SqliteDbContext : DbContext
    {
        public SqliteDbContext() : base("SqliteDbContext")
        {

        }
        public DbSet<UserEntity> Users { get; set; }
        public DbSet<CodeEntity> Code { get; set; }
        public DbSet<BTEntity> BT { get; set; }
        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            var sqliteConnect = new SqliteCreateDatabaseIfNotExists<SqliteDbContext>(modelBuilder);
            //执行
            System.Data.Entity.Database.SetInitializer(sqliteConnect);
        }
    }
}
