using FZK.Application.Share.DebugFolder;
using FZK.Application.Share.Run;
using FZK.Database.Base.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FZK.Application.Run.Service
{
    public class DatabaseService : IDatabaseService
    {
        private readonly IDatabaseManager _dbManager;

        public DatabaseService(IDatabaseManager dbManager)
        {
            _dbManager = dbManager;
        }

        public async Task<bool> VerifyBottomTopCodeAsync(string bottomCode, string topCode)
        {
            if (string.IsNullOrEmpty(bottomCode) || string.IsNullOrEmpty(topCode)) return false;
            var btEntity = await Task.Run(() => _dbManager.BTEntities.FirstOrDefault(t => t.BottomCode == bottomCode));
            return btEntity != null && btEntity.TopCode == topCode;
        }

        public async Task UpdateOrAddCodeEntityAsync(string bottomCode, string topCode, string spCode)
        {
            await Task.Run(() =>
            {
                var existing = _dbManager.CodeEntities.FirstOrDefault(c => c.BottomCode == bottomCode);
                if (existing != null)
                {
                    existing.SPCode = spCode;
                    _dbManager.CodeRepository.Update(existing);
                }
                else
                {
                    _dbManager.CodeRepository.Insert(new CodeEntity
                    {
                        BottomCode = bottomCode,
                        TopCode = topCode,
                        SPCode = spCode,
                        Result = "0",
                        InsertDate = DateTime.Now
                    });
                }
                _dbManager.SaveChanged();
            });
        }
        public async Task AddBTEntityAsync(string bottomCode, string topCode)
        {
            await Task.Run(() =>
            {
                //26_7_1修改：
                //防呆：先前要求底板和盖板同时相等才算存在
                //下面改成只按底板码判断是否已存在，不再要求底板码+盖板码同时匹配
                //var existing = _dbManager.BTEntities.FirstOrDefault(c => c.BottomCode == bottomCode&&c.TopCode==topCode);
                var existing = _dbManager.BTEntities.FirstOrDefault(c => c.BottomCode == bottomCode);

                if (existing == null)
                {
                    _dbManager.BTRepository.Insert(new BTEntity
                    {
                        BottomCode = bottomCode,
                        TopCode = topCode,
                        Counts = "0",
                        InsertDate = DateTime.Now
                    });
                }
                else
                {
                    //（可选）若底板码存在说明之前绑定过了，若有相同的码进来，只需比对信息
                    //如果要系统每次以最新导向板为准，可选择下面两行
                    //existing.TopCode = topCode;
                    //existing.UpdateTime = DateTime.Now;
                }
                _dbManager.SaveChanged();
            });
        }
        public async Task UpdateTestResultAsync(string spCode, int result)
        {
            await Task.Run(() =>
            {
                var entity = _dbManager.CodeEntities.FirstOrDefault(c => c.SPCode == spCode);
                if (entity != null)
                {
                    entity.Result = result.ToString();
                    _dbManager.CodeRepository.Update(entity);
                    _dbManager.SaveChanged();
                }
            });
        }

        public async Task<string> IncrementCountAsync(string bottomCode)
        {
            return await Task.Run(() =>
            {
                var bt = _dbManager.BTEntities.FirstOrDefault(b => b.BottomCode == bottomCode);
                if (bt == null) return "0";
                int current = int.TryParse(bt.Counts, out var val) ? val : 0;
                bt.Counts = (current + 1).ToString();
                _dbManager.BTRepository.Update(bt);
                _dbManager.SaveChanged();
                return bt.Counts;
            });
        }

        public async Task ClearCountAsync(string bottomCode)
        {
            await Task.Run(() =>
            {
                var bt = _dbManager.BTEntities.FirstOrDefault(b => b.BottomCode == bottomCode);
                if (bt != null)
                {
                    bt.Counts = "0";
                    _dbManager.BTRepository.Update(bt);
                    _dbManager.SaveChanged();
                }
            });
        }
    }
}


