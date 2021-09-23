using Newtonsoft.Json;
using NStorage.DataStructure;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Index = NStorage.DataStructure.Index;

namespace NStorage.StorageHandlers
{
    public abstract class StorageHandlerBase : IStorageHandler
    {
        // TODO rename to uppercase
        protected readonly object _storageFilesAccessLock;

        protected long _storageFileLength;
        protected readonly FileStream _storageFileStream;
        protected readonly FileStream _indexFileStream;

        protected readonly ConcurrentDictionary<string, IndexRecord> _recordsCache;

        protected StorageHandlerBase(
            FileStream storageFileStream,
            FileStream indexFileStream,
            Index index,
            object storageFilesAccessLock)
        {
            _storageFilesAccessLock = storageFilesAccessLock;

            _storageFileStream = storageFileStream;
            _indexFileStream = indexFileStream;

            _recordsCache = new ConcurrentDictionary<string, IndexRecord>(index.Records.ToDictionary(item => item.Key));
        }

        public virtual void Init()
        {
            _storageFileLength = _storageFileStream.Length;
            _storageFileStream.Seek(_storageFileLength, SeekOrigin.Begin);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual bool TryGetRecord(string key, out (byte[], DataProperties) record)
        {
            record = default;

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

            record = (bytes, recordData!.Properties);
            return true;
        }

        public abstract void Add(string key, (Memory<byte> memory, DataProperties properties) dataTuple);
        public abstract bool Contains(string key);
        public abstract void EnsureAndBookKey(string key);
        public abstract void Flush();
        public abstract void Dispose();

        protected void FlushFiles()
        {
            FlushStorageFile();
            FlushIndexFile();
        }

        protected void FlushStorageFile()
        {
            _storageFileStream.Flush(); // flush stream
        }

        protected void FlushIndexFile()
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
