# Classes and interfaces

## Overview
NStorage uses two files for its work: index file and storage file. Index file contains references to the storage file (start index, end index of the record) and records metadata (was file compressed, was file encrypted etc). Storage file contains row record bytes one by one. Storage file is useless without index file. Index and storage files are created when you create `BinaryStorage` instance first time, they are created in the folder you passed in `StorageConfiguration`. 

```
|----------------|         |----------------| Init()                 
|                |         |                | EnsureAndBookKey(key)
| IBinaryStorage |  <----  | IStorageHandler| Add(key, dataTuple)
|                |         |                | TryGetRecord(key, dataTuple)
|----------------|         |----------------| Contains(key)
         |                                    Flush()
         |
         |                 |---------------------|
         |                 |                     | DeserializeIndex()
         |--------------   | IIndexStorageHandler| SerializeIndex(Index)
                           |                     |
                           |---------------------|
```

## `IBinaryStorage`
`IBinaryStorage` interface implemented by `BinaryStorage` class is library entrypoint and key class in NStorage. This interface represents the storage and contains main methods to work with file storage.

Behind the scene, `BinaryStorage` handles input streams pre-processing (compression, encryption) and delegates handling of main functions to choosen `IStorageHandler` implementation, which is created for concrete `StorageConfiguration`.

## `IStorageHandler`
`IStorageHandler` class handles most of the logic interaction with storage file. There exist three `IStorageHandler` implementations, which corresponds to `BinaryStorage` flush modes:
- `IntervalFlushStorageHandler` (deferred flush)
- `AtOnceFlushStorageHandler` (at once flush)
- `ManualFlushStorageHandler` (manual flush)

Some common files interaction logic is places in `StorageHandlerBase` class.

## `IIndexStorageHandler`
`IIndexStorageHandler` represents class which interacts with index file. Default implementation is `JsonIndexStorageHandler` which uses JSON index file serialization and de-serialization, but this could be substituted with more fast implementations using Protobuf. 

## Monitoring
NStorage expose some performance metrics and events, which could be concumed either internally in the application via `System.Diagnostics.Tracing.EventListener` implementation or via application insights and Azure or externally by `dotnet counters` cli or other external tools, which can attach to `EventPipes` API. Counters and events namespaces and counter names could be found in `NStorage\Tracing\Consts.cs` file.