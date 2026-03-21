    using ReactiveUI.Fody.Helpers;
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    namespace FZK.Database.Base.Models
    {
        /// <summary>
        /// 底板码盖板码绑定核心表
        /// </summary>
        [Table(nameof(CodeEntity))]
        public class CodeEntity : EntityBase
        {
            /// <summary>
            /// 底板码
            /// </summary>
            [Reactive]
            public string BottomCode { get; set; }
            /// <summary>
            /// 导向板码
            /// </summary>
            [Reactive]
            public string TopCode { get; set; }
            /// <summary>
            /// 主板码
            /// </summary>
            [Reactive]
            public string SPCode { get; set; }
            /// <summary>
            /// 测试结果（建议：0-未测试 1-合格 2-不合格，按业务调整）
            /// </summary>
            [Reactive]
            public string Result { get; set; } = "0";

        }
    }
