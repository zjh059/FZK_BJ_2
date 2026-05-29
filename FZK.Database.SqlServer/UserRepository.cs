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
            using (var db = CreateContext())
            {
                db.Entry(entity).State = EntityState.Deleted;
                return db.SaveChanges();
            }
        }

        public List<UserEntity> FindAll(string keyword)
        {
            using (var db = CreateContext())
            {
                return db.Users.Where(t => t.UserName.Contains(keyword)).ToList();
            }
        }

        public UserEntity Get(int id)
        {
            using (var db = CreateContext())
            {
                return db.Users.Find(id);
            }
        }

        public List<UserEntity> GetAll()
        {
            using (var db = CreateContext())
            {
                return db.Users.ToList();
            }
        }

        public int Insert(UserEntity entity)
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

        public UserEntity Select(string keyword)
        {
            using (var db = CreateContext())
            {
                return db.Users.FirstOrDefault(t => t.UserName == keyword);
                // 原来用 ToList().Find(...) 改为数据库端查询，性能更好
            }
        }

        public int Update(UserEntity entity)
        {
            using (var db = CreateContext())
            {
                db.Entry(entity).State = EntityState.Modified;
                return db.SaveChanges();
            }
        }
    }
}