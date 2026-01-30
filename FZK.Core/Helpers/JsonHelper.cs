using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace FZK.Core.Helpers
{
    /// <summary>
    /// JSON文件读写帮助类
    /// </summary>
    public static class JsonHelper
    {
        /// <summary>
        /// 读本地JSON
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="json"></param>
        /// <returns></returns>
        public static T Load<T>(string json)
        {
            if (!File.Exists(json))
            {
                return default(T);
            }

            string content = File.ReadAllText(json);
            var result = Deserialize<T>(content);
            return result;
        }

        public static T Deserialize<T>(string json)
        {
            return JsonConvert.DeserializeObject<T>(json);
        }

        /// <summary>
        /// 写JSON到本地文件
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="fullname"></param>
        /// <param name="indented"></param>
        public static void Write(object obj, string path, bool indented = false)
        {
            string contents = Serialize(obj, indented);
            File.WriteAllText(path, contents);
        }
        public static string Serialize(object obj, bool indented = false)
        {
            Formatting f = indented ? Formatting.Indented : Formatting.None;
            return JsonConvert.SerializeObject(obj, f);
        }
    }
}
