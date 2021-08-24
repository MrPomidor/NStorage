using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
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

        private readonly byte[] _aesKey;


        public Success1()
        {
            _tempTestFolder = GetTempTestFolderPath("Integration/Success1");
            (_storageFolder, _dataFolder) = GetMainArgs(_tempTestFolder);
            using (var aes = Aes.Create())
            {
                _aesKey = aes.Key;
            }
        }

        private static readonly int[] RecordsToTake = new[]
        {
            10,
            10,
            10,
            10,
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
            FlushMode.AtOnce,
            FlushMode.Deferred,
            FlushMode.AtOnce,
            FlushMode.Deferred,
        };
        private static readonly StreamInfo[] StreamInfos = new[]
        {
            StreamInfo.Empty, //0
            StreamInfo.Empty, //1
            new StreamInfo() { IsCompressed = true }, //2
            new StreamInfo() { IsCompressed = true }, //3
            new StreamInfo() { IsEncrypted = true }, //4
            new StreamInfo() { IsEncrypted = true }, //5
            new StreamInfo() { IsCompressed = true, IsEncrypted = true }, //6
            new StreamInfo() { IsCompressed = true, IsEncrypted = true }, //7
        };
        private static Func<string, int, byte[], StorageConfiguration> GetStorageConfiguration = (storageFolder, index, aesKey) =>
        {
            var storageConfiguration = new StorageConfiguration(storageFolder)
            .EnableEncryption(aesKey);
            if (IndexFlushModes[index] == FlushMode.Deferred)
                storageConfiguration = storageConfiguration.SetFlushModeDeferred();
            return storageConfiguration;
        };

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        [InlineData(5)]
        [InlineData(6)]
        [InlineData(7)]
        public void Test(int dataSetIndex)
        {
            var configuration = GetStorageConfiguration(_storageFolder, dataSetIndex, _aesKey);
            var recordCount = RecordsToTake[dataSetIndex];
            var streamInfo = StreamInfos[dataSetIndex];
            // TODO move to test case scenarious
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
