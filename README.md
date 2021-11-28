# NStorage

NStorage is lightweight library for storing data streams in file database and fetching them. NStorage is easy to use, thread safe and fast file storage with minumum dependencies, which complements you home or prefessional toolset with one-line store and forget tool.

## Use cases
There are plenty of possible use cases for lightweight storage or solutions build on top of that. 
- Backup and compress local files to add more free space, or to move them to external drive for long storage
- Buid encrypted storage of sensitive and important files on top of the NStorage
- Build web application with fast file storage to store and fetch data with NStorage

## Minimum setup
```csharp
// Saving compressed file in storage
using var fileStream = new System.IO.File.OpenRead("C:\AppData\SampleFile.dat");
using var storage = new BinaryStorage(new StorageConfiguration("C:\AppData\Storage"));
storage.Add("unique_key_1", fileStream, new StreamInfo() { IsCompressed = true });
```

```csharp
// Fetching saved file into a stream
using var storage = new BinaryStorage(new StorageConfiguration("C:\AppData\Storage"));
var dataStream = storage.Get("unique_key_1");
```

## Configuration
NStorage is configurable to better match different usage scenarious. 

### When to flush
You can configure when to flush data on your hard disk. Either flushing at once, for data safety, but lower performance, or defer flushing for better performance. Or storing data in memory and flushing manually, when you are absolutely sure when you need to flush.

```csharp
// default behavior is flushing buffer at once, so .Add(...) method became atomic
new StorageConfiguration("C:\AppData\Storage");
```

```csharp
// setting deffered flush behavior either with default or with custom interval
// default can be retrieved from StorageConfiguration.DefaultFlushIntervalMiliseconds const
new StorageConfiguration("C:\AppData\Storage")
        .SetFlushModeDeferred();
new StorageConfiguration("C:\AppData\Storage")
        .SetFlushModeDeferred(flushIntervalMilliseconds: 1000);
```

```csharp
// use this mode, when you are absolutely sure
new StorageConfiguration("C:\AppData\Storage")
        .SetFlushModeManual();
```

### Encryption and Compression
NStorage allows to compress data for storage, or encrypt it with Aes symmetric algorithm to be more safe. Compression is available without additional configuration, but for using encryption you should provide you secret key when configuring storage.

```csharp
var aesKey = Aes.Create().Key; // or read you key from configuration
using var fileStream = new System.IO.File.OpenRead("C:\AppData\SampleFile.dat");
using var storage = new BinaryStorage(
    new StorageConfiguration("C:\AppData\Storage").EnableEncryption(aesKey)
);
storage.Add("compressed", fileStream, new StreamInfo() { IsCompressed = true });
storage.Add("encrypted", fileStream, new StreamInfo() { IsEncrypted = true });
// you can also combine encryption with compressing
storage.Add("compressed_encrypted", fileStream, new StreamInfo() { IsEncrypted = true, IsCompressed = true });
```

**IMPORTANT.** You will not be able to fetch encrypted files from storage if you don't provide encryption key on setup, but other data will be still available.

```csharp
// you can still use your storage
using var storage = new BinaryStorage(new StorageConfiguration("C:\AppData\Storage"));
// success! no error
var stream1 = storage.Get("compressed");
// error! encryption should be configured
...  = storage.Get("encrypted");
```
## Contributors guide
To start contributing you can just download project and restore NuGet packages. To be able to run benchmarks and integration tests, you should initialize test data with simple script, located at `NStorage.Tests\init.ps1`. This script will generate test data sets. Then go to `NStorage.Tests.Common\TestsConsts` and fill the `BasePath` variable to point to `NStorage.Tests` folder in your file system.

Benchmarks are run via `NStorage.Tests.Benchmarks`


## Project purpose
NStorage is my first experience with creating public library and my original intentions were to practice benchmarking, performance measurements and low-level optimization techniques with file operations. Now NStorage could serve as usefull utility in your prefessional toolset, or as education tutorial for beginner programmers on how to build library, setup project file structure with tests and benchmarks. I did not pretent to be an expert, so if you have any implementation or use case proposals - feel free to create a pull request.  

## Further plans
- add more use-case implementations
- add targeting multiple framework, core, standart versions
- add `Delete` operation to all flush modes


