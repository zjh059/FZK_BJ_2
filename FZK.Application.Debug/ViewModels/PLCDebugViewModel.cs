using ControlzEx.Standard;
using FZK.Application.Share.Config;
using FZK.Application.Share.Init;
using FZK.Core.Extensions;
using FZK.Hardware.PLC.Base;
using FZK.Logger;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.RightsManagement;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace FZK.Application.Debug.ViewModels
{
    internal class PLCDebugViewModel : ReactiveObject
    {
        /// <summary>
        /// IP地址
        /// </summary>
        public string IPAdress { get; set; }
        /// <summary>
        /// 网络号
        /// </summary>
        [Reactive]
        public string NetworkNo { get; set; }
        /// <summary>
        /// 计算机节点号
        /// </summary>
        [Reactive]
        public string sourceNode { get; set; }
        /// <summary>
        /// 端口
        /// </summary>
        [Reactive]
        public string Port { get; set; }

        [Reactive]
        public bool Connected { get; set; }
        [Reactive]
        public string RwReadValue { get; set; }
        [Reactive]
        public string RwWriteValue { get; set; }
        [Reactive]
        public string RwAddress { get; set; }
        public ushort adress = 0;
        public ICommand ConnectPlcCommand { get; }
        public ICommand DisconnectPlcCommand { get; }
        public ICommand ReadCommand { get; }
        public ICommand WriteCommand { get; }

        public ISystemConfigManager SystemConfigManager { get; }
        public IPLC OmronPLC { get; }

        public PLCRegisterType RegisterTypes { get; set; }
        public List<PLCRegisterType> PLCRegisterTypes { get; set; } = EnumExtension.ToList<PLCRegisterType>();

        public PLCDebugViewModel(
            ISystemConfigManager systemConfigManager,
            IHardwareManager hardwareManager
            )
        {
            SystemConfigManager = systemConfigManager;
            OmronPLC = hardwareManager.OmronPLC;
            ConnectPlcCommand = ReactiveCommand.Create(OnConnectPlcCommand);
            ReadCommand = ReactiveCommand.Create(OnReadCommand);
            WriteCommand = ReactiveCommand.Create(OnWriteCommand);
            DisconnectPlcCommand = ReactiveCommand.Create(OnDisconnectPlcCommand);
            Connected = OmronPLC.Connected;
            IPAdress = SystemConfigManager.pLCConfig.IpAddress.ToString();
            Port = systemConfigManager.pLCConfig.Port.ToString();
        }
        ushort result = 0;
        private void OnWriteCommand()
        {
            if (Connected)
            {

                if (ushort.TryParse(RwAddress, out adress))
                {
                    OmronPLC.Write(RegisterTypes, adress, Convert.ToInt32(RwWriteValue));
                    MessageBox.Show($"PLC写入{RwAddress}:{RwWriteValue}");
                }
                else
                {
                    MessageBox.Show($"PLC地址错误");
                }
            }
            else
            {
                MessageBox.Show($"PLC未连接,写入失败");
            }
        }

        private void OnReadCommand()
        {
            if (Connected)
            {
                if (ushort.TryParse(RwAddress, out adress))
                {
                    var value = OmronPLC.Read(RegisterTypes, adress);                   
                    MessageBox.Show($"PLC读取{RwAddress}:{value}");
                }
                else
                {
                    MessageBox.Show($"PLC地址错误");
                }
            }
            else
            {
                MessageBox.Show($"PLC未连接,读取失败");
            }
        }

        private void OnConnectPlcCommand()
        {
            if (!Connected)
            {
                OmronPLC.Init(SystemConfigManager.pLCConfig);
                Connected = OmronPLC.Connected;

            }
            Logs.LogInfo("调试：PLC已连接...");
        }

        private void OnDisconnectPlcCommand()
        {
            if (Connected)
            {
                OmronPLC.Close();
                Connected = OmronPLC.Connected;
                Logs.LogInfo("调试：PLC已断开...");

            }
        }
    }
}
