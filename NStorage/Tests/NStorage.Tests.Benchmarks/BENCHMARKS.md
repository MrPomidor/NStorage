# Benchmark results

## BenchmarkDotNet configuration
BenchmarkDotNet=v0.13.1, OS=Windows 10.0.19043.1348 (21H1/May2021Update)
Intel Pentium CPU G4620 3.70GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK=6.0.100
  [Host]     : .NET 6.0.0 (6.0.21.52210), X64 RyuJIT
  Job-ECRUOZ : .NET 6.0.0 (6.0.21.52210), X64 RyuJIT

Runtime=.NET 6.0  IterationCount=20  WarmupCount=2 


## Write
|          Method |    DataSet | FilesCount | IndexFlushMode | IsCompressed | IsEncrypted |       Mean |      Error |     StdDev |
|---------------- |----------- |----------- |--------------- |------------- |------------ |-----------:|-----------:|-----------:|
|   ParallelWrite | SmallFiles |       1000 |         AtOnce |        False |       False | 781.929 ms |  8.5750 ms |  9.5311 ms |
| SequentialWrite | SmallFiles |       1000 |         AtOnce |        False |       False | 754.588 ms |  6.3455 ms |  7.0530 ms |
|   ParallelWrite | SmallFiles |       1000 |         AtOnce |        False |        True | 791.623 ms |  6.0675 ms |  6.7441 ms |
| SequentialWrite | SmallFiles |       1000 |         AtOnce |        False |        True | 750.688 ms |  6.4761 ms |  6.9294 ms |
|   ParallelWrite | SmallFiles |       1000 |         AtOnce |         True |       False | 774.718 ms |  3.5596 ms |  3.8087 ms |
| SequentialWrite | SmallFiles |       1000 |         AtOnce |         True |       False | 754.539 ms |  5.6720 ms |  5.8247 ms |
|   ParallelWrite | SmallFiles |       1000 |         AtOnce |         True |        True | 807.148 ms |  6.4267 ms |  6.8765 ms |
| SequentialWrite | SmallFiles |       1000 |         AtOnce |         True |        True | 798.994 ms | 10.6358 ms | 12.2483 ms |
|   ParallelWrite | SmallFiles |       1000 |       Deferred |        False |       False |  68.695 ms |  7.7143 ms |  8.8837 ms |
| SequentialWrite | SmallFiles |       1000 |       Deferred |        False |       False |  64.464 ms |  5.3505 ms |  6.1616 ms |
|   ParallelWrite | SmallFiles |       1000 |       Deferred |        False |        True |  62.960 ms |  6.0100 ms |  6.6801 ms |
| SequentialWrite | SmallFiles |       1000 |       Deferred |        False |        True |  62.460 ms |  6.0193 ms |  6.6905 ms |
|   ParallelWrite | SmallFiles |       1000 |       Deferred |         True |       False |  72.148 ms |  4.3854 ms |  5.0502 ms |
| SequentialWrite | SmallFiles |       1000 |       Deferred |         True |       False |  62.781 ms |  5.5084 ms |  6.1226 ms |
|   ParallelWrite | SmallFiles |       1000 |       Deferred |         True |        True |  67.437 ms |  6.9912 ms |  8.0511 ms |
| SequentialWrite | SmallFiles |       1000 |       Deferred |         True |        True |  66.836 ms |  7.4756 ms |  8.6089 ms |
|   ParallelWrite | SmallFiles |       1000 |         Manual |        False |       False |   4.393 ms |  0.5656 ms |  0.6051 ms |
| SequentialWrite | SmallFiles |       1000 |         Manual |        False |       False |   4.689 ms |  0.6146 ms |  0.6832 ms |
|   ParallelWrite | SmallFiles |       1000 |         Manual |        False |        True |   6.320 ms |  0.5982 ms |  0.6888 ms |
| SequentialWrite | SmallFiles |       1000 |         Manual |        False |        True |   8.172 ms |  0.7915 ms |  0.9115 ms |
|   ParallelWrite | SmallFiles |       1000 |         Manual |         True |       False |  19.300 ms |  1.2977 ms |  1.4424 ms |
| SequentialWrite | SmallFiles |       1000 |         Manual |         True |       False |  18.858 ms |  1.5484 ms |  1.7210 ms |
|   ParallelWrite | SmallFiles |       1000 |         Manual |         True |        True |  28.685 ms |  2.4514 ms |  2.7247 ms |
| SequentialWrite | SmallFiles |       1000 |         Manual |         True |        True |  22.869 ms |  1.2379 ms |  1.3246 ms |

## Read
|         Method |    DataSet | FilesCount | IndexFlushMode | IsCompressed | IsEncrypted |      Mean |     Error |    StdDev |
|--------------- |----------- |----------- |--------------- |------------- |------------ |----------:|----------:|----------:|
|   ParallelRead | SmallFiles |       1000 |         AtOnce |        False |       False |  7.700 ms | 0.0420 ms | 0.0483 ms |
| SequentialRead | SmallFiles |       1000 |         AtOnce |        False |       False |  6.835 ms | 0.0781 ms | 0.0868 ms |
|   ParallelRead | SmallFiles |       1000 |         AtOnce |        False |        True |  9.277 ms | 0.1944 ms | 0.2239 ms |
| SequentialRead | SmallFiles |       1000 |         AtOnce |        False |        True | 14.758 ms | 0.0747 ms | 0.0799 ms |
|   ParallelRead | SmallFiles |       1000 |         AtOnce |         True |       False | 10.784 ms | 0.2565 ms | 0.2851 ms |
| SequentialRead | SmallFiles |       1000 |         AtOnce |         True |       False | 11.495 ms | 0.0757 ms | 0.0810 ms |
|   ParallelRead | SmallFiles |       1000 |         AtOnce |         True |        True | 11.895 ms | 0.6125 ms | 0.7054 ms |
| SequentialRead | SmallFiles |       1000 |         AtOnce |         True |        True | 19.957 ms | 0.1571 ms | 0.1746 ms |
|   ParallelRead | SmallFiles |       1000 |       Deferred |        False |       False | 73.147 ms | 4.6454 ms | 5.3496 ms |
| SequentialRead | SmallFiles |       1000 |       Deferred |        False |       False | 68.795 ms | 4.7613 ms | 5.4832 ms |
|   ParallelRead | SmallFiles |       1000 |       Deferred |        False |        True | 71.190 ms | 2.7022 ms | 3.0035 ms |
| SequentialRead | SmallFiles |       1000 |       Deferred |        False |        True | 63.846 ms | 1.8914 ms | 1.9423 ms |
|   ParallelRead | SmallFiles |       1000 |       Deferred |         True |       False | 70.902 ms | 4.3689 ms | 5.0312 ms |
| SequentialRead | SmallFiles |       1000 |       Deferred |         True |       False | 67.447 ms | 4.8832 ms | 5.6235 ms |
|   ParallelRead | SmallFiles |       1000 |       Deferred |         True |        True | 68.154 ms | 3.6246 ms | 4.1742 ms |
| SequentialRead | SmallFiles |       1000 |       Deferred |         True |        True | 67.908 ms | 5.2769 ms | 6.0769 ms |
|   ParallelRead | SmallFiles |       1000 |         Manual |        False |       False |  7.709 ms | 0.0650 ms | 0.0667 ms |
| SequentialRead | SmallFiles |       1000 |         Manual |        False |       False |  6.788 ms | 0.0381 ms | 0.0391 ms |
|   ParallelRead | SmallFiles |       1000 |         Manual |        False |        True |  9.287 ms | 0.2998 ms | 0.3332 ms |
| SequentialRead | SmallFiles |       1000 |         Manual |        False |        True | 15.055 ms | 0.0758 ms | 0.0811 ms |
|   ParallelRead | SmallFiles |       1000 |         Manual |         True |       False | 10.965 ms | 0.4068 ms | 0.4685 ms |
| SequentialRead | SmallFiles |       1000 |         Manual |         True |       False | 11.637 ms | 0.0966 ms | 0.1074 ms |
|   ParallelRead | SmallFiles |       1000 |         Manual |         True |        True | 12.347 ms | 0.7997 ms | 0.9210 ms |
| SequentialRead | SmallFiles |       1000 |         Manual |         True |        True | 19.929 ms | 0.0918 ms | 0.0943 ms |

## JsonIndexStorageHandler
|               Method | CyclesCount |         Mean |       Error |      StdDev |
|--------------------- |------------ |-------------:|------------:|------------:|
| SerializeDeserialize |         500 | 101,449.3 μs | 3,995.45 μs | 4,440.93 μs |
|            Serialize |         500 |  46,622.3 μs |   872.56 μs |   969.85 μs |
|          Deserialize |         500 |     809.0 μs |    10.56 μs |    10.37 μs |