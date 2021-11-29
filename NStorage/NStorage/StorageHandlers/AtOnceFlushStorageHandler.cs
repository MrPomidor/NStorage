using System;
using System.IO;
using System.Runtime.CompilerServices;
using NStorage.DataStructure;

namespace NStorage.StorageHandlers
{
    /// <summary>
    /// Storage handler for atomic write operations. No deffer
    /// </summary>
    internal class AtOnceFlushStorageHandler : StorageHandlerBase
    {
        public AtOnceFlushStorageHandler(
            FileStream storageFileStream,
            IIndexStorageHandler indexStorageHandler,
            object storageFilesAccessLock,
            IndexDataStructure index
            ) : base(storageFileStream, indexStorageHandler, index, storageFilesAccessLock)
        { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Add(string key, (byte[] memory, DataProperties properties) dataTuple)
        {
            EnsureNotDisposed();

            long streamLength = dataTuple.memory.Length;

            lock (StorageFilesAccessLock)
            {
                var fileStream = StorageFileStream;
                long startPosition = StorageFileLength;
                fileStream.Write(dataTuple.memory);

                var record = new IndexRecord(new DataReference { StreamStart = startPosition, Length = streamLength }, dataTuple.properties);
                RecordsCache.AddOrUpdate(key, (_) => record, (_, _) => record);

                StorageFileLength += streamLength;

                FlushFiles();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void EnsureAndBookKey(string key)
        {
            EnsureNotDisposed();

            if (!RecordsCache.TryAdd(key, null))
            {
                throw new ArgumentException($"Key {key} already exists in storage");
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool Contains(string key)
        {
            EnsureNotDisposed();

            return RecordsCache.TryGetValue(key, out var recordData) && recordData != null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool TryGetRecord(string key, out (byte[], DataProperties) outRecord)
        {
            EnsureNotDisposed();

            return base.TryGetRecord(key, out outRecord);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Flush()
        {
            lock (StorageFilesAccessLock)
            {
                FlushFiles();
            }
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

            lock (StorageFilesAccessLock)
            {
                FlushFiles();
            }
            _isDisposed = true;
            _isDisposing = false;
        }
    }
}
