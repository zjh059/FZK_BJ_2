using FZK.Application.Share.DebugFolder;
using FZK.Application.Share.Language;
using FZK.Database.Base.Models;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace FZK.Application.Debug.ViewModels
{
    /// <summary>
    /// BT实体调试ViewModel（对齐BOMViewModel设计风格）
    /// </summary>
    public class BTEntityDebugViewModel : ReactiveObject
    {
        #region 依赖注入
        private readonly IDatabaseManager _btDataManager;

        /// <summary>
        /// 构造函数（Prism IOC注入）
        /// </summary>
        /// <param name="btDataManager">BT数据管理服务</param>
        public BTEntityDebugViewModel(IDatabaseManager btDataManager)
        {
            _btDataManager = btDataManager;

            // 初始化命令
            // 页面加载命令
            LoadedCommand = ReactiveCommand.Create(OnLoaded);
            // 新增命令
            AddCommand = ReactiveCommand.Create(OnAdd);
            // 编辑选中项命令
            EditCommand = ReactiveCommand.Create(OnEdit, this.WhenAnyValue(x => x.CanEdit));
            // 编辑指定ID项命令
            EditItemCommand = ReactiveCommand.Create<int>(OnEditItem);
            // 删除指定ID项命令
            DeleteItemCommand = ReactiveCommand.Create<int>(OnDeleteItem);
            // 批量删除命令
            BatchDeleteCommand = ReactiveCommand.Create(OnBatchDelete, this.WhenAnyValue(x => x.CanDelete));
            // 筛选命令
            FilterCommand = ReactiveCommand.Create(OnFilter);
            // 保存命令
            SaveCommand = ReactiveCommand.Create(OnSave);
            // 取消命令
            CancelCommand = ReactiveCommand.Create(OnCancel);
            // 刷新命令
            RefreshCommand = ReactiveCommand.Create(OnRefresh);

            // 同步数据到视图集合
            SyncBTEntities();

            // 监听选中项变化
            this.WhenAnyValue(x => x.SelectedBTEntity)
                .Subscribe(selected =>
                {
                    CanEdit = selected != null;
                    CanDelete = selected != null;
                });
        }
        #endregion

        #region 视图绑定属性（对齐BOMViewModel风格）
        /// <summary>
        /// BT实体列表（供前端绑定）
        /// </summary>
        [Reactive]
        public ObservableCollection<BTEntity> BTEntities { get; set; } = new ObservableCollection<BTEntity>();

        /// <summary>
        /// 当前选中项
        /// </summary>
        [Reactive]
        public BTEntity SelectedBTEntity { get; set; }

        /// <summary>
        /// 底板码筛选条件
        /// </summary>
        [Reactive]
        public string FilterBottomCode { get; set; } = string.Empty;

        /// <summary>
        /// 盖板码筛选条件
        /// </summary>
        [Reactive]
        public string FilterTopCode { get; set; } = string.Empty;

        /// <summary>
        /// 是否显示编辑/新增表单
        /// </summary>
        [Reactive]
        public bool IsFormVisible { get; set; } = false;

        /// <summary>
        /// 表单标题
        /// </summary>
        [Reactive]
        public string FormTitle { get; set; } = MultiLang.AddBindInfo;

        /// <summary>
        /// 当前编辑/新增的实体
        /// </summary>
        [Reactive]
        public BTEntity CurrentEntity { get; set; } = new BTEntity();

        /// <summary>
        /// 是否可编辑
        /// </summary>
        [Reactive]
        public bool CanEdit { get; set; } = false;

        /// <summary>
        /// 是否可删除
        /// </summary>
        [Reactive]
        public bool CanDelete { get; set; } = false;

        /// <summary>
        /// 新增/编辑模式标识
        /// </summary>
        private bool _isAddMode;
        #endregion

        #region 命令定义（对齐BOMViewModel风格）
        public ICommand LoadedCommand { get; }
        public ICommand AddCommand { get; }
        public ICommand EditCommand { get; }
        public ICommand EditItemCommand { get; }
        public ICommand DeleteItemCommand { get; }
        public ICommand BatchDeleteCommand { get; }
        public ICommand FilterCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand RefreshCommand { get; }
        #endregion

        #region 命令初始化

        #endregion

        #region 核心业务逻辑（对齐BOMViewModel风格，补充异常处理）
        /// <summary>
        /// 页面加载逻辑
        /// </summary>
        private void OnLoaded()
        {
            try
            {
                _btDataManager.GetAll();
                SyncBTEntities();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"{MultiLang.DataLoadFailed}：{ex.Message}", MultiLang.Error, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 刷新数据
        /// </summary>
        private void OnRefresh()
        {
            OnLoaded(); // 复用加载逻辑
        }

        /// <summary>
        /// 同步数据到视图集合
        /// </summary>
        private void SyncBTEntities()
        {
            BTEntities.Clear();
            foreach (var item in _btDataManager.BTEntities)
            {
                BTEntities.Add(item);
            }
        }

        /// <summary>
        /// 筛选数据
        /// </summary>
        private void OnFilter()
        {
            try
            {
                var filtered = _btDataManager.BTEntities.AsEnumerable();

                // 底板码筛选
                if (!string.IsNullOrEmpty(FilterBottomCode))
                {
                    filtered = filtered.Where(t => t.BottomCode.Contains(FilterBottomCode));
                }

                // 盖板码筛选
                if (!string.IsNullOrEmpty(FilterTopCode))
                {
                    filtered = filtered.Where(t => t.TopCode.Contains(FilterTopCode));
                }

                // 更新视图集合
                BTEntities.Clear();
                foreach (var item in filtered)
                {
                    BTEntities.Add(item);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"{MultiLang.FilterFailed}：{ex.Message}", MultiLang.Error, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 新增操作
        /// </summary>
        private void OnAdd()
        {
            _isAddMode = true;
            FormTitle = MultiLang.AddBindInfo;
            CurrentEntity = new BTEntity
            {
                Counts = "0",
                UpdateTime = DateTime.Now
            };
            IsFormVisible = true;
        }

        /// <summary>
        /// 编辑选中项
        /// </summary>
        private void OnEdit()
        {
            if (SelectedBTEntity != null)
            {
                OnEditItem(SelectedBTEntity.Id);
            }
        }

        /// <summary>
        /// 编辑指定ID项
        /// </summary>
        /// <param name="id">实体ID</param>
        private void OnEditItem(int id)
        {
            try
            {
                _isAddMode = false;
                var entity = _btDataManager.BTRepository.Get(id);
                if (entity == null)
                {
                    MessageBox.Show(MultiLang.BindInfoNotFound, MultiLang.Tip, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                FormTitle = MultiLang.EditBindInfo;
                CurrentEntity = entity;
                IsFormVisible = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"{MultiLang.EditDataLoadFailed}：{ex.Message}", MultiLang.Error, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 删除指定ID项
        /// </summary>
        /// <param name="id">实体ID</param>
        private void OnDeleteItem(int id)
        {
            try
            {
                var result = MessageBox.Show(MultiLang.ConfirmDeleteBindInfo, MultiLang.ConfirmDelete,
                    MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result != MessageBoxResult.Yes) return;

                var entity = _btDataManager.BTRepository.Get(id);
                if (entity == null)
                {
                    MessageBox.Show(MultiLang.BindInfoNotFound, MultiLang.Tip, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                _btDataManager.BTRepository.Delete(entity);
                OnRefresh(); // 刷新数据
                MessageBox.Show(MultiLang.DeleteSuccess, MultiLang.Tip, MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"{MultiLang.DeleteFailed}：{ex.Message}", MultiLang.Error, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 批量删除选中项
        /// </summary>
        private void OnBatchDelete()
        {
            try
            {
                var result = MessageBox.Show(MultiLang.ConfirmBatchDeleteBindInfo, MultiLang.ConfirmBatchDelete,
                    MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result != MessageBoxResult.Yes) return;

                _btDataManager.BTRepository.Delete(SelectedBTEntity);
                OnRefresh(); // 刷新数据
                MessageBox.Show(MultiLang.BatchDeleteSuccess, MultiLang.Tip, MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"{MultiLang.BatchDeleteFailed}：{ex.Message}", MultiLang.Error, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 保存操作（新增/编辑）
        /// </summary>
        private void OnSave()
        {
            // 数据验证（对齐BOMViewModel的验证逻辑）
            if (string.IsNullOrEmpty(CurrentEntity.BottomCode))
            {
                MessageBox.Show(MultiLang.BottomCodeNotEmpty, MultiLang.ValidateTip, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrEmpty(CurrentEntity.TopCode))
            {
                MessageBox.Show(MultiLang.TopCodeNotEmpty, MultiLang.ValidateTip, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 唯一性验证（对齐BOMViewModel的重复校验）
            var existEntity = _btDataManager.BTRepository.Select(CurrentEntity.BottomCode);
            if (_isAddMode && existEntity != null)
            {
                MessageBox.Show($"{MultiLang.BottomCodeExists}{CurrentEntity.BottomCode}", MultiLang.ValidateTip, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                CurrentEntity.UpdateTime = DateTime.Now;

                int count = 0;
                if (_isAddMode)
                {
                    // 新增
                    count = _btDataManager.BTRepository.Insert(CurrentEntity);
                }
                else
                {
                    // 编辑
                    count = _btDataManager.BTRepository.Update(CurrentEntity);
                }

                if (count > 0)
                {
                    MessageBox.Show($"{(_isAddMode ? MultiLang.Add : MultiLang.Edit)}{MultiLang.Success2}", MultiLang.Tip, MessageBoxButton.OK, MessageBoxImage.Information);
                    OnRefresh(); // 刷新数据
                    OnCancel(); // 关闭表单
                }
                else
                {
                    MessageBox.Show($"{(_isAddMode ? MultiLang.Add : MultiLang.Edit)}{MultiLang.Fail2}", MultiLang.Error, MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"{(_isAddMode ? MultiLang.Add : MultiLang.Edit)}{MultiLang.Fail2}：{ex.Message}", MultiLang.Error, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 取消操作
        /// </summary>
        private void OnCancel()
        {
            IsFormVisible = false;
            CurrentEntity = new BTEntity();
        }
        #endregion
    }
}