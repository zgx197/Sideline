using System;
using System.IO;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;
using Lattice.Benchmarks;

// Lattice 性能基准测试入口
// 运行方式:
//   dotnet run --configuration Release
//   dotnet run --configuration Release -- --filter "*FP*"

Console.WriteLine("Lattice Performance Benchmarks");
Console.WriteLine("==============================");

// 设置 artifacts 输出目录为项目根目录下的 benchmark-results
var artifactsPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "benchmark-results");
var fullPath = Path.GetFullPath(artifactsPath);
Console.WriteLine($"Artifacts path: {fullPath}");

var config = DefaultConfig.Instance
    .WithOption(ConfigOptions.DisableOptimizationsValidator, true)
    .WithArtifactsPath(fullPath);

BenchmarkSwitcher
    .FromAssembly(typeof(Program).Assembly)
    .Run(args, config);
