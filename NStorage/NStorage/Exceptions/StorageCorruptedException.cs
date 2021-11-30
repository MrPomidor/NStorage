using System;

namespace NStorage.Exceptions
{
    public class StorageCorruptedException : Exception
    {
        public StorageCorruptedException(string message) : base(message)
        {
        }
    }
}
