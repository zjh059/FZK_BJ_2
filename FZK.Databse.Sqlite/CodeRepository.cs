using FZK.Database.Base.Models;
using FZK.Database.Base.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FZK.Databse.Sqlite
{
    class CodeRepository : RepositoryBase, ICodeRepository
    {
        public int Delete(CodeEntity entity)
        {
            db.Entry(entity).State = System.Data.Entity.EntityState.Deleted;
            return db.SaveChanges();
        }

        public List<CodeEntity> FindAll(string keyword)
        {
            return db.Code.Where(t=>t.BottomCode.Contains(keyword)).ToList();
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
            db.Entry(entity).State = System.Data.Entity.EntityState.Added;
            return db.SaveChanges();
        }

        public int SaveChanged()
        {
            return db.SaveChanges();
        }

        public CodeEntity Select(string keyword)
        {
            return db.Code.ToList().Find(t => t.TopCode == keyword || t.BottomCode == keyword || t.SPCode == keyword || t.Result == keyword);
        }

        public int Update(CodeEntity entity)
        {
            db.Entry(entity).State = System.Data.Entity.EntityState.Modified;
            return db.SaveChanges();
        }
    }
}
