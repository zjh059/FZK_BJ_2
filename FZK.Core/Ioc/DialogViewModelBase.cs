using Prism.Services.Dialogs;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FZK.Core.Ioc
{
    public class DialogViewModelBase : ReactiveObject, IDialogAware
    {
        public string Title { get; set; } = "对话框标题";

        public event Action<IDialogResult> RequestClose;

        /// <summary>
        /// 关闭对话框,判断是否可以关闭
        /// </summary>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public virtual bool CanCloseDialog()
        {
            return true;
        }
        /// <summary>
        /// 关闭时执行的方法
        /// </summary>
        /// <exception cref="NotImplementedException"></exception>
        public virtual void OnDialogClosed()
        {

        }

        public virtual void OnDialogOpened(IDialogParameters parameters)
        {
        }

        /// <summary>
        /// 关闭对话框
        /// </summary>
        /// <param name="buttonResult"></param>
        /// <param name="dialogParameters"></param>
        protected virtual void CloseDialog(ButtonResult buttonResult, IDialogParameters dialogParameters = null)
        {
            RequestClose?.Invoke(new DialogResult(buttonResult, dialogParameters));
        }
    }
}
