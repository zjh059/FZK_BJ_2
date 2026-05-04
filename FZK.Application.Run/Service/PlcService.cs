using FZK.Application.Share.Run;
using FZK.Logger;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FZK.Application.Run.Service
{
    public class PlcService : IPlcService
    {
        private readonly IHardwareService _hardwareService;
        private readonly bool _isNoHardwareMode;

        public PlcService(IHardwareService hardwareService, bool isNoHardwareMode)
        {
            _hardwareService = hardwareService;
            _isNoHardwareMode = isNoHardwareMode;
        }

        public async Task<Dictionary<int, int>> ReadTriggerRegistersAsync(IEnumerable<int> addresses)
        {
            var list = addresses.ToList();
            if (_isNoHardwareMode)
                return list.ToDictionary(a => a, a => 0);
            return await _hardwareService.ReadPlcRegisters(list);
        }

        public async Task WriteRegisterAsync(int address, int value)
        {
            if (_isNoHardwareMode) return;
            await _hardwareService.WritePlcRegister(address, value);
        }

        public async Task WriteTriggerResetAsync(int address)
        {
            await WriteRegisterAsync(address, 0);
        }

        public async Task<bool> IsRisingEdgeAsync(Func<Task<int>> readCurrent, int lastValue)
        {
            int current = await readCurrent();
            return lastValue == 0 && current == 1;
        }

        public async Task<int> ReadRegisterAsync(int address)
        {
            if (_isNoHardwareMode) return 0;
            return await _hardwareService.ReadPlcRegister(address);
        }
    }
}