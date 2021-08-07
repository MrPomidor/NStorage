using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NStorage.DataStructure;
using NStorage.Exceptions;
using Index = NStorage.DataStructure.Index;

namespace NStorage
{
    // TODO should be singletone
    public class BinaryStorage : IBinaryStorage
    {
        private const string IndexFile = "index.json";
        private const string StorageFile = "storage.bin";

        private readonly string _indexFilePath;
        private readonly string _storageFilePath;

        private readonly Index _index;

        private readonly FileStream _storageFileStream;
        private readonly FileStream _indexFileStream;

        public BinaryStorage(StorageConfiguration configuration)
        {
            if (string.IsNullOrEmpty(configuration.WorkingFolder))
                throw new ArgumentException(paramName: nameof(configuration), message: "Working folder should be defined");
            if (!Directory.Exists(configuration.WorkingFolder))
                throw new ArgumentException(paramName: nameof(configuration), message: "Working folder should exist");

            var indexFilePath = Path.Combine(configuration.WorkingFolder, IndexFile);
            var storageFilePath = Path.Combine(configuration.WorkingFolder, StorageFile);

            if (!File.Exists(indexFilePath))
                File.WriteAllText(indexFilePath, string.Empty);
            if (!File.Exists(storageFilePath))
                File.WriteAllText(storageFilePath, string.Empty);

            _indexFilePath = indexFilePath;
            _storageFilePath = storageFilePath;

            try
            {
                //_indexFileStream = File.Open(_indexFilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                _indexFileStream = File.Open(_indexFilePath, new FileStreamOptions
                {
                    Mode = FileMode.Open,
                    Access = FileAccess.ReadWrite,
                    Share = FileShare.None,
                    Options = FileOptions.RandomAccess
                });
                //_storageFileStream = File.Open(_storageFilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                _storageFileStream = File.Open(_storageFilePath, new FileStreamOptions
                {
                    Mode = FileMode.Open,
                    Access = FileAccess.ReadWrite,
                    Share = FileShare.None,
                    Options = FileOptions.RandomAccess
                });

                _index = DeserializeIndex(); // for more performace index file should be stored in memory

                CheckIndexNotCorrupted();
                CheckStorageNotCorrupted();
            }
            catch (Exception ex)
            {
                // TODO log it somewhere
                DisposeInternal(flushIndex: false);

                throw;
            }

            // TODO check if storage file length is expected
            // TODO lock storage and index files for current process
        }

        private Index DeserializeIndex()
        {
            using var streamReader = new StreamReader(_indexFileStream, leaveOpen: true);
            var indexAsTest = streamReader.ReadToEnd();
            return JsonConvert.DeserializeObject<Index>(indexAsTest) ?? new Index();
        }

        private void CheckIndexNotCorrupted()
        {
            var index = _index;
            long lastEndPosition = 0;
            for (int i = 0; i < index.Records.Count; i++)
            {
                var record = index.Records[i];
                if (lastEndPosition > 0)
                {
                    if (record.DataReference.StreamStart != lastEndPosition)
                        throw new IndexCorruptedException($"Record {i} with key {record.Key} expect to be started at {lastEndPosition}, but started at {record.DataReference.StreamStart}");
                }
                lastEndPosition = record.DataReference.StreamStart + record.DataReference.Length;
            }
        }

        private void CheckStorageNotCorrupted()
        {
            var expectedStorageLengthBytes = _index.Records.Sum(x => x.DataReference.Length);
            var storageLength = new FileInfo(_storageFilePath).Length;
            if (storageLength != expectedStorageLengthBytes)
                throw new StorageCorruptedException($"Storage length is not as expected in summ of index data records. FileLength {storageLength}, expected {expectedStorageLengthBytes}");
        }

        private void FlushIndex()
        {
            var indexSerialized = JsonConvert.SerializeObject(_index);
            var bytes = Encoding.UTF8.GetBytes(indexSerialized);

            _indexFileStream.Seek(0, SeekOrigin.Begin);
            _indexFileStream.SetLength(0);

            _indexFileStream.Write(bytes);

            _indexFileStream.Flush();
        }

        public void Add(string key, Stream data, StreamInfo parameters)
        {
            lock (this)
            {
                EnsureKeyNotExist(key);

                (long startPosition, long length) = AppendStreamToStorage(data, parameters);

                var index = _index;
                var record = new IndexRecord(key, new DataReference { StreamStart = startPosition, Length = length }, new DataProperties());
                index.Records.Add(record);

                FlushIndex();
            }
        }

        private (long startPosition, long length) AppendStreamToStorage(Stream data, StreamInfo parameters)
        {
            long startPosition = 0;
            long streamLength = 0;

            using (var memStream = new MemoryStream())
            {
                data.CopyTo(memStream);
                memStream.Seek(0, SeekOrigin.Begin);

                streamLength = memStream.Length;

                var fileStream = _storageFileStream;

                startPosition = fileStream.Length;
                fileStream.Seek(fileStream.Length, SeekOrigin.Begin);
                memStream.CopyTo(fileStream);

                fileStream.Flush();
            }

            return (startPosition, length: streamLength);
        }

        private void EnsureKeyNotExist(string key)
        {
            if (Contains(key))
                throw new ArgumentException($"Key {key} already exists in storage");
        }

        public Stream Get(string key)
        {
            lock (this)
            {
                if (!Contains(key))
                    throw new KeyNotFoundException(key);

                var recordData = _index.Records.Single(x => x.Key == key);

                var fileStream = _storageFileStream;

                fileStream.Seek(recordData.DataReference.StreamStart, SeekOrigin.Begin);

                var bytes = new byte[recordData.DataReference.Length];
                var bytesRead = fileStream.Read(bytes);
                var memStream = new MemoryStream(bytes);
                return memStream;
            }
        }

        public bool Contains(string key)
        {
            // TODO check with concurrency
            return _index.Records.Any(x => x.Key == key);
        }

        private void DisposeInternal(bool flushIndex)
        {
            if (flushIndex)
            {
                try
                {
                    FlushIndex();
                    // TODO close all file blockings
                }
                catch (Exception ex)
                {
                    // TODO what to do if file could not be flushed ?
                }
            }

            try
            {
                _indexFileStream?.Dispose();
                _storageFileStream?.Dispose();
            }
            catch (Exception ex)
            {
                // TODO what to do if file could not be flushed ?
            }
        }

        public void Dispose()
        {
            DisposeInternal(flushIndex: true);
        }
    }
}
