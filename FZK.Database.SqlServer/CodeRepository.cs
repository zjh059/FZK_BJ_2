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
            using (var db = CreateContext())
            {
                db.Entry(entity).State = EntityState.Deleted;
                return db.SaveChanges();
            }
        }

        public List<CodeEntity> FindAll(string keyword)
        {
            using (var db = CreateContext())
            {
                return db.Code.Where(t => t.BottomCode.Contains(keyword)).ToList();
            }
        }

        public CodeEntity Get(int id)
        {
            using (var db = CreateContext())
            {
                return db.Code.Find(id);
            }
        }

        public List<CodeEntity> GetAll()
        {
            using (var db = CreateContext())
            {
                return db.Code.ToList();
            }
        }

        public int Insert(CodeEntity entity)
        {
            using (var db = CreateContext())
            {
                db.Entry(entity).State = EntityState.Added;
                return db.SaveChanges();
            }
        }

        public int SaveChanged()
        {
            using (var db = CreateContext())
            {
                return db.SaveChanges();
            }
        }

        public CodeEntity Select(string keyword)
        {
            using (var db = CreateContext())
            {
                return db.Code.FirstOrDefault(t => t.TopCode == keyword ||
                                                   t.BottomCode == keyword ||
                                                   t.SPCode == keyword ||
                                                   t.Result == keyword);
            }
        }

        public int Update(CodeEntity entity)
        {
            using (var db = CreateContext())
            {
                db.Entry(entity).State = EntityState.Modified;
                return db.SaveChanges();
            }
        }
    }
}