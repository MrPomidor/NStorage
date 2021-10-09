using Index = NStorage.DataStructure.Index;

namespace NStorage.StorageHandlers
{
    internal interface IIndexStorageHandler
    {
        Index DeserializeIndex();

        void SerializeIndex(Index index);
    }
}
