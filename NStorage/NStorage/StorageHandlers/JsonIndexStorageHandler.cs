using Jil;
using NStorage.DataStructure;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace NStorage.StorageHandlers
{
    internal class JsonIndexStorageHandler : IIndexStorageHandler
    {
        private FileStream _indexFileStream;
        public JsonIndexStorageHandler(FileStream indexFileStream)
        {
            _indexFileStream = indexFileStream;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IndexDataStructure DeserializeIndex()
        {
            _indexFileStream.Seek(0, SeekOrigin.Begin);
            if (_indexFileStream.Length == 0)
                return new IndexDataStructure();

            using var streamReader = new StreamReader(_indexFileStream, leaveOpen: true);
            var indexAsText = streamReader.ReadToEnd();
            return JSON.Deserialize<IndexDataStructure>(indexAsText);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SerializeIndex(IndexDataStructure index)
        {
            var indexSerialized = JSON.Serialize(index);
            var bytes = Encoding.UTF8.GetBytes(indexSerialized);

            _indexFileStream.Seek(0, SeekOrigin.Begin);
            _indexFileStream.SetLength(0);
            _indexFileStream.Write(bytes);
            _indexFileStream.Flush();
        }

        public void Dispose()
        {
            _indexFileStream = null;
        }
    }
}
