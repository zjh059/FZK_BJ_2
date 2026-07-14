using FZK.Application.Share.DebugFolder;
using FZK.Database.Base.Models;
using FZK.Database.Base.Repositories;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FZK.Application.Debug.Service
{
    /// <summary>
    /// 数据库管理服务（完善异常处理和数据关联）
    /// </summary>
    public class DatabaseManager : ReactiveObject, IDatabaseManager
    {
        [Reactive]
        public List<BTEntity> BTEntities { get; private set; } = new List<BTEntity>();

        [Reactive]
        public List<CodeEntity> CodeEntities { get; private set; } = new List<CodeEntity>();

        [Reactive]
        public List<UserEntity> UserEntities { get; private set; } = new List<UserEntity>();

        /// <summary>
        /// BT仓储实例
        /// </summary>
        public IBTRepository BTRepository { get; }

        /// <summary>
        /// 码值仓储实例
        /// </summary>
        public ICodeRepository CodeRepository { get; }

        /// <summary>
        /// 用户仓储实例
        /// </summary>
        public IUserRepository UserRepository { get; }

        /// <summary>
        /// 构造函数（依赖注入）
        /// </summary>
        /// <param name="btRepository">BT仓储</param>
        /// <param name="codeRepository">码值仓储</param>
        /// <param name="userRepository">用户仓储</param>
        public DatabaseManager(IBTRepository btRepository,
           ICodeRepository codeRepository,
           IUserRepository userRepository)
        {
            BTRepository = btRepository;
            CodeRepository = codeRepository;
            UserRepository = userRepository;

            // 初始化加载数据（带异常捕获）
            GetAll();
        }

        /// <summary>
        /// 加载所有数据（完善异常处理）
        /// </summary>
        public void GetAll()
        {
            try
            {
                UserEntities = UserRepository.GetAll() ?? new List<UserEntity>();
                BTEntities = BTRepository.GetAll() ?? new List<BTEntity>();
                CodeEntities = CodeRepository.GetAll() ?? new List<CodeEntity>();

                // 通知UI数据更新
                this.RaisePropertyChanged(nameof(UserEntities));
                this.RaisePropertyChanged(nameof(BTEntities));
                this.RaisePropertyChanged(nameof(CodeEntities));
            }
            catch (Exception ex)
            {
                // 记录日志+兜底空列表
                System.Diagnostics.Debug.WriteLine($"数据加载失败：{ex.Message}");
                UserEntities = new List<UserEntity>();
                BTEntities = new List<BTEntity>();
                CodeEntities = new List<CodeEntity>();
            }
        }

        /// <summary>
        /// 保存变更（完善异常处理）
        /// </summary>
        /// <returns>是否保存成功</returns>
        public bool SaveChanged()
        {
            try
            {
                BTRepository.SaveChanged();
                CodeRepository.SaveChanged();
                UserRepository.SaveChanged();
                GetAll();
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存数据变更失败：{ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 重载SaveChanged（兼容原有无返回值调用）
        /// </summary>
        void IDatabaseManager.SaveChanged()
        {
            SaveChanged();
        }
    }
}
