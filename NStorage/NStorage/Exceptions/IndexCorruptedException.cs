using System;

namespace NStorage.Exceptions
{
    public class IndexCorruptedException : Exception
    {
        public IndexCorruptedException(string message) : base(message)
        {
        }
    }
}
