using System.Data.Entity;
using FZK.Database.Base.Models;

namespace FZK.Database.SqlServer
{
    public class SqlServerDbContext : DbContext
    {
        // 静态构造函数，程序首次使用该类时自动执行一次
        static SqlServerDbContext()
        {
            // 如果数据库不存在则创建（开发期用）
            System.Data.Entity.Database.SetInitializer(new CreateDatabaseIfNotExists<SqlServerDbContext>());

            // 如果以后需要自动迁移，则改为：
            // Database.SetInitializer(new MigrateDatabaseToLatestVersion<SqlServerDbContext, Migrations.Configuration>());
        }

        public SqlServerDbContext() : base("name=SqlServerDbContext")
        {
        }

        public DbSet<UserEntity> Users { get; set; }
        public DbSet<CodeEntity> Code { get; set; }
        public DbSet<BTEntity> BT { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            // 这里不再需要 SetInitializer，可以保留用于其他 Fluent API 配置
            base.OnModelCreating(modelBuilder);
        }
    }
}