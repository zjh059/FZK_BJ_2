using System;
using System.Collections.Generic;
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
        /// 扫码内容
        /// </summary>
        string Content { get; }

        /// <summary>
        /// 多码列表
        /// </summary>
        List<string> MultiCodes { get; }

        /// <summary>
        /// 产品码观察者
        /// </summary>
        IObservable<string> ContentObservable { get; }

        /// <summary>
        /// 多码列表观察者
        /// </summary>
        IObservable<List<string>> MultiCodesObservable { get; }

        /// <summary>
        /// 相机扫码器初始化
        /// </summary>
        /// <param name="scannerConfig">扫码器配置</param>
        /// <returns>初始化是否成功</returns>
        bool Init(ScannerConfig scannerConfig);

        /// <summary>
        /// 异步初始化
        /// </summary>
        /// <param name="scannerConfig">扫码器配置</param>
        /// <returns>初始化是否成功</returns>
        Task<bool> InitAsync(ScannerConfig scannerConfig);

        /// <summary>
        /// 触发扫码
        /// </summary>
        /// <returns>触发是否成功</returns>
        Task<bool> TriggerAsync();

        /// <summary>
        /// 清空扫码结果缓存
        /// </summary>
        void ClearContent();

        /// <summary>
        /// 关闭扫码器
        /// </summary>
        void Close();

        /// <summary>
        /// 异步关闭
        /// </summary>
        /// <returns>关闭任务</returns>
        Task CloseAsync();

        /// <summary>
        /// 检查连接状态（自动重连）
        /// </summary>
        /// <returns>连接是否正常</returns>
        Task<bool> CheckConnectionAsync();
    }
}