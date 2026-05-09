using System;
using System.Collections.Generic;

namespace FZK.Hardware.PLC.Base
{
    /// <summary>
    /// PLC通信核心接口（所有PLC驱动类统一实现）
    /// </summary>
    public interface IPLC
    {
        #region 状态属性（只读）
        /// <summary>是否初始化完成</summary>
        bool Initialized { get; }
        /// <summary>是否连接成功</summary>
        bool Connected { get; }
        /// <summary>PLC当前通信状态</summary>
        PLCState PLCState { get; }
        /// <summary>最新异常/状态消息</summary>
        string Message { get; }
        /// <summary>最新接收的响应帧（十六进制字符串）</summary>
        string ReceiveFrame { get; }
        #endregion

        #region 响应式可观察对象
        IObservable<string> MessageObservable { get; }
        IObservable<string> ReceiveFrameObservable { get; }
        #endregion

        #region 核心方法（与原有签名完全一致）
        bool Init(PLCConfig config);
        int Read(PLCRegisterType registerType, ushort address, bool isBCD = false);
        List<int> BatchRead(PLCRegisterType registerType, ushort startAddress, ushort count, bool isBCD = false);
        bool Write(PLCRegisterType registerType, ushort address, int value, bool isBCD = false, bool Require = true);
        bool BatchWrite(PLCRegisterType registerType, ushort startAddress, List<int> values, bool isBCD = false, bool Require = true);
        bool CheckConnection();
        void Close();
        #endregion
    }
}