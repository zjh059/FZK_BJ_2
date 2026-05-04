using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FZK.Application.Share.Run
{
    public interface IJigFlowEngine
    {
        Task ProcessScanAsync();      // 对应底部+顶部扫码比对
        Task ProcessWeldAsync();      // 对应焊接后扫码查询MES并累加计数
        Task ProcessClearAsync();     // 对应清零扫码并清除数据库计数
    }
}
