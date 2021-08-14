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
using System.IO.Pipes;
using System.Threading;

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

        private readonly FlushMode? _flushMode;
        private const int DefaultFlushIntervalMiliseconds = 100;
        //private readonly Timer? _flushTimer;
        //private long _flushStreamLength;
        //private MemoryStream? _storageFlushStream;
        //private ConcurrentDictionary<string, IndexRecord>? _tempRecordsCache;
        // TODO temp sync object
        //private readonly object _flushLock = new object();

        private readonly ConcurrentDictionary<string, (Memory<byte> memory, DataProperties properties)?>? _tempRecordsCache;
        private readonly ConcurrentQueue<(string key, (Memory<byte> memory, DataProperties properties))>? _recordsQueue;

        private CancellationTokenSource _source = new CancellationTokenSource();
        private CancellationToken _token;

        private static ManualResetEvent _flushDisposed = new ManualResetEvent(false);

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
                // TODO store stream length and current position in field
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
                _tempRecordsCache = new ConcurrentDictionary<string, (Memory<byte> memory, DataProperties properties)?>();
                _recordsQueue = new ConcurrentQueue<(string key, (Memory<byte> memory, DataProperties properties))>();

                _token = _source.Token;
                Task.Run(OnTick, _token);
                //_flushStreamLength = 0;
                //_storageFlushStream = new MemoryStream();
                //_tempRecordsCache = new ConcurrentDictionary<string, IndexRecord>();
                //_flushTimer = new Timer(DefaultFlushIntervalMiliseconds);
                //_flushTimer.AutoReset = true;
                //_flushTimer.Elapsed += FlushTimer_Elapsed;
                //_flushTimer.Start();
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

            EnsureAndBookKey(key);

            if (_flushMode == FlushMode.AtOnce)
            {
                using (var dataStream = GetProcessedStream(data, parameters))
                {
                    long streamLength = dataStream.Length;

                    lock (this)
                    {
                        var fileStream = _storageFileStream;
                        long startPosition = _storageFileLength;
                        //fileStream.Seek(fileStream.Length, SeekOrigin.Begin); // TODO do we need seek here
                        dataStream.CopyTo(fileStream);

                        var record = new IndexRecord(key, new DataReference { StreamStart = startPosition, Length = streamLength }, new DataProperties());
                        _recordsCache.AddOrUpdate(key, (_) => record, (_, _) => record);

                        _storageFileLength += streamLength;

                        fileStream.Flush(); // flush stream
                        FlushIndex();
                    }
                }
            }
            else if (_flushMode == FlushMode.Deferred)
            {
                var dataTuple = GetProcessedMemory(data, parameters);

                _tempRecordsCache!.AddOrUpdate(key, (_) => dataTuple, (_, _) => dataTuple);
                _recordsQueue.Enqueue((key, dataTuple));

                //using (var dataStream = GetProcessedMemStream(data, parameters))
                //{
                //    long streamLength = dataStream.Length;
                //    Memory<byte> bytes = dataStream;
                //    lock (_flushLock)
                //    {
                //        var tempStream = _storageFlushStream!;
                //        long startPosition = _flushStreamLength;
                //        //tempStream.Seek(tempStream.Length, SeekOrigin.Begin); // TODO do we need seek here ?
                //        dataStream.CopyTo(tempStream);

                //        var record = new IndexRecord(key, new DataReference { StreamStart = startPosition, Length = streamLength }, new DataProperties());
                //        _tempRecordsCache!.AddOrUpdate(key, (_) => record, (_, _) => record);

                //        _flushStreamLength += streamLength;
                //    }
                //}
            }
            else
            {
                throw new Exception("Bad thisgs happening");
            }
        }

        // TODO remove copy if not needed 
        private Stream GetProcessedStream(Stream data, StreamInfo parameters)
        {
            var memStream = new MemoryStream();
            data.CopyTo(memStream);
            memStream.Seek(0, SeekOrigin.Begin);
            return memStream;
        }

        private (Memory<byte> memory, DataProperties properties) GetProcessedMemory(Stream data, StreamInfo parameters)
        {
            var bytes = new byte[data.Length];
            data.Read(bytes);
            //Span<byte> bytes = new Span<byte>(data.Length);
            //data.Read(bytes);
            //var memStream = new MemoryStream();
            //data.CopyTo(memStream);
            //memStream.Seek(0, SeekOrigin.Begin);
            return (bytes, new DataProperties());
        }

        private async Task OnTick()
        {
            var processingBuffer = new List<(string key, (Memory<byte> memory, DataProperties properties))>();
            while (true)
            {
                if (_recordsQueue!.TryDequeue(out var queueItem))
                {
                    processingBuffer.Add(queueItem);
                    continue;
                }

                var newStorageLength = _storageFileLength;

                var keys = new List<string>();
                using (var dataStream = new MemoryStream())
                {
                    foreach (var item in processingBuffer)
                    {
                        var key = item.key;
                        keys.Add(key);
                        (var memory, var dataProperties) = item.Item2;

                        await dataStream.WriteAsync(memory);
                        var record = new IndexRecord(key, new DataReference { StreamStart = newStorageLength, Length = memory.Length }, dataProperties);
                        _recordsCache.AddOrUpdate(key, (_) => record, (_, _) => record);
                        newStorageLength += memory.Length;
                    }

                    dataStream.Seek(0, SeekOrigin.Begin);

                    lock (this)
                    {
                        var fileStream = _storageFileStream;
                        dataStream.CopyTo(fileStream);

                        _storageFileLength = newStorageLength;

                        // TODO common method
                        fileStream.Flush(); // flush stream
                        FlushIndex();
                    }
                }

                foreach (var key in keys)
                {
                    _tempRecordsCache!.Remove(key, out _);
                }

                processingBuffer.Clear();

                if (_token.IsCancellationRequested)
                {
                    _flushDisposed.Set();
                    return;
                }

                await Task.Delay(DefaultFlushIntervalMiliseconds);
            }
        }

        private void FlushIndex()
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

        //private void FlushTimer_Elapsed(object sender, ElapsedEventArgs e)
        //{
        //    FlushFromTemp();
        //}

        //private void FlushFromTemp()
        //{
        //    lock (this)
        //    {
        //        MemoryStream? flushStream = null;
        //        try
        //        {
        //            ConcurrentDictionary<string, IndexRecord> recordsCache;
        //            lock (_flushLock)
        //            {
        //                flushStream = _storageFlushStream!;
        //                _storageFlushStream = new MemoryStream();
        //                _flushStreamLength = 0;
        //                recordsCache = _tempRecordsCache!;
        //                _tempRecordsCache = new ConcurrentDictionary<string, IndexRecord>();
        //            }

        //            var storageLength = _storageFileLength;

        //            flushStream.Seek(0, SeekOrigin.Begin);
        //            //_storageFileStream.Seek(storageLength, SeekOrigin.Begin); // TODO do we need seek here ?
        //            flushStream.CopyTo(_storageFileStream);

        //            _storageFileLength += flushStream.Length;

        //            var indexRecords = recordsCache!.Values.ToArray().Where(x => x != null).OrderBy(x => x.DataReference.StreamStart).ToList();

        //            foreach (var item in indexRecords)
        //            {
        //                item.DataReference.StreamStart += storageLength;
        //                _recordsCache.TryAdd(item.Key, item);
        //            }

        //            // TODO distinct method
        //            _storageFileStream.Flush();
        //            FlushIndex();
        //        }
        //        finally
        //        {
        //            flushStream?.Dispose();
        //        }
        //    }
        //}

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
                throw new Exception("Bad things");
            }
        }

        // TODO get from temp streams also !
        public Stream Get(string key)
        {
            // TODO predict situation, when data is not in temp memory, but not in main memory yet
            EnsureNotDisposed();

            if (_flushMode == FlushMode.Deferred && _tempRecordsCache!.TryGetValue(key, out var record) && record != null)
            {
                // TODO perform stream modifications
                var memoryStream = new MemoryStream(record.Value.memory.ToArray());
                return memoryStream;
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
            var memStream = new MemoryStream(bytes);
            return memStream;
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
            if (_isDisposed)
                return;

            _isDisposing = true;

            //if (_flushMode == FlushMode.Deferred)
            //{
            //    _source.Cancel();
            //    _flushDisposed.WaitOne();

            //    _tempRecordsCache!.Clear();
            //    _recordsQueue!.Clear();
            //}

            //if (_flushTimer != null)
            //{
            //    try
            //    {
            //        _flushTimer.Stop();
            //        _flushTimer.Elapsed -= FlushTimer_Elapsed;
            //        _flushTimer.Dispose();
            //    }
            //    catch (Exception ex)
            //    {
            //        // TODO what to do if file could not be flushed ?
            //    }
            //}

            if (flushBuffers)
            {
                try
                {
                    // TODO flush buffers in different way, depending on the mode
                    if (_flushMode == FlushMode.AtOnce)
                    {
                        // TODO unite in single method
                        lock (this)
                        {
                            _storageFileStream.Flush();
                            FlushIndex();
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
