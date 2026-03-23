// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

using System;
using System.Diagnostics;
using Lattice.Core;
using Lattice.ECS.Core;
using Lattice.Math;
using Xunit;
using Xunit.Abstractions;

namespace Lattice.Tests.Performance
{
    /// <summary>
    /// Storage 性能基准测试
    /// </summary>
    public unsafe class StorageBenchmark
    {
        private const int EntityCount = 1000;
        private const int IterationCount = 10;

        private readonly ITestOutputHelper _output;

        private struct TestComponent : IComponent
        {
            public int X;
            public int Y;
            public int Z;
            public int W;

            public TestComponent(int v)
            {
                X = Y = Z = W = v;
            }
        }

        public StorageBenchmark(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void Benchmark_AddComponents()
        {
            var storage = new Storage<TestComponent>();
            storage.Initialize(EntityCount);

            var sw = Stopwatch.StartNew();
            for (int iter = 0; iter < IterationCount; iter++)
            {
                for (int i = 0; i < EntityCount; i++)
                {
                    var entity = new EntityRef(i, 1);
                    storage.Add(entity, new TestComponent(i));
                }

                // 清理
                for (int i = 0; i < EntityCount; i++)
                {
                    var entity = new EntityRef(i, 1);
                    if (storage.Has(entity))
                        storage.Remove(entity);
                }
            }
            sw.Stop();

            storage.Dispose();

            double opsPerSecond = (EntityCount * IterationCount) / (sw.ElapsedMilliseconds / 1000.0);
            _output.WriteLine($"AddComponents: {opsPerSecond:F0} ops/sec ({sw.ElapsedMilliseconds}ms)");
        }

        [Fact]
        public void Benchmark_BlockIterator()
        {
            var storage = new Storage<TestComponent>();
            storage.Initialize(EntityCount);

            // 填充数据
            for (int i = 0; i < EntityCount; i++)
            {
                var entity = new EntityRef(i, 1);
                storage.Add(entity, new TestComponent(i));
            }

            long sum = 0;
            var sw = Stopwatch.StartNew();

            for (int iter = 0; iter < IterationCount * 10; iter++)
            {
                var iterator = new ComponentBlockIterator<TestComponent>(&storage);
                while (iterator.Next(out var entity, out var component))
                {
                    sum += component->X;
                }
            }

            sw.Stop();
            storage.Dispose();

            double opsPerSecond = (EntityCount * IterationCount * 10) / (sw.ElapsedMilliseconds / 1000.0);
            _output.WriteLine($"BlockIterator: {opsPerSecond:F0} ops/sec ({sw.ElapsedMilliseconds}ms)");
            _output.WriteLine($"Sum: {sum}"); // 防止优化掉
        }

        [Fact]
        public void Benchmark_RandomAccess()
        {
            var storage = new Storage<TestComponent>();
            storage.Initialize(EntityCount);

            // 填充数据
            for (int i = 0; i < EntityCount; i++)
            {
                var entity = new EntityRef(i, 1);
                storage.Add(entity, new TestComponent(i));
            }

            long sum = 0;
            var sw = Stopwatch.StartNew();

            // 降低迭代次数避免 CI 环境下的内存压力
            for (int iter = 0; iter < IterationCount; iter++)
            {
                for (int i = 0; i < EntityCount; i++)
                {
                    var entity = new EntityRef(i, 1);
                    if (storage.Has(entity))
                    {
                        ref var comp = ref storage.Get(entity);
                        sum += comp.X;
                    }
                }
            }

            sw.Stop();
            storage.Dispose();

            double opsPerSecond = (EntityCount * IterationCount) / (sw.ElapsedMilliseconds / 1000.0);
            _output.WriteLine($"RandomAccess: {opsPerSecond:F0} ops/sec ({sw.ElapsedMilliseconds}ms)");
        }

        [Fact]
        public void Benchmark_MemoryLayout()
        {
            var storage = new Storage<TestComponent>();
            storage.Initialize(1000);

            _output.WriteLine($"Component size: {sizeof(TestComponent)} bytes");
            _output.WriteLine($"Block capacity: {storage.BlockItemCapacity}");
            _output.WriteLine($"Block data size: {storage.BlockItemCapacity * sizeof(TestComponent)} bytes");
            
            // 添加一些组件
            for (int i = 0; i < 500; i++)
            {
                var entity = new EntityRef(i, 1);
                storage.Add(entity, new TestComponent(i));
            }

            _output.WriteLine($"Block count: {storage.BlockCount}");
            _output.WriteLine($"Component count: {storage.Count}");

            storage.Dispose();
        }
    }
}
