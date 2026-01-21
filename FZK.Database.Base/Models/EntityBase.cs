using ReactiveUI;
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
    /// 实体基类
    /// </summary>
    public class EntityBase : ReactiveObject
    {
        /// <summary>
        /// 主键
        /// </summary>
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]//自增
        [Reactive]
        public int Id { get; set; }
        /// <summary>
        /// 插入时间
        /// </summary>
        /// 
        [Reactive]
        public DateTime InsertDate { get; set; } = DateTime.Now;
    }
}
