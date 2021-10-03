using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Newtonsoft.Json;
using NStorage.DataStructure;
using Index = NStorage.DataStructure.Index;

namespace NStorage.StorageHandlers
{
    internal abstract class StorageHandlerBase : IStorageHandler
    {
        protected readonly object StorageFilesAccessLock;
        protected readonly FileStream StorageFileStream;
        protected readonly FileStream IndexFileStream;
        protected readonly ConcurrentDictionary<string, IndexRecord> RecordsCache;

        protected long StorageFileLength; // TODO volatile ?

        protected StorageHandlerBase(
            FileStream storageFileStream,
            FileStream indexFileStream,
            Index index,
            object storageFilesAccessLock)
        {
            StorageFilesAccessLock = storageFilesAccessLock;

            StorageFileStream = storageFileStream;
            IndexFileStream = indexFileStream;

            RecordsCache = new ConcurrentDictionary<string, IndexRecord>(index.Records.ToDictionary(item => item.Key));
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
            StorageFileStream.Flush(); // flush stream
        }

        protected void FlushIndexFile()
        {
            var index = new Index { Records = RecordsCache.Values.ToArray().Where(x => x != null).OrderBy(x => x.DataReference.StreamStart).ToList() };
            var indexSerialized = JsonConvert.SerializeObject(index);
            var bytes = Encoding.UTF8.GetBytes(indexSerialized);

            // TODO find way to rewrite using single operation system method
            IndexFileStream.Seek(0, SeekOrigin.Begin);
            IndexFileStream.SetLength(0);
            IndexFileStream.Write(bytes);
            IndexFileStream.Flush();
        }
    }
}
