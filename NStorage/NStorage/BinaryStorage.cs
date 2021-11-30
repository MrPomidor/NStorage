using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NStorage.DataStructure;
using NStorage.Exceptions;
using NStorage.StorageHandlers;
using NStorage.Tracing;
using NStorage.Utils;

namespace NStorage
{
    public class BinaryStorage : IBinaryStorage
    {
        public const string IndexFile = "index.dat";
        public const string StorageFile = "storage.dat";

        private const int AesEncryption_IVLength = 16;
#if NET6_0_OR_GREATER
        private const string LogPrefix = $"{nameof(BinaryStorage)}::";
#else
        private const string LogPrefix = nameof(BinaryStorage) + "::";
#endif

        private readonly object _storageFilesAccessLock = new object();

        private readonly FileStream _storageFileStream;
        private readonly FileStream _indexFileStream;

        private readonly byte[] _aesEncryption_Key;
        private readonly bool _encryptionEnalbed;

        private readonly ILogger _logger;
        private readonly IStorageHandler _handler;
        private readonly IIndexStorageHandler _indexHandler;

        public BinaryStorage(StorageConfiguration configuration) : this(logger: null, configuration) { }

        public BinaryStorage(ILogger logger,
            StorageConfiguration configuration)
        {
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            _logger = logger ?? NullLogger.Instance;

            if (configuration.AesEncryptionKey != null)
            {
                using (var aes = Aes.Create())
                {
#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
                    if (!aes.ValidKeySize(bitLength: 8 * configuration.AesEncryptionKey.Length))
                        throw new ArgumentException("Encryption key length is invalid for current encryption algorithm", nameof(configuration));
#endif
                }
                _encryptionEnalbed = true;
                _aesEncryption_Key = configuration.AesEncryptionKey;
            }

            if (!Directory.Exists(configuration.WorkingFolder))
                throw new ArgumentException(paramName: nameof(configuration), message: "Working folder should exist");

            var indexFilePath = Path.Combine(configuration.WorkingFolder, IndexFile);
            var storageFilePath = Path.Combine(configuration.WorkingFolder, StorageFile);

            if (!File.Exists(indexFilePath))
                File.WriteAllText(indexFilePath, string.Empty);
            if (!File.Exists(storageFilePath))
                File.WriteAllText(storageFilePath, string.Empty);

            try
            {
                var fileMode = FileMode.Open;
                var fileAccess = FileAccess.ReadWrite;
                var fileShare = FileShare.None;

#if NET6_0_OR_GREATER
                _indexFileStream = File.Open(indexFilePath, new FileStreamOptions
                {
                    Mode = fileMode,
                    Access = fileAccess,
                    Share = fileShare,
                    Options = FileOptions.RandomAccess
                });
#else
                _indexFileStream = File.Open(indexFilePath, fileMode, fileAccess, fileShare);
#endif
#if NET6_0_OR_GREATER
                _storageFileStream = File.Open(storageFilePath, new FileStreamOptions
                {
                    Mode = fileMode,
                    Access = fileAccess,
                    Share = fileShare,
                    Options = FileOptions.RandomAccess
                });
#else
                _storageFileStream = File.Open(storageFilePath, fileMode, fileAccess, fileShare);
#endif

                _indexHandler = new JsonIndexStorageHandler(_indexFileStream);

                var index = _indexHandler.DeserializeIndex(); // for more performace index file should be stored in memory

                CheckIndexNotCorrupted(index);
                CheckStorageNotCorrupted(index, _storageFileStream.Length);

                var flushMode = configuration.FlushMode;
                switch (flushMode)
                {
                    case FlushMode.AtOnce:
                        _handler = new AtOnceFlushStorageHandler(
                            storageFileStream: _storageFileStream,
                            indexStorageHandler: _indexHandler,
                            index: index,
                            storageFilesAccessLock: _storageFilesAccessLock);
                        break;
                    case FlushMode.Deferred:
                        _handler = new IntervalFlushStorageHandler(
                            storageFileStream: _storageFileStream,
                            indexStorageHandler: _indexHandler,
                            storageFilesAccessLock: _storageFilesAccessLock,
                            index: index,
                            flushIntervalMilliseconds: configuration.FlushIntervalMilliseconds ?? StorageConfiguration.DefaultFlushIntervalMiliseconds);
                        break;
                    case FlushMode.Manual:
                        _handler = new ManualFlushStorageHandler(
                            storageFileStream: _storageFileStream,
                            indexStorageHandler: _indexHandler,
                            storageFilesAccessLock: _storageFilesAccessLock,
                            index: index);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException("Unknown FlushMode");
                }
                _handler.Init();
            }
            catch (Exception ex)
            {
                LogError(ex, $"{LogPrefix}Could not initialize BinaryStorage");
                DisposeInternal(disposing: false, flushBuffers: false);
                throw;
            }

            // TODO check if storage file length is expected
        }

        ~BinaryStorage()
        {
            DisposeInternal(disposing: false, flushBuffers: true);
        }

        // TODO move to distinct class
        private void CheckIndexNotCorrupted(IndexDataStructure index)
        {
            long lastEndPosition = 0;
            var kvp = index.Records.OrderBy(x => x.Value.DataReference.StreamStart).ToArray();
            for (int i = 0; i < kvp.Length; i++)
            {
                var record = kvp[i];
                var dataReference = record.Value.DataReference;
                if (lastEndPosition > 0)
                {
                    if (dataReference.StreamStart != lastEndPosition)
                        throw new IndexCorruptedException($"Record {i} with key {record.Key} expect to be started at {lastEndPosition}, but started at {dataReference.StreamStart}");
                }
                lastEndPosition = dataReference.StreamStart + dataReference.Length;
            }
        }

        // TODO move to distinct class
        private void CheckStorageNotCorrupted(IndexDataStructure index, long storageLength)
        {
            var expectedStorageLengthBytes = index.Records.Values.Sum(x => x.DataReference.Length);
            // TODO consider optimization and crop file length if it is greater then expected
            if (storageLength != expectedStorageLengthBytes)
                throw new StorageCorruptedException($"Storage length is not as expected in summ of index data records. FileLength {storageLength}, expected {expectedStorageLengthBytes}");
        }

        public void Add(string key, Stream data, StreamInfo parameters = null)
        {
            EnsureNotDisposed();

            parameters = parameters ?? StreamInfo.Empty;

            EnsureStreamParametersCorrect(parameters);

            EnsureAndBookKey(key);

            var dataTuple = GetProcessedMemory(data, parameters);
            _handler.Add(key, dataTuple);

            AddEventSource.Log.AddStream(dataTuple.memory.Length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EnsureStreamParametersCorrect(StreamInfo parameters)
        {
            if (parameters.IsEncrypted && !_encryptionEnalbed)
                throw new ArgumentException(message: "Encryption was not configured in StorageConfiguration", paramName: nameof(parameters));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private (byte[] memory, DataProperties properties) GetProcessedMemory(Stream data, StreamInfo parameters)
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EnsureAndBookKey(string key)
        {
            _handler.EnsureAndBookKey(key);
        }

        public Stream Get(string key)
        {
            EnsureNotDisposed();

            if (!_handler.TryGetRecord(key, out var record))
                throw new KeyNotFoundException(key);

            return PreProcessStream(record.recordBytes, record.recordProperties, key);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private MemoryStream PreProcessStream(byte[] bytes, DataProperties dataProperties, string key)
        {
            EnsureDataPropertiesCorrect(dataProperties);

            if (!dataProperties.IsCompressed) // not compressed
            {
                if (!dataProperties.IsEncrypted) // not compressed, not encrypted
                    return new MemoryStream(bytes);

                // not compressed, encrypted
                using (var inputStream = new MemoryStream(bytes)) // TODO memstream pooling 
                {
                    var resultStream = GetNewStreamFromDecrypt(inputStream, key);
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
                decrypted = GetNewStreamFromDecrypt(inputStream, key);
            }
            using (decrypted)
            {
                decrypted.Seek(0, SeekOrigin.Begin);
                var resultStream = GetNewStreamFromDecompress(decrypted);
                return resultStream;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EnsureDataPropertiesCorrect(DataProperties dataProperties)
        {
            if (dataProperties.IsEncrypted && !_encryptionEnalbed)
                throw new ArgumentException(message: "Item encrypted. Configure encryption in StorageConfiguration");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private MemoryStream GetNewStreamFromDecrypt(MemoryStream inputStream, string key)
        {
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
                LogError(cryptoException, $"{LogPrefix}Error reading encrypted data for key \"{key}\"");
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

        public bool Contains(string key)
        {
            EnsureNotDisposed();

            return _handler.Contains(key);
        }

        public void Flush()
        {
            EnsureNotDisposed();

            _handler.Flush();

            FlushEventSource.Log.FlushManual();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EnsureNotDisposed()
        {
            if (_isDisposed || _isDisposing)
                throw new ObjectDisposedException(GetType().FullName);
        }

        private volatile bool _isDisposed = false;
        private volatile bool _isDisposing = false;
        private void DisposeInternal(bool disposing, bool flushBuffers)
        {
            if (_isDisposed)
                return;

            _isDisposing = true;

            if (!disposing)
            {
                _logger.LogWarning($"{LogPrefix}Calling {nameof(DisposeInternal)} outside of Dispose");
            }

            if (flushBuffers)
            {
                try
                {
                    _handler?.Dispose();
                    _indexHandler?.Dispose();
                }
                catch (Exception ex)
                {
                    LogError(ex, $"{LogPrefix}Error occured on disposing handler");
                }
            }

            try
            {
                _indexFileStream?.Dispose();
                _storageFileStream?.Dispose();
            }
            catch (Exception ex)
            {
                LogError(ex, $"{LogPrefix}Error occurred on disposing file streams");
            }

            _isDisposed = true;
            _isDisposing = false;
        }

        public void Dispose()
        {
            DisposeInternal(disposing: true, flushBuffers: true);

            GC.SuppressFinalize(this);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void LogError(Exception ex, string message)
        {
#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
            _logger.LogError(ex, message);
#else
            _logger.LogError(default(EventId), ex, message);
#endif
        }
    }
}
