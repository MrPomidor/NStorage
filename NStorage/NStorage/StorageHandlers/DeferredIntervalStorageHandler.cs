using NStorage.DataStructure;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Index = NStorage.DataStructure.Index;

namespace NStorage.StorageHandlers
{
    public class DeferredIntervalStorageHandler : StorageHandlerBase
    {
        private readonly int _flushIntervalMilliseconds;

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
            : base(storageFileStream, indexFileStream, index, storageFilesAccessLock)
        {
            _tempRecordsCache = new ConcurrentDictionary<string, (Memory<byte> memory, DataProperties properties)?>();
            _recordsQueue = new ConcurrentQueue<(string key, (Memory<byte> memory, DataProperties properties))>();

            _flushIntervalMilliseconds = flushIntervalMilliseconds;
        }

        public override void Init()
        {
            EnsureNotDisposed();

            base.Init();

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
        public override void EnsureAndBookKey(string key)
        {
            EnsureNotDisposed();

            if (_recordsCache.TryGetValue(key, out _) || !_tempRecordsCache!.TryAdd(key, null))
            {
                throw new ArgumentException($"Key {key} already exists in storage"); // TODO better exception ?
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Add(string key, (Memory<byte> memory, DataProperties properties) dataTuple)
        {
            EnsureNotDisposed();

            _tempRecordsCache!.AddOrUpdate(key, (_) => dataTuple, (_, _) => dataTuple);
            _recordsQueue!.Enqueue((key, dataTuple));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool Contains(string key)
        {
            EnsureNotDisposed();

            if (_tempRecordsCache!.TryGetValue(key, out var value) && value != null)
                return true;
            return _recordsCache.TryGetValue(key, out var recordData) && recordData != null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool TryGetRecord(string key, out (byte[], DataProperties) outRecord)
        {
            EnsureNotDisposed();

            if (_tempRecordsCache!.TryGetValue(key, out var record) && record != null)
            {
                outRecord = (record.Value.memory.ToArray(), record.Value.properties);
                return true;
            }

            return base.TryGetRecord(key, out outRecord);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EnsureNotDisposed()
        {
            if (_isDisposed || _isDisposing)
                throw new ObjectDisposedException(GetType().FullName);
        }

        private volatile bool _isDisposed = false;
        private volatile bool _isDisposing = false;
        public override void Dispose()
        {
            if (_isDisposed || _isDisposing)
                return;

            _isDisposing = true;

            _source.Cancel();
            _flushDisposed.WaitOne();
            _flushDisposed?.Dispose();

            _tempRecordsCache!.Clear();
            _recordsQueue!.Clear();

            _isDisposed = true;
            _isDisposing = false;
        }
    }
}
