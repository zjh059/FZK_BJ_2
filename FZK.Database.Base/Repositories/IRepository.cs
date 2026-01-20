using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FZK.Database.Base.Repositories
{
    public interface IRepository<T> where T : class
    {
        T Get(int id);
        int Update(T entity);
        int Delete(T entity);
        int Insert(T entity);
        List<T> GetAll();
        List<T> FindAll(string keyword);
        T Select(string keyword);
        int SaveChanged();
    }
}
