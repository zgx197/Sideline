// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

using BenchmarkDotNet.Running;

namespace Lattice.Tests
{
    /// <summary>
    /// 基准测试运行器
    /// 
    /// 使用方法：
    /// dotnet run --project Tests/Lattice.Tests.csproj -c Release -- --benchmark
    /// </summary>
    public class BenchmarksRunner
    {
        public static void RunAllBenchmarks()
        {
            Console.WriteLine("=== Lattice ECS 性能基准测试 ===");
            Console.WriteLine();

            // ComponentSet 基准测试
            Console.WriteLine("运行 ComponentSet 基准测试...");
            BenchmarkRunner.Run<Benchmarks.ComponentSetBenchmarks>();

            Console.WriteLine();
            Console.WriteLine("运行 EntityRef 基准测试...");
            BenchmarkRunner.Run<Benchmarks.EntityRefBenchmarks>();

            Console.WriteLine();
            Console.WriteLine("运行哈希算法基准测试...");
            BenchmarkRunner.Run<Benchmarks.HashBenchmarks>();

            Console.WriteLine();
            Console.WriteLine("运行确定性集合基准测试...");
            BenchmarkRunner.Run<Benchmarks.DeterministicCollectionsBenchmarks>();
        }
    }
}
