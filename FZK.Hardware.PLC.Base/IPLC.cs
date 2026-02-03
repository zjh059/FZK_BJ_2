using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FZK.Hardware.PLC.Base
{
    /// <summary>
    /// PLC通信核心接口（所有PLC驱动类统一实现）
    /// </summary>
    public interface IPLC
    {
        #region 状态属性
        /// <summary>
        /// 是否初始化完成
        /// </summary>
        bool Initialized { get; set; }
        /// <summary>
        /// 是否连接成功
        /// </summary>
        bool Connected { get; set; }
        /// <summary>
        /// PLC当前通信状态
        /// </summary>
        PLCState PLCState { get; set; }
        /// <summary>
        /// 最新异常/状态消息
        /// </summary>
        string Message { get; set; }
        /// <summary>
        /// 最新接收的Fins响应帧（十六进制字符串，便于调试）
        /// </summary>
        string ReceiveFrame { get; set; }
        #endregion

        #region 响应式可观察对象（外部订阅）
        /// <summary>
        /// 消息可观察对象（外部订阅获取状态/异常消息）
        /// </summary>
        IObservable<string> MessageObservable { get; }
        /// <summary>
        /// 响应帧可观察对象（外部订阅获取PLC返回的Fins帧）
        /// </summary>
        IObservable<string> ReceiveFrameObservable { get; }
        #endregion

        #region 核心方法
        /// <summary>
        /// 初始化PLC连接（启动所有后台任务）
        /// </summary>
        /// <param name="config">PLC配置</param>
        /// <returns>是否初始化成功</returns>
        bool Init(PLCConfig config);

        /// <summary>
        /// 读取PLC寄存器（单地址，读1个字）
        /// </summary>
        /// <param name="registerType">寄存器类型</param>
        /// <param name="address">寄存器地址（如D100则传100）</param>
        /// <param name="isBCD">是否为BCD码（默认false，二进制）</param>
        /// <returns>读取结果（-1表示失败）</returns>
        int Read(PLCRegisterType registerType, ushort address, bool isBCD = false);

        /// <summary>
        /// 批量读取PLC寄存器（连续地址）
        /// </summary>
        /// <param name="registerType">寄存器类型</param>
        /// <param name="startAddress">起始地址</param>
        /// <param name="count">读取个数</param>
        /// <param name="isBCD">是否为BCD码（默认false，二进制）</param>
        /// <returns>读取结果集合（空表示失败）</returns>
        List<int> BatchRead(PLCRegisterType registerType, ushort startAddress, ushort count, bool isBCD = false);

        /// <summary>
        /// 写入PLC寄存器（单地址，写1个字）
        /// </summary>
        /// <param name="registerType">寄存器类型</param>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入值（需在寄存器取值范围内）</param>
        /// <param name="isBCD">是否按BCD码写入（默认false，二进制）</param>
        /// <returns>是否写入成功</returns>
        bool Write(PLCRegisterType registerType, ushort address, int value, bool isBCD = false);

        /// <summary>
        /// 批量写入PLC寄存器（连续地址）
        /// </summary>
        /// <param name="registerType">寄存器类型</param>
        /// <param name="startAddress">起始地址</param>
        /// <param name="values">写入值集合</param>
        /// <param name="isBCD">是否按BCD码写入（默认false，二进制）</param>
        /// <returns>是否写入成功</returns>
        bool BatchWrite(PLCRegisterType registerType, ushort startAddress, List<int> values, bool isBCD = false);

        /// <summary>
        /// 检查并重新建立连接（外部手动调用）
        /// </summary>
        /// <returns>是否连接成功</returns>
        bool CheckConnection();

        /// <summary>
        /// 优雅关闭连接（释放所有资源，终止后台任务）
        /// </summary>
        void Close();
        #endregion
    }
}
