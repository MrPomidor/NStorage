using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace NStorage.Tests.Benchmarks.Benchmarks
{
    [SimpleJob(RuntimeMoniker.Net60, targetCount: 20)]
    public class Read
    {
        [Params(5000)]
        public int FilesCount;

        [Params(FlushMode.AtOnce, FlushMode.Deferred, FlushMode.Manual)]
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
            _tempStorageFolderName = GetTempTestFolderPath("Benchmarks/Write");
            using (var aes = Aes.Create())
            {
                _aesKey = aes.Key;
            }
            var streamInfo = StreamInfo.Empty;
            streamInfo.IsCompressed = IsCompressed;
            streamInfo.IsEncrypted = IsEncrypted;

            using (var storage = new BinaryStorage(new StorageConfiguration(_tempStorageFolderName).EnableEncryption(_aesKey)))
            {
                var files = Directory.EnumerateFiles(Consts.GetLargeTestDataSetFolder(), "*", SearchOption.AllDirectories).Take(FilesCount).ToArray();
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


        // TODO common class
        private string GetTempTestFolderPath(string testName)
        {
            var guid = Guid.NewGuid().ToString();
            var solutionFolder = Directory.GetCurrentDirectory();
            var path = Path.Combine(solutionFolder, testName, guid);
            Directory.CreateDirectory(path);
            return path;
        }

        private void CleanupTest(string tempTestFolder)
        {
            Directory.Delete(tempTestFolder, true);
        }

        [Benchmark]
        public void ParallelRead()
        {
            using (var storage = new BinaryStorage(GetStorageConfiguration()))
            {
                _fileNames
                    .AsParallel().WithDegreeOfParallelism(4).ForAll(fileName =>
                    {
                        using var resultStream = storage.Get(fileName);
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
                    using var resultStream = storage.Get(fileName);
                }
            }
        }

        private StorageConfiguration GetStorageConfiguration()
        {
            var storageConfiguration = new StorageConfiguration(_tempStorageFolderName)
                .EnableEncryption(_aesKey);
            switch (IndexFlushMode)
            {
                case FlushMode.Deferred:
                    storageConfiguration = storageConfiguration.SetFlushModeDeferred();
                    break;
                case FlushMode.Manual:
                    storageConfiguration = storageConfiguration.SetFlushModeManual();
                    break;
            }
            return storageConfiguration;
        }
    }
}
