using NStorage.DataStructure;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Index = NStorage.DataStructure.Index;

namespace NStorage.StorageHandlers
{
    public class IntervalFlushStorageHandler : DeferredFlushStorageHandlerBase
    {
        private readonly int _flushIntervalMilliseconds;

        private CancellationTokenSource _source = new();
        private CancellationToken _token;

        private ManualResetEvent _flushDisposed = new ManualResetEvent(false);

        public IntervalFlushStorageHandler(
             FileStream storageFileStream,
             FileStream indexFileStream,
             object storageFilesAccessLock,
             Index index,
             int flushIntervalMilliseconds)
            : base(storageFileStream, indexFileStream, index, storageFilesAccessLock)
        {
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

                while (_recordsQueue.TryDequeue(out var queueItem))
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
                    FlushInternal(processingBuffer);

                    processingBuffer.Clear();
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void EnsureAndBookKey(string key)
        {
            EnsureNotDisposed();

            base.EnsureAndBookKey(key);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Add(string key, (Memory<byte> memory, DataProperties properties) dataTuple)
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
            throw new InvalidOperationException($"Flush operation is not supported in {nameof(IntervalFlushStorageHandler)}");
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
            _flushDisposed.Dispose();

            DisposeInternal();

            _isDisposed = true;
            _isDisposing = false;
        }
    }
}
