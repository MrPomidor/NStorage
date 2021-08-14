using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NStorage.Tests.Benchmarks.Benchmarks
{
    [SimpleJob(RuntimeMoniker.Net60, targetCount: 20)]
    public class Read
    {
        [Params(5000)]
        public int FilesCount;

        [Params(FlushMode.AtOnce, FlushMode.Deferred)]
        public FlushMode IndexFlushMode;

        [Params(true, false)]
        public bool IsCompressed;

        private string _tempStorageFolderName;
        private string[] _fileNames;

        [GlobalSetup]
        public void GlobalSetup()
        {
            _tempStorageFolderName = GetTempTestFolderPath("Benchmarks/Write");
            var streamInfo = StreamInfo.Empty;
            streamInfo.IsCompressed = IsCompressed;

            using (var storage = new BinaryStorage(new StorageConfiguration() { WorkingFolder = _tempStorageFolderName }))
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
            using (var storage = new BinaryStorage(new StorageConfiguration() { WorkingFolder = _tempStorageFolderName, FlushMode = IndexFlushMode }))
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
            using (var storage = new BinaryStorage(new StorageConfiguration() { WorkingFolder = _tempStorageFolderName, FlushMode = IndexFlushMode }))
            {
                foreach (var fileName in _fileNames)
                {
                    using var resultStream = storage.Get(fileName);
                }
            }
        }
    }
}
