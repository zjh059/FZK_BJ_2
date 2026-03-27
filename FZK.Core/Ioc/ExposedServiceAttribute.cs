using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FZK.Core.Ioc
{
    /// <summary>
    /// 类型的生命周期枚举
    /// </summary>
    public enum Lifetime
    {
        /// <summary>
        /// 单例
        /// </summary>
        Singleton,
        /// <summary>
        /// 多里
        /// </summary>
        Transien
    }
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class ExposedServiceAttribute : Attribute
    {
        public Lifetime Lifetime { get; set; }
        public bool AutoInitialize { get; set; }
        public Type[] Types { get; set; }
        /// <summary>
        /// 标注类型的生命周期是否自动初始化
        /// </summary>
        /// <param name="lifetime"></param>
        /// <param name="types"></param>
        public ExposedServiceAttribute(Lifetime lifetime = Lifetime.Transien, params Type[] types)
        {
            Lifetime = lifetime;
            Types = types;
        }
    }
}
