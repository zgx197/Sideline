// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

using System;
using System.Diagnostics;
using Lattice.ECS.Core;
using Xunit;
using Xunit.Abstractions;

namespace Lattice.Tests.ECS
{
    /// <summary>
    /// ComponentSet 性能测试
    /// 注意：这些测试主要作为性能回归检测，不是严格的基准测试
    /// </summary>
    public class ComponentSetBenchmarkTests
    {
        private readonly ITestOutputHelper _output;
        private const int WarmupIterations = 1000;
        private const int BenchmarkIterations = 100000;

        public ComponentSetBenchmarkTests(ITestOutputHelper output)
        {
            _output = output;
        }

        #region 微基准测试

        [Fact]
        public void Benchmark_IsSet()
        {
            var set = CreateRandomSet(100);
            var sw = Stopwatch.StartNew();

            for (int i = 0; i < BenchmarkIterations; i++)
            {
                _ = set.IsSet(i % 512);
            }

            sw.Stop();
            Report("IsSet", sw.ElapsedMilliseconds, BenchmarkIterations);
        }

        [Fact]
        public void Benchmark_Add()
        {
            var sw = Stopwatch.StartNew();

            for (int i = 0; i < BenchmarkIterations; i++)
            {
                var set = new ComponentSet();
                set.Add(i % 512);
            }

            sw.Stop();
            Report("Add", sw.ElapsedMilliseconds, BenchmarkIterations);
        }

        [Fact]
        public void Benchmark_Union()
        {
            var a = CreateRandomSet(50);
            var b = CreateRandomSet(50);
            var sw = Stopwatch.StartNew();

            for (int i = 0; i < BenchmarkIterations; i++)
            {
                _ = a.Union(b);
            }

            sw.Stop();
            Report("Union", sw.ElapsedMilliseconds, BenchmarkIterations);
        }

        [Fact]
        public void Benchmark_UnionWith()
        {
            var a = CreateRandomSet(50);
            var b = CreateRandomSet(50);
            var sw = Stopwatch.StartNew();

            for (int i = 0; i < BenchmarkIterations; i++)
            {
                var result = a;
                result.UnionWith(b);
            }

            sw.Stop();
            Report("UnionWith", sw.ElapsedMilliseconds, BenchmarkIterations);
        }

        [Fact]
        public void Benchmark_IsSubsetOf()
        {
            var superset = CreateRandomSet(100);
            var subset = CreateRandomSet(50);
            var sw = Stopwatch.StartNew();

            for (int i = 0; i < BenchmarkIterations; i++)
            {
                _ = subset.IsSubsetOf(superset);
            }

            sw.Stop();
            Report("IsSubsetOf", sw.ElapsedMilliseconds, BenchmarkIterations);
        }

        [Fact]
        public void Benchmark_Overlaps()
        {
            var a = CreateRandomSet(50);
            var b = CreateRandomSet(50);
            var sw = Stopwatch.StartNew();

            for (int i = 0; i < BenchmarkIterations; i++)
            {
                _ = a.Overlaps(b);
            }

            sw.Stop();
            Report("Overlaps", sw.ElapsedMilliseconds, BenchmarkIterations);
        }

        [Fact]
        public void Benchmark_CountBits()
        {
            var set = CreateRandomSet(100);
            var sw = Stopwatch.StartNew();

            for (int i = 0; i < BenchmarkIterations; i++)
            {
                _ = set.CountBits();
            }

            sw.Stop();
            Report("CountBits", sw.ElapsedMilliseconds, BenchmarkIterations);
        }

        [Fact]
        public void Benchmark_GetHashCode()
        {
            var set = CreateRandomSet(100);
            var sw = Stopwatch.StartNew();

            for (int i = 0; i < BenchmarkIterations; i++)
            {
                _ = set.GetHashCode();
            }

            sw.Stop();
            Report("GetHashCode", sw.ElapsedMilliseconds, BenchmarkIterations);
        }

        [Fact]
        public void Benchmark_Serialization()
        {
            var set = CreateRandomSet(100);
            var sw = Stopwatch.StartNew();

            for (int i = 0; i < BenchmarkIterations; i++)
            {
                var bytes = set.ToLittleEndianBytes();
                _ = ComponentSet.FromLittleEndianBytes(bytes);
            }

            sw.Stop();
            Report("Serialization RoundTrip", sw.ElapsedMilliseconds, BenchmarkIterations);
        }

        #endregion

        #region 场景测试

        [Fact]
        public void Benchmark_QueryScenario()
        {
            // 模拟 ECS 查询场景
            var required = ComponentSet.Create<TestComp1, TestComp2>();
            var excluded = ComponentSet.Create<TestComp3>();

            var entitySets = new ComponentSet[1000];
            var random = new Random(42);
            for (int i = 0; i < entitySets.Length; i++)
            {
                entitySets[i] = CreateRandomSet(random.Next(20));
            }

            var sw = Stopwatch.StartNew();
            int matchCount = 0;

            for (int iter = 0; iter < 1000; iter++)
            {
                for (int i = 0; i < entitySets.Length; i++)
                {
                    if (required.IsSubsetOf(entitySets[i]) && !excluded.Overlaps(entitySets[i]))
                    {
                        matchCount++;
                    }
                }
            }

            sw.Stop();
            _output.WriteLine($"Query Scenario: {sw.ElapsedMilliseconds}ms for 1M queries");
            _output.WriteLine($"  Matches: {matchCount}");
        }

        [Fact]
        public void Benchmark_BatchOperations()
        {
            const int batchSize = 1000;
            var sets = new ComponentSet[batchSize];
            var random = new Random(42);

            for (int i = 0; i < batchSize; i++)
            {
                sets[i] = CreateRandomSet(random.Next(10, 50));
            }

            // 测试批量并集
            var sw = Stopwatch.StartNew();
            var result = ComponentSet.Empty;

            for (int iter = 0; iter < 100; iter++)
            {
                for (int i = 0; i < batchSize; i++)
                {
                    result.UnionWith(sets[i]);
                }
                result = ComponentSet.Empty;
            }

            sw.Stop();
            _output.WriteLine($"Batch Union: {sw.ElapsedMilliseconds}ms for 100K operations");
        }

        [Fact]
        public void Benchmark_CreateOperations()
        {
            var sw = Stopwatch.StartNew();

            for (int i = 0; i < BenchmarkIterations; i++)
            {
                _ = ComponentSet.Create<TestComp1>();
                _ = ComponentSet.Create<TestComp1, TestComp2>();
                _ = ComponentSet.Create<TestComp1, TestComp2, TestComp3>();
            }

            sw.Stop();
            _output.WriteLine($"Create Operations: {sw.ElapsedMilliseconds}ms for {BenchmarkIterations * 3} creates");
        }

        #endregion

        #region 内存测试

        [Fact]
        public void Memory_SizeVerification()
        {
            var set = ComponentSet.Empty;
            var size = System.Runtime.InteropServices.Marshal.SizeOf(set);

            _output.WriteLine($"ComponentSet size: {size} bytes");
            Assert.Equal(64, size);
        }

        [Fact]
        public void Memory_CacheLineEfficiency()
        {
            // 验证 64 字节对齐，正好是一个缓存行
            Assert.Equal(64, ComponentSet.SIZE);

            // 测试缓存行效率
            const int count = 10000;
            var sets = new ComponentSet[count];
            var random = new Random(42);

            for (int i = 0; i < count; i++)
            {
                sets[i] = CreateRandomSet(random.Next(20));
            }

            var sw = Stopwatch.StartNew();
            int total = 0;

            for (int iter = 0; iter < 100; iter++)
            {
                for (int i = 0; i < count; i++)
                {
                    total += sets[i].CountBits();
                }
            }

            sw.Stop();
            _output.WriteLine($"Cache line scan: {sw.ElapsedMilliseconds}ms (total={total})");
        }

        #endregion

        #region 辅助方法

        private struct TestComp1 : IComponent { }
        private struct TestComp2 : IComponent { }
        private struct TestComp3 : IComponent { }

        private ComponentSet CreateRandomSet(int bitCount)
        {
            var set = new ComponentSet();
            var random = new Random(42);

            for (int i = 0; i < bitCount; i++)
            {
                set.Add(random.Next(ComponentSet.MAX_COMPONENTS));
            }

            return set;
        }

        private void Report(string name, long elapsedMs, int iterations)
        {
            double nsPerOp = (elapsedMs * 1_000_000.0) / iterations;
            _output.WriteLine($"{name}: {elapsedMs}ms for {iterations:N0} ops ({nsPerOp:F2} ns/op)");
        }

        #endregion
    }
}
