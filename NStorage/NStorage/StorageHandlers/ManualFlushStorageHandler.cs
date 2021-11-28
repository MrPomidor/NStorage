using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using NStorage.DataStructure;
using Index = NStorage.DataStructure.Index;

namespace NStorage.StorageHandlers
{
    internal class ManualFlushStorageHandler : DeferredFlushStorageHandlerBase
    {
        private readonly object _flushLock = new();

        public ManualFlushStorageHandler(
            FileStream storageFileStream,
            IIndexStorageHandler indexStorageHandler,
            Index index,
            object storageFilesAccessLock)
            : base(storageFileStream, indexStorageHandler, index, storageFilesAccessLock)
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void EnsureAndBookKey(string key)
        {
            EnsureNotDisposed();

            base.EnsureAndBookKey(key);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Add(string key, (byte[] memory, DataProperties properties) dataTuple)
        {
            EnsureNotDisposed();

            base.Add(key, dataTuple);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool Contains(string key)
        {
            EnsureNotDisposed();

            return base.Contains(key);
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
            EnsureNotDisposed();

            lock (_flushLock)
            {
                EnsureNotDisposed();

                var processingBuffer = new List<(string key, (byte[] memory, DataProperties properties))>();
                while (_recordsQueue.TryDequeue(out var queueItem))
                {
                    processingBuffer.Add(queueItem);
                }

                FlushInternal(processingBuffer);
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Dispose()
        {
            if (_isDisposed || _isDisposing)
                return;

            lock (_flushLock)
            {
                FlushInternal(_recordsQueue.ToList());
            }

            DisposeInternal();

            _isDisposed = true;
            _isDisposing = false;
        }
    }
}
