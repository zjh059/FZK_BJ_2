using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using FZK.Database.Base.Models;
using FZK.Database.Base.Repositories;
using FZK.Database.SqlServer.FZK.Database.SqlServer;

namespace FZK.Database.SqlServer
{
    public class BTRepository : RepositoryBase, IBTRepository
    {
        public int Delete(BTEntity entity)
        {
            using (var db = CreateContext())
            {
                db.Entry(entity).State = EntityState.Deleted;
                return db.SaveChanges();
            }
        }

        public List<BTEntity> FindAll(string keyword)
        {
            using (var db = CreateContext())
            {
                return db.BT.Where(t => t.BottomCode.Contains(keyword)).ToList();
            }
        }

        public BTEntity Get(int id)
        {
            using (var db = CreateContext())
            {
                return db.BT.Find(id);
            }
        }

        public List<BTEntity> GetAll()
        {
            using (var db = CreateContext())
            {
                return db.BT.ToList();
            }
        }

        public int Insert(BTEntity entity)
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

        public BTEntity Select(string keyword)
        {
            using (var db = CreateContext())
            {
                return db.BT.FirstOrDefault(t => t.TopCode == keyword ||
                                                 t.BottomCode == keyword ||
                                                 t.Counts == keyword);
            }
        }
        public BTEntity GetByBottomCode(string bottomCode)
        {
            using (var db = CreateContext())
            {
                return db.BT.FirstOrDefault(t => t.BottomCode == bottomCode);
            }
        }
        public int Update(BTEntity entity)
        {
            using (var db = CreateContext())
            {
                db.Entry(entity).State = EntityState.Modified;
                return db.SaveChanges();
            }
        }
    }
}