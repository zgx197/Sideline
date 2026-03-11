using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;
using Lattice.Benchmarks;

// Lattice 性能基准测试入口
// 运行方式:
//   dotnet run --configuration Release
//   dotnet run --configuration Release -- --filter "*FP*"

Console.WriteLine("Lattice Performance Benchmarks");
Console.WriteLine("==============================");

var config = DefaultConfig.Instance
    .WithOption(ConfigOptions.DisableOptimizationsValidator, true);

BenchmarkSwitcher
    .FromAssembly(typeof(Program).Assembly)
    .Run(args, config);
