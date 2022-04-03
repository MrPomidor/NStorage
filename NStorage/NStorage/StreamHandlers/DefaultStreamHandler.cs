using System;
using System.IO;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using NStorage.DataStructure;
using NStorage.Exceptions;
using NStorage.Extensions;
using static NStorage.Consts;
using LoggerExtensions = NStorage.Extensions.LoggerExtensions;

namespace NStorage.StreamHandlers
{
    internal class DefaultStreamHandler : IStreamHandler
    {
        private const int AesEncryption_IVLength = 16; // TODO consts ???

        private readonly byte[] _aesEncryption_Key;

        private readonly ILogger _logger;

        public DefaultStreamHandler(ILogger logger, byte[] aesEncryptionKey = null)
        {
            // TODO make helper method to throw argument exception on argument null (view from NET 6)
            if (logger == null)
                throw new ArgumentNullException(nameof(logger));

            _aesEncryption_Key = aesEncryptionKey;

            _logger = logger;
        }

        public (byte[] memory, DataProperties properties) PackData(Stream data, StreamInfo parameters)
        {
            if (!parameters.IsCompressed) // not compressed
            {
                if (!parameters.IsEncrypted) // not compressed, not encrypted
                {
                    var bytes = new byte[data.Length];
                    data.Read(bytes);
                    return (bytes, new DataProperties());
                }

                // not compressed, encrypted
                using (var encrypted = GetEncryptedStream(data))
                {
                    var bytes = new byte[encrypted.Length];
                    encrypted.Read(bytes);
                    return (bytes, new DataProperties { IsEncrypted = true });
                }
            }

            if (!parameters.IsEncrypted) // compressed, not encrypted
            {
                using (var compressed = GetCompressedStream(data))
                {
                    var bytes = new byte[compressed.Length];
                    compressed.Read(bytes);
                    return (bytes, new DataProperties { IsCompressed = true });
                }
            }

            // compressed, encrypted
            // first compress, then decrypt
            MemoryStream compressedEncrypted = null;
            using (var compressed = GetCompressedStream(data))
            {
                compressedEncrypted = GetEncryptedStream(compressed);
            }
            using (compressedEncrypted)
            {
                var bytes = new byte[compressedEncrypted.Length];
                compressedEncrypted.Read(bytes);
                return (bytes, new DataProperties { IsCompressed = true, IsEncrypted = true });
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private MemoryStream GetEncryptedStream(Stream dataStream)
        {
            if (_aesEncryption_Key == null)
            {
                var errorMessage = "Encryption is not configured";
                LogError(errorMessage);
                throw new InvalidOperationException(errorMessage);
            }

#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
            var resultStream = new MemoryStream(); // TODO memstream pooling
            using (var aes = Aes.Create())
            {
                var iv = aes.IV;
                resultStream.Write(iv, 0, iv.Length);
                using (var encryptor = aes.CreateEncryptor(_aesEncryption_Key, iv))
                using (var cryptoStream = new CryptoStream(resultStream, encryptor, CryptoStreamMode.Write, leaveOpen: true))
                {
                    dataStream.CopyTo(cryptoStream);
                }
            }
            resultStream.Seek(0, SeekOrigin.Begin);
            return resultStream;
#else
            var resultStream = new MemoryStream();
            using (var bufferStream = new MemoryStream()) // TODO memstream pooling
            using (var aes = Aes.Create())
            {
                var iv = aes.IV;
                resultStream.Write(iv, 0, iv.Length);
                using (var encryptor = aes.CreateEncryptor(_aesEncryption_Key, iv))
                using (var cryptoStream = new CryptoStream(bufferStream, encryptor, CryptoStreamMode.Write))
                {
                    dataStream.CopyTo(cryptoStream);
                    cryptoStream.FlushFinalBlock();
                    bufferStream.Seek(0, SeekOrigin.Begin);
                    bufferStream.CopyTo(resultStream);
                }
            }
            resultStream.Seek(0, SeekOrigin.Begin);
            return resultStream;
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private MemoryStream GetCompressedStream(Stream dataStream)
        {
            var resultStream = new MemoryStream(); // TODO memstream pooling
            using (var stream = new DeflateStream(resultStream, CompressionMode.Compress, leaveOpen: true))
            {
                dataStream.CopyTo(stream);
                stream.Flush();
            }
            resultStream.Seek(0, SeekOrigin.Begin);
            return resultStream;
        }

        public MemoryStream UnPackData(byte[] bytes, DataProperties dataProperties)
        {
            if (!dataProperties.IsCompressed) // not compressed
            {
                if (!dataProperties.IsEncrypted) // not compressed, not encrypted
                    return new MemoryStream(bytes);

                // not compressed, encrypted
                using (var inputStream = new MemoryStream(bytes)) // TODO memstream pooling 
                {
                    var resultStream = GetNewStreamFromDecrypt(inputStream);
                    return resultStream;
                }
            }

            if (!dataProperties.IsEncrypted) // compressed, not encrypted
            {
                using (var inputStream = new MemoryStream(bytes)) // TODO memstream pooling
                {
                    var resultStream = GetNewStreamFromDecompress(inputStream);
                    return resultStream;
                }
            }

            // compressed, encrypted
            // first decrypt, then decompress
            MemoryStream decrypted = null;
            using (var inputStream = new MemoryStream(bytes)) // TODO memstream pooling
            {
                decrypted = GetNewStreamFromDecrypt(inputStream);
            }
            using (decrypted)
            {
                decrypted.Seek(0, SeekOrigin.Begin);
                var resultStream = GetNewStreamFromDecompress(decrypted);
                return resultStream;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private MemoryStream GetNewStreamFromDecrypt(MemoryStream inputStream)
        {
            if (_aesEncryption_Key == null)
            {
                var errorMessage = "Encryption is not configured";
                LogError(errorMessage);
                throw new InvalidOperationException(errorMessage);
            }

            try
            {
                var IVBytes = new byte[AesEncryption_IVLength]; // TODO array pool
                inputStream.Read(IVBytes, 0, AesEncryption_IVLength);
                using (var aes = Aes.Create())
                using (var decryptor = aes.CreateDecryptor(_aesEncryption_Key, IVBytes))
                {
                    using (var cryptoStream = new CryptoStream(inputStream, decryptor, CryptoStreamMode.Read))
                    {
                        var resultMemoryStream = new MemoryStream();
                        cryptoStream.CopyTo(resultMemoryStream);
                        resultMemoryStream.Seek(0, SeekOrigin.Begin);
                        return resultMemoryStream;
                    }
                }
            }
            catch (CryptographicException cryptoException)
            {
                LogError(cryptoException, $"{LogPrefix}Error reading encrypted data");
                throw new InvalidEncryptionKeyException("Invalid encryption key. Please check storage configuration");
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private MemoryStream GetNewStreamFromDecompress(MemoryStream inputStream)
        {
            var resultMemoryStream = new MemoryStream();
            using (var decompress = new DeflateStream(inputStream, CompressionMode.Decompress))
            {
                decompress.CopyTo(resultMemoryStream);
                decompress.Flush();
                resultMemoryStream.Seek(0, SeekOrigin.Begin);
                return resultMemoryStream;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void LogError(string message)
        {
            LoggerExtensions.LogError(_logger, message);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void LogError(Exception ex, string message)
        {
            LoggerExtensions.LogError(_logger, ex, message);
        }
    }
}
