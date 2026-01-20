using FZK.Core.Helper;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FZK.Core.Config
{
    public class ConfigManager : IConfigManager
    {

        public static bool NoHardwareMode { get; set; } = true;

        private const string root = "Config";

        private string GetFullPath(ValueType key)
        {
            return Path.Combine(root, key.GetType().FullName + "." + key.ToString() + ".json");
        }

        /// <summary>
        /// 读配置文件
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <returns></returns>
        public T Read<T>(ValueType key)
        {
            string fileName = GetFullPath(key);
            return JsonHelper.Load<T>(fileName);
        }

        /// <summary>
        /// 写配置文件
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public void Write<T>(ValueType key, T value)
        {
            Directory.CreateDirectory(root);
            string fileName = GetFullPath(key);
            JsonHelper.Write(value, fileName, true);
        }
    }
}
