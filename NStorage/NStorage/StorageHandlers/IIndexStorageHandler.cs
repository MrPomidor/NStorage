using System;
using NStorage.DataStructure;

namespace NStorage.StorageHandlers
{
    internal interface IIndexStorageHandler : IDisposable
    {
        IndexDataStructure DeserializeIndex();

        void SerializeIndex(IndexDataStructure index);
    }
}
