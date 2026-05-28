using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using FZK.Database.Base.Models;
using FZK.Database.Base.Repositories;
using FZK.Database.SqlServer.FZK.Database.SqlServer;

namespace FZK.Database.SqlServer
{
    public class UserRepository : RepositoryBase, IUserRepository
    {
        public int Delete(UserEntity entity)
        {
            db.Entry(entity).State = EntityState.Deleted;
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
            db.Entry(entity).State = EntityState.Added;
            return db.SaveChanges();
        }

        public int SaveChanged()
        {
            return db.SaveChanges();
        }

        public UserEntity Select(string keyword)
        {
            return db.Users.ToList()
                .Find(t => t.UserName == keyword);
        }

        public int Update(UserEntity entity)
        {
            db.Entry(entity).State = EntityState.Modified;
            return db.SaveChanges();
        }
    }
}