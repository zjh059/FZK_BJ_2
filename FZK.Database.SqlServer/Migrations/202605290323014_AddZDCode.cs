namespace FZK.Database.SqlServer.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddZDCode : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.CodeEntity", "ZDCode", c => c.String(defaultValue: "ZD001"));
    
        }
        
        public override void Down()
        {
            DropColumn("dbo.CodeEntity", "ZDCode");
        }
    }
}
