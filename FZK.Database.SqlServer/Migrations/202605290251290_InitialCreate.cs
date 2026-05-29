namespace FZK.Database.SqlServer.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class InitialCreate : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.BTEntity",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        BottomCode = c.String(),
                        TopCode = c.String(),
                        Counts = c.String(),
                        UpdateTime = c.DateTime(nullable: false),
                        InsertDate = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.Id);
            
            CreateTable(
                "dbo.CodeEntity",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        BottomCode = c.String(),
                        TopCode = c.String(),
                        SPCode = c.String(),
                        Result = c.String(),
                        InsertDate = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.Id);
            
            CreateTable(
                "dbo.UserEntity",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        UserName = c.String(),
                        Password = c.String(),
                        Role = c.Int(nullable: false),
                        InsertDate = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.Id);
            
        }
        
        public override void Down()
        {
            DropTable("dbo.UserEntity");
            DropTable("dbo.CodeEntity");
            DropTable("dbo.BTEntity");
        }
    }
}
