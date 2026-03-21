using FZK.Application.Debug.Service;
using FZK.Application.Share.DebugFolder;
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
        public string FormTitle { get; set; } = "新增码值绑定";

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
                MessageBox.Show($"数据刷新失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
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
                MessageBox.Show($"筛选失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 新增操作
        /// </summary>
        private void OnAdd()
        {
            _isAddMode = true;
            FormTitle = "新增码值绑定";
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
                    MessageBox.Show("未找到指定的码值绑定信息", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                FormTitle = "编辑码值绑定";
                CurrentEntity = entity;
                IsFormVisible = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载编辑数据失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
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
                var result = MessageBox.Show("确定要删除这条码值绑定信息吗？", "确认删除",
                    MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result != MessageBoxResult.Yes) return;

                var entity = _databaseManager.CodeRepository.Get(id);
                if (entity == null)
                {
                    MessageBox.Show("未找到指定的码值绑定信息", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                _databaseManager.CodeRepository.Delete(entity);
                OnRefresh();
                MessageBox.Show("删除成功", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"删除失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 批量删除选中项
        /// </summary>
        private void OnBatchDelete()
        {
            try
            {
                var result = MessageBox.Show("确定要删除选中的码值绑定信息吗？", "确认批量删除",
                    MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result != MessageBoxResult.Yes) return;

                _databaseManager.CodeRepository.Delete(SelectedCodeEntity);
                OnRefresh();
                MessageBox.Show("批量删除成功", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"批量删除失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
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
                MessageBox.Show("底板码不能为空", "验证提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (string.IsNullOrEmpty(CurrentEntity.TopCode))
            {
                MessageBox.Show("盖板码不能为空", "验证提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (string.IsNullOrEmpty(CurrentEntity.SPCode))
            {
                MessageBox.Show("主板码不能为空", "验证提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (string.IsNullOrEmpty(CurrentEntity.Result))
            {
                MessageBox.Show("测试结果不能为空", "验证提示", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                        MessageBox.Show("新增成功", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show("新增失败", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }
                else
                {
                    var count = _databaseManager.CodeRepository.Update(CurrentEntity);
                    if (count > 0)
                    {
                        MessageBox.Show("编辑成功", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show("编辑失败", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }

                // 保存后刷新并关闭表单
                OnRefresh();
                OnCancel();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"{(_isAddMode ? "新增" : "编辑")}失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
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
                MessageBox.Show("Excel导出功能暂未实现", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Excel导出失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
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
                MessageBox.Show("CSV导出功能暂未实现", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"CSV导出失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion
    }
}