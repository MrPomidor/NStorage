using System;
using NStorage.DataStructure;

namespace NStorage.StorageHandlers
{
    // TODO documentation
    internal interface IStorageHandler : IDisposable
    {
        void Init();
        void EnsureAndBookKey(string key);
        void Add(string key, (Memory<byte> memory, DataProperties properties) dataTuple); // TODO why we use memory here ? (remove if incompatible with NET Framework)
        bool TryGetRecord(string key, out (byte[] recordBytes, DataProperties recordProperties) record);
        bool Contains(string key);
        void Flush();
    }
}
