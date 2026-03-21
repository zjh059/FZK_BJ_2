using FZK.Database.Base.Models;
using FZK.Database.Base.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FZK.Databse.Sqlite
{
    class BTRepository : RepositoryBase, IBTRepository
    {
        public int Delete(BTEntity entity)
        {
            db.Entry(entity).State = System.Data.Entity.EntityState.Deleted;
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
            db.Entry(entity).State = System.Data.Entity.EntityState.Added;
            return db.SaveChanges();
        }

        public int SaveChanged()
        {
            return db.SaveChanges();
        }

        public BTEntity Select(string keyword)
        {
            return db.BT.ToList().Find(t => t.TopCode == keyword || t.BottomCode == keyword || t.Counts == keyword );
        }
        public int Update(BTEntity entity)
        {
            db.Entry(entity).State = System.Data.Entity.EntityState.Modified;
            return db.SaveChanges();
        }
    }
}
