using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using NStorage.DataStructure;

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
            _tempRecordsCache.AddOrUpdate(key, (_) => dataTuple, (_, _) => dataTuple);
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
        public override bool TryGetRecord(string key, out (byte[], DataProperties) outRecord)
        {
            if (_tempRecordsCache.TryGetValue(key, out var record) && record != null)
            {
                outRecord = (record.Value.memory, record.Value.properties);
                return true;
            }

            return base.TryGetRecord(key, out outRecord);
        }

        protected void FlushInternal(List<(string key, (byte[] memory, DataProperties properties))> processingBuffer)
        {
            var newStorageLength = StorageFileLength;

            var keys = new List<string>();
            var fileStream = StorageFileStream;

            foreach (var item in processingBuffer)
            {
                var streamStart = newStorageLength;

                var key = item.key;
                keys.Add(key);
                (var memory, var dataProperties) = item.Item2;

                lock (StorageFilesAccessLock)
                {
                    fileStream.Write(memory);
                    newStorageLength += memory.Length;
                    StorageFileLength = newStorageLength;
                }

                var record = new IndexRecord(new DataReference { StreamStart = streamStart, Length = memory.Length }, dataProperties);
                RecordsCache.AddOrUpdate(key, (_) => record, (_, _) => record);
            }

            lock (StorageFilesAccessLock)
            {
                FlushFiles();
            }

            foreach (var key in keys)
            {
                _tempRecordsCache.Remove(key, out _);
            }
        }

        protected void DisposeInternal()
        {
            _tempRecordsCache.Clear();
            _recordsQueue.Clear();
        }
    }
}
