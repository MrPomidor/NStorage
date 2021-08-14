using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace NStorage.Tests.Integration
{
    // TODO rename into "First write all then read all (sequentially)"

    [Collection("Sequential")]
    /// <summary>
    /// Test encapsulate general success scenario, whewre several threads are reading and writing from storage in a paralell
    /// All data should be saved correctly without losses
    /// </summary>
    public class Success1 : IDisposable
    {
        private readonly string _tempTestFolder;

        private readonly string _storageFolder;
        private readonly string _dataFolder;

        public Success1()
        {
            _tempTestFolder = GetTempTestFolderPath("Integration/Success1");
            (_storageFolder, _dataFolder) = GetMainArgs(_tempTestFolder);
        }

        private static readonly int[] RecordsToTake = new[]
        {
            10,
            10,
            10,
            10,
        };
        private static readonly FlushMode[] IndexFlushModes = new[]
        {
            FlushMode.AtOnce,
            FlushMode.Deferred,
            FlushMode.AtOnce,
            FlushMode.Deferred,
        };
        private static readonly StreamInfo[] StreamInfos = new[]
        {
            StreamInfo.Empty,
            StreamInfo.Empty,
            new StreamInfo() { IsCompressed = true },
            new StreamInfo() { IsCompressed = true },
        };
        private static Func<string, int, StorageConfiguration> GetStorageConfiguration = (storageFolder, index) =>
        {
            return new StorageConfiguration
            {
                WorkingFolder = storageFolder,
                FlushMode = IndexFlushModes[index]
            };
        };

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        public void Test(int dataSetIndex)
        {
            var configuration = GetStorageConfiguration(_storageFolder, dataSetIndex);
            var recordCount = RecordsToTake[dataSetIndex];
            var streamInfo = StreamInfos[dataSetIndex];
            // act
            NStorage.App.Program.Run(storageFolder: _storageFolder, dataFolder: _dataFolder, take: recordCount, storageConfiguration: configuration, streamInfo: streamInfo);
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

        private (string storageFilesFolder, string testFilderFolder) GetMainArgs(string tempTestFolder)
        {
            var testDataPath = GetLargeTestDataSetFolder();
            var storagePath = Path.Combine(tempTestFolder, "Storage");
            Directory.CreateDirectory(storagePath);
            return (storagePath, testDataPath);
        }

        // TODO delete
        private string GetLargeTestDataSetFolder()
        {
            var testFilesFolderPath = Consts.TestFilesFolderPath;
            if (!Directory.Exists(testFilesFolderPath) || Directory.GetFiles(testFilesFolderPath).Length == 0)
                throw new Exception($"No test data present ! Please execute \"init.ps1\" to fill test folder with data");

            return testFilesFolderPath;
        }

        public void Dispose()
        {
            CleanupTest(_tempTestFolder);
        }

        private void CleanupTest(string tempTestFolder)
        {
            Directory.Delete(tempTestFolder, true);
        }
    }
}
