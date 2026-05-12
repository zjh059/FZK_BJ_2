using ReactiveUI.Fody.Helpers;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FZK.Application.Run.Models
{
    public class ScanRecord : ReactiveObject
    {
        [Reactive] public DateTime CreateTime { get; set; }
        [Reactive] public string JigNo { get; set; }
        [Reactive] public string ScanType { get; set; }
        [Reactive] public string BottomCode { get; set; }
        [Reactive] public string TopCode { get; set; }
        [Reactive] public string SPCode { get; set; }
        [Reactive] public string Result { get; set; }
        [Reactive] public string Remark { get; set; }
    }
}
