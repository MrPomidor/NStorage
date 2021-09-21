using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace NStorage.Tests.Integration
{
    // TODO description about writing/reading in paralell
    [Collection("Sequential")]
    public class Success2 : IDisposable
    {
        private readonly string _tempTestFolder;

        private readonly string _storageFolder;
        private readonly string _dataFolder;

        private readonly byte[] _aesKey;

        public Success2()
        {
            _tempTestFolder = GetTempTestFolderPath("Integration/Success2");
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

            if (!Directory.Exists(dataFolder) || !Directory.Exists(storageFolder))
            {
                throw new ArgumentException("Usage: NStorage.App.exe InputFolder StorageFolder");
            }

            storageConfiguration = storageConfiguration ?? new StorageConfiguration(storageFolder);

            var files = Directory.EnumerateFiles(dataFolder, "*", SearchOption.AllDirectories);
            files = take.HasValue ? files.Take(take.Value) : files;

            // Create storage and add data
            Console.WriteLine("Creating storage from " + dataFolder);
            using (var storage = new BinaryStorage(storageConfiguration))
            {
                var writeTask = WriteFiles(storage, files, streamInfo);
                var readTask = ReadFiles(storage);


                await Task.WhenAll(writeTask, readTask);


                //files
                //    .AsParallel().WithDegreeOfParallelism(4).ForAll(s =>
                //    {
                //        AddFile(storage, s);
                //    });

            }

            // Open storage and read data
            //Console.WriteLine("Verifying data");
            //using (var storage = new BinaryStorage(storageConfiguration))
            //{
            //    files
            //        .AsParallel().WithDegreeOfParallelism(4).ForAll(s =>
            //        {
            //            using (var resultStream = storage.Get(s))
            //            {
            //                using (var sourceStream = new FileStream(s, FileMode.Open, FileAccess.Read))
            //                {
            //                    if (sourceStream.Length != resultStream.Length)
            //                    {
            //                        throw new Exception(string.Format("Length did not match: Source - '{0}', Result - {1}", sourceStream.Length, resultStream.Length));
            //                    }

            //                    byte[] hash1, hash2;
            //                    using (MD5 md5 = MD5.Create())
            //                    {
            //                        hash1 = md5.ComputeHash(sourceStream);

            //                        md5.Initialize();
            //                        hash2 = md5.ComputeHash(resultStream);
            //                    }

            //                    if (!hash1.SequenceEqual(hash2))
            //                    {
            //                        throw new Exception(string.Format("Hashes do not match for file - '{0}'  ", s));
            //                    }
            //                }
            //            }
            //        });
            //}
        }

        private bool _haveFiles = true;
        private ConcurrentQueue<string> _filesQueue = new ConcurrentQueue<string>();
        private async Task WriteFiles(BinaryStorage storage, IEnumerable<string> files, StreamInfo streamInfo)
        {
            files
                .AsParallel().WithDegreeOfParallelism(2).ForAll(s =>
                {
                    AddFile(storage, s, streamInfo);
                });
            _haveFiles = false;
        }

        private async Task ReadFiles(BinaryStorage storage)
        {
            while (_haveFiles)
            {
                if (!_filesQueue.TryDequeue(out var file))
                {
                    continue;
                }

                using (var resultStream = storage.Get(file))
                {
                    using (var sourceStream = new FileStream(file, FileMode.Open, FileAccess.Read))
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
                            throw new Exception(string.Format("Hashes do not match for file - '{0}'  ", file));
                        }
                    }
                }
            }
        }

        private void AddFile(IBinaryStorage storage, string fileName, StreamInfo streamInfo)
        {
            using (var file = new FileStream(fileName, FileMode.Open))
            {
                storage.Add(fileName, file, streamInfo);
            }
            _filesQueue.Enqueue(fileName);
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
