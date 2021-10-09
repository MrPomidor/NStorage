using System.IO;
using System.Linq;
using System.Security.Cryptography;
using BenchmarkDotNet.Attributes;
using NStorage.Tests.Benchmarks.Benchmarks;

namespace NStorage.Tests.Benchmarks
{
    public class WriteBenchmark : BenchmarkBase
    {
        [Params(1000)]
        public int FilesCount;

        [Params(FlushMode.AtOnce, FlushMode.Deferred, FlushMode.Manual)]
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
            for (int i = 0; i < files.Length; i++)
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

        [IterationCleanup]
        public void IterationCleanup()
        {
            CleanupTest(_tempStorageFolderName);
            _tempStorageFolderName = null;
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
                foreach (var (stream, fileName) in _fileStreams)
                {
                    storage.Add(fileName, stream, streamInfo);
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
