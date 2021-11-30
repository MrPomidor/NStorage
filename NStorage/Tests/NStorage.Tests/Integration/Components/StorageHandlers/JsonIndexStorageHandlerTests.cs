using System;
using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using NStorage.DataStructure;
using NStorage.StorageHandlers;
using Xunit;

namespace NStorage.Tests.Integration.Components.StorageHandlers
{
    /// <summary>
    /// Tests Json based IIndexStorageHandler implementation
    /// </summary>
    [Collection("Sequential")]
    public class JsonIndexStorageHandlerTests : IDisposable
    {
        protected readonly string _tempTestFolder;
        private readonly FileStream _indexFileStream;
        public JsonIndexStorageHandlerTests()
        {
            _tempTestFolder = GetTempTestFolderPath("Integration/JsonIndexHandlerTests");
            _indexFileStream = File.Create(Path.Combine(_tempTestFolder, "index.bat"));
        }

        [Fact]
        public void ShouldSerializeAndDeserializeCorrectly()
        {
            var index = new IndexDataStructure()
            {
                Records = new Dictionary<string, IndexRecord>
                {
                    { "key1", new IndexRecord(new DataReference() { StreamStart = 1, Length = 10 }, new DataProperties { IsCompressed = true, IsEncrypted = false }) },
                    { "key2", new IndexRecord(new DataReference() { StreamStart = 11, Length = 1 }, new DataProperties { IsCompressed = false, IsEncrypted = true }) },
                    { "key3", new IndexRecord(new DataReference() { StreamStart = 12, Length = 20 }, new DataProperties { IsCompressed = false, IsEncrypted = false }) },
                }
            };

            using (var storageHandler = new JsonIndexStorageHandler(_indexFileStream))
            {
                storageHandler.SerializeIndex(index);

                var indexDeserialized = storageHandler.DeserializeIndex();

                indexDeserialized.Should().BeEquivalentTo(index);
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

        public void Dispose()
        {
            _indexFileStream?.Dispose();
            CleanupTest(_tempTestFolder);
        }

        private void CleanupTest(string tempTestFolder)
        {
            Directory.Delete(tempTestFolder, true);
        }
    }
}
