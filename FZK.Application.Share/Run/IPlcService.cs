using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FZK.Application.Share.Run
{
    public interface IPlcService
    {
        Task<Dictionary<int, int>> ReadTriggerRegistersAsync(IEnumerable<int> addresses);
        Task WriteTriggerResetAsync(int address);
        Task<bool> IsRisingEdgeAsync(Func<Task<int>> readCurrent, int lastValue);
        // 通用读写
        Task<int> ReadRegisterAsync(int address);
        Task WriteRegisterAsync(int address, int value);
    }
}
