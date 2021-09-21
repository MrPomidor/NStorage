using NStorage.DataStructure;
using System;

namespace NStorage.StorageHandlers
{
    // TODO documentation
    public interface IStorageHandler : IDisposable
    {
        void Init();
        void EnsureAndBookKey(string key);
        void Add(string key, (Memory<byte> memory, DataProperties properties) dataTuple); // TODO why we use memory here ? (remove if incompatible with NET Framework)
        bool TryGetRecord(string key, out (byte[], DataProperties) record);
        bool Contains(string key);
    }
}
