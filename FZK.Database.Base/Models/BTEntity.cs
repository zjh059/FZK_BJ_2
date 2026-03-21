using ReactiveUI.Fody.Helpers;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FZK.Database.Base.Models
{
    /// <summary>
    /// 底板码盖板码绑定核心表
    /// </summary>
    [Table(nameof(BTEntity))]
    public class BTEntity:EntityBase
    {
        /// <summary>
        /// 底板码
        /// </summary>
        [Reactive]
        public string BottomCode { get; set; }
        /// <summary>
        /// d导向板码
        /// </summary>
        [Reactive]
        public string TopCode { get; set; }

        /// <summary>
        /// 使用次数
        /// </summary>
        [Reactive]
        public string Counts { get; set; } = "0"; // 默认初始使用次数0
        /// <summary>
        /// 最后修改时间
        /// </summary>
        [Reactive]
        public DateTime UpdateTime { get; set; } = DateTime.Now;
    }
}
