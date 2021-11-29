using System;
using NStorage.DataStructure;

namespace NStorage.StorageHandlers
{
    /// <summary>
    /// Handles most of the logic interaction with storage file
    /// </summary>
    internal interface IStorageHandler : IDisposable
    {
        void Init();
        void EnsureAndBookKey(string key);
        void Add(string key, (byte[] memory, DataProperties properties) dataTuple);
        bool TryGetRecord(string key, out (byte[] recordBytes, DataProperties recordProperties) record);
        bool Contains(string key);
        void Flush();
    }
}
