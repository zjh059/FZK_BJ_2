using FZK.Hardware.PLC.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FZK.Hardware.PLC.Omron
{
    class PlcDemo
    {
        static void Main(string[] args)
        {
            // 1. 初始化PLC配置
            var plcConfig = new PLCConfig
            {
                IpAddress = "192.168.1.10", // 你的PLC实际IP
                Port = 9600, // Fins TCP默认9600
                Timeout = 3000,
                MaxReconnectCount = 10,
                ReconnectDelay = 2000,
                HeartbeatInterval = 3000, // 3秒一次心跳
                MaxSendQueueLength = 100,
                SendInterval = 100,
                PlcNode = 0x01 // PLC节点号，需和PLC实际配置一致
            };

            // 2. 创建PLC实例并初始化
            IPLC omronPlc = new PLCOmronNJ501_1400();
            bool initSuccess = omronPlc.Init(plcConfig);
            if (!initSuccess)
            {
                Console.WriteLine("PLC初始化失败：" + omronPlc.Message);
                return;
            }
            Console.WriteLine("PLC初始化成功：" + omronPlc.Message);

            // 3. 订阅PLC状态消息（实时监控）
            omronPlc.MessageObservable.Subscribe(msg =>
            {
                Console.WriteLine($"【PLC状态】：{DateTime.Now:HH:mm:ss} - {msg}");
            });

            // 4. 单条读写示例
            // 读D100寄存器（二进制）
            int d100Value = omronPlc.Read(PLCRegisterType.D, 100);
            Console.WriteLine($"D100当前值：{d100Value}");

            // 写D100寄存器为1234（二进制）
            bool writeSuccess = omronPlc.Write(PLCRegisterType.D, 100, 1234);
            Console.WriteLine(writeSuccess ? "D100写入成功" : "D100写入失败");

            // 5. 批量读写示例
            // 批量读D200-D204（5个寄存器，BCD码）
            var batchReadValues = omronPlc.BatchRead(PLCRegisterType.D, 200, 5, isBCD: true);
            if (batchReadValues.Count > 0)
            {
                Console.WriteLine($"批量读D200-D204：{string.Join(",", batchReadValues)}");
            }

            // 批量写D200-D204为[100,200,300,400,500]（BCD码）
            var writeValues = new List<int> { 100, 200, 300, 400, 500 };
            bool batchWriteSuccess = omronPlc.BatchWrite(PLCRegisterType.D, 200, writeValues, isBCD: true);
            Console.WriteLine(batchWriteSuccess ? "D200-D204批量写入成功" : "D200-D204批量写入失败");

            // 6. 手动检查连接
            bool isConnected = omronPlc.CheckConnection();
            Console.WriteLine(isConnected ? "PLC连接正常" : "PLC连接异常");

            // 7. 程序退出时关闭连接
            Console.WriteLine("按任意键关闭PLC连接...");
            Console.ReadKey();
            omronPlc.Close();
            Console.WriteLine("PLC连接已关闭");
        }
    }
}