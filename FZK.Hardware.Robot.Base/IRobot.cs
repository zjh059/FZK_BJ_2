using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FZK.Hardware.Robot.Base
{
    /// <summary>
    /// 表示一台工业机械臂的接口
    /// </summary>
    public interface IRobot
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
        /// 消息观察者
        /// </summary>
        IObservable<string> MessageObservable { get; }

        /// <summary>
        /// 相机初始化
        /// </summary>
        /// <param name="cameraConfig"></param>
        /// <returns></returns>
        bool Init(RobotConfig robotConfig);


        /// <summary>
        /// 最新接收的机械手数据/状态
        /// </summary>
        string ReceiveContent { get; }

        /// <summary>
        /// 接收数据可观察对象（外部订阅机械手返回数据）
        /// </summary>
        IObservable<string> ReceiveContentObservable { get; }


        /// <summary>
        /// 手动发送指令给机械手
        /// </summary>
        /// <param name="command">指令内容（无需带结束符，内部自动拼接）</param>
        /// <returns>是否发送成功</returns>
        bool SendCommand(string command);

        /// <summary>
        /// 检查并重新建立连接
        /// </summary>
        /// <returns>是否连接成功</returns>
        bool CheckConnection();

        /// <summary>
        /// 优雅关闭连接并释放资源
        /// </summary>
        void Close();
    }
}
