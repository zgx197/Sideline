```

BenchmarkDotNet v0.13.11, Windows 10 (10.0.19045.6466/22H2/2022Update)
13th Gen Intel Core i9-13900KF, 1 CPU, 32 logical and 24 physical cores
.NET SDK 9.0.312
  [Host]     : .NET 8.0.25 (8.0.2526.11203), X64 RyuJIT AVX2
  DefaultJob : .NET 8.0.25 (8.0.2526.11203), X64 RyuJIT AVX2


```
| Method         | Mean      | Error     | StdDev    | Allocated |
|--------------- |----------:|----------:|----------:|----------:|
| SingleValidate | 0.1781 ns | 0.0161 ns | 0.0151 ns |         - |
