using FZK.Core.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FZK.Core.Language
{
    public interface ILanguageManager
    {
        string this[string key] { get; }
        /// <summary>
        /// 当前语言
        /// </summary>
        LanguageType Current { get; }
        /// <summary>
        /// 设置语言
        /// </summary>
        /// <param name="type"></param>
        void Set(LanguageType type);
    }
}
