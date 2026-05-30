using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FZK.Application.Share.Run
{
    public interface IDatabaseService
    {
        Task<bool> VerifyBottomTopCodeAsync(string bottomCode, string topCode);
        Task UpdateOrAddCodeEntityAsync(string bottomCode, string topCode, string spCode);
        Task AddBTEntityAsync(string bottomCode, string topCode);
        Task UpdateTestResultAsync(string spCode, int result);
        Task<string> IncrementCountAsync(string bottomCode);  // 返回递增后的计数
        Task ClearCountAsync(string bottomCode);
    }
}
