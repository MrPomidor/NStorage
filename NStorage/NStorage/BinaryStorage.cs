using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NStorage.DataStructure;
using NStorage.Exceptions;
using Index = NStorage.DataStructure.Index;
using System.Threading;
using System.IO.Compression;
using System.Security.Cryptography;

namespace NStorage
{
    // TODO should be singletone
    public class BinaryStorage : IBinaryStorage
    {
        private const string IndexFile = "index.json";
        private const string StorageFile = "storage.bin";

        private readonly string _indexFilePath;
        private readonly string _storageFilePath;

        private readonly ConcurrentDictionary<string, IndexRecord> _recordsCache;

        private long _storageFileLength;
        private readonly FileStream _storageFileStream;
        private readonly FileStream _indexFileStream;

        private const int AesEncryption_IVLength = 16;
        private readonly byte[]? _aesEncryption_Key;
        private readonly bool _encryptionEnalbed;

        private readonly FlushMode? _flushMode;
        private readonly int _flushIntervalMilliseconds;
        private const int DefaultFlushIntervalMiliseconds = 100;

        private readonly ConcurrentDictionary<string, (Memory<byte> memory, DataProperties properties)?>? _tempRecordsCache;
        private readonly ConcurrentQueue<(string key, (Memory<byte> memory, DataProperties properties))>? _recordsQueue;

        private CancellationTokenSource _source = new CancellationTokenSource();
        private CancellationToken _token;

        private ManualResetEvent _flushDisposed = new ManualResetEvent(false);

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
                _recordsCache = new ConcurrentDictionary<string, IndexRecord>(index.Records.ToDictionary(item => item.Key));

                CheckIndexNotCorrupted(index);
                CheckStorageNotCorrupted(index);

                _storageFileLength = _storageFileStream.Length;
                _storageFileStream.Seek(_storageFileLength, SeekOrigin.Begin);
            }
            catch (Exception ex)
            {
                // TODO log it somewhere
                DisposeInternal(disposing: false, flushBuffers: false);

                throw;
            }

            _flushMode = configuration.FlushMode;
            if (_flushMode == FlushMode.Deferred)
            {
                _flushIntervalMilliseconds = configuration.FlushIntervalMilliseconds ?? DefaultFlushIntervalMiliseconds;

                _tempRecordsCache = new ConcurrentDictionary<string, (Memory<byte> memory, DataProperties properties)?>();
                _recordsQueue = new ConcurrentQueue<(string key, (Memory<byte> memory, DataProperties properties))>();

                _token = _source.Token;
                Task.Run(OnTick, _token);
            }

            // TODO validate encryption keys provided
            if (configuration.AesEncryptionKey != null)
            {
                _encryptionEnalbed = true;
                _aesEncryption_Key = configuration.AesEncryptionKey;
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

            if (_flushMode == FlushMode.AtOnce)
            {
                var dataTuple = GetProcessedMemory(data, parameters);
                long streamLength = dataTuple.memory.Length;

                lock (this)
                {
                    var fileStream = _storageFileStream;
                    long startPosition = _storageFileLength;
                    fileStream.Write(dataTuple.memory.Span);

                    var record = new IndexRecord(key, new DataReference { StreamStart = startPosition, Length = streamLength }, dataTuple.properties);
                    _recordsCache.AddOrUpdate(key, (_) => record, (_, _) => record);

                    _storageFileLength += streamLength;

                    FlushFiles();
                }
            }
            else if (_flushMode == FlushMode.Deferred)
            {
                var dataTuple = GetProcessedMemory(data, parameters);

                _tempRecordsCache!.AddOrUpdate(key, (_) => dataTuple, (_, _) => dataTuple);
                _recordsQueue!.Enqueue((key, dataTuple));
            }
            else
            {
                throw new Exception("Bad thisgs happening");
            }
        }

        private void EnsureStreamParametersCorrect(StreamInfo parameters)
        {
            if (parameters.IsEncrypted && !_encryptionEnalbed)
                throw new InvalidOperationException("Encryption was not configured in StorageConfiguration"); // TODO some exception
        }

        private (Memory<byte> memory, DataProperties properties) GetProcessedMemory(Stream data, StreamInfo parameters)
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

        private MemoryStream GetEncryptedStream(Stream dataStream)
        {
            var resultStream = new MemoryStream(); // TODO memstream pooling
            // TODO aes managed ?
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

        private async Task OnTick()
        {
            var processingBuffer = new List<(string key, (Memory<byte> memory, DataProperties properties))>();
            while (true)
            {
                if (!_token.IsCancellationRequested)
                {
                    await Task.Delay(_flushIntervalMilliseconds);
                }

                while (_recordsQueue!.TryDequeue(out var queueItem))
                {
                    processingBuffer.Add(queueItem);
                }

                if (processingBuffer.Count == 0)
                {
                    if (_token.IsCancellationRequested)
                    {
                        _flushDisposed.Set();
                        return;
                    }
                }
                else
                {
                    await OnTick_Internal(processingBuffer);

                    processingBuffer.Clear();
                }
            }
        }

        // TODO rename
        private async Task OnTick_Internal(List<(string key, (Memory<byte> memory, DataProperties properties))> processingBuffer)
        {
            var newStorageLength = _storageFileLength;

            var keys = new List<string>();
            var fileStream = _storageFileStream;

            foreach (var item in processingBuffer)
            {
                var streamStart = newStorageLength;

                var key = item.key;
                keys.Add(key);
                (var memory, var dataProperties) = item.Item2;

                lock (this)
                {
                    fileStream.Write(memory.Span);
                    newStorageLength += memory.Length;
                    _storageFileLength = newStorageLength;
                }

                var record = new IndexRecord(key, new DataReference { StreamStart = streamStart, Length = memory.Length }, dataProperties);
                _recordsCache.AddOrUpdate(key, (_) => record, (_, _) => record);
            }

            lock (this)
            {
                FlushFiles();
            }

            foreach (var key in keys)
            {
                _tempRecordsCache!.Remove(key, out _);
            }
        }

        private void FlushFiles()
        {
            FlushStorageFile();
            FlushIndexFile();
        }

        private void FlushStorageFile()
        {
            _storageFileStream.Flush(); // flush stream
        }

        private void FlushIndexFile()
        {
            var index = new Index { Records = _recordsCache.Values.ToArray().Where(x => x != null).OrderBy(x => x.DataReference.StreamStart).ToList() };
            var indexSerialized = JsonConvert.SerializeObject(index);
            var bytes = Encoding.UTF8.GetBytes(indexSerialized);

            // TODO find way to rewrite using single operation system method
            _indexFileStream.Seek(0, SeekOrigin.Begin);
            _indexFileStream.SetLength(0);
            _indexFileStream.Write(bytes);
            _indexFileStream.Flush();
        }

        private void EnsureAndBookKey(string key)
        {
            if (_flushMode == FlushMode.AtOnce)
            {
                if (!_recordsCache.TryAdd(key, null))
                {
                    throw new ArgumentException($"Key {key} already exists in storage");
                }
            }
            else if (_flushMode == FlushMode.Deferred)
            {
                if (_recordsCache.TryGetValue(key, out _) || !_tempRecordsCache!.TryAdd(key, null))
                {
                    throw new ArgumentException($"Key {key} already exists in storage");
                }
            }
            else
            {
                throw new Exception("Bad things"); // TODO new name
            }
        }

        public Stream Get(string key)
        {
            EnsureNotDisposed();

            if (_flushMode == FlushMode.Deferred && _tempRecordsCache!.TryGetValue(key, out var record) && record != null)
            {
                return GetProcessedStream(record.Value.memory.ToArray(), record.Value.properties);
            }

            if (!_recordsCache.TryGetValue(key, out var recordData) || recordData == null)
                throw new KeyNotFoundException(key);

            var fileStream = _storageFileStream;
            var bytes = new byte[recordData!.DataReference.Length];
            lock (this)
            {
                var fileStreamLength = _storageFileLength;
                fileStream.Seek(recordData.DataReference.StreamStart, SeekOrigin.Begin);
                var bytesRead = fileStream.Read(bytes);
                fileStream.Seek(fileStreamLength, SeekOrigin.Begin);
            }
            // TODO check bytes read count
            return GetProcessedStream(bytes, recordData!.Properties);
        }

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

        private void EnsureDataPropertiesCorrect(DataProperties dataProperties)
        {
            if (dataProperties.IsEncrypted && !_encryptionEnalbed)
                throw new InvalidOperationException("Encryption was not configured in StorageConfiguration"); // TODO some exception
        }

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

            if (_flushMode == FlushMode.Deferred && _tempRecordsCache!.TryGetValue(key, out var value) && value != null)
                return true;
            return _recordsCache.TryGetValue(key, out var recordData) && recordData != null;
        }

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

            if (flushBuffers)
            {
                try
                {
                    if (_flushMode == FlushMode.AtOnce)
                    {
                        lock (this)
                        {
                            _storageFileStream.Flush();
                            FlushIndexFile();
                        }
                    }
                    else if (_flushMode == FlushMode.Deferred)
                    {
                        _source.Cancel();
                        _flushDisposed.WaitOne();

                        _tempRecordsCache!.Clear();
                        _recordsQueue!.Clear();
                    }
                    else
                    {
                        throw new Exception("Bad thisgs happening");
                    }
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
                _flushDisposed?.Dispose();
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
