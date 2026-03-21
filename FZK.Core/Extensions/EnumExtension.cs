using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FZK.Core.Extensions
{
    public class EnumExtension
    {
        public static List<string> ToString<T>() where T : Enum
        {
            return Enum.GetNames(typeof(T)).ToList();
        }

        public static List<T> ToList<T>() where T : Enum
        {
            return Enum.GetValues(typeof(T)).Cast<T>().ToList();
        }
    }
}