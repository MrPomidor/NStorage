using NStorage.DataStructure;
using System;

namespace NStorage.StorageHandlers
{
    internal interface IIndexStorageHandler : IDisposable
    {
        IndexDataStructure DeserializeIndex();

        void SerializeIndex(IndexDataStructure index);
    }
}
