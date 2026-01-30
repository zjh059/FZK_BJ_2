using FZK.Application.Config.Models;
using FZK.Application.Share.Config;
using FZK.Core.Config;
using FZK.Core.Enums;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FZK.Application.Config.Services
{
     class SystemConfigManager : ReactiveObject, ISystemConfigManager
    {
        private SystemConfigModel Config { get; set; }

        public SoftwareConfig SoftwareConfig => Config.SoftwareConfig;

        private IConfigManager ConfigManager { get; }

        public SystemConfigManager(IConfigManager configManager)
        {
            ConfigManager = configManager;
            Load();
        }

        public void Load()
        {
            //加载JSON配置文件，反序列化成SystemConfigModel对象
            Config = ConfigManager.Read<SystemConfigModel>(ConfigKey.SystemConfig);

            if (Config == null)
            {
                Config = new SystemConfigModel();
                ConfigManager.Write(ConfigKey.SystemConfig, Config);//序列化
            }
        }

        /// <summary>
        /// 保存系统参数
        /// </summary>
        public void Save()
        {
            ConfigManager.Write(ConfigKey.SystemConfig, Config);//序列化
        }
    }
}

