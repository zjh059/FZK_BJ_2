using FZK.Hardware.Scanner.Base;
using FZK.Hardware.Robot.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FZK.Application.Share.Init;

namespace FZK.Application.Share.Run
{
    /// <summary>
    /// 硬件服务接口（适配真实硬件）
    /// </summary>
    public interface IHardwareService
    {

        void Init();
        void Stop();
        Task<Dictionary<int, int>> ReadPlcRegisters(List<int> addresses);
        Task WritePlcRegister(int address, int value);
        Task<string> TriggerScanner(ScannerType scannerType);
        Task<string> GetRobotCommand();
        Task SendRobotResponse(bool success);
        Task<int> ReadPlcRegister(int address);

        Task<bool> TriggerScannerAndValidateAsync(
           ScannerType scannerType,
           int expectedLength,
           bool enableDebug,
           bool enableSfc);
    }
}
