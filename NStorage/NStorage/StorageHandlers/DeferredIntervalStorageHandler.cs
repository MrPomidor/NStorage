using Newtonsoft.Json;
using NStorage.DataStructure;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Index = NStorage.DataStructure.Index;

namespace NStorage.StorageHandlers
{
    public class DeferredIntervalStorageHandler : IStorageHandler
    {
        private readonly object _storageFilesAccessLock;

        private long _storageFileLength;
        private readonly FileStream _storageFileStream;
        private readonly FileStream _indexFileStream;

        private readonly int _flushIntervalMilliseconds;

        private readonly ConcurrentDictionary<string, IndexRecord> _recordsCache;

        private readonly ConcurrentDictionary<string, (Memory<byte> memory, DataProperties properties)?>? _tempRecordsCache;
        private readonly ConcurrentQueue<(string key, (Memory<byte> memory, DataProperties properties))>? _recordsQueue;

        private CancellationTokenSource _source = new CancellationTokenSource();
        private CancellationToken _token;

        private ManualResetEvent _flushDisposed = new ManualResetEvent(false);

        public DeferredIntervalStorageHandler(
             FileStream storageFileStream,
             FileStream indexFileStream,
             object storageFilesAccessLock,
             Index index,
             int flushIntervalMilliseconds)
        {
            _storageFileStream = storageFileStream;
            _indexFileStream = indexFileStream;

            _storageFilesAccessLock = storageFilesAccessLock;

            _recordsCache = new ConcurrentDictionary<string, IndexRecord>(index.Records.ToDictionary(item => item.Key)); // TODO move to method to base class

            _tempRecordsCache = new ConcurrentDictionary<string, (Memory<byte> memory, DataProperties properties)?>();
            _recordsQueue = new ConcurrentQueue<(string key, (Memory<byte> memory, DataProperties properties))>();

            _flushIntervalMilliseconds = flushIntervalMilliseconds;
        }

        public void Init()
        {
            // TODO move to base
            _storageFileLength = _storageFileStream.Length;
            _storageFileStream.Seek(_storageFileLength, SeekOrigin.Begin);

            _token = _source.Token;
            Task.Run(OnTick, _token); // TODO on tick
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

                lock (_storageFilesAccessLock)
                {
                    fileStream.Write(memory.Span);
                    newStorageLength += memory.Length;
                    _storageFileLength = newStorageLength;
                }

                var record = new IndexRecord(key, new DataReference { StreamStart = streamStart, Length = memory.Length }, dataProperties);
                _recordsCache.AddOrUpdate(key, (_) => record, (_, _) => record);
            }

            lock (_storageFilesAccessLock)
            {
                FlushFiles();
            }

            foreach (var key in keys)
            {
                _tempRecordsCache!.Remove(key, out _);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EnsureAndBookKey(string key)
        {
            if (_recordsCache.TryGetValue(key, out _) || !_tempRecordsCache!.TryAdd(key, null))
            {
                throw new ArgumentException($"Key {key} already exists in storage"); // TODO better exception ?
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(string key, (Memory<byte> memory, DataProperties properties) dataTuple)
        {
            _tempRecordsCache!.AddOrUpdate(key, (_) => dataTuple, (_, _) => dataTuple);
            _recordsQueue!.Enqueue((key, dataTuple));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Contains(string key)
        {
            if (_tempRecordsCache!.TryGetValue(key, out var value) && value != null)
                return true;
            return _recordsCache.TryGetValue(key, out var recordData) && recordData != null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetRecord(string key, out (byte[], DataProperties) outRecord)
        {
            outRecord = default;

            if (_tempRecordsCache!.TryGetValue(key, out var record) && record != null)
            {
                outRecord = (record.Value.memory.ToArray(), record.Value.properties);
                return true;
            }

            // TODO move to base class

            if (!_recordsCache.TryGetValue(key, out var recordData) || recordData == null)
                return false;

            var fileStream = _storageFileStream;
            var bytes = new byte[recordData!.DataReference.Length];
            lock (_storageFilesAccessLock)
            {
                var fileStreamLength = _storageFileLength;
                fileStream.Seek(recordData.DataReference.StreamStart, SeekOrigin.Begin);
                var bytesRead = fileStream.Read(bytes);
                fileStream.Seek(fileStreamLength, SeekOrigin.Begin);
            }

            outRecord = (bytes, recordData!.Properties);
            return true;
        }

        // TODO disposed check

        private bool _isDisposed = false;
        public void Dispose()
        {
            if (_isDisposed)
                return;

            _source.Cancel();
            _flushDisposed.WaitOne();
            _flushDisposed?.Dispose();

            _tempRecordsCache!.Clear();
            _recordsQueue!.Clear();

            _isDisposed = true;
        }

        // TODO base class methods
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
    }
}
