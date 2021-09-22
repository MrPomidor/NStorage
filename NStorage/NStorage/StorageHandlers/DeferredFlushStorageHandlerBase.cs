﻿using NStorage.DataStructure;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using Index = NStorage.DataStructure.Index; // TODO rename index

namespace NStorage.StorageHandlers
{
    public abstract class DeferredFlushStorageHandlerBase : StorageHandlerBase
    {
        protected readonly ConcurrentDictionary<string, (Memory<byte> memory, DataProperties properties)?> _tempRecordsCache;
        protected readonly ConcurrentQueue<(string key, (Memory<byte> memory, DataProperties properties))> _recordsQueue;

        protected DeferredFlushStorageHandlerBase(
            FileStream storageFileStream, 
            FileStream indexFileStream, 
            Index index, 
            object storageFilesAccessLock) 
            : base(storageFileStream, indexFileStream, index, storageFilesAccessLock)
        {
            _tempRecordsCache = new ConcurrentDictionary<string, (Memory<byte> memory, DataProperties properties)?>();
            _recordsQueue = new ConcurrentQueue<(string key, (Memory<byte> memory, DataProperties properties))>();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void EnsureAndBookKey(string key)
        {
            if (_recordsCache.TryGetValue(key, out _) || !_tempRecordsCache.TryAdd(key, null))
            {
                throw new ArgumentException($"Key {key} already exists in storage"); // TODO better exception ?
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Add(string key, (Memory<byte> memory, DataProperties properties) dataTuple)
        {
            _tempRecordsCache.AddOrUpdate(key, (_) => dataTuple, (_, _) => dataTuple);
            _recordsQueue.Enqueue((key, dataTuple));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool Contains(string key)
        {
            if (_tempRecordsCache.TryGetValue(key, out var value) && value != null)
                return true;
            return _recordsCache.TryGetValue(key, out var recordData) && recordData != null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool TryGetRecord(string key, out (byte[], DataProperties) outRecord)
        {
            if (_tempRecordsCache.TryGetValue(key, out var record) && record != null)
            {
                outRecord = (record.Value.memory.ToArray(), record.Value.properties);
                return true;
            }

            return base.TryGetRecord(key, out outRecord);
        }

        protected void FlushInternal(List<(string key, (Memory<byte> memory, DataProperties properties))> processingBuffer)
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
