using System;
using System.Collections.Generic;

namespace FZK.Hardware.PLC.Omron
{
    /// <summary>
    /// FINS协议编解码工具类（纯协议层，无网络依赖）
    /// 严格遵循欧姆龙FinsTCP官方文档实现
    /// </summary>
    public static class FinsHelper
    {
        #region 地址类型码
        public const byte AreaCode_DM = 0x82;    // DM区
        public const byte AreaCode_CIO = 0x30;   // CIO区
        public const byte AreaCode_WR = 0x31;    // WR区
        public const byte AreaCode_HR = 0x32;    // HR区
        public const byte AreaCode_AR = 0x33;    // AR区
        public const byte AreaCode_TIM = 0x90;   // TIM区
        public const byte AreaCode_CNTR = 0x91;  // CNTR区
        #endregion

        #region FINS指令码
        private const byte CmdRead_H = 0x01;     // 读指令高字节
        private const byte CmdRead_L = 0x01;     // 读指令低字节
        private const byte CmdWrite_H = 0x01;    // 写指令高字节
        private const byte CmdWrite_L = 0x02;    // 写指令低字节
        #endregion

        #region FINS头固定值
        private const byte ICF = 0xC0;   // 控制帧：1100 0000 (响应要求=1 + 命令帧=1)
        private const byte RSV = 0x00;   // 系统保留
        private const byte GCT = 0x01;   // 网关数：0x01（无网关，直接连接）
        private const byte DA2 = 0x00;   // 目标单元号（CPU单元固定0）
        private const byte SA2 = 0x00;   // 源单元号（CPU单元固定0）
        #endregion

        #region 协议常量
        private const byte TcpCommandCode = 0x01;  // 命令帧码
        private const byte TcpResponseCode = 0x02; // 响应帧码

        // 帧结构常量
        private const int TcpHeaderLength = 16;     // TCP头长度
        private const int FinsHeaderLength = 10;    // FINS头长度
        private const int CommandCodeLength = 2;    // 指令码长度
        private const int ErrorCodeLength = 2;      // 错误码长度

        // 最小响应长度 = TCP头 + FINS头 + 指令码 + 错误码
        private const int MinResponseLength = TcpHeaderLength + FinsHeaderLength + CommandCodeLength + ErrorCodeLength; // 30字节

        private const int HandshakeLength = 20;     // 握手指令长度
        #endregion

        #region 合法区域码集合
        private static readonly HashSet<byte> ValidAreaCodes = new HashSet<byte>
        {
            AreaCode_DM, AreaCode_CIO, AreaCode_WR, AreaCode_HR,
            AreaCode_AR, AreaCode_TIM, AreaCode_CNTR
        };
        #endregion

        #region FINS配置模型
        public class FinsConfig
        {
            public byte SourceNode { get; set; } = 0x01;
            public byte TargetNode { get; set; } = 0x02;
            public byte NetworkNo { get; set; } = 0x00;
            public byte SID { get; set; } = 0x00;
            public int Timeout { get; set; } = 3000;
            public int RetryCount { get; set; } = 3;

            public FinsConfig CloneForRequest()
            {
                return new FinsConfig
                {
                    SourceNode = this.SourceNode,
                    TargetNode = this.TargetNode,
                    NetworkNo = this.NetworkNo,
                    SID = this.SID == 0 ? SIDManager.GetNextSID() : this.SID,
                    Timeout = this.Timeout,
                    RetryCount = this.RetryCount
                };
            }
            public FinsConfig HeartRequest()
            {
                return new FinsConfig
                {
                    SourceNode = this.SourceNode,
                    TargetNode = this.TargetNode,
                    NetworkNo = this.NetworkNo,
                    SID = 0xFE,
                    Timeout = this.Timeout,
                    RetryCount = this.RetryCount
                };
            }
        }

        #endregion

        #region SID管理器
        public static class SIDManager
        {
            private static byte _currentSID = 0;
            private static readonly object _lock = new object();

            public static byte GetNextSID()
            {
                lock (_lock)
                {
                    _currentSID++;
                    if (_currentSID >= 0xFE) _currentSID = 1;
                    return _currentSID;
                }
            }
        }
        #endregion

        #region 地址范围配置
        public enum PLCType
        {
            CP1H, CJ2, NJ, Custom
        }

        public class AddressRangeConfig
        {
            public ushort DM_Min { get; set; } = 0;
            public ushort DM_Max { get; set; } = 32767;
            public ushort CIO_Min { get; set; } = 0;
            public ushort CIO_Max { get; set; } = 6143;
            public ushort WR_Min { get; set; } = 0;
            public ushort WR_Max { get; set; } = 511;
            public ushort HR_Min { get; set; } = 0;
            public ushort HR_Max { get; set; } = 511;
            public ushort AR_Min { get; set; } = 0;
            public ushort AR_Max { get; set; } = 447;
            public ushort Timer_Max { get; set; } = 4095;
            public ushort Counter_Max { get; set; } = 4095;

            public static AddressRangeConfig GetConfig(PLCType type)
            {
                switch (type)
                {
                    case PLCType.CP1H:
                        return new AddressRangeConfig
                        {
                            DM_Max = 32767,
                            CIO_Max = 6143,
                            WR_Max = 511,
                            HR_Max = 511,
                            AR_Max = 447
                        };
                    case PLCType.CJ2:
                        return new AddressRangeConfig
                        {
                            DM_Max = 32767,
                            CIO_Max = 6143,
                            WR_Max = 511,
                            HR_Max = 511,
                            AR_Max = 447
                        };
                    case PLCType.NJ:
                        return new AddressRangeConfig
                        {
                            DM_Max = 65535,
                            CIO_Max = 65535,
                            WR_Max = 4095,
                            HR_Max = 4095,
                            AR_Max = 4095,
                            Timer_Max = 65535,
                            Counter_Max = 65535
                        };
                    default:
                        return new AddressRangeConfig();
                }
            }
        }

        private static AddressRangeConfig _addressRangeConfig = new AddressRangeConfig();
        private static bool _enableAddressValidation = true;

        public static void SetPLCType(PLCType type) => _addressRangeConfig = AddressRangeConfig.GetConfig(type);
        public static void SetAddressRange(AddressRangeConfig config) => _addressRangeConfig = config ?? throw new ArgumentNullException(nameof(config));
        public static void EnableAddressValidation(bool enable) => _enableAddressValidation = enable;

        private static string GetAreaName(byte areaCode)
        {
            switch (areaCode)
            {
                case AreaCode_DM: return "DM区";
                case AreaCode_CIO: return "CIO区";
                case AreaCode_WR: return "WR区";
                case AreaCode_HR: return "HR区";
                case AreaCode_AR: return "AR区";
                case AreaCode_TIM: return "TIM区";
                case AreaCode_CNTR: return "CNTR区";
                default: return $"0x{areaCode:X2}";
            }
        }

        private static (ushort Min, ushort Max) GetRangeWithConfig(byte areaCode)
        {
            switch (areaCode)
            {
                case AreaCode_DM: return (_addressRangeConfig.DM_Min, _addressRangeConfig.DM_Max);
                case AreaCode_CIO: return (_addressRangeConfig.CIO_Min, _addressRangeConfig.CIO_Max);
                case AreaCode_WR: return (_addressRangeConfig.WR_Min, _addressRangeConfig.WR_Max);
                case AreaCode_HR: return (_addressRangeConfig.HR_Min, _addressRangeConfig.HR_Max);
                case AreaCode_AR: return (_addressRangeConfig.AR_Min, _addressRangeConfig.AR_Max);
                case AreaCode_TIM: return (0, _addressRangeConfig.Timer_Max);
                case AreaCode_CNTR: return (0, _addressRangeConfig.Counter_Max);
                default: throw new ArgumentException($"未知区域码：0x{areaCode:X2}");
            }
        }
        #endregion

        #region 校验方法
        private static void ValidateAreaCode(byte areaCode)
        {
            if (!ValidAreaCodes.Contains(areaCode))
                throw new ArgumentException($"非法的区域码：0x{areaCode:X2}");
        }

        private static void ValidateAddress(byte areaCode, ushort address, ushort length)
        {
            if (!_enableAddressValidation) return;
            try
            {
                var (minAddr, maxAddr) = GetRangeWithConfig(areaCode);
                string areaName = GetAreaName(areaCode);
                if (address < minAddr || address > maxAddr)
                    throw new ArgumentOutOfRangeException(nameof(address), $"{areaName}地址 {address} 超出合法范围 [{minAddr}-{maxAddr}]");
                if (address + length - 1 > maxAddr)
                    throw new ArgumentOutOfRangeException(nameof(length), $"{areaName}批量操作结束地址 {address + length - 1} 超出合法范围 [{minAddr}-{maxAddr}]");
            }
            catch (KeyNotFoundException) { }
        }
        #endregion

        #region 构建指令
        public static byte[] BuildBatchReadCommand(FinsConfig finsConfig, byte areaCode, ushort startAddress, ushort length)
        {
            ValidateAreaCode(areaCode);
            ValidateAddress(areaCode, startAddress, length);
            if (length == 0 || length > 1000)
                throw new ArgumentException("读取长度必须在1-1000之间");

            var requestConfig = finsConfig.CloneForRequest();
            byte[] finsBody = BuildFinsBody(requestConfig, true, areaCode, startAddress, length, 0);
            return BuildTcpFrame(finsBody);
        }

        public static byte[] BuildBatchWriteCommand(FinsConfig finsConfig, byte areaCode, ushort startAddress, ushort[] values)
        {
            ValidateAreaCode(areaCode);
            ValidateAddress(areaCode, startAddress, (ushort)values.Length);
            if (values == null || values.Length == 0 || values.Length > 1000)
                throw new ArgumentException("写入数据必须在1-1000个之间");

            var requestConfig = finsConfig.CloneForRequest();
            byte[] finsBody = BuildFinsBody(requestConfig, false, areaCode, startAddress, (ushort)values.Length, 0, values);
            return BuildTcpFrame(finsBody);
        }

        public static byte[] BuildReadUInt16Command(FinsConfig finsConfig, byte areaCode, ushort address)
            => BuildBatchReadCommand(finsConfig, areaCode, address, 1);

        public static byte[] BuildWriteUInt16Command(FinsConfig finsConfig, byte areaCode, ushort address, ushort value)
            => BuildBatchWriteCommand(finsConfig, areaCode, address, new ushort[] { value });
        #endregion

        #region 构建FINS应用体
        private static byte[] BuildFinsBody(FinsConfig config, bool isRead, byte areaCode, ushort address, ushort length, ushort writeValue)
            => BuildFinsBody(config, isRead, areaCode, address, length, writeValue, null);

        private static byte[] BuildFinsBody(FinsConfig config, bool isRead, byte areaCode, ushort address, ushort length, ushort writeValue, ushort[] writeValues)
        {
            // 计算FINS应用体长度（标准格式）
            // 读: 10(FINS头) + 2(指令码) + 1(区域码) + 2(地址) + 1(位位置) + 2(长度) = 18
            // 写: 18 + (length * 2) 字节数据
            int bodyLen = 18 + (isRead ? 0 : (length * 2));
            byte[] body = new byte[bodyLen];
            int index = 0;

            // 1. 构建10字节FINS头
            body[index++] = ICF;                    // ICF
            body[index++] = RSV;                     // RSV
            body[index++] = GCT;                     // GCT
            body[index++] = config.NetworkNo;        // DNA
            body[index++] = config.TargetNode;       // DA1
            body[index++] = DA2;                      // DA2
            body[index++] = config.NetworkNo;        // SNA
            body[index++] = config.SourceNode;       // SA1
            body[index++] = SA2;                      // SA2
            body[index++] = config.SID;               // SID

            // 2. 构建2字节指令码
            body[index++] = isRead ? CmdRead_H : CmdWrite_H;
            body[index++] = isRead ? CmdRead_L : CmdWrite_L;

            // 3. 构建参数段：区域码 + 地址(2) + 位位置(1) + 长度(2)
            body[index++] = areaCode;                 // 区域码
            body[index++] = (byte)(address >> 8);     // 地址高字节
            body[index++] = (byte)(address & 0xFF);   // 地址低字节
            body[index++] = 0x00;                      // 位位置（字操作时为0）
            body[index++] = (byte)(length >> 8);      // 长度高字节
            body[index++] = (byte)(length & 0xFF);    // 长度低字节

            // 4. 写指令添加数据（批量写入支持）
            if (!isRead)
            {
                if (writeValues != null && writeValues.Length > 0)
                {
                    for (int i = 0; i < writeValues.Length; i++)
                    {
                        body[index++] = (byte)(writeValues[i] >> 8);
                        body[index++] = (byte)(writeValues[i] & 0xFF);
                    }
                }
                else
                {
                    for (int i = 0; i < length; i++)
                    {
                        body[index++] = (byte)(writeValue >> 8);
                        body[index++] = (byte)(writeValue & 0xFF);
                    }
                }
            }

            return body;
        }
        #endregion

        #region 构建TCP帧
        private static byte[] BuildTcpFrame(byte[] finsBody)
        {
            int totalLen = 16 + finsBody.Length;
            byte[] frame = new byte[totalLen];
            int index = 0;

            frame[index++] = 0x46;
            frame[index++] = 0x49;
            frame[index++] = 0x4E;
            frame[index++] = 0x53;

            int dataLength = 8 + finsBody.Length;  // 8 = 命令码(4) + 错误码(4)
            frame[index++] = (byte)((dataLength >> 24) & 0xFF);
            frame[index++] = (byte)((dataLength >> 16) & 0xFF);
            frame[index++] = (byte)((dataLength >> 8) & 0xFF);
            frame[index++] = (byte)(dataLength & 0xFF);

            frame[index++] = 0x00; frame[index++] = 0x00; frame[index++] = 0x00; frame[index++] = TcpCommandCode;
            frame[index++] = 0x00; frame[index++] = 0x00; frame[index++] = 0x00; frame[index++] = 0x00;

            Array.Copy(finsBody, 0, frame, index, finsBody.Length);
            return frame;
        }
        #endregion

        #region 解析响应
        public static ushort[] ParseBatchReadResponse(byte[] responseBytes, ushort expectedLength)
        {
            if (!ValidateResponse(responseBytes, out byte mainCode, out byte subCode))
                throw new Exception($"FINS响应校验失败，主码：0x{mainCode:X2}，副码：0x{subCode:X2}，{GetErrorDescription(mainCode, subCode)}");

            int dataStartIndex = TcpHeaderLength + FinsHeaderLength + CommandCodeLength + ErrorCodeLength;
            int dataLength = responseBytes.Length - dataStartIndex;
            if (dataLength != expectedLength * 2)
                throw new Exception($"响应数据长度不符：期望{expectedLength * 2}字节，实际{dataLength}字节");

            ushort[] result = new ushort[expectedLength];
            for (int i = 0; i < expectedLength; i++)
            {
                int pos = dataStartIndex + i * 2;
                result[i] = (ushort)((responseBytes[pos] << 8) | responseBytes[pos + 1]);
            }
            return result;
        }

        public static ushort ParseReadUInt16Response(byte[] responseBytes)
            => ParseBatchReadResponse(responseBytes, 1)[0];

        public static bool ParseBatchWriteResponse(byte[] responseBytes)
            => ValidateResponse(responseBytes, out _, out _);

        public static bool ParseWriteUInt16Response(byte[] responseBytes)
            => ParseBatchWriteResponse(responseBytes);
        #endregion

        #region 校验响应
        private static bool ValidateResponse(byte[] responseBytes, out byte mainCode, out byte subCode)
        {
            mainCode = 0xFF;
            subCode = 0xFF;

            if (responseBytes == null || responseBytes.Length < MinResponseLength)
                return false;

            if (responseBytes[0] != 0x46 || responseBytes[1] != 0x49 ||
                responseBytes[2] != 0x4E || responseBytes[3] != 0x53)
                return false;

            int responseLength = (responseBytes[4] << 24) | (responseBytes[5] << 16) |
                                 (responseBytes[6] << 8) | responseBytes[7];
            if (responseBytes.Length < 8 + responseLength)
                return false;

            if (responseBytes[8] != 0x00 || responseBytes[9] != 0x00 ||
                responseBytes[10] != 0x00 || responseBytes[11] != TcpResponseCode)
                return false;

            // 错误码位置：TCP头(16) + FINS头(10) + 命令码(2) = 28
            int errorPos = 16 + 10 + 2;
            mainCode = responseBytes[errorPos];
            subCode = responseBytes[errorPos + 1];

            return (mainCode == 0x00 && subCode == 0x00);
        }
        #endregion

        #region 握手指令
        public static byte[] BuildHandshakeCommand(byte sourceNode = 0x01)
        {
            byte[] handshake = new byte[HandshakeLength];
            handshake[0] = 0x46; handshake[1] = 0x49; handshake[2] = 0x4E; handshake[3] = 0x53;
            handshake[4] = 0x00; handshake[5] = 0x00; handshake[6] = 0x00; handshake[7] = 0x0C;
            handshake[8] = 0x00; handshake[9] = 0x00; handshake[10] = 0x00; handshake[11] = TcpCommandCode;
            handshake[12] = 0x00; handshake[13] = 0x00; handshake[14] = 0x00; handshake[15] = 0x00;
            handshake[16] = sourceNode; handshake[17] = 0x00; handshake[18] = 0x00; handshake[19] = 0x00;
            return handshake;
        }

        public static bool ParseHandshakeResponse(byte[] responseBytes)
        {
            if (responseBytes == null || responseBytes.Length < HandshakeLength) return false;
            if (responseBytes[0] != 0x46 || responseBytes[1] != 0x49 || responseBytes[2] != 0x4E || responseBytes[3] != 0x53) return false;
            if (responseBytes[8] != 0x00 || responseBytes[9] != 0x00 || responseBytes[10] != 0x00 || responseBytes[11] != TcpResponseCode) return false;
            if (responseBytes[12] != 0x00 || responseBytes[13] != 0x00 || responseBytes[14] != 0x00 || responseBytes[15] != 0x00) return false;
            return true;
        }
        #endregion

        #region 错误码描述
        public static string GetErrorDescription(byte mainCode, byte subCode)
        {
            if (mainCode == 0x00 && subCode == 0x00) return "正常完成";
            if (mainCode == 0x01)
            {
                switch (subCode)
                {
                    case 0x01: return "指令过长";
                    case 0x02: return "指令过短";
                    case 0x03: return "数据过长/过短";
                    case 0x04: return "数据个数不符";
                    default: return $"服务中止，副码：0x{subCode:X2}";
                }
            }
            if (mainCode == 0x02)
            {
                switch (subCode)
                {
                    case 0x01: return "未定义指令";
                    default: return $"未定义指令，副码：0x{subCode:X2}";
                }
            }
            if (mainCode == 0x04)
            {
                switch (subCode)
                {
                    case 0x01: return "目标节点不存在";
                    case 0x02: return "目标单元不存在";
                    case 0x03: return "网络繁忙";
                    case 0x20: return "节点地址异常";
                    default: return $"路由异常，副码：0x{subCode:X2}";
                }
            }
            if (mainCode == 0x21)
            {
                switch (subCode)
                {
                    case 0x01: return "超出最大地址";
                    case 0x02: return "写入只读区";
                    default: return $"I/O数据异常，副码：0x{subCode:X2}";
                }
            }
            return $"未知错误，主码：0x{mainCode:X2}，副码：0x{subCode:X2}";
        }
        #endregion
    }
}