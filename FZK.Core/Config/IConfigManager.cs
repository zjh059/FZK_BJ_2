using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FZK.Core.Config
{
    /// <summary>
    /// 配置管理器接口
    /// </summary>
    public interface IConfigManager
    {
        /// <summary>
        /// 读取本地配置
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <returns></returns>
        T Read<T>(ValueType key);
        /// <summary>
        /// 写入配置到本地
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <param name="value"></param>
        void Write<T>(ValueType key, T value);
    }
}
