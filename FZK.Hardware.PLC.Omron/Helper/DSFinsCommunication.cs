using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FZK.Hardware.PLC.Omron.Helper
{
    public class DSFinsCommunication
    {
        #region 常量定义
        /// <summary>
        /// FINS帧头固定长度
        /// </summary>
        public const int FINS_HEADER_LENGTH = 10;
        /// <summary>
        /// 内存区代码
        /// </summary>
        public static class AreaCode
        {
            public const byte CIO = 0x80;      // CIO区
            public const byte WR = 0x81;        // WR区
            public const byte HR = 0x82;        // HR区
            public const byte DM = 0x82;        // DM区 (与HR相同)
            public const byte AR = 0x83;        // AR区
            public const byte EM0 = 0xC0;       // EM区 bank0
            public const byte EM1 = 0xC1;       // EM区 bank1
            public const byte EM2 = 0xC2;       // EM区 bank2
            public const byte TIM = 0xE0;       // 定时器当前值
            public const byte CNT = 0xE1;       // 计数器当前值
            public const byte TIM_STATUS = 0xE2; // 定时器完成标志
            public const byte CNT_STATUS = 0xE3; // 计数器完成标志
        }
        /// <summary>
        /// 命令码
        /// </summary>
        public static class CommandCode
        {
            public const byte MEMORY_AREA_READ = 0x01;      // 内存区读
            public const byte MEMORY_AREA_WRITE = 0x02;     // 内存区写
            public const byte MEMORY_AREA_FILL = 0x03;      // 内存区填充
            public const byte MULTIPLE_MEMORY_READ = 0x04;  // 多内存区读
            public const byte MEMORY_AREA_TRANSFER = 0x05;  // 内存区传送
            public const byte PARAMETER_READ = 0x07;        // 参数读
            public const byte PARAMETER_WRITE = 0x08;       // 参数写
            public const byte PROGRAM_AREA_READ = 0x23;     // 程序区读
            public const byte PROGRAM_AREA_WRITE = 0x24;    // 程序区写
            public const byte RUN = 0x04;                    // 运行
            public const byte STOP = 0x02;                   // 停止
            public const byte CONTROLLER_STATUS_READ = 0x06; // 控制器状态读
            public const byte CYCLE_TIME_READ = 0x20;        // 循环时间读
        }
        /// <summary>
        /// ICF字段位定义
        /// </summary>
        public static class ICFBits
        {
            public const byte RESPONSE_NOT_REQUIRED = 0x80; // 不需要响应 (Bit7=1)
            public const byte RESPONSE_REQUIRED = 0x00;     // 需要响应 (Bit7=0)
            public const byte COMMAND_FRAME = 0x00;         // 命令帧 (Bit6=0)
            public const byte RESPONSE_FRAME = 0x40;        // 响应帧 (Bit6=1)

            // 常用组合
            public const byte COMMAND_NO_RESPONSE = 0x80;   // 命令帧，不需要响应
            public const byte COMMAND_WITH_RESPONSE = 0x00; // 命令帧，需要响应 (实际常用)
            public const byte RESPONSE = 0xC0;               // 响应帧
        }
        #endregion

        #region    FINS帧头构建

        /// <summary>
        /// 构建FINS帧头
        /// </summary>
        /// <param name="icf">信息控制字段</param>
        /// <param name="rsv">系统保留(固定00)</param>
        /// <param name="gct">允许网关数(固定02)</param>
        /// <param name="dna">目标网络地址</param>
        /// <param name="da1">目标节点号</param>
        /// <param name="da2">目标单元号</param>
        /// <param name="sna">源网络地址</param>
        /// <param name="sa1">源节点号</param>
        /// <param name="sa2">源单元号</param>
        /// <param name="sid">服务ID</param>
        /// <returns>10字节FINS帧头</returns>
        public static byte[] BuildHeader(byte icf = 0x80, byte rsv = 0x00, byte gct = 0x02,
                                        byte dna = 0x00, byte da1 = 0x00, byte da2 = 0x00,
                                        byte sna = 0x00, byte sa1 = 0x00, byte sa2 = 0x00,
                                        byte sid = 0x00)
        {
            return new byte[]
            {
                icf, rsv, gct,
                dna, da1, da2,
                sna, sa1, sa2,
                sid
            };
        }
        /// <summary>
        /// 构建标准命令帧头（常用）
        /// </summary>
        /// <param name="targetNode">目标节点号</param>
        /// <param name="sourceNode">源节点号</param>
        /// <param name="sid">服务ID</param>
        /// <returns>10字节FINS帧头</returns>
        public static byte[] BuildCommandHeader(byte targetNode, byte sourceNode, byte sid = 0x00)
        {
            return BuildHeader(
                icf: ICFBits.COMMAND_WITH_RESPONSE, // 需要响应
                rsv: 0x00,
                gct: 0x02,
                dna: 0x00,
                da1: targetNode,
                da2: 0x00,      // CPU单元
                sna: 0x00,
                sa1: sourceNode,
                sa2: 0x00,      // CPU单元
                sid: sid
            );
        }
        #endregion
        #region 指令构建
        /// <summary>
        /// 构建内存区读指令 (0101)
        /// </summary>
        /// <param name="header">FINS帧头(10字节)</param>
        /// <param name="areaCode">内存区代码</param>
        /// <param name="startAddress">起始地址</param>
        /// <param name="readCount">读取数量(字/位)</param>
        /// <returns>完整FINS指令帧</returns>
        public static byte[] BuildReadCommand(byte[] header, byte areaCode, int startAddress, int readCount)
        {
            if (header == null || header.Length != FINS_HEADER_LENGTH)
                throw new ArgumentException($"FINS帧头必须为{FINS_HEADER_LENGTH}字节");

            if (readCount <= 0 || readCount > 2000)
                throw new ArgumentException("读取数量必须在1-2000之间");

            List<byte> command = new List<byte>();

            // 添加帧头
            command.AddRange(header);

            // 命令码: 内存区读 (01 01)
            command.Add(0x01); // MRC
            command.Add(0x01); // SRC

            // 内存区代码
            command.Add(areaCode);

            // 起始地址 (3字节, 大端序)
            command.AddRange(ConvertAddressToFinsBytes(startAddress));

            // 读取数量 (2字节, 大端序)
            command.AddRange(ConvertCountToFinsBytes(readCount));

            return command.ToArray();
        }
        /// <summary>
        /// 构建内存区写指令 (0102)
        /// </summary>
        /// <param name="header">FINS帧头(10字节)</param>
        /// <param name="areaCode">内存区代码</param>
        /// <param name="startAddress">起始地址</param>
        /// <param name="data">要写入的数据(字数组)</param>
        /// <returns>完整FINS指令帧</returns>
        public static byte[] BuildWriteCommand(byte[] header, byte areaCode, int startAddress, ushort[] data)
        {
            if (header == null || header.Length != FINS_HEADER_LENGTH)
                throw new ArgumentException($"FINS帧头必须为{FINS_HEADER_LENGTH}字节");

            if (data == null || data.Length == 0)
                throw new ArgumentException("数据不能为空");

            if (data.Length > 2000)
                throw new ArgumentException("写入数量不能超过2000字");

            List<byte> command = new List<byte>();

            // 添加帧头
            command.AddRange(header);

            // 命令码: 内存区写 (01 02)
            command.Add(0x01); // MRC
            command.Add(0x02); // SRC

            // 内存区代码
            command.Add(areaCode);

            // 起始地址 (3字节, 大端序)
            command.AddRange(ConvertAddressToFinsBytes(startAddress));

            // 写入数量 (2字节, 大端序)
            command.AddRange(ConvertCountToFinsBytes(data.Length));

            // 写入数据 (每个字2字节, 大端序)
            foreach (ushort value in data)
            {
                command.Add((byte)(value >> 8));   // 高位
                command.Add((byte)(value & 0xFF)); // 低位
            }

            return command.ToArray();
        }
        /// <summary>
        /// 构建内存区写指令 (位写入)
        /// </summary>
        /// <param name="header">FINS帧头(10字节)</param>
        /// <param name="areaCode">内存区代码</param>
        /// <param name="startAddress">起始地址</param>
        /// <param name="bitPosition">位位置(0-15)</param>
        /// <param name="value">要写入的值(true=1, false=0)</param>
        /// <returns>完整FINS指令帧</returns>
        public static byte[] BuildWriteBitCommand(byte[] header, byte areaCode, int startAddress, int bitPosition, bool value)
        {
            if (bitPosition < 0 || bitPosition > 15)
                throw new ArgumentException("位位置必须在0-15之间");

            // 对于位操作，需要先读取当前字，然后修改特定位
            // 这里直接构造0102指令写入整个字
            ushort currentValue = 0; // 实际应用中可能需要先读取

            if (value)
                currentValue |= (ushort)(1 << bitPosition);
            else
                currentValue &= (ushort)~(1 << bitPosition);

            return BuildWriteCommand(header, areaCode, startAddress, new ushort[] { currentValue });
        }
        /// <summary>
        /// 构建内存区填充指令 (0103)
        /// </summary>
        /// <param name="header">FINS帧头(10字节)</param>
        /// <param name="areaCode">内存区代码</param>
        /// <param name="startAddress">起始地址</param>
        /// <param name="count">填充数量</param>
        /// <param name="fillData">填充数据</param>
        /// <returns>完整FINS指令帧</returns>
        public static byte[] BuildFillCommand(byte[] header, byte areaCode, int startAddress, int count, ushort fillData)
        {
            if (header == null || header.Length != FINS_HEADER_LENGTH)
                throw new ArgumentException($"FINS帧头必须为{FINS_HEADER_LENGTH}字节");

            if (count <= 0 || count > 2000)
                throw new ArgumentException("填充数量必须在1-2000之间");

            List<byte> command = new List<byte>();

            command.AddRange(header);
            command.Add(0x01); // MRC
            command.Add(0x03); // SRC
            command.Add(areaCode);
            command.AddRange(ConvertAddressToFinsBytes(startAddress));
            command.AddRange(ConvertCountToFinsBytes(count));

            // 填充数据
            command.Add((byte)(fillData >> 8));
            command.Add((byte)(fillData & 0xFF));

            return command.ToArray();
        }

        /// <summary>
        /// 构建控制器状态读指令 (0601)
        /// </summary>
        /// <param name="header">FINS帧头(10字节)</param>
        /// <returns>完整FINS指令帧</returns>
        public static byte[] BuildControllerStatusReadCommand(byte[] header)
        {
            if (header == null || header.Length != FINS_HEADER_LENGTH)
                throw new ArgumentException($"FINS帧头必须为{FINS_HEADER_LENGTH}字节");

            List<byte> command = new List<byte>();

            command.AddRange(header);
            command.Add(0x06); // MRC
            command.Add(0x01); // SRC

            return command.ToArray();
        }

        /// <summary>
        /// 构建运行命令 (0401)
        /// </summary>
        /// <param name="header">FINS帧头(10字节)</param>
        /// <param name="runMode">运行模式(00=运行, 01=监控)</param>
        /// <returns>完整FINS指令帧</returns>
        public static byte[] BuildRunCommand(byte[] header, byte runMode = 0x00)
        {
            if (header == null || header.Length != FINS_HEADER_LENGTH)
                throw new ArgumentException($"FINS帧头必须为{FINS_HEADER_LENGTH}字节");

            List<byte> command = new List<byte>();

            command.AddRange(header);
            command.Add(0x04); // MRC
            command.Add(0x01); // SRC
            command.Add(runMode); // 运行模式

            return command.ToArray();
        }

        /// <summary>
        /// 构建停止命令 (0402)
        /// </summary>
        /// <param name="header">FINS帧头(10字节)</param>
        /// <returns>完整FINS指令帧</returns>
        public static byte[] BuildStopCommand(byte[] header)
        {
            if (header == null || header.Length != FINS_HEADER_LENGTH)
                throw new ArgumentException($"FINS帧头必须为{FINS_HEADER_LENGTH}字节");

            List<byte> command = new List<byte>();

            command.AddRange(header);
            command.Add(0x04); // MRC
            command.Add(0x02); // SRC

            return command.ToArray();
        }
        #endregion
        #region 响应解析
        /// <summary>
        /// 解析读取响应
        /// </summary>
        /// <param name="response">FINS响应帧（不含TCP头）</param>
        /// <param name="readCount">期望读取的数量</param>
        /// <returns>解析出的数据数组</returns>
        public static ParseResult<ushort[]> ParseReadResponse(byte[] response, int readCount)
        {
            var result = new ParseResult<ushort[]>();

            try
            {
                if (response == null || response.Length < 2)
                {
                    result.Success = false;
                    result.ErrorMessage = "响应数据为空或太短";
                    return result;
                }

                // 检查命令码
                if (response[0] != 0x01 || response[1] != 0x01)
                {
                    result.Success = false;
                    result.ErrorMessage = $"命令码不匹配，期望0101，实际{response[0]:X2}{response[1]:X2}";
                    return result;
                }

                // 检查结束码
                int endCodeIndex = response.Length - 2;
                if (endCodeIndex >= 0)
                {
                    byte endCodeHigh = response[endCodeIndex];
                    byte endCodeLow = response[endCodeIndex + 1];

                    if (endCodeHigh != 0x00 || endCodeLow != 0x00)
                    {
                        result.Success = false;
                        result.ErrorMessage = GetErrorDescription(endCodeHigh, endCodeLow);
                        result.ErrorCode = (endCodeHigh << 8) | endCodeLow;
                        return result;
                    }
                }

                // 解析数据
                List<ushort> data = new List<ushort>();
                for (int i = 2; i < response.Length - 2; i += 2)
                {
                    if (i + 1 < response.Length - 2)
                    {
                        ushort value = (ushort)((response[i] << 8) | response[i + 1]);
                        data.Add(value);
                    }
                }

                if (data.Count != readCount)
                {
                    result.Success = false;
                    result.ErrorMessage = $"数据数量不匹配，期望{readCount}，实际{data.Count}";
                    return result;
                }

                result.Success = true;
                result.Data = data.ToArray();
                return result;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = $"解析异常: {ex.Message}";
                return result;
            }
        }

        /// <summary>
        /// 解析写入响应
        /// </summary>
        /// <param name="response">FINS响应帧（不含TCP头）</param>
        /// <returns>解析结果</returns>
        public static ParseResult<bool> ParseWriteResponse(byte[] response)
        {
            var result = new ParseResult<bool>();

            try
            {
                if (response == null || response.Length < 4) // 0102 + 结束码(2字节) = 4字节
                {
                    result.Success = false;
                    result.ErrorMessage = "响应数据为空或太短";
                    return result;
                }

                // 检查命令码
                if (response[0] != 0x01 || response[1] != 0x02)
                {
                    result.Success = false;
                    result.ErrorMessage = $"命令码不匹配，期望0102，实际{response[0]:X2}{response[1]:X2}";
                    return result;
                }

                // 检查结束码
                byte endCodeHigh = response[2];
                byte endCodeLow = response[3];

                if (endCodeHigh != 0x00 || endCodeLow != 0x00)
                {
                    result.Success = false;
                    result.ErrorMessage = GetErrorDescription(endCodeHigh, endCodeLow);
                    result.ErrorCode = (endCodeHigh << 8) | endCodeLow;
                    result.Data = false;
                    return result;
                }

                result.Success = true;
                result.Data = true;
                return result;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = $"解析异常: {ex.Message}";
                return result;
            }
        }

        /// <summary>
        /// 解析控制器状态响应
        /// </summary>
        public static ParseResult<ControllerStatus> ParseControllerStatusResponse(byte[] response)
        {
            var result = new ParseResult<ControllerStatus>();

            try
            {
                if (response == null || response.Length < 10)
                {
                    result.Success = false;
                    result.ErrorMessage = "响应数据太短";
                    return result;
                }

                // 检查命令码
                if (response[0] != 0x06 || response[1] != 0x01)
                {
                    result.Success = false;
                    result.ErrorMessage = "命令码不匹配";
                    return result;
                }

                // 检查结束码
                int endCodeIndex = response.Length - 2;
                byte endCodeHigh = response[endCodeIndex];
                byte endCodeLow = response[endCodeIndex + 1];

                if (endCodeHigh != 0x00 || endCodeLow != 0x00)
                {
                    result.Success = false;
                    result.ErrorMessage = GetErrorDescription(endCodeHigh, endCodeLow);
                    result.ErrorCode = (endCodeHigh << 8) | endCodeLow;
                    return result;
                }

                var status = new ControllerStatus
                {
                    Mode = response[2],        // 运行模式
                    Status = response[3],      // 状态
                    FatalError = (response[6] & 0x80) != 0, // 致命错误标志
                    NonFatalError = (response[6] & 0x40) != 0 // 非致命错误标志
                };

                result.Success = true;
                result.Data = status;
                return result;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = $"解析异常: {ex.Message}";
                return result;
            }
        }
        #endregion

        #region 工具方法
        /// <summary>
        /// 将地址转换为FINS 3字节格式（大端序）
        /// </summary>
        private static byte[] ConvertAddressToFinsBytes(int address)
        {
            byte[] bytes = new byte[3];

            if (address < 0 || address > 0xFFFFFF)
                throw new ArgumentException("地址必须在0-16777215之间");

            bytes[0] = (byte)((address >> 16) & 0xFF); // 高位
            bytes[1] = (byte)((address >> 8) & 0xFF);  // 中位
            bytes[2] = (byte)(address & 0xFF);         // 低位

            return bytes;
        }

        /// <summary>
        /// 将数量转换为FINS 2字节格式（大端序）
        /// </summary>
        private static byte[] ConvertCountToFinsBytes(int count)
        {
            if (count < 0 || count > 0xFFFF)
                throw new ArgumentException("数量必须在0-65535之间");

            return new byte[]
            {
                (byte)((count >> 8) & 0xFF), // 高位
                (byte)(count & 0xFF)          // 低位
            };
        }

        /// <summary>
        /// 从FINS 3字节格式解析地址
        /// </summary>
        public static int ParseAddressFromFinsBytes(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 3)
                throw new ArgumentException("需要3字节数据");

            return (bytes[0] << 16) | (bytes[1] << 8) | bytes[2];
        }

        /// <summary>
        /// 获取错误描述
        /// </summary>
        public static string GetErrorDescription(byte high, byte low)
        {
            var errors = new Dictionary<string, string>
            {
                ["0000"] = "正常完成",
                ["0001"] = "本地节点错误",
                ["0101"] = "FINS命令不支持",
                ["0102"] = "访问权限错误",
                ["0103"] = "数据帧长度错误",
                ["0104"] = "命令太短",
                ["0105"] = "命令太长",
                ["0201"] = "目标节点不可达",
                ["0202"] = "目标单元不存在",
                ["0203"] = "目标节点地址错误",
                ["0301"] = "控制器错误",
                ["0302"] = "内存区域不存在",
                ["0303"] = "地址超出范围",
                ["0304"] = "数据格式错误",
                ["0305"] = "访问只读区域",
                ["0306"] = "访问保护区域",
                ["0401"] = "参数错误",
                ["1001"] = "程序区不存在",
                ["1002"] = "程序区不可读",
                ["1101"] = "任务不存在",
                ["1102"] = "任务不可执行",
                ["1103"] = "任务已停止",
                ["1104"] = "任务未注册",
                ["1105"] = "任务已被使用",
                ["1106"] = "任务无法启动",
                ["1107"] = "任务无法停止",
                ["1108"] = "任务无法暂停",
                ["1109"] = "任务状态错误",
                ["2201"] = "数据项太多",
                ["2202"] = "数据长度错误",
                ["2203"] = "数据项太少",
            };

            string code = $"{high:X2}{low:X2}";
            return errors.TryGetValue(code, out string desc) ? desc : $"未知错误: {code}";
        }

        /// <summary>
        /// 格式化FINS帧为十六进制字符串
        /// </summary>
        public static string FormatFinsFrame(byte[] frame)
        {
            if (frame == null) return "null";

            return string.Join(" ", frame.Select(b => b.ToString("X2")));
        }

        /// <summary>
        /// 从十六进制字符串解析FINS帧
        /// </summary>
        public static byte[] ParseFinsFrame(string hexString)
        {
            if (string.IsNullOrWhiteSpace(hexString))
                return Array.Empty<byte>();

            string cleaned = hexString.Replace(" ", "").Replace("-", "");

            if (cleaned.Length % 2 != 0)
                throw new ArgumentException("十六进制字符串长度必须为偶数");

            byte[] result = new byte[cleaned.Length / 2];
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = Convert.ToByte(cleaned.Substring(i * 2, 2), 16);
            }

            return result;
        }
        #endregion
    }

    #region 辅助类
    /// <summary>
    /// 解析结果
    /// </summary>
    public class ParseResult<T>
    {
        /// <summary>
        /// 是否成功
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// 错误信息
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// 错误码
        /// </summary>
        public int ErrorCode { get; set; }

        /// <summary>
        /// 解析出的数据
        /// </summary>
        public T Data { get; set; }
    }

    /// <summary>
    /// 控制器状态
    /// </summary>
    public class ControllerStatus
    {
        /// <summary>
        /// 运行模式 (00=PROGRAM, 01=MONITOR, 02=RUN)
        /// </summary>
        public byte Mode { get; set; }

        /// <summary>
        /// 状态
        /// </summary>
        public byte Status { get; set; }

        /// <summary>
        /// 致命错误
        /// </summary>
        public bool FatalError { get; set; }

        /// <summary>
        /// 非致命错误
        /// </summary>
        public bool NonFatalError { get; set; }

        /// <summary>
        /// 模式描述
        /// </summary>
        public string ModeDescription
        {
            get
            {               
                switch (Mode)
                {
                    case 0x00:
                        return "PROGRAM模式";
                    case 0x01:
                        return "MONITOR模式";
                    case 0x02:
                        return "RUN模式";
                    default:
                        // 保持原有的16进制格式化输出逻辑
                        return $"未知模式({Mode:X2})";
                }
            }
        }

        #endregion
    }
}