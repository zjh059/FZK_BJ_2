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
            db.Entry(entity).State = EntityState.Deleted;
            return db.SaveChanges();
        }

        public List<BTEntity> FindAll(string keyword)
        {
            return db.BT.Where(t => t.BottomCode.Contains(keyword)).ToList();
        }

        public BTEntity Get(int id)
        {
            return db.BT.Find(id);
        }

        public List<BTEntity> GetAll()
        {
            return db.BT.ToList();
        }

        public int Insert(BTEntity entity)
        {
            db.Entry(entity).State = EntityState.Added;
            return db.SaveChanges();
        }

        public int SaveChanged()
        {
            return db.SaveChanges();
        }

        public BTEntity Select(string keyword)
        {
            return db.BT.ToList()
                .Find(t => t.TopCode == keyword ||
                           t.BottomCode == keyword ||
                           t.Counts == keyword);
        }

        public int Update(BTEntity entity)
        {
            db.Entry(entity).State = EntityState.Modified;
            return db.SaveChanges();
        }
    }
}