using FZK.Application.Config.Models;
using FZK.Application.Share.Config;
using FZK.Core.Config;
using FZK.Core.Enums;
using FZK.Hardware.PLC.Base;
using FZK.Hardware.Scanner.Base;
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

        public ScannerConfig LeftUpScannerConfig => Config.LeftUpScannerConfig;

        public ScannerConfig RightUpScannerConfig => Config.RightUpScannerConfig;

        public ScannerConfig LeftDownScannerConfig => Config.LeftDownScannerConfig;

        public ScannerConfig RightDownScannerConfig => Config.RightDownScannerConfig;

        public ScannerConfig RobotScannerConfig => Config.RobotScannerConfig;

        public Hardware.PLC.Base.PLCConfig pLCConfig => Config.pLCConfig;
        public Hardware.Robot.Base.RobotConfig robotConfig => Config.robotConfig;

        public PlcAddressConfig plcAddressConfig => Config.plcAddressConfig;

        public RunConfig runConfig => Config.runConfig;

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
            elsSquare();
            ConfigManager.Write(ConfigKey.SystemConfig, Config);//序列化
        }
        /// <summary>
        /// 对齐颗粒度
        /// </summary>
        public void elsSquare()
        {
            var delaytime = LeftUpScannerConfig.DelayTime;
            RightUpScannerConfig.DelayTime = delaytime;
            LeftDownScannerConfig.DelayTime = delaytime;
            RightDownScannerConfig.DelayTime = delaytime;
            RobotScannerConfig.DelayTime = delaytime;

            var triggerCommand = LeftUpScannerConfig.TriggerCommand;
            RightUpScannerConfig.TriggerCommand = triggerCommand;
            LeftDownScannerConfig.TriggerCommand = triggerCommand;
            RightDownScannerConfig.TriggerCommand = triggerCommand;
            RobotScannerConfig.TriggerCommand = triggerCommand;

            var maxReconnectCount = LeftUpScannerConfig.MaxReconnectCount;
            RightUpScannerConfig.MaxReconnectCount = maxReconnectCount;
            LeftDownScannerConfig.MaxReconnectCount = maxReconnectCount;
            RightDownScannerConfig.MaxReconnectCount = maxReconnectCount;
            RobotScannerConfig.MaxReconnectCount = maxReconnectCount;

            var reconnectDelay = LeftUpScannerConfig.ReconnectDelay;
            RightUpScannerConfig.ReconnectDelay = reconnectDelay;
            LeftDownScannerConfig.ReconnectDelay = reconnectDelay;
            RightDownScannerConfig.ReconnectDelay = reconnectDelay;
            RobotScannerConfig.ReconnectDelay = reconnectDelay;
        }
    }
}

