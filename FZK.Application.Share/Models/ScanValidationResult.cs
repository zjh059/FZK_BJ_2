using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FZK.Application.Share.Models
{
    public class ScanValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Codes { get; set; }
        public string PrimaryCode => Codes?.FirstOrDefault() ?? string.Empty;
    }
}
