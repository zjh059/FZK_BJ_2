using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FZK.Application.Share.Init
{
    /// <summary>
    /// 硬件初始化返回结果实体
    /// </summary>
    public struct InitResult
    {
        public string Message { get; set; }
        public bool Success { get; set; }

    }
}
