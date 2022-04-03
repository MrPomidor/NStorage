using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NStorage.DataStructure;
using NStorage.Exceptions;
using NStorage.StorageHandlers;
using NStorage.StreamHandlers;
using NStorage.Tracing;
using LoggerExtensions = NStorage.Extensions.LoggerExtensions;

namespace NStorage
{
    public class BinaryStorage : IBinaryStorage
    {
        public const string IndexFile = "index.dat";
        public const string StorageFile = "storage.dat";

        private readonly object _storageFilesAccessLock = new object();

        private readonly bool _encryptionEnalbed;

        private readonly FileStream _storageFileStream;
        private readonly FileStream _indexFileStream;

        private readonly ILogger _logger;
        private readonly IStorageHandler _handler;
        private readonly IIndexStorageHandler _indexHandler;
        private readonly IStreamHandler _streamHandler;

        public BinaryStorage(StorageConfiguration configuration) : this(logger: null, configuration) { }

        public BinaryStorage(ILogger logger,
            StorageConfiguration configuration)
        {
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            _logger = logger ?? NullLogger.Instance;

            CheckEncryptionKeyCorrect(configuration, out _encryptionEnalbed);

            _streamHandler = new DefaultStreamHandler(_logger, configuration.AesEncryptionKey);

            try
            {
                (_indexFileStream, _storageFileStream) = OpenStreams(configuration);

                _indexHandler = new JsonIndexStorageHandler(_indexFileStream);

                var index = _indexHandler.DeserializeIndex(); // for more performace index file should be stored in memory

                CheckIndexNotCorrupted(index);
                CheckStorageNotCorrupted(index, _storageFileStream.Length);

                _handler = GetStorageHandler(configuration, index);
                _handler.Init();
            }
            catch (Exception ex)
            {
                LogError(ex, "Could not initialize BinaryStorage");
                DisposeInternal(disposing: false, flushBuffers: false);
                throw;
            }

            // TODO check if storage file length is expected
        }

        ~BinaryStorage()
        {
            DisposeInternal(disposing: false, flushBuffers: true);
        }

        private (FileStream indexFileStream, FileStream storageFileStream) OpenStreams(StorageConfiguration configuration)
        {
            if (!Directory.Exists(configuration.WorkingFolder))
                throw new ArgumentException(paramName: nameof(configuration), message: "Working folder should exist");

            var indexFilePath = Path.Combine(configuration.WorkingFolder, IndexFile);
            var storageFilePath = Path.Combine(configuration.WorkingFolder, StorageFile);

            if (!File.Exists(indexFilePath))
                File.WriteAllText(indexFilePath, string.Empty);
            if (!File.Exists(storageFilePath))
                File.WriteAllText(storageFilePath, string.Empty);

            var fileMode = FileMode.Open;
            var fileAccess = FileAccess.ReadWrite;
            var fileShare = FileShare.None;

#if NET6_0_OR_GREATER
            var indexFileStream = File.Open(indexFilePath, new FileStreamOptions
            {
                Mode = fileMode,
                Access = fileAccess,
                Share = fileShare,
                Options = FileOptions.RandomAccess
            });
#else
            var indexFileStream = File.Open(indexFilePath, fileMode, fileAccess, fileShare);
#endif
#if NET6_0_OR_GREATER
            var storageFileStream = File.Open(storageFilePath, new FileStreamOptions
            {
                Mode = fileMode,
                Access = fileAccess,
                Share = fileShare,
                Options = FileOptions.RandomAccess
            });
#else
            var storageFileStream = File.Open(storageFilePath, fileMode, fileAccess, fileShare);
#endif
            return (indexFileStream, storageFileStream);
        }

        private IStorageHandler GetStorageHandler(StorageConfiguration configuration, IndexDataStructure index)
        {
            var flushMode = configuration.FlushMode;
            switch (flushMode)
            {
                case FlushMode.AtOnce:
                    return new AtOnceFlushStorageHandler(
                        storageFileStream: _storageFileStream,
                        indexStorageHandler: _indexHandler,
                        index: index,
                        storageFilesAccessLock: _storageFilesAccessLock);
                case FlushMode.Deferred:
                    return new IntervalFlushStorageHandler(
                        storageFileStream: _storageFileStream,
                        indexStorageHandler: _indexHandler,
                        storageFilesAccessLock: _storageFilesAccessLock,
                        index: index,
                        flushIntervalMilliseconds: configuration.FlushIntervalMilliseconds ?? StorageConfiguration.DefaultFlushIntervalMiliseconds);
                case FlushMode.Manual:
                    return new ManualFlushStorageHandler(
                        storageFileStream: _storageFileStream,
                        indexStorageHandler: _indexHandler,
                        storageFilesAccessLock: _storageFilesAccessLock,
                        index: index);
                default:
                    throw new ArgumentOutOfRangeException("Unknown FlushMode");
            }
        }

        private void CheckEncryptionKeyCorrect(StorageConfiguration configuration, out bool enableEncryption)
        {
            enableEncryption = false;
            if (configuration.AesEncryptionKey != null)
            {
                using (var aes = Aes.Create())
                {
#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
                    if (!aes.ValidKeySize(bitLength: 8 * configuration.AesEncryptionKey.Length))
                        throw new ArgumentException("Encryption key length is invalid for current encryption algorithm", nameof(configuration));
#endif
                }
                enableEncryption = true;
            }
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

            _handler.EnsureAndBookKey(key);

            var dataTuple = _streamHandler.PackData(data, parameters);
            _handler.Add(key, dataTuple);

            AddEventSource.Log.AddStream(dataTuple.memory.Length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EnsureStreamParametersCorrect(StreamInfo parameters)
        {
            if (parameters.IsEncrypted && !_encryptionEnalbed)
                throw new ArgumentException(message: "Encryption was not configured in StorageConfiguration", paramName: nameof(parameters));
        }

        public Stream Get(string key)
        {
            EnsureNotDisposed();

            var record = _handler.GetRecord(key);
            if (record == null)
                throw new KeyNotFoundException(key);

            EnsureDataPropertiesCorrect(record.Value.recordProperties);

            return _streamHandler.UnPackData(record.Value.recordBytes, record.Value.recordProperties);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EnsureDataPropertiesCorrect(DataProperties dataProperties)
        {
            if (dataProperties.IsEncrypted && !_encryptionEnalbed)
                throw new ArgumentException(message: "Item encrypted. Configure encryption in StorageConfiguration");
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
                LogWarning($"Calling {nameof(DisposeInternal)} outside of Dispose");
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
                    LogError(ex, "Error occured on disposing handler");
                }
            }

            try
            {
                _indexFileStream?.Dispose();
                _storageFileStream?.Dispose();
            }
            catch (Exception ex)
            {
                LogError(ex, "Error occurred on disposing file streams");
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
            LoggerExtensions.LogError(_logger, ex, message);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void LogWarning(string message)
        {
            LoggerExtensions.LogWarning(_logger, message);
        }
    }
}
