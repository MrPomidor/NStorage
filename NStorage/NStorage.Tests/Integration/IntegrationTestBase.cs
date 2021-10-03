using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace NStorage.Tests.Integration
{
    public abstract class IntegrationTestBase : IDisposable
    {
        #region Test data sets
        protected static readonly int[] RecordsToTake = new[]
        {
            10, //0
            10, //1
            10, //2
            10, //3
            10, //4
            10, //5
            10, //6
            10, //7
            10, //8
            10, //9
            10, //10
            10, //11
        };
        protected static readonly FlushMode[] IndexFlushModes = new[]
        {
            FlushMode.AtOnce,   //0
            FlushMode.Deferred, //1
            FlushMode.Manual,   //2
            FlushMode.AtOnce,   //3
            FlushMode.Deferred, //4
            FlushMode.Manual,   //5
            FlushMode.AtOnce,   //6
            FlushMode.Deferred, //7
            FlushMode.Manual,   //8
            FlushMode.AtOnce,   //9
            FlushMode.Deferred, //10
            FlushMode.Manual,   //11
        };
        protected static readonly StreamInfo[] StreamInfos = new[]
        {
            StreamInfo.Empty, //0
            StreamInfo.Empty, //1
            StreamInfo.Empty, //2
            new StreamInfo() { IsCompressed = true }, //3
            new StreamInfo() { IsCompressed = true }, //4
            new StreamInfo() { IsCompressed = true }, //5
            new StreamInfo() { IsEncrypted = true }, //6
            new StreamInfo() { IsEncrypted = true }, //7
            new StreamInfo() { IsEncrypted = true }, //8
            new StreamInfo() { IsCompressed = true, IsEncrypted = true }, //9
            new StreamInfo() { IsCompressed = true, IsEncrypted = true }, //10
            new StreamInfo() { IsCompressed = true, IsEncrypted = true }, //11
        };
        #endregion

        protected static Func<string, int, byte[], StorageConfiguration> GetStorageConfiguration = (storageFolder, index, aesKey) =>
        {
            var storageConfiguration = new StorageConfiguration(storageFolder)
                .EnableEncryption(aesKey);
            switch (IndexFlushModes[index])
            {
                case FlushMode.Deferred:
                    storageConfiguration = storageConfiguration.SetFlushModeDeferred();
                    break;
                case FlushMode.Manual:
                    storageConfiguration = storageConfiguration.SetFlushModeManual();
                    break;
            }
            return storageConfiguration;
        };

        protected readonly string _tempTestFolder;

        protected readonly string _storageFolder;
        protected readonly string _dataFolder;

        protected readonly byte[] _aesKey;
        public IntegrationTestBase(string integrationTestPath)
        {
            _tempTestFolder = GetTempTestFolderPath(integrationTestPath);
            // Storage folder - folder where NStorage repository files should be created
            // Data folder - folder with files to store in the storage
            (_storageFolder, _dataFolder) = GetMainArgs(_tempTestFolder);
            using (var aes = Aes.Create())
            {
                _aesKey = aes.Key;
            }
        }

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
            var testDataPath = Consts.GetLargeTestDataSetFolder();
            var storagePath = Path.Combine(tempTestFolder, "Storage");
            Directory.CreateDirectory(storagePath);
            return (storagePath, testDataPath);
        }

        protected void CheckFileHash(IBinaryStorage storage, string fileName)
        {
            using (var resultStream = storage.Get(fileName))
            {
                using (var sourceStream = new FileStream(fileName, FileMode.Open, FileAccess.Read))
                {
                    if (sourceStream.Length != resultStream.Length)
                    {
                        throw new Exception(string.Format("Length did not match: Source - '{0}', Result - {1}", sourceStream.Length, resultStream.Length));
                    }

                    byte[] hash1, hash2;
                    using (MD5 md5 = MD5.Create())
                    {
                        hash1 = md5.ComputeHash(sourceStream);

                        md5.Initialize();
                        hash2 = md5.ComputeHash(resultStream);
                    }

                    if (!hash1.SequenceEqual(hash2))
                    {
                        throw new Exception(string.Format("Hashes do not match for file - '{0}'  ", fileName));
                    }
                }
            }
        }

        protected void AddFile(IBinaryStorage storage, string fileName, StreamInfo streamInfo)
        {
            using (var file = new FileStream(fileName, FileMode.Open))
            {
                storage.Add(fileName, file, streamInfo);
            }
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
