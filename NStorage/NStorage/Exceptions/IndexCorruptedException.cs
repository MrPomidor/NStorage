using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NStorage.Exceptions
{
    public class IndexCorruptedException : Exception
    {
        public IndexCorruptedException(string? message) : base(message)
        {
        }
    }
}
