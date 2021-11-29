using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using NStorage.DataStructure;

namespace NStorage.StorageHandlers
{
    internal abstract class StorageHandlerBase : IStorageHandler
    {
        protected readonly object StorageFilesAccessLock;
        protected readonly FileStream StorageFileStream;
        protected readonly IIndexStorageHandler IndexStorageHandler;
        protected readonly ConcurrentDictionary<string, IndexRecord> RecordsCache;

        protected long StorageFileLength;

        protected StorageHandlerBase(
            FileStream storageFileStream,
            IIndexStorageHandler indexStorageHandler,
            IndexDataStructure index,
            object storageFilesAccessLock)
        {
            StorageFilesAccessLock = storageFilesAccessLock;

            StorageFileStream = storageFileStream;
            IndexStorageHandler = indexStorageHandler;

            RecordsCache = new ConcurrentDictionary<string, IndexRecord>(index.Records);
        }

        public virtual void Init()
        {
            StorageFileLength = StorageFileStream.Length;
            StorageFileStream.Seek(StorageFileLength, SeekOrigin.Begin);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual bool TryGetRecord(string key, out (byte[], DataProperties) record)
        {
            record = default;

            if (!RecordsCache.TryGetValue(key, out var recordData) || recordData == null)
                return false;

            var fileStream = StorageFileStream;
            var bytes = new byte[recordData!.DataReference.Length];
            lock (StorageFilesAccessLock)
            {
                var fileStreamLength = StorageFileLength;
                fileStream.Seek(recordData.DataReference.StreamStart, SeekOrigin.Begin);
                var bytesRead = fileStream.Read(bytes);
                fileStream.Seek(fileStreamLength, SeekOrigin.Begin);
            }

            record = (bytes, recordData!.Properties);
            return true;
        }

        public abstract void Add(string key, (byte[] memory, DataProperties properties) dataTuple);
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
            StorageFileStream.Flush(); // flush stream
        }

        protected void FlushIndexFile()
        {
            var index = new IndexDataStructure { Records = RecordsCache.ToArray().Where(x => x.Value != null).ToDictionary(x => x.Key, y => y.Value) };
            IndexStorageHandler.SerializeIndex(index);
        }
    }
}
