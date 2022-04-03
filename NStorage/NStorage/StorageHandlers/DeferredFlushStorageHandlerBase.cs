using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using NStorage.DataStructure;
using NStorage.Extensions;

namespace NStorage.StorageHandlers
{
    internal abstract class DeferredFlushStorageHandlerBase : StorageHandlerBase
    {
        protected readonly ConcurrentDictionary<string, (byte[] memory, DataProperties properties)?> _tempRecordsCache;
        protected readonly ConcurrentQueue<(string key, (byte[] memory, DataProperties properties))> _recordsQueue;

        protected DeferredFlushStorageHandlerBase(
            FileStream storageFileStream,
            IIndexStorageHandler indexStorageHandler,
            IndexDataStructure index,
            object storageFilesAccessLock)
            : base(storageFileStream, indexStorageHandler, index, storageFilesAccessLock)
        {
            _tempRecordsCache = new ConcurrentDictionary<string, (byte[] memory, DataProperties properties)?>();
            _recordsQueue = new ConcurrentQueue<(string key, (byte[] memory, DataProperties properties))>();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void EnsureAndBookKey(string key)
        {
            if (RecordsCache.TryGetValue(key, out _) || !_tempRecordsCache.TryAdd(key, null))
            {
                throw new ArgumentException($"Key {key} already exists in storage");
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Add(string key, (byte[] memory, DataProperties properties) dataTuple)
        {
            _tempRecordsCache.AddOrUpdate(key, (k) => dataTuple, (k, prev) => dataTuple);
            _recordsQueue.Enqueue((key, dataTuple));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool Contains(string key)
        {
            if (_tempRecordsCache.TryGetValue(key, out var value) && value != null)
                return true;
            return RecordsCache.TryGetValue(key, out var recordData) && recordData != null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override (byte[], DataProperties)? GetRecord(string key)
        {
            if (_tempRecordsCache.TryGetValue(key, out var record) && record != null)
            {
                return (record.Value.memory, record.Value.properties);
            }

            return base.GetRecord(key);
        }

        protected void FlushInternal(List<(string key, (byte[] memory, DataProperties properties))> processingBuffer)
        {
            var keys = new List<string>();
            var fileStream = StorageFileStream;

            foreach (var item in processingBuffer)
            {
                var key = item.key;
                keys.Add(key);
                (var memory, var dataProperties) = item.Item2;

                long streamStart;
                lock (StorageFilesAccessLock)
                {
                    var newStorageLength = StorageFileLength;
                    streamStart = newStorageLength;

                    fileStream.Write(memory);
                    newStorageLength += memory.Length;
                    StorageFileLength = newStorageLength;
                }

                var record = new IndexRecord(new DataReference { StreamStart = streamStart, Length = memory.Length }, dataProperties);
                RecordsCache.AddOrUpdate(key, (k) => record, (k, prev) => record);
            }

            lock (StorageFilesAccessLock)
            {
                FlushFiles();
            }

            foreach (var key in keys)
            {
                _tempRecordsCache.TryRemove(key, out _);
            }
        }

        protected void DisposeInternal()
        {
            _tempRecordsCache.Clear();
#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
            _recordsQueue.Clear();
#endif
        }
    }
}
