using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using System;
using System.IO;
using System.Linq;

namespace NStorage.Tests.Benchmarks
{
    // TODO target count, launch count ?
    [SimpleJob(RuntimeMoniker.Net60)]
    public class Write
    {
        //[Params(10)]
        [Params(1000)]
        public int FilesCount;

        private string _tempStorageFolderName;
        private (Stream stream, string fileName)[] _fileStreams;

        [GlobalSetup]
        public void GlobalSetup()
        {
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

        [Benchmark]
        public void ParalellWrite()
        {
            using (var storage = new BinaryStorage(new StorageConfiguration() { WorkingFolder = _tempStorageFolderName }))
            {
                _fileStreams
                    .AsParallel().WithDegreeOfParallelism(4).ForAll(s =>
                    {
                        storage.Add(s.fileName, s.stream, StreamInfo.Empty);
                    });

            }
        }
    }
}
