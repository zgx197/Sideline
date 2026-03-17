// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

using System;
using System.Diagnostics;
using Lattice.Core;
using Lattice.ECS.Core;
using Xunit;

namespace Lattice.Tests
{
    /// <summary>
    /// 简化性能测试
    /// 用于快速验证优化效果（非精确基准测试）
    /// </summary>
    public class PerformanceTests
    {
        private const int Iterations = 100000;

        [Fact]
        public void Performance_EntityRef_GetHashCode()
        {
            var entity = new EntityRef(12345, 67890);
            
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < Iterations; i++)
            {
                _ = entity.GetHashCode();
            }
            sw.Stop();

            // 应该非常快（< 1ms for 100k）
            Assert.True(sw.ElapsedMilliseconds < 10, 
                $"GetHashCode too slow: {sw.ElapsedMilliseconds}ms for {Iterations} iterations");
        }

        [Fact]
        public void Performance_EntityRef_Equality()
        {
            var e1 = new EntityRef(12345, 67890);
            var e2 = new EntityRef(12345, 67890);
            bool result = false;

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < Iterations; i++)
            {
                result = e1 == e2;
            }
            sw.Stop();

            Assert.True(result); // 验证正确性
            Assert.True(sw.ElapsedMilliseconds < 10,
                $"Equality check too slow: {sw.ElapsedMilliseconds}ms for {Iterations} iterations");
        }

        [Fact]
        public void Performance_ComponentSetNet8_Add()
        {
            var sw = Stopwatch.StartNew();
            
            for (int i = 0; i < Iterations; i++)
            {
                var set = new ComponentSetNet8();
                for (int j = 0; j < 100; j++)
                {
                    set.Add(j);
                }
            }
            sw.Stop();

            Assert.True(sw.ElapsedMilliseconds < 500,
                $"ComponentSet Add too slow: {sw.ElapsedMilliseconds}ms");
        }

        [Fact]
        public void Performance_ComponentSetNet8_IsSet()
        {
            var set = new ComponentSetNet8();
            for (int i = 0; i < 100; i++)
            {
                set.Add(i);
            }

            bool result = false;
            var sw = Stopwatch.StartNew();
            
            for (int i = 0; i < Iterations; i++)
            {
                for (int j = 0; j < 100; j++)
                {
                    result = set.IsSet(j);
                }
            }
            sw.Stop();

            Assert.True(result); // 验证正确性
            Assert.True(sw.ElapsedMilliseconds < 500,
                $"ComponentSet IsSet too slow: {sw.ElapsedMilliseconds}ms");
        }

        [Fact]
        public void Performance_ComponentSetNet8_IsSupersetOf()
        {
            var set1 = new ComponentSetNet8();
            var set2 = new ComponentSetNet8();
            
            for (int i = 0; i < 50; i++)
            {
                set1.Add(i);
                if (i < 25)
                    set2.Add(i);
            }

            bool result = false;
            var sw = Stopwatch.StartNew();
            
            for (int i = 0; i < Iterations; i++)
            {
                result = set1.IsSupersetOf(ref set2);
            }
            sw.Stop();

            Assert.True(result); // set1 是 set2 的超集
            Assert.True(sw.ElapsedMilliseconds < 100,
                $"IsSupersetOf too slow: {sw.ElapsedMilliseconds}ms");
        }

        [Fact]
        public void Performance_DeterministicHash_Fnv1a32()
        {
            var data = new byte[64];
            new Random(42).NextBytes(data);

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < Iterations; i++)
            {
                _ = DeterministicHash.Fnv1a32(data);
            }
            sw.Stop();

            Assert.True(sw.ElapsedMilliseconds < 200,
                $"Fnv1a32 too slow: {sw.ElapsedMilliseconds}ms");
        }

        [Fact]
        public void Performance_DeterministicIdMap_Lookup()
        {
            var map = new DeterministicIdMap<int>(256);
            for (int i = 1; i <= 100; i++)
            {
                map.Add(i, i * 10);
            }

            int result = 0;
            var sw = Stopwatch.StartNew();
            
            for (int i = 0; i < Iterations; i++)
            {
                for (int j = 1; j <= 100; j++)
                {
                    map.TryGetValue(j, out result);
                }
            }
            sw.Stop();

            Assert.True(sw.ElapsedMilliseconds < 500,
                $"DeterministicIdMap lookup too slow: {sw.ElapsedMilliseconds}ms");
        }

        [Fact]
        public void SIMD_Detect_Availability()
        {
            // 输出 SIMD 可用性信息
            var simdInfo = new System.Text.StringBuilder();
            simdInfo.AppendLine("SIMD 支持状态:");
            
            if (System.Runtime.Intrinsics.Vector512.IsHardwareAccelerated)
            {
                simdInfo.AppendLine("  ✓ Vector512 (512-bit)");
            }
            else if (System.Runtime.Intrinsics.Vector256.IsHardwareAccelerated)
            {
                simdInfo.AppendLine("  ✓ Vector256 (256-bit)");
            }
            else
            {
                simdInfo.AppendLine("  ✗ SIMD not available (using scalar fallback)");
            }

            simdInfo.AppendLine($"  CPU: {System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture}");
            
            Console.WriteLine(simdInfo.ToString());
            
            // 测试通过即成功
            Assert.True(true);
        }
    }
}
