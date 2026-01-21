using FZK.Application.Share.Init;
using FZK.Application.Share.Models;
using Prism.Events;
using Prism.Ioc;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;

namespace FZK.Application.Initialize.Services
{
    /// <summary>
    /// 表示所有硬件是否加载
    /// </summary>
    class HardwareManager : ReactiveObject, IHardwareManager
    {
        [Reactive]
        public bool Initialized { get; set; }
        [Reactive]
        public string Message { get; set; }
        private IEventAggregator EventAggregator { get; }
        public HardwareManager(
            IEventAggregator eventAggregator,
            IContainerProvider containerProvider)
        {
            EventAggregator = eventAggregator;
        }


        public async Task<InitResult> InitAsync()
        {
            if (Initialized) throw new Exception("重复初始化硬件");

            InitResult result = new InitResult();
            result.Message = "初始化硬件失败";
            result.Success = false;
            await Task.Delay(500);//模拟耗时

            //todo  初始化硬件
            //Task<bool> leftCameraTask = Task.Run(() => LeftCamera.Init(SystemConfigManager.LeftCameraConfig));
            //Task<bool> rightCameraTask = Task.Run(() => RightCamera.Init(SystemConfigManager.RightCameraConfig));
            //Task<bool> controlcardTask = Task.Run(() => ControlCard.Init(SystemConfigManager.ControlCardConfig));
            //Task<bool> lightTask = Task.Run(() => Light.Init(SystemConfigManager.LightConfig));
            //Task<bool> bridgeTask = Task.Run(() => Bridge.Init(SystemConfigManager.BridgeConfig));
            //var boolArray = await Task.WhenAll(leftCameraTask, rightCameraTask, controlcardTask, lightTask, bridgeTask);
            //bool temp = boolArray.All(p => p);//判断所有子任务的加载结果是否都为true
            
          //  if (temp)
            {
                Initialized = true;
                result.Message = "初始化硬件成功";
                result.Success = true;
                EventAggregator.GetEvent<InitSuccessEvent>().Publish();//通知主窗体跳转
                return result;
            }
          //  else
            {
                //其中有一些模块加载失败
                //string msg = "以下硬件模块初始化失败\r\n";
                //if (leftCameraTask.Result == false)
                //{
                //    msg += "左相机模块 ";
                //}
                //if (rightCameraTask.Result == false)
                //{
                //    msg += "右相机模块 ";
                //}
                //if (controlcardTask.Result == false)
                //{
                //    msg += "控制卡模块 ";
                //}
                //if (lightTask.Result == false)
                //{
                //    msg += "光源模块 ";
                //}
                //if (bridgeTask.Result == false)
                //{
                //    msg += "电桥模块 ";
                //}
                //result.Message = msg;
                //return result;
            }
        }
    }
}
