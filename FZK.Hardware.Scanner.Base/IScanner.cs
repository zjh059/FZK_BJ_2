using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FZK.Hardware.Scanner.Base
{
    /// <summary>
    /// 表示一台工业扫码器的接口
    /// </summary>
    public interface IScanner
    {
        /// <summary>
        /// 是否加载成功
        /// </summary>
        bool Initialized { get; }
        /// <summary>
        /// 连接状态 
        /// </summary>
        bool Connected { get; }
        /// <summary>
        /// 运行消息
        /// </summary>
        string Message { get; }
        /// <summary>
        /// 运行信息观察者
        /// </summary>
        IObservable<string> MessageObservable { get; }
        /// <summary>
        /// 相机扫码器初始化
        /// </summary>
        /// <param name="cameraConfig"></param>
        /// <returns></returns>
        bool Init(ScannerConfig scannerConfig);
        /// <summary>
        /// 触发
        /// </summary>
        /// <returns></returns>
        Task<bool> TriggerAsync();
        string Content { get; }
        /// <summary>
        /// 产品码观察者
        /// </summary>
        IObservable<string> ContentObservable { get; }
        /// <summary>
        /// 关闭扫码
        /// </summary>
        void Close();
    }
}
