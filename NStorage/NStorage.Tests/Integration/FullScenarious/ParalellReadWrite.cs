using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace NStorage.Tests.Integration.FullScenarious
{
    /// <summary>
    /// Several threads are reading and writing from storage in a paralell
    /// All data should be saved correctly without losses
    /// </summary>
    [Collection("Sequential")]
    public class ParalellReadWrite : IntegrationTestBase
    {
        public ParalellReadWrite() : base("Integration/ParalellReadWrite") { }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        [InlineData(5)]
        [InlineData(6)]
        [InlineData(7)]
        [InlineData(8)]
        [InlineData(9)]
        [InlineData(10)]
        [InlineData(11)]
        public async Task Test(int dataSetIndex)
        {
            var configuration = GetStorageConfiguration(_storageFolder, dataSetIndex, _aesKey);
            var recordCount = RecordsToTake[dataSetIndex];
            var streamInfo = StreamInfos[dataSetIndex];

            // act
            await Run(storageFolder: _storageFolder, dataFolder: _dataFolder, take: recordCount, storageConfiguration: configuration, streamInfo);
        }

        private async Task Run(string storageFolder, string dataFolder, int? take = 10, StorageConfiguration storageConfiguration = null, StreamInfo streamInfo = null)
        {
            streamInfo = streamInfo ?? StreamInfo.Empty;

            storageConfiguration = storageConfiguration ?? new StorageConfiguration(storageFolder);

            var files = Directory.EnumerateFiles(dataFolder, "*", SearchOption.AllDirectories);
            files = take.HasValue ? files.Take(take.Value) : files;

            // Create storage and add data
            using (var storage = new BinaryStorage(storageConfiguration))
            {
                var writeTask = Task.Run(() => WriteFiles(storage, files, streamInfo));
                var readTask = Task.Run(() => ReadFiles(storage));

                await Task.WhenAll(writeTask, readTask);
            }
        }

        private bool _haveFiles = true;
        private ConcurrentQueue<string> _filesQueue = new ConcurrentQueue<string>();
        private void WriteFiles(BinaryStorage storage, IEnumerable<string> files, StreamInfo streamInfo)
        {
            files
                .AsParallel().WithDegreeOfParallelism(2).ForAll(s =>
                {
                    AddFile(storage, s, streamInfo);
                });
            _haveFiles = false;
        }

        private void ReadFiles(BinaryStorage storage)
        {
            while (_haveFiles)
            {
                if (!_filesQueue.TryDequeue(out var fileName))
                {
                    continue;
                }

                CheckFileHash(storage, fileName);
            }
        }
    }
}
