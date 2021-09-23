namespace NStorage
{
    public class StreamInfo
    {
        public static readonly StreamInfo Empty = new StreamInfo();

        /// <summary>
        /// True if stream is compressed. Default false
        /// </summary>
        public bool IsCompressed { get; set; }

        /// <summary>
        /// True if stream is encrypted (AES)
        /// </summary>
        public bool IsEncrypted { get; set; }
    }
}
