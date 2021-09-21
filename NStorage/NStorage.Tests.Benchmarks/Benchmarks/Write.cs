using BenchmarkDotNet.Attributes;
//using BenchmarkDotNet.Diagnostics.Windows.Configs;
using BenchmarkDotNet.Jobs;
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace NStorage.Tests.Benchmarks
{
    // TODO target count, launch count ?
    [SimpleJob(RuntimeMoniker.Net60, targetCount:20)]
    public class Write
    {
        [Params(1000)]
        public int FilesCount;

        [Params(FlushMode.AtOnce, FlushMode.Deferred)]
        public FlushMode IndexFlushMode;

        [Params(true, false)]
        public bool IsCompressed;

        [Params(true, false)]
        public bool IsEncrypted;

        private string _tempStorageFolderName;
        private (Stream stream, string fileName)[] _fileStreams;

        private byte[] _aesKey;

        [GlobalSetup]
        public void GlobalSetup()
        {
            using (var aes = Aes.Create())
            {
                _aesKey = aes.Key;
            }

            _fileStreams = new (Stream, string)[FilesCount];

            var files = Directory.EnumerateFiles(Consts.GetLargeTestDataSetFolder(), "*", SearchOption.AllDirectories).Take(FilesCount).ToArray();
            for (int i =0; i < files.Length; i++)
            {
                var fileName = files[i];
                var memStream = new MemoryStream();

                using (var fileStream = new FileStream(fileName, FileMode.Open))
                {
                    fileStream.CopyTo(memStream);
                    memStream.Seek(0, SeekOrigin.Begin);
                }

                _fileStreams[i] = (memStream, fileName);
            }
        }

        [GlobalCleanup]
        public void GlobalCleanup()
        {
            for (int i = 0; i < _fileStreams.Length; i++)
            {
                var stream = _fileStreams[i];
                stream.stream.Dispose();
                _fileStreams[i] = default;
            }
            _fileStreams = null;
        }

        [IterationSetup]
        public void IterationSetup()
        {
            foreach (var stream in _fileStreams)
            {
                stream.stream.Seek(0, SeekOrigin.Begin);
            }

            _tempStorageFolderName = GetTempTestFolderPath("Benchmarks/Write");
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

        [IterationCleanup]
        public void IterationCleanup()
        {
            CleanupTest(_tempStorageFolderName);
            _tempStorageFolderName = null;
        }

        private void CleanupTest(string tempTestFolder)
        {
            Directory.Delete(tempTestFolder, true);
        }

        private StreamInfo GetStreamInfo()
        {
            var streamInfo = StreamInfo.Empty;
            streamInfo.IsCompressed = IsCompressed;
            streamInfo.IsEncrypted = IsEncrypted;
            return streamInfo;
        }

        [Benchmark]
        public void ParallelWrite()
        {
            var streamInfo = GetStreamInfo();
            using (var storage = new BinaryStorage(GetStorageConfiguration()))
            {
                _fileStreams
                    .AsParallel().WithDegreeOfParallelism(4).ForAll(s =>
                    {
                        storage.Add(s.fileName, s.stream, streamInfo);
                    });

            }
        }

        [Benchmark]
        public void SequentialWrite()
        {
            var streamInfo = GetStreamInfo();
            using (var storage = new BinaryStorage(GetStorageConfiguration()))
            {
                foreach(var s in _fileStreams)
                {
                    storage.Add(s.fileName, s.stream, streamInfo);
                }
            }
        }

        private StorageConfiguration GetStorageConfiguration()
        {
            var storageConfiguration = new StorageConfiguration(_tempStorageFolderName)
                .EnableEncryption(_aesKey);
            if (IndexFlushMode == FlushMode.Deferred)
                storageConfiguration = storageConfiguration.SetFlushModeDeferred();
            return storageConfiguration;
        }
    }
}
