using System;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using static NStorage.Consts;

namespace NStorage.Extensions
{
    internal static class LoggerExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LogError(ILogger logger, string message)
        {
#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
            logger.LogError($"{LogPrefix}{message}");
#else
            logger.LogError(default(EventId), $"{LogPrefix}{message}");
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LogError(ILogger logger, Exception ex, string message)
        {
#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
            logger.LogError(ex, $"{LogPrefix}{message}");
#else
            logger.LogError(default(EventId), ex, $"{LogPrefix}{message}");
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LogWarning(ILogger logger, string message)
        {
            logger.LogWarning($"{LogPrefix}{message}");
        }
    }
}
