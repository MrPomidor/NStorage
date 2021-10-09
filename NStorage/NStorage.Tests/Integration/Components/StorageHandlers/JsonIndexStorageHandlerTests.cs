﻿using System;
using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using NStorage.DataStructure;
using NStorage.StorageHandlers;
using Xunit;
using Index = NStorage.DataStructure.Index;

namespace NStorage.Tests.Integration.Components.StorageHandlers
{
    // TODO summary
    [Collection("Sequential")]
    public class JsonIndexStorageHandlerTests : IDisposable
    {
        // TODO integration test base class
        // TODO test that serialization and deserialization completes successfully
        protected readonly string _tempTestFolder;
        private readonly FileStream _indexFileStream;
        public JsonIndexStorageHandlerTests()
        {
            _tempTestFolder = GetTempTestFolderPath("Integration/JsonIndexHandlerTests");
            _indexFileStream = File.Create(Path.Combine(_tempTestFolder, "index.bat"));
        }

        [Fact]
        // TODO move code to base class
        public void ShouldSerializeAndDeserializeCorrectly()
        {
            var index = new Index()
            {
                Records = new List<IndexRecord>()
                {
                    new IndexRecord("key1", new DataReference() { StreamStart = 1, Length = 10 }, new DataProperties { IsCompressed = true, IsEncrypted = false }),
                    new IndexRecord("key2", new DataReference() { StreamStart = 11, Length = 1 }, new DataProperties { IsCompressed = false, IsEncrypted = true }),
                    new IndexRecord("key3", new DataReference() { StreamStart = 12, Length = 20 }, new DataProperties { IsCompressed = false, IsEncrypted = false })
                }
            };

            var storageHandler = new JsonIndexStorageHandler(_indexFileStream);

            storageHandler.SerializeIndex(index);

            var indexDeserialized = storageHandler.DeserializeIndex();

            indexDeserialized.Should().BeEquivalentTo(index);
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
