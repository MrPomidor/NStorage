using System.IO;
using System.Linq;
using Xunit;

namespace NStorage.Tests.Integration
{
    /// <summary>
    /// Several threads are reading then writing
    /// All data should be saved correctly without losses
    /// </summary>
    [Collection("Sequential")]
    public class SequentialReadWrite : IntegrationTestBase
    {
        public SequentialReadWrite() : base("Integration/SequentialReadWrite") { }

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
        public void Test(int dataSetIndex)
        {
            var configuration = GetStorageConfiguration(_storageFolder, dataSetIndex, _aesKey);
            var recordCount = RecordsToTake[dataSetIndex];
            var streamInfo = StreamInfos[dataSetIndex];

            // act
            Run(storageFolder: _storageFolder, dataFolder: _dataFolder, take: recordCount, storageConfiguration: configuration, streamInfo: streamInfo);
        }

        private void Run(string storageFolder, string dataFolder, int? take = 10, StorageConfiguration storageConfiguration = null, StreamInfo streamInfo = null)
        {
            streamInfo = streamInfo ?? StreamInfo.Empty;

            storageConfiguration = storageConfiguration ?? new StorageConfiguration(storageFolder);

            var files = Directory.EnumerateFiles(dataFolder, "*", SearchOption.AllDirectories);
            files = take.HasValue ? files.Take(take.Value) : files;

            // Create storage and add data
            using (var storage = new BinaryStorage(storageConfiguration))
            {
                files
                    .AsParallel().WithDegreeOfParallelism(4).ForAll(s =>
                    {
                        AddFile(storage, s, streamInfo);
                    });

            }

            // Open storage and read data
            using (var storage = new BinaryStorage(storageConfiguration))
            {
                files
                    .AsParallel().WithDegreeOfParallelism(4).ForAll(fileName =>
                    {
                        CheckFileHash(storage, fileName);
                    });
            }
        }
    }
}
