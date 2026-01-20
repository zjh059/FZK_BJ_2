using FZK.Database.Base.Models;
using FZK.Database.Base.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FZK.Databse.Sqlite
{
    class UserRepository : RepositoryBase, IUserRepository
    {
        public int Delete(UserEntity entity)
        {
            db.Entry(entity).State = System.Data.Entity.EntityState.Deleted;
            return db.SaveChanges();
        }

        public List<UserEntity> FindAll(string keyword)
        {
            return db.Users.Where(t => t.UserName.Contains(keyword)).ToList();
        }

        public UserEntity Get(int id)
        {
            return db.Users.Find(id);
        }

        public List<UserEntity> GetAll()
        {
            return db.Users.ToList();
        }

        public int Insert(UserEntity entity)
        {
            db.Entry(entity).State = System.Data.Entity.EntityState.Added;
            return db.SaveChanges();
        }

        public int SaveChanged()
        {
            return db.SaveChanges();
        }

        public UserEntity Select(string keyword)
        {
            return db.Users.ToList().Find(t => t.UserName == keyword);
        }

        public int Update(UserEntity entity)
        {
            db.Entry(entity).State = System.Data.Entity.EntityState.Modified;
            return db.SaveChanges();
        }
    }
}
