using System;

namespace NStorage.Exceptions
{
    public class InvalidEncryptionKeyException : Exception
    {
        public InvalidEncryptionKeyException(string message) : base(message)
        {
        }
    }
}
