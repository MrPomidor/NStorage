using System.IO;
using BenchmarkDotNet.Attributes;
using NStorage.DataStructure;
using NStorage.StorageHandlers;

namespace NStorage.Tests.Benchmarks.Benchmarks
{
    public class JsonIndexStorageHandlerBenchmark : BenchmarkBase
    {
        [Params(500)]
        public int CyclesCount;

        private string _tempStorageFolderName;
        private FileStream _indexFileStream;
        private JsonIndexStorageHandler _indexStorageHandler;
        private IndexDataStructure _index;

        [GlobalSetup]
        public void GlobalSetup()
        {
            _tempStorageFolderName = GetTempTestFolderPath("Benchmarks/JsonIndexStorage");
            var indexFilePath = Path.Combine(_tempStorageFolderName, "index.bat");
            File.WriteAllText(indexFilePath, string.Empty);
            // file open should copy options from Binary storage
            _indexFileStream = File.Open(indexFilePath, new FileStreamOptions
            {
                Mode = FileMode.Open,
                Access = FileAccess.ReadWrite,
                Share = FileShare.None,
                Options = FileOptions.RandomAccess
            });
            _indexStorageHandler = new JsonIndexStorageHandler(_indexFileStream);
            _index = new IndexDataStructure();
            for (int i = 0; i < 100; i++)
            {
                var key = $"key{i}";
                // data correctness is not mandatory for these benchmarks
                _index.Records[key] = new IndexRecord(new DataReference() { StreamStart = 1000, Length = 100000 }, new DataProperties { IsCompressed = false, IsEncrypted = false });
            }
        }

        [Benchmark]
        public void SerializeDeserialize()
        {
            for (int i = 0; i < CyclesCount; i++)
            {
                _indexStorageHandler.SerializeIndex(_index);
                _index = _indexStorageHandler.DeserializeIndex();
            }
        }

        [Benchmark]
        public void Serialize()
        {
            for (int i = 0; i < CyclesCount; i++)
            {
                _indexStorageHandler.SerializeIndex(_index);
            }
        }

        [Benchmark]
        public void Deserialize()
        {
            for (int i = 0; i < CyclesCount; i++)
            {
                _index = _indexStorageHandler.DeserializeIndex();
            }
        }

        [GlobalCleanup]
        public void GlobalCleanup()
        {
            _index = null;
            _indexFileStream?.Dispose();
            _indexStorageHandler?.Dispose();
            _indexStorageHandler = null;
            CleanupTest(_tempStorageFolderName);
        }
    }
}
