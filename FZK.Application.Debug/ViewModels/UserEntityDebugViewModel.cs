using FZK.Application.Debug.Service;
using FZK.Application.Share.DebugFolder;
using FZK.Database.Base.Models;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace FZK.Application.Debug.ViewModels
{
    /// <summary>
    /// 用户管理ViewModel（适配.NET 4.7.2 + ReactiveUI + Prism）
    /// </summary>
    public class UserEntityDebugViewModel : ReactiveObject
    {
        #region 依赖注入
        private readonly IDatabaseManager _databaseManager;

        /// <summary>
        /// 构造函数（Prism IOC注入）
        /// </summary>
        /// <param name="databaseManager">数据库管理服务</param>
        public UserEntityDebugViewModel(IDatabaseManager databaseManager)
        {
            _databaseManager = databaseManager;

            // 初始化命令（直接在构造函数内赋值，无额外方法封装）
            AddCommand = ReactiveCommand.Create(OnAdd);
            EditCommand = ReactiveCommand.Create(OnEdit, this.WhenAnyValue(x => x.CanEdit));
            EditItemCommand = ReactiveCommand.Create<int>(OnEditItem);
            DeleteItemCommand = ReactiveCommand.Create<int>(OnDeleteItem);
            BatchDeleteCommand = ReactiveCommand.Create(OnBatchDelete, this.WhenAnyValue(x => x.CanDelete));
            RefreshCommand = ReactiveCommand.Create(OnRefresh);
            FilterCommand = ReactiveCommand.Create(OnFilter);
            SaveCommand = ReactiveCommand.Create(OnSave);
            CancelCommand = ReactiveCommand.Create(OnCancel);

            // 同步数据到视图集合
            SyncUserEntities();

            // 监听选中项变化，更新按钮状态
            this.WhenAnyValue(x => x.SelectedUserEntity)
                .Subscribe(selected =>
                {
                    CanEdit = selected != null;
                    CanDelete = selected != null;
                });

            // 初始化筛选角色为全部
            FilterRole = -1;
        }
        #endregion

        #region 视图绑定属性
        /// <summary>
        /// 用户实体列表（供前端绑定）
        /// </summary>
        [Reactive]
        public ObservableCollection<UserEntity> UserEntities { get; set; } = new ObservableCollection<UserEntity>();

        /// <summary>
        /// 当前选中项
        /// </summary>
        [Reactive]
        public UserEntity SelectedUserEntity { get; set; }

        /// <summary>
        /// 用户名筛选条件
        /// </summary>
        [Reactive]
        public string FilterUserName { get; set; } = string.Empty;

        /// <summary>
        /// 角色筛选条件（-1=全部，0=普通用户，1=管理员，2=超级管理员）
        /// </summary>
        [Reactive]
        public int FilterRole { get; set; } = -1;

        /// <summary>
        /// 是否显示编辑/新增表单
        /// </summary>
        [Reactive]
        public bool IsFormVisible { get; set; } = false;

        /// <summary>
        /// 表单标题
        /// </summary>
        [Reactive]
        public string FormTitle { get; set; } = "新增用户";

        /// <summary>
        /// 当前编辑/新增的实体
        /// </summary>
        [Reactive]
        public UserEntity CurrentEntity { get; set; } = new UserEntity();

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
        #endregion

        #region 核心业务逻辑
        /// <summary>
        /// 同步数据到视图集合
        /// </summary>
        private void SyncUserEntities()
        {
            UserEntities.Clear();
            foreach (var item in _databaseManager.UserEntities)
            {
                UserEntities.Add(item);
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
                SyncUserEntities();
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
                var filtered = _databaseManager.UserEntities.AsEnumerable();

                // 用户名筛选
                if (!string.IsNullOrEmpty(FilterUserName))
                {
                    filtered = filtered.Where(t => t.UserName.Contains(FilterUserName));
                }

                // 角色筛选（-1=全部）
                if (FilterRole != -1)
                {
                    filtered = filtered.Where(t => t.Role == FilterRole);
                }

                // 更新视图集合
                UserEntities.Clear();
                foreach (var item in filtered)
                {
                    UserEntities.Add(item);
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
            FormTitle = "新增用户";
            CurrentEntity = new UserEntity
            {
                Role = 0, // 默认普通用户
                UserName = string.Empty,
                Password = string.Empty
            };
            IsFormVisible = true;
        }

        /// <summary>
        /// 编辑选中项
        /// </summary>
        private void OnEdit()
        {
            if (SelectedUserEntity != null)
            {
                OnEditItem(SelectedUserEntity.Id);
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
                var entity = _databaseManager.UserRepository.Get(id);
                if (entity == null)
                {
                    MessageBox.Show("未找到指定的用户信息", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                FormTitle = "编辑用户";
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
                var result = MessageBox.Show("确定要删除这个用户吗？", "确认删除",
                    MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result != MessageBoxResult.Yes) return;

                var entity = _databaseManager.UserRepository.Get(id);
                if (entity == null)
                {
                    MessageBox.Show("未找到指定的用户信息", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                _databaseManager.UserRepository.Delete(entity);
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
                var result = MessageBox.Show("确定要删除选中的用户吗？", "确认批量删除",
                    MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result != MessageBoxResult.Yes) return;

                _databaseManager.UserRepository.Delete(SelectedUserEntity);
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
            if (string.IsNullOrEmpty(CurrentEntity.UserName))
            {
                MessageBox.Show("用户名不能为空", "验证提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (string.IsNullOrEmpty(CurrentEntity.Password))
            {
                MessageBox.Show("密码不能为空", "验证提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 新增时校验用户名唯一性
            if (_isAddMode)
            {
                var existUser = _databaseManager.UserRepository.Select(CurrentEntity.UserName);
                if (existUser != null)
                {
                    MessageBox.Show($"用户名{CurrentEntity.UserName}已存在", "验证提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            try
            {
                int count = 0;
                if (_isAddMode)
                {
                    // 新增
                    count = _databaseManager.UserRepository.Insert(CurrentEntity);
                }
                else
                {
                    // 编辑
                    count = _databaseManager.UserRepository.Update(CurrentEntity);
                }

                if (count > 0)
                {
                    MessageBox.Show($"{(_isAddMode ? "新增" : "编辑")}成功", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    OnRefresh();
                    OnCancel();
                }
                else
                {
                    MessageBox.Show($"{(_isAddMode ? "新增" : "编辑")}失败", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
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
            CurrentEntity = new UserEntity();
        }
        #endregion
    }

    /// <summary>
    /// PasswordBox绑定辅助类（解决PasswordBox无法直接绑定的问题）
    /// </summary>
    public static class PasswordBoxHelper
    {
        public static readonly DependencyProperty BindPasswordProperty =
            DependencyProperty.RegisterAttached("BindPassword", typeof(string), typeof(PasswordBoxHelper),
                new PropertyMetadata(string.Empty, OnBindPasswordChanged));

        public static string GetBindPassword(DependencyObject obj)
        {
            return (string)obj.GetValue(BindPasswordProperty);
        }

        public static void SetBindPassword(DependencyObject obj, string value)
        {
            obj.SetValue(BindPasswordProperty, value);
        }

        private static void OnBindPasswordChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is PasswordBox passwordBox)
            {
                passwordBox.PasswordChanged -= PasswordBox_PasswordChanged;
                passwordBox.Password = e.NewValue.ToString();
                passwordBox.PasswordChanged += PasswordBox_PasswordChanged;
            }
        }

        private static void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (sender is PasswordBox passwordBox)
            {
                SetBindPassword(passwordBox, passwordBox.Password);
            }
        }
    }

    /// <summary>
    /// 密码脱敏转换器
    /// </summary>
    public class PasswordMaskConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is string password && !string.IsNullOrEmpty(password))
            {
                return new string('*', password.Length);
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}