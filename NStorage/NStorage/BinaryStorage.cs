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
using System.Diagnostics;
using System.Timers;

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

        private readonly FileStream _storageFileStream;
        private readonly FileStream _indexFileStream;

        private readonly IndexFlushMode _flushMode;
        private const int DefaultIndexFlushIntervalMiliseconds = 100;
        private readonly Timer? _indexFlushTimer;

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
            }
            catch (Exception ex)
            {
                // TODO log it somewhere
                DisposeInternal(disposing: false, flushBuffers: false);

                throw;
            }

            _flushMode = configuration.IndexFlushMode;
            //_flushInterval = TimeSpan.FromMilliseconds(100);
            if (_flushMode == IndexFlushMode.Deferred)
            {
                _indexFlushTimer = new Timer(DefaultIndexFlushIntervalMiliseconds);
                _indexFlushTimer.AutoReset = true;
                _indexFlushTimer.Elapsed += IndexFlushTimer_Elapsed;
                _indexFlushTimer.Start();
            }
            // TODO check if storage file length is expected
            // TODO lock storage and index files for current process
        }

        private void IndexFlushTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            FlushIndex();
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
            var storageLength = new FileInfo(_storageFilePath).Length;
            if (storageLength != expectedStorageLengthBytes)
                throw new StorageCorruptedException($"Storage length is not as expected in summ of index data records. FileLength {storageLength}, expected {expectedStorageLengthBytes}");
        }

        public void Add(string key, Stream data, StreamInfo parameters)
        {
            EnsureNotDisposed();

            EnsureKeyNotExist(key);

            (var startPosition, var length) = AppendToStorage(data, parameters);

            var record = new IndexRecord(key, new DataReference { StreamStart = startPosition, Length = length }, new DataProperties());
            _recordsCache.TryAdd(key, record);

            if (_flushMode == IndexFlushMode.AtOnce)
            {
                FlushIndex();
            }
        }

        private Stream GetProcessedStream(Stream data, StreamInfo parameters)
        {
            var memStream = new MemoryStream();
            data.CopyTo(memStream);
            memStream.Seek(0, SeekOrigin.Begin);
            return memStream;
        }

        private (long startPosition, long length) AppendToStorage(Stream inputData, StreamInfo parameters)
        {
            using (var dataStream = GetProcessedStream(inputData, parameters))
            {
                var fileStream = _storageFileStream;
                long streamLength = dataStream.Length;

                lock (this)
                {
                    long startPosition = fileStream.Length;
                    fileStream.Seek(fileStream.Length, SeekOrigin.Begin);
                    dataStream.CopyTo(fileStream);
                    fileStream.Flush();

                    return (startPosition, length: streamLength);
                }
            }
        }

        private void FlushIndex()
        {
            var index = new Index { Records = _recordsCache.Values.OrderBy(x => x.DataReference.StreamStart).ToList() };
            var indexSerialized = JsonConvert.SerializeObject(index);
            var bytes = Encoding.UTF8.GetBytes(indexSerialized);

            lock (this)
            {
                _indexFileStream.Seek(0, SeekOrigin.Begin);
                _indexFileStream.SetLength(0);
                _indexFileStream.Write(bytes);
                _indexFileStream.Flush();
            }
        }

        private void EnsureKeyNotExist(string key)
        {
            if (Contains(key))
                throw new ArgumentException($"Key {key} already exists in storage");
        }

        public Stream Get(string key)
        {
            EnsureNotDisposed();

            if (!Contains(key, out var recordData))
                throw new KeyNotFoundException(key);

            var fileStream = _storageFileStream;
            var bytes = new byte[recordData.DataReference.Length];
            lock (this)
            {
                fileStream.Seek(recordData.DataReference.StreamStart, SeekOrigin.Begin);
                var bytesRead = fileStream.Read(bytes);
            }
            // TODO check bytes read count
            var memStream = new MemoryStream(bytes);
            return memStream;
        }

        public bool Contains(string key)
        {
            EnsureNotDisposed();

            return Contains(key, out _);
        }

        private bool Contains(string key, out IndexRecord recordData)
        {
            return _recordsCache.TryGetValue(key, out recordData);
        }

        private void EnsureNotDisposed()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(GetType().FullName);
        }

        private bool _isDisposed = false;
        private void DisposeInternal(bool disposing, bool flushBuffers)
        {
            if (_isDisposed)
                return;

            if (flushBuffers)
            {
                try
                {
                    FlushIndex();
                    // TODO flush storage
                    // TODO close all file blockings
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
                if (_indexFlushTimer != null)
                {
                    _indexFlushTimer.Elapsed -= IndexFlushTimer_Elapsed;
                    _indexFlushTimer.Dispose();
                }
            }
            catch (Exception ex)
            {
                // TODO what to do if file could not be flushed ?
            }

            _isDisposed = true;
        }

        public void Dispose()
        {
            DisposeInternal(disposing: true, flushBuffers: true);

            GC.SuppressFinalize(this);
        }
    }
}
