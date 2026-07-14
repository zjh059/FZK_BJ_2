using FZK.Application.Share.DebugFolder;
using FZK.Application.Share.Language;
using FZK.Application.Share.Run;
using FZK.Database.Base.Models;
using FZK.Logger;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

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
            if (string.IsNullOrEmpty(bottomCode) || string.IsNullOrEmpty(topCode))
                return false;
            else
            {
                bottomCode = bottomCode.Replace(" ", "");
                topCode = topCode.Replace(" ", "");
            }
            //添加记录
            await AddBTEntityAsync(bottomCode, topCode);

            if (bottomCode == topCode)
                return true;
            else
                return false;

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
                //var existing = _dbManager.BTEntities.FirstOrDefault(c => c.BottomCode == bottomCode && c.TopCode == topCode);
                //if (existing == null)
                //{
                //    _dbManager.BTRepository.Insert(new BTEntity
                //    {
                //        BottomCode = bottomCode,
                //        TopCode = topCode,
                //        Counts = "0",
                //        InsertDate = DateTime.Now
                //    });
                //    _dbManager.SaveChanged();
                //}

                var existEntity = _dbManager.BTRepository.GetByBottomCode(bottomCode);
                if (existEntity != null)
                {
                    Logs.LogInfo($"{MultiLang.BottomCodeExists}{bottomCode}");
                    return;
                }

                try
                {
                    int count = 0;

                    // 新增
                    count = _dbManager.BTRepository.Insert(new BTEntity
                    {
                        BottomCode = bottomCode,
                        TopCode = topCode,
                        Counts = "0",
                        InsertDate = DateTime.Now
                    });

                    if (count > 0)
                    {
                        Logs.LogInfo($"{bottomCode}添加成功");

                    }
                    else
                    {
                        Logs.LogInfo($"{bottomCode}添加失败");

                    }
                }
                catch (Exception ex)
                {
                    Logs.LogInfo($"{bottomCode}添加异常:{ex.ToString()}");

                }
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


