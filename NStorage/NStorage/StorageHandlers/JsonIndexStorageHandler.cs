using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using Newtonsoft.Json;
using Index = NStorage.DataStructure.Index;

namespace NStorage.StorageHandlers
{
    internal class JsonIndexStorageHandler : IIndexStorageHandler
    {
        private readonly FileStream _indexFileStream;
        public JsonIndexStorageHandler(FileStream indexFileStream)
        {
            _indexFileStream = indexFileStream;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Index DeserializeIndex()
        {
            // TODO find a way to read file content in one method
            _indexFileStream.Seek(0, SeekOrigin.Begin);
            using var streamReader = new StreamReader(_indexFileStream, leaveOpen: true);
            var indexAsText = streamReader.ReadToEnd();
            return JsonConvert.DeserializeObject<Index>(indexAsText) ?? new Index();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SerializeIndex(Index index)
        {
            var indexSerialized = JsonConvert.SerializeObject(index);
            var bytes = Encoding.UTF8.GetBytes(indexSerialized);

            // TODO find way to rewrite using single operation system method
            _indexFileStream.Seek(0, SeekOrigin.Begin);
            _indexFileStream.SetLength(0);
            _indexFileStream.Write(bytes);
            _indexFileStream.Flush();
        }
    }
}
