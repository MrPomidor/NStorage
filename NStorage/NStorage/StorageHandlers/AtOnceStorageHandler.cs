using Newtonsoft.Json;
using NStorage.DataStructure;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Index = NStorage.DataStructure.Index;

namespace NStorage.StorageHandlers
{
    // TODO description
    // TODO internal
    public class AtOnceStorageHandler : IStorageHandler
    {
        private readonly object _storageFilesAccessLock;

        private long _storageFileLength;
        private readonly FileStream _storageFileStream;
        private readonly FileStream _indexFileStream;

        private readonly ConcurrentDictionary<string, IndexRecord> _recordsCache;

        public AtOnceStorageHandler(
            FileStream storageFileStream,
            FileStream indexFileStream,
            object storageFilesAccessLock,
            Index index
            )
        {
            _storageFileStream = storageFileStream;
            _indexFileStream = indexFileStream;

            _storageFilesAccessLock = storageFilesAccessLock;

            _recordsCache = new ConcurrentDictionary<string, IndexRecord>(index.Records.ToDictionary(item => item.Key)); // TODO move to method to base class
        }

        public void Init()
        {
            // TODO move to base
            _storageFileLength = _storageFileStream.Length;
            _storageFileStream.Seek(_storageFileLength, SeekOrigin.Begin);

            // TODO nothing to do here ?
            //throw new NotImplementedException();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(string key, (Memory<byte> memory, DataProperties properties) dataTuple)
        {
            long streamLength = dataTuple.memory.Length;

            lock (_storageFilesAccessLock)
            {
                var fileStream = _storageFileStream;
                long startPosition = _storageFileLength;
                fileStream.Write(dataTuple.memory.Span);

                var record = new IndexRecord(key, new DataReference { StreamStart = startPosition, Length = streamLength }, dataTuple.properties);
                _recordsCache.AddOrUpdate(key, (_) => record, (_, _) => record);

                _storageFileLength += streamLength;

                FlushFiles();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EnsureAndBookKey(string key)
        {
            if (!_recordsCache.TryAdd(key, null))
            {
                throw new ArgumentException($"Key {key} already exists in storage"); // TODO better exception ?
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Contains(string key)
        {
            return _recordsCache.TryGetValue(key, out var recordData) && recordData != null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetRecord(string key, out (byte[], DataProperties) outRecord)
        {
            outRecord = default;
            if (!_recordsCache.TryGetValue(key, out var recordData) || recordData == null)
                return false;

            // TODO move to base class

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

            lock (_storageFilesAccessLock)
            {
                _storageFileStream.Flush();
                FlushIndexFile();
            }
            _isDisposed = true;
        }

        // TODO base methods
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
