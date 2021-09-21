using NStorage.DataStructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NStorage.StorageHandlers
{
    // TODO documentation
    public interface IStorageHandler : IDisposable
    {
        void Init();
        void EnsureAndBookKey(string key);
        void Add(string key, (Memory<byte> memory, DataProperties properties) dataTuple);
        bool TryGetRecord(string key, out (byte[], DataProperties) record);
        bool Contains(string key);
    }
}
