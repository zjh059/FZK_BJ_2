using FZK.Application.Debug.Service;
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
    /// 码值绑定管理ViewModel（适配.NET 4.7.2 + ReactiveUI + Prism）
    /// </summary>
    public class CodeEntityDebugViewModel : ReactiveObject
    {
        #region 依赖注入
        private readonly IDatabaseManager _databaseManager;

        /// <summary>
        /// 构造函数（Prism IOC注入）
        /// </summary>
        /// <param name="databaseManager">数据库管理服务</param>
        public CodeEntityDebugViewModel(IDatabaseManager databaseManager)
        {
            _databaseManager = databaseManager;

            // 初始化命令
            AddCommand = ReactiveCommand.Create(OnAdd);
            EditCommand = ReactiveCommand.Create(OnEdit, this.WhenAnyValue(x => x.CanEdit));
            EditItemCommand = ReactiveCommand.Create<int>(OnEditItem);
            DeleteItemCommand = ReactiveCommand.Create<int>(OnDeleteItem);
            BatchDeleteCommand = ReactiveCommand.Create(OnBatchDelete, this.WhenAnyValue(x => x.CanDelete));
            RefreshCommand = ReactiveCommand.Create(OnRefresh);
            FilterCommand = ReactiveCommand.Create(OnFilter);
            SaveCommand = ReactiveCommand.Create(OnSave);
            CancelCommand = ReactiveCommand.Create(OnCancel);

            // 导出命令（预留实现）
            ExportExcelCommand = ReactiveCommand.Create(OnExportExcel);
            ExportCsvCommand = ReactiveCommand.Create(OnExportCsv);

            // 同步数据到视图集合
            SyncCodeEntities();

            // 监听选中项变化，更新按钮状态
            this.WhenAnyValue(x => x.SelectedCodeEntity)
                .Subscribe(selected =>
                {
                    CanEdit = selected != null;
                    CanDelete = selected != null;
                });
        }
        #endregion

        #region 视图绑定属性
        /// <summary>
        /// 码值实体列表（供前端绑定）
        /// </summary>
        [Reactive]
        public ObservableCollection<CodeEntity> CodeEntities { get; set; } = new ObservableCollection<CodeEntity>();

        /// <summary>
        /// 当前选中项
        /// </summary>
        [Reactive]
        public CodeEntity SelectedCodeEntity { get; set; }

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
        /// 主板码筛选条件
        /// </summary>
        [Reactive]
        public string FilterSPCode { get; set; } = string.Empty;

        /// <summary>
        /// 是否显示编辑/新增表单
        /// </summary>
        [Reactive]
        public bool IsFormVisible { get; set; } = false;

        /// <summary>
        /// 表单标题
        /// </summary>
        [Reactive]
        public string FormTitle { get; set; } = MultiLang.AddCodeBind;

        /// <summary>
        /// 当前编辑/新增的实体
        /// </summary>
        [Reactive]
        public CodeEntity CurrentEntity { get; set; } = new CodeEntity();

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

        #region 命令定义
        public ICommand AddCommand { get; }
        public ICommand EditCommand { get; }
        public ICommand EditItemCommand { get; }
        public ICommand DeleteItemCommand { get; }
        public ICommand BatchDeleteCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand FilterCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand ExportExcelCommand { get; }
        public ICommand ExportCsvCommand { get; }
        #endregion



        #region 核心业务逻辑
        /// <summary>
        /// 同步数据到视图集合
        /// </summary>
        private void SyncCodeEntities()
        {
            CodeEntities.Clear();
            foreach (var item in _databaseManager.CodeEntities)
            {
                CodeEntities.Add(item);
            }
        }

        /// <summary>
        /// 刷新数据
        /// </summary>
        private void OnRefresh()
        {
            try
            {
                _databaseManager.GetAll();
                SyncCodeEntities();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"{MultiLang.DataRefreshFailed}：{ex.Message}", MultiLang.Error, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 筛选数据
        /// </summary>
        private void OnFilter()
        {
            try
            {
                var filtered = _databaseManager.CodeEntities.AsEnumerable();

                // 多条件筛选
                if (!string.IsNullOrEmpty(FilterBottomCode))
                {
                    filtered = filtered.Where(t => t.BottomCode.Contains(FilterBottomCode));
                }
                if (!string.IsNullOrEmpty(FilterTopCode))
                {
                    filtered = filtered.Where(t => t.TopCode.Contains(FilterTopCode));
                }
                if (!string.IsNullOrEmpty(FilterSPCode))
                {
                    filtered = filtered.Where(t => t.SPCode.Contains(FilterSPCode));
                }

                // 更新视图集合
                CodeEntities.Clear();
                foreach (var item in filtered)
                {
                    CodeEntities.Add(item);
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
            FormTitle = MultiLang.AddCodeBind;
            CurrentEntity = new CodeEntity
            {
                Result = "0", // 默认未测试
                InsertDate = DateTime.Now
            };
            IsFormVisible = true;
        }

        /// <summary>
        /// 编辑选中项
        /// </summary>
        private void OnEdit()
        {
            if (SelectedCodeEntity != null)
            {
                OnEditItem(SelectedCodeEntity.Id);
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
                var entity = _databaseManager.CodeRepository.Get(id);
                if (entity == null)
                {
                    MessageBox.Show(MultiLang.CodeBindNotFound, MultiLang.Tip, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                FormTitle = MultiLang.EditCodeBind;
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
                var result = MessageBox.Show(MultiLang.ConfirmDeleteCodeBind, MultiLang.ConfirmDelete,
                    MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result != MessageBoxResult.Yes) return;

                var entity = _databaseManager.CodeRepository.Get(id);
                if (entity == null)
                {
                    MessageBox.Show(MultiLang.CodeBindNotFound, MultiLang.Tip, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                _databaseManager.CodeRepository.Delete(entity);
                OnRefresh();
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
                var result = MessageBox.Show(MultiLang.ConfirmBatchDeleteCodeBind, MultiLang.ConfirmBatchDelete,
                    MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result != MessageBoxResult.Yes) return;

                _databaseManager.CodeRepository.Delete(SelectedCodeEntity);
                OnRefresh();
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
            // 数据验证
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
            if (string.IsNullOrEmpty(CurrentEntity.SPCode))
            {
                MessageBox.Show(MultiLang.SPCodeNotEmpty, MultiLang.ValidateTip, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (string.IsNullOrEmpty(CurrentEntity.Result))
            {
                MessageBox.Show(MultiLang.TestResultNotEmpty, MultiLang.ValidateTip, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                if (_isAddMode)
                {
                    CurrentEntity.InsertDate = DateTime.Now;
                    var count = _databaseManager.CodeRepository.Insert(CurrentEntity);
                    if (count > 0)
                    {
                        MessageBox.Show(MultiLang.AddSuccess, MultiLang.Tip, MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show(MultiLang.AddFailed, MultiLang.Error, MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }
                else
                {
                    var count = _databaseManager.CodeRepository.Update(CurrentEntity);
                    if (count > 0)
                    {
                        MessageBox.Show(MultiLang.EditSuccess, MultiLang.Tip, MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show(MultiLang.EditFailed, MultiLang.Error, MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }

                // 保存后刷新并关闭表单
                OnRefresh();
                OnCancel();
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
            CurrentEntity = new CodeEntity();
        }

        /// <summary>
        /// 导出Excel（预留实现）
        /// </summary>
        private void OnExportExcel()
        {
            try
            {
                // 此处可添加Excel导出逻辑
                MessageBox.Show(MultiLang.ExcelNotImplemented, MultiLang.Tip, MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"{MultiLang.ExcelExportFailed}：{ex.Message}", MultiLang.Error, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 导出CSV（预留实现）
        /// </summary>
        private void OnExportCsv()
        {
            try
            {
                // 此处可添加CSV导出逻辑
                MessageBox.Show(MultiLang.CsvNotImplemented, MultiLang.Tip, MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"{MultiLang.CsvExportFailed}：{ex.Message}", MultiLang.Error, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion
    }
}