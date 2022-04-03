using System.IO;
using System.Runtime.CompilerServices;

namespace NStorage.Extensions
{
    internal static class StreamExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Write(this Stream fileStream, byte[] bytes)
        {
#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
            fileStream.Write(bytes);
#else
            fileStream.Write(bytes, 0, bytes.Length);
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Read(this Stream fileStream, byte[] bytes)
        {
#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
            return fileStream.Read(bytes);
#else
            return fileStream.Read(bytes, 0, bytes.Length);
#endif
        }
    }
}
