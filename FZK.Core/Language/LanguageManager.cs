using FZK.Core.Enums;
using FZK.Core.Helpers;
using FZK.Core.Ioc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace FZK.Core.Language
{
    [ExposedService(Lifetime.Singleton, typeof(ILanguageManager))]
    public class LanguageManager : ILanguageManager
    {
        private ResourceDictionary Resource { get; set; }
        private string Uri { get; set; }
        public LanguageManager()
        {
            Set(LanguageType.CNS);
        }
        /// <summary>
        /// 索引器
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public string this[string key]
        {
            get
            {
                if (this.Resource != null && this.Resource.Contains(key))
                {
                    return Resource[key].ToString();
                }
                return this[key];
            }
        }

        public LanguageType Current { get; private set; }

        public void Set(LanguageType language)
        {
            Assert.NotNull(language);
            if (this.Uri == null)
            {
                ResourceDictionary resourceDictionary = System.Windows.Application.Current.Resources.MergedDictionaries[0];
                string path = resourceDictionary.Source.AbsolutePath;
                this.Uri = path.Remove(path.LastIndexOf("/"));
            }
            string target = $"{Uri}/{language}.xaml";
            this.Resource = (ResourceDictionary)System.Windows.Application.LoadComponent(new Uri(target, UriKind.RelativeOrAbsolute));
            System.Windows.Application.Current.Resources.MergedDictionaries.RemoveAt(0);
            System.Windows.Application.Current.Resources.MergedDictionaries.Insert(0, Resource);
            if (Current != language)
            {
                Current = language;
                //to do保存到系统设置
            }
        }
    }
}
