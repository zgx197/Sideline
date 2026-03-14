```

BenchmarkDotNet v0.13.11, Windows 10 (10.0.19045.6466/22H2/2022Update)
13th Gen Intel Core i9-13900KF, 1 CPU, 32 logical and 24 physical cores
.NET SDK 9.0.312
  [Host]   : .NET 8.0.25 (8.0.2526.11203), X64 RyuJIT AVX2
  ShortRun : .NET 8.0.25 (8.0.2526.11203), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=3  LaunchCount=1  
WarmupCount=3  

```
| Method             | Mean       | Error       | StdDev     | Median    | Allocated |
|------------------- |-----------:|------------:|-----------:|----------:|----------:|
| SingleCreate       | 10.8083 ns | 236.8464 ns | 12.9824 ns | 3.3724 ns |         - |
| SingleValidate     |  0.2099 ns |   0.0903 ns |  0.0050 ns | 0.2118 ns |         - |
| SingleValidateFast |  0.1944 ns |   0.2666 ns |  0.0146 ns | 0.1910 ns |         - |
| GetVersion         |  0.0029 ns |   0.0931 ns |  0.0051 ns | 0.0000 ns |         - |
| IsAlive            |  0.0073 ns |   0.0434 ns |  0.0024 ns | 0.0079 ns |         - |
