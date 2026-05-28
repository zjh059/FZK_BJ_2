using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using FZK.Database.Base.Models;
using FZK.Database.Base.Repositories;
using FZK.Database.SqlServer.FZK.Database.SqlServer;

namespace FZK.Database.SqlServer
{
    public class CodeRepository : RepositoryBase, ICodeRepository
    {
        public int Delete(CodeEntity entity)
        {
            db.Entry(entity).State = EntityState.Deleted;
            return db.SaveChanges();
        }

        public List<CodeEntity> FindAll(string keyword)
        {
            return db.Code.Where(t => t.BottomCode.Contains(keyword)).ToList();
        }

        public CodeEntity Get(int id)
        {
            return db.Code.Find(id);
        }

        public List<CodeEntity> GetAll()
        {
            return db.Code.ToList();
        }

        public int Insert(CodeEntity entity)
        {
            db.Entry(entity).State = EntityState.Added;
            return db.SaveChanges();
        }

        public int SaveChanged()
        {
            return db.SaveChanges();
        }

        public CodeEntity Select(string keyword)
        {
            return db.Code.ToList()
                .Find(t => t.TopCode == keyword ||
                           t.BottomCode == keyword ||
                           t.SPCode == keyword ||
                           t.Result == keyword);
        }

        public int Update(CodeEntity entity)
        {
            db.Entry(entity).State = EntityState.Modified;
            return db.SaveChanges();
        }
    }
}