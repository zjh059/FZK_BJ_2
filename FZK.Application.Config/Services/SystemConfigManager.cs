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

        public ScannerConfig Jig1UpScannerConfig => Config.LeftUpScannerConfig;

        public ScannerConfig Jig2UpScannerConfig => Config.RightUpScannerConfig;

        public ScannerConfig Jig1DownScannerConfig => Config.LeftDownScannerConfig;

        public ScannerConfig Jig2DownScannerConfig => Config.RightDownScannerConfig;

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
            var delaytime = Jig1UpScannerConfig.DelayTime;
            Jig2UpScannerConfig.DelayTime = delaytime;
            Jig1DownScannerConfig.DelayTime = delaytime;
            Jig2DownScannerConfig.DelayTime = delaytime;
            RobotScannerConfig.DelayTime = delaytime;

            var triggerCommand = Jig1UpScannerConfig.TriggerCommand;
            Jig2UpScannerConfig.TriggerCommand = triggerCommand;
            Jig1DownScannerConfig.TriggerCommand = triggerCommand;
            Jig2DownScannerConfig.TriggerCommand = triggerCommand;
            RobotScannerConfig.TriggerCommand = triggerCommand;

            var maxReconnectCount = Jig1UpScannerConfig.MaxReconnectCount;
            Jig2UpScannerConfig.MaxReconnectCount = maxReconnectCount;
            Jig1DownScannerConfig.MaxReconnectCount = maxReconnectCount;
            Jig2DownScannerConfig.MaxReconnectCount = maxReconnectCount;
            RobotScannerConfig.MaxReconnectCount = maxReconnectCount;

            var reconnectDelay = Jig1UpScannerConfig.ReconnectDelay;
            Jig2UpScannerConfig.ReconnectDelay = reconnectDelay;
            Jig1DownScannerConfig.ReconnectDelay = reconnectDelay;
            Jig2DownScannerConfig.ReconnectDelay = reconnectDelay;
            RobotScannerConfig.ReconnectDelay = reconnectDelay;
        }
    }
}

