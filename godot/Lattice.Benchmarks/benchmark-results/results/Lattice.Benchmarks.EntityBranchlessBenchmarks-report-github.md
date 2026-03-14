```

BenchmarkDotNet v0.13.11, Windows 10 (10.0.19045.6466/22H2/2022Update)
13th Gen Intel Core i9-13900KF, 1 CPU, 32 logical and 24 physical cores
.NET SDK 9.0.312
  [Host]     : .NET 8.0.25 (8.0.2526.11203), X64 RyuJIT AVX2
  Job-PNBJWX : .NET 8.0.25 (8.0.2526.11203), X64 RyuJIT AVX2
  ShortRun   : .NET 8.0.25 (8.0.2526.11203), X64 RyuJIT AVX2

Runtime=.NET 8.0  LaunchCount=1  WarmupCount=3  

```
| Method             | Job        | IterationCount | EntityCount | Mean        | Error        | StdDev     | Ratio | RatioSD | Rank | Allocated | Alloc Ratio |
|------------------- |----------- |--------------- |------------ |------------:|-------------:|-----------:|------:|--------:|-----:|----------:|------------:|
| **ValidateStandard**   | **Job-PNBJWX** | **5**              | **100**         |    **73.60 ns** |     **4.946 ns** |   **1.284 ns** |  **1.00** |    **0.00** |    **2** |         **-** |          **NA** |
| ValidateFast       | Job-PNBJWX | 5              | 100         |    49.14 ns |     1.549 ns |   0.402 ns |  0.67 |    0.01 |    1 |         - |          NA |
| ValidateBranchless | Job-PNBJWX | 5              | 100         |    73.99 ns |     5.190 ns |   1.348 ns |  1.01 |    0.02 |    2 |         - |          NA |
| ValidateBatch      | Job-PNBJWX | 5              | 100         |    72.86 ns |     6.559 ns |   1.703 ns |  0.99 |    0.04 |    2 |         - |          NA |
|                    |            |                |             |             |              |            |       |         |      |           |             |
| ValidateStandard   | ShortRun   | 3              | 100         |    74.06 ns |     8.759 ns |   0.480 ns |  1.00 |    0.00 |    3 |         - |          NA |
| ValidateFast       | ShortRun   | 3              | 100         |    50.53 ns |    11.592 ns |   0.635 ns |  0.68 |    0.00 |    1 |         - |          NA |
| ValidateBranchless | ShortRun   | 3              | 100         |    71.96 ns |     9.536 ns |   0.523 ns |  0.97 |    0.01 |    2 |         - |          NA |
| ValidateBatch      | ShortRun   | 3              | 100         |    76.40 ns |    23.447 ns |   1.285 ns |  1.03 |    0.02 |    4 |         - |          NA |
|                    |            |                |             |             |              |            |       |         |      |           |             |
| **ValidateStandard**   | **Job-PNBJWX** | **5**              | **1000**        |   **725.88 ns** |    **36.653 ns** |   **9.519 ns** |  **1.00** |    **0.00** |    **3** |         **-** |          **NA** |
| ValidateFast       | Job-PNBJWX | 5              | 1000        |   428.39 ns |     5.549 ns |   1.441 ns |  0.59 |    0.01 |    1 |         - |          NA |
| ValidateBranchless | Job-PNBJWX | 5              | 1000        |   728.87 ns |    40.142 ns |  10.425 ns |  1.00 |    0.00 |    3 |         - |          NA |
| ValidateBatch      | Job-PNBJWX | 5              | 1000        |   698.22 ns |    37.091 ns |   9.632 ns |  0.96 |    0.02 |    2 |         - |          NA |
|                    |            |                |             |             |              |            |       |         |      |           |             |
| ValidateStandard   | ShortRun   | 3              | 1000        |   696.01 ns |    27.849 ns |   1.526 ns |  1.00 |    0.00 |    2 |         - |          NA |
| ValidateFast       | ShortRun   | 3              | 1000        |   439.15 ns |    61.654 ns |   3.379 ns |  0.63 |    0.01 |    1 |         - |          NA |
| ValidateBranchless | ShortRun   | 3              | 1000        |   685.40 ns |   145.626 ns |   7.982 ns |  0.98 |    0.01 |    2 |         - |          NA |
| ValidateBatch      | ShortRun   | 3              | 1000        |   689.73 ns |   195.755 ns |  10.730 ns |  0.99 |    0.01 |    2 |         - |          NA |
|                    |            |                |             |             |              |            |       |         |      |           |             |
| **ValidateStandard**   | **Job-PNBJWX** | **5**              | **10000**       | **7,204.65 ns** |   **489.578 ns** | **127.142 ns** |  **1.00** |    **0.00** |    **3** |         **-** |          **NA** |
| ValidateFast       | Job-PNBJWX | 5              | 10000       | 4,462.71 ns |   250.327 ns |  65.009 ns |  0.62 |    0.01 |    1 |         - |          NA |
| ValidateBranchless | Job-PNBJWX | 5              | 10000       | 6,854.66 ns |   353.158 ns |  54.652 ns |  0.95 |    0.02 |    2 |         - |          NA |
| ValidateBatch      | Job-PNBJWX | 5              | 10000       | 6,848.88 ns |   253.378 ns |  65.802 ns |  0.95 |    0.02 |    2 |         - |          NA |
|                    |            |                |             |             |              |            |       |         |      |           |             |
| ValidateStandard   | ShortRun   | 3              | 10000       | 7,075.95 ns | 1,740.325 ns |  95.393 ns |  1.00 |    0.00 |    2 |         - |          NA |
| ValidateFast       | ShortRun   | 3              | 10000       | 4,282.79 ns |   152.832 ns |   8.377 ns |  0.61 |    0.01 |    1 |         - |          NA |
| ValidateBranchless | ShortRun   | 3              | 10000       | 7,095.05 ns | 3,538.605 ns | 193.963 ns |  1.00 |    0.04 |    2 |         - |          NA |
| ValidateBatch      | ShortRun   | 3              | 10000       | 7,060.43 ns | 2,036.386 ns | 111.621 ns |  1.00 |    0.02 |    2 |         - |          NA |
