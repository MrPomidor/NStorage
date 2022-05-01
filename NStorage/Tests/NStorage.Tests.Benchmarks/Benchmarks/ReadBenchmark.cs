using System.IO;
using System.Linq;
using System.Security.Cryptography;
using BenchmarkDotNet.Attributes;
using NStorage.Tests.Common;

namespace NStorage.Tests.Benchmarks.Benchmarks
{
    public class ReadBenchmark : BenchmarkBase
    {
        [Params(TestDataSet.SmallFiles)]
        public TestDataSet DataSet;

        [Params(1000)]
        public int FilesCount;

        //[Params(FlushMode.AtOnce, FlushMode.Deferred, FlushMode.Manual)]
        [Params(FlushMode.AtOnce)]
        public FlushMode IndexFlushMode;

        [Params(true, false)]
        public bool IsCompressed;

        [Params(true, false)]
        public bool IsEncrypted;

        private string _tempStorageFolderName;
        private string[] _fileNames;

        private byte[] _aesKey;

        [GlobalSetup]
        public void GlobalSetup()
        {
            _tempStorageFolderName = GetTempTestFolderPath("Benchmarks/Read");
            using (var aes = Aes.Create())
            {
                _aesKey = aes.Key;
            }
            var streamInfo = GetStreamInfo();

            using (var storage = new BinaryStorage(new StorageConfiguration(_tempStorageFolderName).EnableEncryption(_aesKey)))
            {
                var files = Directory.EnumerateFiles(TestConsts.GetDataSetFolder(DataSet), "*", SearchOption.AllDirectories).Take(FilesCount).ToArray();
                _fileNames = new string[files.Length];
                for (int i = 0; i < files.Length; i++)
                {
                    var fileName = files[i];
                    using (var fileStream = new FileStream(fileName, FileMode.Open))
                    {
                        storage.Add(fileName, fileStream, streamInfo);
                    }
                    _fileNames[i] = fileName;
                }
            }
        }

        [GlobalCleanup]
        public void GlobalCleanup()
        {
            CleanupTest(_tempStorageFolderName);
        }

        [Benchmark]
        public void ParallelRead()
        {
            using (var storage = new BinaryStorage(GetStorageConfiguration()))
            {
                _fileNames
                    .AsParallel().WithDegreeOfParallelism(4).ForAll(fileName =>
                    {
                        using (var resultStream = storage.Get(fileName)) { }
                    });

            }
        }

        [Benchmark]
        public void SequentialRead()
        {
            using (var storage = new BinaryStorage(GetStorageConfiguration()))
            {
                foreach (var fileName in _fileNames)
                {
                    using (var resultStream = storage.Get(fileName)) { }
                }
            }
        }

        private StreamInfo GetStreamInfo()
        {
            if (!IsCompressed && !IsEncrypted)
                return StreamInfo.Empty;
            else if (IsCompressed && IsEncrypted)
                return StreamInfo.CompressedAndEncrypted;
            else if (IsCompressed)
                return StreamInfo.Compressed;
            else // IsEncrypted
                return StreamInfo.Encrypted;
        }

        private StorageConfiguration GetStorageConfiguration()
        {
            var storageConfiguration = new StorageConfiguration(_tempStorageFolderName)
                .EnableEncryption(_aesKey);
            switch (IndexFlushMode)
            {
                case FlushMode.Deferred:
                    storageConfiguration = storageConfiguration.SetFlushModeDeferred(flushIntervalMilliseconds: 50);
                    break;
                case FlushMode.Manual:
                    storageConfiguration = storageConfiguration.SetFlushModeManual();
                    break;
            }
            return storageConfiguration;
        }
    }
}
