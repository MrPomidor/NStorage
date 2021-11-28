using System;

namespace NStorage
{
    public class StorageConfiguration
    {
        public const int DefaultFlushIntervalMiliseconds = 50;

        /// <summary>
        /// Folder where implementation should store Index and Storage File
        /// </summary>
        public string WorkingFolder { get; private set; }

        public FlushMode FlushMode { get; private set; } = FlushMode.AtOnce;

        public int? FlushIntervalMilliseconds { get; private set; }

        public byte[]? AesEncryptionKey { get; private set; }

        public StorageConfiguration(string workingFolder)
        {
            if (string.IsNullOrEmpty(workingFolder))
                throw new ArgumentException(paramName: nameof(workingFolder), message: "Working folder should be defined");
            WorkingFolder = workingFolder;
        }

        public StorageConfiguration SetFlushModeManual()
        {
            FlushMode = FlushMode.Manual;
            return this;
        }

        public StorageConfiguration SetFlushModeDeferred(int? flushIntervalMilliseconds = null)
        {
            if (flushIntervalMilliseconds.HasValue && flushIntervalMilliseconds <= 0)
                throw new ArgumentException(paramName: nameof(flushIntervalMilliseconds), message: "Flush interval value invalid");

            FlushMode = FlushMode.Deferred;
            FlushIntervalMilliseconds = flushIntervalMilliseconds;
            return this;
        }

        public StorageConfiguration EnableEncryption(byte[] aesEncryptionKey)
        {
            if (aesEncryptionKey == null)
                throw new ArgumentNullException(nameof(aesEncryptionKey));
            // TODO validate encryption key
            AesEncryptionKey = aesEncryptionKey;
            return this;
        }
    }

    public enum FlushMode
    {
        AtOnce,
        Deferred,
        Manual
    }
}
