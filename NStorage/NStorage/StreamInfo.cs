namespace NStorage
{
    public class StreamInfo
    {
        public static readonly StreamInfo Empty = new StreamInfo();

        public static readonly StreamInfo Compressed = new StreamInfo { IsCompressed = true };

        public static readonly StreamInfo Encrypted = new StreamInfo { IsEncrypted = true };

        public static readonly StreamInfo CompressedAndEncrypted = new StreamInfo { IsCompressed = true, IsEncrypted = true };

        private StreamInfo() { }

        /// <summary>
        /// True if stream is compressed. Default false
        /// </summary>
        public bool IsCompressed { get; private set; }

        /// <summary>
        /// True if stream is encrypted (AES)
        /// </summary>
        public bool IsEncrypted { get; private set; }
    }
}
