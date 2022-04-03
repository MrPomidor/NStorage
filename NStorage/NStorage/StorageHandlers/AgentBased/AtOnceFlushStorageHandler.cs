using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using NStorage.DataStructure;
using NStorage.Extensions;

namespace NStorage.StorageHandlers.AgentBased
{
    internal class AtOnceFlushStorageHandler : StorageHandlerBase
    {
        internal class AgentMessage
        {
            public AgentMessageType Type { get; set; }
            public string Key { get; set; }
            public (byte[] memory, DataProperties properties)? DataToAdd { get; set; }
            public TaskCompletionSource<object> WriteTaskCompletionSource { get; set; }
            public TaskCompletionSource<(byte[], DataProperties)?> ReadTaskCompletionSource { get; set; }
        }

        internal enum AgentMessageType
        {
            Read, Write
        }

        private ActionBlock<AgentMessage> _handleAgent;

        public AtOnceFlushStorageHandler(
            FileStream storageFileStream, 
            IIndexStorageHandler indexStorageHandler, 
            IndexDataStructure index, 
            object storageFilesAccessLock) 
            : base(storageFileStream, indexStorageHandler, index, storageFilesAccessLock)
        {
        }

        public override void Init()
        {
            base.Init();

            _handleAgent = new ActionBlock<AgentMessage>(agentMessage =>
            {
                switch (agentMessage.Type)
                {
                    case AgentMessageType.Read:
                        {
                            try
                            {
                                var record = GetRecordInternal(agentMessage.Key);
                                agentMessage.ReadTaskCompletionSource.SetResult(record);
                            }
                            catch(Exception ex)
                            {
                                agentMessage.ReadTaskCompletionSource.SetException(ex);
                            }
                            break;
                        }
                    case AgentMessageType.Write:
                        {
                            try
                            {
                                AddInternal(agentMessage.Key, agentMessage.DataToAdd.Value);
                                agentMessage.WriteTaskCompletionSource.SetResult(null);
                            }
                            catch (Exception ex)
                            {
                                agentMessage.WriteTaskCompletionSource.SetException(ex);
                            }
                            break;
                        }
                }
            }, new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = 1
            });
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private (byte[], DataProperties)? GetRecordInternal(string key)
        {
            EnsureNotDisposed();

            if (!RecordsCache.TryGetValue(key, out var recordData) || recordData == null)
                return null;

            var fileStream = StorageFileStream;
            var bytes = new byte[recordData.DataReference.Length];
            {
                var fileStreamLength = StorageFileLength;
                fileStream.Seek(recordData.DataReference.StreamStart, SeekOrigin.Begin);
                var bytesRead = fileStream.Read(bytes);
                fileStream.Seek(fileStreamLength, SeekOrigin.Begin);
            }

            return (bytes, recordData.Properties);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddInternal(string key, (byte[] memory, DataProperties properties) dataTuple)
        {
            EnsureNotDisposed();

            long streamLength = dataTuple.memory.Length;

            var fileStream = StorageFileStream;
            long startPosition = StorageFileLength;
            fileStream.Write(dataTuple.memory);

            var record = new IndexRecord(new DataReference { StreamStart = startPosition, Length = streamLength }, dataTuple.properties);
            RecordsCache.AddOrUpdate(key, (k) => record, (k, prev) => record);

            StorageFileLength += streamLength;

            FlushFiles();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void EnsureAndBookKey(string key)
        {
            EnsureNotDisposed();

            if (!RecordsCache.TryAdd(key, null))
            {
                throw new ArgumentException($"Key {key} already exists in storage");
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool Contains(string key)
        {
            EnsureNotDisposed();

            return RecordsCache.TryGetValue(key, out var recordData) && recordData != null;
        }

        public override void Add(string key, (byte[] memory, DataProperties properties) dataTuple)
        {
            EnsureNotDisposed();

            var message = new AgentMessage
            {
                Type = AgentMessageType.Write,
                Key = key,
                DataToAdd = dataTuple,
                WriteTaskCompletionSource = new TaskCompletionSource<object>()
            };

            if (!_handleAgent.Post(message))
                throw new Exception("Cannot add item");

            message.WriteTaskCompletionSource.Task.Wait();
        }

        public override (byte[], DataProperties)? GetRecord(string key)
        {
            EnsureNotDisposed();

            var message = new AgentMessage
            {
                Type = AgentMessageType.Read,
                Key = key,
                ReadTaskCompletionSource = new TaskCompletionSource<(byte[], DataProperties)?>()
            };

            if (!_handleAgent.Post(message))
                throw new Exception("Cannot get item");

            message.ReadTaskCompletionSource.Task.Wait();

            return message.ReadTaskCompletionSource.Task.Result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Flush()
        {
            throw new InvalidOperationException("No need to call Flush on AtOnce flush mode");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EnsureNotDisposed()
        {
            if (_isDisposed || _isDisposing)
                throw new ObjectDisposedException(GetType().FullName);
        }

        private volatile bool _isDisposed = false;
        private volatile bool _isDisposing = false;
        public override void Dispose()
        {
            if (_isDisposed || _isDisposing)
                return;

            _isDisposing = true;

            if (_handleAgent != null)
            {
                _handleAgent.Complete();
                _handleAgent.Completion.Wait(); // TODO timeout ?
            }

            _isDisposed = true;
            _isDisposing = false;
        }
    }
}
