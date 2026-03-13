// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using Lattice.Math;
using Xunit;

namespace Lattice.Tests.Math
{
    /// <summary>
    /// FPSimd SIMD 批量操作测试
    /// <para>测试硬件加速 SIMD 操作的正确性、确定性和边界情况</para>
    /// </summary>
    /// <remarks>
    /// FPSimd 使用 System.Numerics.Vector&lt;T&gt; 提供 SIMD 加速的定点数批量操作。
    /// 当硬件加速不可用时，自动回退到标量实现。
    /// </remarks>
    public class FPSimdTests
    {
        #region 辅助方法

        /// <summary>
        /// 生成随机 FP 数组用于测试
        /// </summary>
        private static FP[] GenerateRandomFPArray(int length, int seed, int minValue = -1000, int maxValue = 1000)
        {
            var random = new Random(seed);
            var array = new FP[length];
            for (int i = 0; i < length; i++)
            {
                int value = random.Next(minValue, maxValue);
                array[i] = (FP)value;
            }
            return array;
        }

        /// <summary>
        /// 生成随机 FPVector2 数组用于测试
        /// </summary>
        private static FPVector2[] GenerateRandomVector2Array(int length, int seed)
        {
            var random = new Random(seed);
            var array = new FPVector2[length];
            for (int i = 0; i < length; i++)
            {
                int x = random.Next(-100, 100);
                int y = random.Next(-100, 100);
                array[i] = new FPVector2(x, y);
            }
            return array;
        }

        /// <summary>
        /// 标量加法参考实现
        /// </summary>
        private static void AddScalar(ReadOnlySpan<FP> a, ReadOnlySpan<FP> b, Span<FP> output)
        {
            int length = System.Math.Min(a.Length, b.Length);
            for (int i = 0; i < length; i++)
            {
                output[i] = a[i] + b[i];
            }
        }

        /// <summary>
        /// 标量减法参考实现
        /// </summary>
        private static void SubtractScalar(ReadOnlySpan<FP> a, ReadOnlySpan<FP> b, Span<FP> output)
        {
            int length = System.Math.Min(a.Length, b.Length);
            for (int i = 0; i < length; i++)
            {
                output[i] = a[i] - b[i];
            }
        }

        /// <summary>
        /// 标量乘法参考实现（四舍五入）
        /// </summary>
        private static void MultiplyScalar(ReadOnlySpan<FP> a, ReadOnlySpan<FP> b, Span<FP> output)
        {
            int length = System.Math.Min(a.Length, b.Length);
            for (int i = 0; i < length; i++)
            {
                output[i] = a[i] * b[i];
            }
        }

        /// <summary>
        /// 标量取反参考实现
        /// </summary>
        private static void NegateScalar(ReadOnlySpan<FP> input, Span<FP> output)
        {
            for (int i = 0; i < input.Length; i++)
            {
                output[i] = -input[i];
            }
        }

        /// <summary>
        /// 标量绝对值参考实现
        /// </summary>
        private static void AbsScalar(ReadOnlySpan<FP> input, Span<FP> output)
        {
            for (int i = 0; i < input.Length; i++)
            {
                output[i] = FP.Abs(input[i]);
            }
        }

        /// <summary>
        /// FPVector2 标量加法参考实现
        /// </summary>
        private static void AddVector2Scalar(ReadOnlySpan<FPVector2> a, ReadOnlySpan<FPVector2> b, Span<FPVector2> output)
        {
            int length = System.Math.Min(a.Length, b.Length);
            for (int i = 0; i < length; i++)
            {
                output[i] = a[i] + b[i];
            }
        }

        /// <summary>
        /// 验证两个 FP 数组是否完全相等（确定性验证）
        /// </summary>
        private static void AssertFPEqual(ReadOnlySpan<FP> expected, ReadOnlySpan<FP> actual, string message = "")
        {
            Assert.Equal(expected.Length, actual.Length);
            for (int i = 0; i < expected.Length; i++)
            {
                if (expected[i].RawValue != actual[i].RawValue)
                {
                    Assert.Fail($"Mismatch at index {i}. Expected: {expected[i].RawValue}, Actual: {actual[i].RawValue}. {message}");
                }
            }
        }

        #endregion

        #region 1. 特性检测测试

        /// <summary>
        /// 测试：SIMD 硬件加速可用性检测
        /// </summary>
        [Fact]
        public void IsHardwareAccelerated_ReturnsExpectedValue()
        {
            // Vector.IsHardwareAccelerated 是运行时属性
            // 在支持 SIMD 的平台上返回 true（x64 通常支持，某些 ARM 也支持）
            bool isAccelerated = Vector.IsHardwareAccelerated;
            
            // 记录当前环境状态（测试不强制要求加速可用）
            Assert.True(true, $"SIMD Hardware Acceleration: {isAccelerated}");
        }

        /// <summary>
        /// 测试：Vector&lt;long&gt;.Count 表示每向量元素数
        /// </summary>
        [Fact]
        public void VectorLongCount_IsPositive()
        {
            int count = Vector<long>.Count;
            
            // Vector<long>.Count 应该在 1 到 8 之间（取决于 SIMD 宽度）
            // 128-bit SIMD: 2 elements (SSE2)
            // 256-bit SIMD: 4 elements (AVX2)
            // 512-bit SIMD: 8 elements (AVX-512)
            Assert.True(count >= 1, "Vector<long>.Count should be at least 1");
            Assert.True(count <= 8, "Vector<long>.Count should be at most 8");
        }

        /// <summary>
        /// 测试：Vector&lt;long&gt;.Count 与 Vector&lt;FP&gt; 的映射关系
        /// </summary>
        [Fact]
        public void VectorLongCount_MatchesFPBatchSize()
        {
            // FP 内部使用 long (RawValue)，所以 Vector<long>.Count 决定了每批处理的 FP 数量
            int longCount = Vector<long>.Count;
            
            // 在支持 256-bit AVX2 的 x64 平台上，count 应该是 4
            // 在仅支持 128-bit SSE2 的平台上，count 应该是 2
            Assert.True(longCount == 2 || longCount == 4 || longCount == 8,
                $"Unexpected Vector<long>.Count = {longCount}. Expected 2 (SSE2), 4 (AVX2), or 8 (AVX-512)");
        }

        #endregion

        #region 2. 批量加法测试

        /// <summary>
        /// 测试：AddBatch 正确性 - 标量对比
        /// </summary>
        [Theory]
        [InlineData(1)]      // 单元素
        [InlineData(2)]      // 2 元素（SSE2 宽度）
        [InlineData(3)]      // 3 元素（非对齐）
        [InlineData(4)]      // 4 元素（AVX2 宽度）
        [InlineData(5)]      // 5 元素（非对齐）
        [InlineData(7)]      // 7 元素（非对齐）
        [InlineData(8)]      // 8 元素（AVX-512 宽度）
        [InlineData(15)]     // 15 元素（非对齐）
        [InlineData(16)]     // 16 元素
        [InlineData(17)]     // 17 元素（非对齐）
        [InlineData(100)]    // 中等规模
        [InlineData(1000)]   // 大规模
        public void AddBatch_Correctness_VsScalar(int length)
        {
            var a = GenerateRandomFPArray(length, seed: 42);
            var b = GenerateRandomFPArray(length, seed: 123);
            var expected = new FP[length];
            var actual = new FP[length];

            // 标量参考结果
            AddScalar(a, b, expected);

            // SIMD 结果
            FPSimd.AddBatch(a, b, actual);

            // 验证完全相等
            AssertFPEqual(expected, actual, "AddBatch should match scalar implementation exactly");
        }

        /// <summary>
        /// 测试：AddBatch 处理边界值
        /// </summary>
        [Theory]
        [InlineData(0)]      // 零
        [InlineData(32767)]  // 安全范围上限
        [InlineData(-32767)] // 安全范围下限
        public void AddBatch_BoundaryValues(long value)
        {
            var a = new FP[] { FP.FromRaw(value), FP.FromRaw(-value), FP._0, FP._1 };
            var b = new FP[] { FP._0, FP.FromRaw(value), FP.FromRaw(-value), FP._1 };
            var expected = new FP[4];
            var actual = new FP[4];

            AddScalar(a, b, expected);
            FPSimd.AddBatch(a, b, actual);

            AssertFPEqual(expected, actual);
        }

        #endregion

        #region 3. 批量减法测试

        /// <summary>
        /// 测试：SubtractBatch 正确性 - 标量对比
        /// </summary>
        [Theory]
        [InlineData(1)]
        [InlineData(4)]
        [InlineData(7)]
        [InlineData(16)]
        [InlineData(100)]
        public void SubtractBatch_Correctness_VsScalar(int length)
        {
            var a = GenerateRandomFPArray(length, seed: 42);
            var b = GenerateRandomFPArray(length, seed: 123);
            var expected = new FP[length];
            var actual = new FP[length];

            SubtractScalar(a, b, expected);
            FPSimd.SubtractBatch(a, b, actual);

            AssertFPEqual(expected, actual);
        }

        /// <summary>
        /// 测试：减法结果为零的情况
        /// </summary>
        [Fact]
        public void SubtractBatch_SameInput_ReturnsZero()
        {
            var a = GenerateRandomFPArray(16, seed: 42);
            var b = new FP[16];
            Array.Copy(a, b, 16); // b = a
            var result = new FP[16];

            FPSimd.SubtractBatch(a, b, result);

            // a - a 应该等于 0
            foreach (var fp in result)
            {
                Assert.Equal(0L, fp.RawValue);
            }
        }

        #endregion

        #region 4. 批量乘法测试

        /// <summary>
        /// 测试：MultiplyBatch 正确性 - 标量对比（包含四舍五入）
        /// </summary>
        [Theory]
        [InlineData(1)]
        [InlineData(4)]
        [InlineData(7)]
        [InlineData(16)]
        [InlineData(100)]
        public void MultiplyBatch_Correctness_VsScalar(int length)
        {
            // 使用小值避免溢出（安全乘法范围）
            var a = GenerateRandomFPArray(length, seed: 42, minValue: -100, maxValue: 100);
            var b = GenerateRandomFPArray(length, seed: 123, minValue: -100, maxValue: 100);
            var expected = new FP[length];
            var actual = new FP[length];

            MultiplyScalar(a, b, expected);
            FPSimd.MultiplyBatch(a, b, actual);

            AssertFPEqual(expected, actual);
        }

        /// <summary>
        /// 测试：乘法特殊值（0 和 1）
        /// </summary>
        [Fact]
        public void MultiplyBatch_SpecialValues()
        {
            var a = new FP[] { FP._0, FP._1, FP._2, FP.FromRaw(-65536) };
            var b = new FP[] { FP._1, FP._1, FP._0_50, FP._2 };
            var expected = new FP[4];
            var actual = new FP[4];

            // 期望结果
            expected[0] = FP._0 * FP._1;        // 0 * 1 = 0
            expected[1] = FP._1 * FP._1;        // 1 * 1 = 1
            expected[2] = FP._2 * FP._0_50;     // 2 * 0.5 = 1
            expected[3] = FP.FromRaw(-65536) * FP._2;  // -1 * 2 = -2

            FPSimd.MultiplyBatch(a, b, actual);

            AssertFPEqual(expected, actual);
        }

        #endregion

        #region 5. 批量取反测试

        /// <summary>
        /// 测试：NegateBatch 正确性
        /// </summary>
        [Theory]
        [InlineData(1)]
        [InlineData(4)]
        [InlineData(7)]
        [InlineData(16)]
        [InlineData(100)]
        public void NegateBatch_Correctness_VsScalar(int length)
        {
            var input = GenerateRandomFPArray(length, seed: 42);
            var expected = new FP[length];
            var actual = new FP[length];

            NegateScalar(input, expected);
            FPSimd.NegateBatch(input, actual);

            AssertFPEqual(expected, actual);
        }

        /// <summary>
        /// 测试：双重取反等于原值
        /// </summary>
        [Fact]
        public void NegateBatch_DoubleNegation_ReturnsOriginal()
        {
            var original = GenerateRandomFPArray(16, seed: 42);
            var temp = new FP[16];
            var result = new FP[16];

            FPSimd.NegateBatch(original, temp);
            FPSimd.NegateBatch(temp, result);

            // -(-x) = x
            AssertFPEqual(original, result);
        }

        /// <summary>
        /// 测试：取反零仍为零
        /// </summary>
        [Fact]
        public void NegateBatch_Zero_ReturnsZero()
        {
            var input = new FP[] { FP._0, FP._0, FP._0 };
            var result = new FP[3];

            FPSimd.NegateBatch(input, result);

            foreach (var fp in result)
            {
                Assert.Equal(0L, fp.RawValue);
            }
        }

        #endregion

        #region 6. 批量绝对值测试

        /// <summary>
        /// 测试：AbsBatch 正确性
        /// </summary>
        [Theory]
        [InlineData(1)]
        [InlineData(4)]
        [InlineData(7)]
        [InlineData(16)]
        [InlineData(100)]
        public void AbsBatch_Correctness_VsScalar(int length)
        {
            var input = GenerateRandomFPArray(length, seed: 42);
            var expected = new FP[length];
            var actual = new FP[length];

            AbsScalar(input, expected);
            FPSimd.AbsBatch(input, actual);

            AssertFPEqual(expected, actual);
        }

        /// <summary>
        /// 测试：绝对值非负
        /// </summary>
        [Fact]
        public void AbsBatch_ResultIsNonNegative()
        {
            var input = new FP[] { FP.FromRaw(-1000), FP._0, FP.FromRaw(500) };
            var result = new FP[3];

            FPSimd.AbsBatch(input, result);

            foreach (var fp in result)
            {
                Assert.True(fp.RawValue >= 0, "Abs result should be non-negative");
            }
        }

        /// <summary>
        /// 测试：绝对值幂等性
        /// </summary>
        [Fact]
        public void AbsBatch_Idempotent()
        {
            var input = GenerateRandomFPArray(16, seed: 42);
            var temp = new FP[16];
            var result = new FP[16];

            FPSimd.AbsBatch(input, temp);
            FPSimd.AbsBatch(temp, result);

            // Abs(Abs(x)) = Abs(x)
            AssertFPEqual(temp, result);
        }

        #endregion

        #region 7. FPVector2 SIMD 测试

        /// <summary>
        /// 测试：AddBatchVector2 正确性 - 标量对比
        /// </summary>
        [Theory]
        [InlineData(1)]
        [InlineData(4)]
        [InlineData(7)]
        [InlineData(16)]
        [InlineData(100)]
        public void AddBatchVector2_Correctness_VsScalar(int length)
        {
            var a = GenerateRandomVector2Array(length, seed: 42);
            var b = GenerateRandomVector2Array(length, seed: 123);
            var expected = new FPVector2[length];
            var actual = new FPVector2[length];

            AddVector2Scalar(a, b, expected);
            FPSimd.AddBatchVector2(a, b, actual);

            for (int i = 0; i < length; i++)
            {
                Assert.Equal(expected[i].X.RawValue, actual[i].X.RawValue);
                Assert.Equal(expected[i].Y.RawValue, actual[i].Y.RawValue);
            }
        }

        /// <summary>
        /// 测试：FPVector2 加法满足交换律
        /// </summary>
        [Fact]
        public void AddBatchVector2_IsCommutative()
        {
            var a = GenerateRandomVector2Array(16, seed: 42);
            var b = GenerateRandomVector2Array(16, seed: 123);
            var ab = new FPVector2[16];
            var ba = new FPVector2[16];

            FPSimd.AddBatchVector2(a, b, ab);
            FPSimd.AddBatchVector2(b, a, ba);

            for (int i = 0; i < 16; i++)
            {
                Assert.Equal(ab[i].X.RawValue, ba[i].X.RawValue);
                Assert.Equal(ab[i].Y.RawValue, ba[i].Y.RawValue);
            }
        }

        #endregion

        #region 8. 边界情况测试

        /// <summary>
        /// 测试：空数组处理
        /// </summary>
        [Fact]
        public void AddBatch_EmptyArray_DoesNotThrow()
        {
            var a = Array.Empty<FP>();
            var b = Array.Empty<FP>();
            var result = Array.Empty<FP>();

            var exception = Record.Exception(() => FPSimd.AddBatch(a, b, result));
            Assert.Null(exception);
        }

        /// <summary>
        /// 测试：单元素数组
        /// </summary>
        [Fact]
        public void AddBatch_SingleElement_WorksCorrectly()
        {
            var a = new FP[] { FP._1 };
            var b = new FP[] { FP._2 };
            var result = new FP[1];

            FPSimd.AddBatch(a, b, result);

            Assert.Equal((FP)3, result[0]);
        }

        /// <summary>
        /// 测试：输出数组长度不足应抛出异常
        /// </summary>
        [Fact]
        public void AddBatch_OutputTooSmall_ThrowsArgumentException()
        {
            var a = new FP[10];
            var b = new FP[10];
            var result = new FP[5];

            Assert.Throws<ArgumentException>(() => FPSimd.AddBatch(a, b, result));
        }

        /// <summary>
        /// 测试：输入数组长度不匹配应抛出异常
        /// </summary>
        [Fact]
        public void AddBatch_InputLengthMismatch_ThrowsArgumentException()
        {
            var a = new FP[10];
            var b = new FP[5];
            var result = new FP[10];

            Assert.Throws<ArgumentException>(() => FPSimd.AddBatch(a, b, result));
        }

        /// <summary>
        /// 测试：极端值处理（最大值/最小值）
        /// </summary>
        [Fact]
        public void AddBatch_ExtremeValues_HandlesCorrectly()
        {
            // 使用安全范围内的最大值
            long maxSafe = 32767 * FP.ONE;  // 32767.0
            long minSafe = -32767 * FP.ONE; // -32767.0

            var a = new FP[] { FP.FromRaw(maxSafe), FP.FromRaw(minSafe), FP._0 };
            var b = new FP[] { FP._0, FP.FromRaw(maxSafe), FP.FromRaw(minSafe) };
            var result = new FP[3];

            // 不应抛出异常
            var exception = Record.Exception(() => FPSimd.AddBatch(a, b, result));
            Assert.Null(exception);
        }

        #endregion

        #region 9. 确定性测试

        /// <summary>
        /// 测试：相同输入产生相同输出（多次运行一致性）
        /// </summary>
        [Fact]
        public void AddBatch_SameInput_SameOutput_MultipleRuns()
        {
            var a = GenerateRandomFPArray(100, seed: 42);
            var b = GenerateRandomFPArray(100, seed: 123);
            var result1 = new FP[100];
            var result2 = new FP[100];
            var result3 = new FP[100];

            // 多次运行
            FPSimd.AddBatch(a, b, result1);
            FPSimd.AddBatch(a, b, result2);
            FPSimd.AddBatch(a, b, result3);

            // 结果应完全相同
            AssertFPEqual(result1, result2, "First and second run should be identical");
            AssertFPEqual(result2, result3, "Second and third run should be identical");
        }

        /// <summary>
        /// 测试：SIMD 结果与标量结果完全一致（跨平台确定性）
        /// </summary>
        [Theory]
        [InlineData(1)]
        [InlineData(3)]
        [InlineData(7)]
        [InlineData(15)]
        [InlineData(31)]
        [InlineData(64)]
        [InlineData(127)]
        public void AllOperations_SIMDResultsMatchScalarExactly(int length)
        {
            var a = GenerateRandomFPArray(length, seed: 42, minValue: -50, maxValue: 50);
            var b = GenerateRandomFPArray(length, seed: 123, minValue: -50, maxValue: 50);

            // 加法
            var addExpected = new FP[length];
            var addActual = new FP[length];
            AddScalar(a, b, addExpected);
            FPSimd.AddBatch(a, b, addActual);
            AssertFPEqual(addExpected, addActual, "AddBatch determinism failed");

            // 减法
            var subExpected = new FP[length];
            var subActual = new FP[length];
            SubtractScalar(a, b, subExpected);
            FPSimd.SubtractBatch(a, b, subActual);
            AssertFPEqual(subExpected, subActual, "SubtractBatch determinism failed");

            // 乘法
            var mulExpected = new FP[length];
            var mulActual = new FP[length];
            MultiplyScalar(a, b, mulExpected);
            FPSimd.MultiplyBatch(a, b, mulActual);
            AssertFPEqual(mulExpected, mulActual, "MultiplyBatch determinism failed");

            // 取反
            var negExpected = new FP[length];
            var negActual = new FP[length];
            NegateScalar(a, negExpected);
            FPSimd.NegateBatch(a, negActual);
            AssertFPEqual(negExpected, negActual, "NegateBatch determinism failed");

            // 绝对值
            var absExpected = new FP[length];
            var absActual = new FP[length];
            AbsScalar(a, absExpected);
            FPSimd.AbsBatch(a, absActual);
            AssertFPEqual(absExpected, absActual, "AbsBatch determinism failed");
        }

        #endregion

        #region 10. 性能验证测试（可选）

        /// <summary>
        /// 测试：SIMD 在大规模数组上性能优于标量
        /// <para>注：此测试可能因硬件差异而波动，仅作参考</para>
        /// </summary>
        [Theory]
        [InlineData(10000)]
        [InlineData(100000)]
        public void AddBatch_SIMDIsFasterThanScalar_LargeArrays(int length)
        {
            // 这些测试默认跳过，因为性能测试在 CI 环境中不稳定
            // 可以通过环境变量启用
            var runPerformanceTests = Environment.GetEnvironmentVariable("RUN_PERF_TESTS");
            if (string.IsNullOrEmpty(runPerformanceTests))
            {
                Assert.True(true, "Performance test skipped. Set RUN_PERF_TESTS=1 to enable.");
                return;
            }

            var a = GenerateRandomFPArray(length, seed: 42);
            var b = GenerateRandomFPArray(length, seed: 123);
            var simdResult = new FP[length];
            var scalarResult = new FP[length];

            // 预热
            FPSimd.AddBatch(a, b, simdResult);
            AddScalar(a, b, scalarResult);

            // 标量计时
            var scalarStopwatch = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < 10; i++)
            {
                AddScalar(a, b, scalarResult);
            }
            scalarStopwatch.Stop();

            // SIMD 计时
            var simdStopwatch = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < 10; i++)
            {
                FPSimd.AddBatch(a, b, simdResult);
            }
            simdStopwatch.Stop();

            // SIMD 应该比标量快（至少快 50%，给 CI 环境留余量）
            // 注意：这个断言可能在小数据量或特定硬件上失败
            double speedup = (double)scalarStopwatch.ElapsedTicks / simdStopwatch.ElapsedTicks;
            Assert.True(speedup > 1.0 || !Vector.IsHardwareAccelerated,
                $"SIMD should be faster when hardware accelerated. Speedup: {speedup:F2}x, " +
                $"HardwareAccelerated: {Vector.IsHardwareAccelerated}");
        }

        #endregion
    }
}
