using FZK.Core.Ioc;
using Prism.Ioc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FZK.Core.Extension
{
    /// <summary>
    /// 依赖注入扩展类,可以实现加载模块时实例化标注为ExposedServiceAttribute特性的类
    /// </summary>
    public static class DependencyExtension
    {

        //获取当前程序集所有的Class;通过反射,获取程序集的类型列表
        //CustomerAttribute是指有自定义特性的类

        private static List<Type> GetTypes(Assembly assembly)
        {
            var result = assembly.GetTypes().Where(t => t != null && t.IsClass && !t.IsAbstract &&
            t.CustomAttributes.Any(p => p.AttributeType == typeof(ExposedServiceAttribute))).ToList(); //自定义特性的类型要等于我们自己定义的ExposedServiceAttribute
            return result;
        }


        /// <summary>
        /// 扩展IContainerRegistry接口的注册类型的功能
        /// </summary>
        /// <param name="container"></param>
        /// <param name="assembly">程序集包含了:每一个项目里面的所有东西,通常是Class,当然也包括一些枚举/资源文件一些</param>
        public static void RegisterAssembly(this IContainerRegistry container, Assembly assembly)
        {
            var list = GetTypes(assembly);
            foreach (var type in list)
            {
                RegisterAssembly(container, type);//多态
            }
        }

        private static IEnumerable<ExposedServiceAttribute> GetExposedServices(Type type)
        {
            var typeInfo = type.GetTypeInfo();
            return typeInfo.GetCustomAttributes<ExposedServiceAttribute>();
        }

        private static void RegisterAssembly(IContainerRegistry container, Type type)
        {
            var list = GetExposedServices(type).ToList();
            foreach (var typeInfo in list)
            {
                if (typeInfo.Lifetime == Lifetime.Singleton)
                {
                    container.RegisterSingleton(type);//注册单例
                }

                foreach (var IType in typeInfo.Types)
                {
                    if (typeInfo.Lifetime == Lifetime.Singleton)
                    {
                        container.RegisterSingleton(IType, type);//以接口注册单例
                    }
                    else if (typeInfo.Lifetime == Lifetime.Transien)
                    {
                        container.Register(IType, type);//以接口注册多例
                    }
                }
            }
        }


        //从容器中注册完成之后我们要从容器中拿出来 要初始化
        /// <summary>
        /// 初始化程序集中所有标注为的ExposedServiceAttribute特性的类,要求单例具有自动加载AutoInitialize=true
        /// </summary>
        /// <param name="container"></param>
        /// <param name="assembly"></param>
        public static void InitializeAssembly(this IContainerProvider container, Assembly assembly)
        {
            var list = GetTypes(assembly);
            foreach (var item in list)
            {
                InitializeAssembly(container, item);
            }
        }

        private static void InitializeAssembly(IContainerProvider container, Type type)
        {
            var list = GetExposedServices(type);

            foreach (var typeInfo in list)
            {
                if (typeInfo.Lifetime == Lifetime.Singleton && typeInfo.AutoInitialize)
                {
                    container.Resolve(type);
                }
            }
        }
    }
}
