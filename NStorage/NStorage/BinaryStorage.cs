using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using NStorage.DataStructure;
using NStorage.Exceptions;
using Index = NStorage.DataStructure.Index;
using System.IO.Compression;
using System.Security.Cryptography;
using NStorage.StorageHandlers;
using System.Runtime.CompilerServices;

namespace NStorage
{
    // TODO should be singletone
    public class BinaryStorage : IBinaryStorage
    {
        private const string IndexFile = "index.json";
        private const string StorageFile = "storage.bin";

        private readonly string _indexFilePath;
        private readonly string _storageFilePath;

        private readonly object _storageFilesAccessLock = new object();

        private readonly FileStream _storageFileStream;
        private readonly FileStream _indexFileStream;

        private const int AesEncryption_IVLength = 16;
        private readonly byte[]? _aesEncryption_Key;
        private readonly bool _encryptionEnalbed;

        private const int DefaultFlushIntervalMiliseconds = 100;

        private readonly IStorageHandler _handler;

        public BinaryStorage(StorageConfiguration configuration)
        {
            if (string.IsNullOrEmpty(configuration.WorkingFolder))
                throw new ArgumentException(paramName: nameof(configuration), message: "Working folder should be defined");
            if (!Directory.Exists(configuration.WorkingFolder))
                throw new ArgumentException(paramName: nameof(configuration), message: "Working folder should exist");

            var indexFilePath = Path.Combine(configuration.WorkingFolder, IndexFile);
            var storageFilePath = Path.Combine(configuration.WorkingFolder, StorageFile);

            if (!File.Exists(indexFilePath))
                File.WriteAllText(indexFilePath, string.Empty);
            if (!File.Exists(storageFilePath))
                File.WriteAllText(storageFilePath, string.Empty);

            _indexFilePath = indexFilePath;
            _storageFilePath = storageFilePath;

            // TODO validate encryption keys provided
            if (configuration.AesEncryptionKey != null)
            {
                _encryptionEnalbed = true;
                _aesEncryption_Key = configuration.AesEncryptionKey;
            }

            try
            {
                //_indexFileStream = File.Open(_indexFilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                _indexFileStream = File.Open(_indexFilePath, new FileStreamOptions
                {
                    Mode = FileMode.Open,
                    Access = FileAccess.ReadWrite,
                    Share = FileShare.None,
                    Options = FileOptions.RandomAccess
                });
                //_storageFileStream = File.Open(_storageFilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                _storageFileStream = File.Open(_storageFilePath, new FileStreamOptions
                {
                    Mode = FileMode.Open,
                    Access = FileAccess.ReadWrite,
                    Share = FileShare.None,
                    Options = FileOptions.RandomAccess
                });

                var index = DeserializeIndex(); // for more performace index file should be stored in memory

                CheckIndexNotCorrupted(index);
                CheckStorageNotCorrupted(index);

                var flushMode = configuration.FlushMode;
                switch (flushMode)
                {
                    case FlushMode.AtOnce:
                        _handler = new AtOnceStorageHandler(
                            storageFileStream: _storageFileStream,
                            indexFileStream: _indexFileStream,
                            index: index,
                            storageFilesAccessLock: _storageFilesAccessLock);
                        break;
                    case FlushMode.Deferred:
                        _handler = new DeferredIntervalStorageHandler(
                            storageFileStream: _storageFileStream,
                            indexFileStream: _indexFileStream,
                            storageFilesAccessLock: _storageFilesAccessLock,
                            index: index,
                            flushIntervalMilliseconds: configuration.FlushIntervalMilliseconds ?? DefaultFlushIntervalMiliseconds);
                        break;
                    default:
                        throw new InvalidOperationException("Unknown FlushMode"); // TODO better exception, do validation before try/catch
                }
                _handler.Init();
            }
            catch (Exception ex)
            {
                // TODO log it somewhere
                DisposeInternal(disposing: false, flushBuffers: false);

                throw;
            }

            // TODO check if storage file length is expected
            // TODO lock storage and index files for current process
        }

        ~BinaryStorage()
        {
            DisposeInternal(disposing: false, flushBuffers: true);
        }

        private Index DeserializeIndex()
        {
            using var streamReader = new StreamReader(_indexFileStream, leaveOpen: true);
            var indexAsTest = streamReader.ReadToEnd();
            return JsonConvert.DeserializeObject<Index>(indexAsTest) ?? new Index();
        }

        private void CheckIndexNotCorrupted(Index index)
        {
            long lastEndPosition = 0;
            for (int i = 0; i < index.Records.Count; i++)
            {
                var record = index.Records[i];
                if (lastEndPosition > 0)
                {
                    if (record.DataReference.StreamStart != lastEndPosition)
                        throw new IndexCorruptedException($"Record {i} with key {record.Key} expect to be started at {lastEndPosition}, but started at {record.DataReference.StreamStart}");
                }
                lastEndPosition = record.DataReference.StreamStart + record.DataReference.Length;
            }
        }

        private void CheckStorageNotCorrupted(Index index)
        {
            var expectedStorageLengthBytes = index.Records.Sum(x => x.DataReference.Length);
            // TODO consider optimization and crop file length if it is greater then expected
            var storageLength = new FileInfo(_storageFilePath).Length;
            if (storageLength != expectedStorageLengthBytes)
                throw new StorageCorruptedException($"Storage length is not as expected in summ of index data records. FileLength {storageLength}, expected {expectedStorageLengthBytes}");
        }

        public void Add(string key, Stream data, StreamInfo parameters)
        {
            EnsureNotDisposed();

            EnsureStreamParametersCorrect(parameters);

            EnsureAndBookKey(key);

            var dataTuple = GetProcessedMemory(data, parameters);
            _handler.Add(key, dataTuple);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EnsureStreamParametersCorrect(StreamInfo parameters)
        {
            if (parameters.IsEncrypted && !_encryptionEnalbed)
                throw new InvalidOperationException("Encryption was not configured in StorageConfiguration"); // TODO some exception
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private (Memory<byte> memory, DataProperties properties) GetProcessedMemory(Stream data, StreamInfo parameters) // TODO why we use memory here ? (remove if incompatible with NET Framework)
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
            MemoryStream? compressedEncrypted = null;
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
            var resultStream = new MemoryStream(); // TODO memstream pooling
            using (var aes = Aes.Create()) // TODO create instance variable ??
            {
                var iv = aes.IV;
                resultStream.Write(iv, 0, iv.Length);
                using (var encryptor = aes.CreateEncryptor(_aesEncryption_Key!, iv))
                using (var cryptoStream = new CryptoStream(resultStream, encryptor, CryptoStreamMode.Write, leaveOpen: true))
                {
                    dataStream.CopyTo(cryptoStream);
                }
            }
            resultStream.Seek(0, SeekOrigin.Begin);
            return resultStream;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private MemoryStream GetCompressedStream(Stream dataStream)
        {
            var resultStream = new MemoryStream(); // TODO memstream pooling
            using (var stream = new DeflateStream(resultStream, CompressionMode.Compress, leaveOpen:true))
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

            // TODO check bytes read count
            return GetProcessedStream(record.Item1, record.Item2); // TODO rename item1 item2
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        // TODO better naming
        private MemoryStream GetProcessedStream(byte[] bytes, DataProperties dataProperties)
        {
            EnsureDataPropertiesCorrect(dataProperties);

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
            MemoryStream? decrypted = null;
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
        private void EnsureDataPropertiesCorrect(DataProperties dataProperties)
        {
            if (dataProperties.IsEncrypted && !_encryptionEnalbed)
                throw new InvalidOperationException("Encryption was not configured in StorageConfiguration"); // TODO some exception
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private MemoryStream GetNewStreamFromDecrypt(MemoryStream inputStream)
        {
            var IVBytes = new byte[AesEncryption_IVLength]; // TODO array pool
            inputStream.Read(IVBytes, 0, AesEncryption_IVLength);
            using (var aes = Aes.Create())
            using (var decryptor = aes.CreateDecryptor(_aesEncryption_Key!, IVBytes))
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
            // TODO log fact of calling dispose from finalizer

            if (_isDisposed)
                return;

            _isDisposing = true;

            if (flushBuffers) // TODO revisit this logic
            {
                try
                {
                    _handler?.Dispose();
                }
                catch (Exception ex)
                {
                    // TODO what to do if file could not be flushed ?
                }
            }

            try
            {
                _indexFileStream?.Dispose();
                _storageFileStream?.Dispose();
            }
            catch (Exception ex)
            {
                // TODO what to do if file could not be flushed ?
            }

            _isDisposed = true;
            _isDisposing = false;
        }

        public void Dispose()
        {
            DisposeInternal(disposing: true, flushBuffers: true);

            GC.SuppressFinalize(this);
        }
    }
}
