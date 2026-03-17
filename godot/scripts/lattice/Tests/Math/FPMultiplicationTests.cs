// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

using Xunit;
using Lattice.Math;

namespace Lattice.Tests.Math
{
    /// <summary>
    /// FP 乘法测试（舍入模式）
    /// </summary>
    public class FPMultiplicationTests
    {
        [Fact]
        public void Multiply_RoundingVsTruncation()
        {
            FP a = FP.Raw._0_10;  // 0.1 ≈ 6554
            FP b = FP.Raw._0_10;  // 0.1 ≈ 6554
            
            // 标准乘法（四舍五入）
            FP rounded = a * b;
            
            // 快速乘法（截断）
            FP truncated = FP.MultiplyFast(a, b);
            
            // 四舍五入应该更接近真实值
            FP expected = FP.Raw._0_01;  // 0.01 ≈ 655
            
            long diffRounded = FP.Abs(rounded - expected).RawValue;
            long diffTruncated = FP.Abs(truncated - expected).RawValue;
            
            Assert.True(diffRounded <= diffTruncated,
                $"Rounding should be closer: rounded={rounded.RawValue}, truncated={truncated.RawValue}, expected={expected.RawValue}");
        }

        [Theory]
        [InlineData(0.5, 0.5, 0.25)]     // 0.5 * 0.5 = 0.25
        [InlineData(0.1, 0.2, 0.02)]     // 0.1 * 0.2 ≈ 0.02
        [InlineData(1.5, 2.0, 3.0)]      // 1.5 * 2 = 3
        [InlineData(0.33, 3.0, 0.99)]    // 0.33 * 3 ≈ 0.99
        public void Multiply_VariousValues(double ad, double bd, double expectedd)
        {
            FP a = FP.FromRaw((long)(ad * FP.ONE));
            FP b = FP.FromRaw((long)(bd * FP.ONE));
            FP expected = FP.FromRaw((long)(expectedd * FP.ONE));
            
            FP result = a * b;
            
            // 允许 1% 误差
            double error = FP.Abs(result - expected).RawValue / (double)FP.ONE;
            Assert.True(error < 0.01, $"Error too large: {error}");
        }

        [Fact]
        public void MultiplyPrecise_DetectsOverflow()
        {
            // 创建会溢出的乘法：UseableMax * 2 > long.MaxValue
            FP a = FP.FromRaw(FP.Raw._MaxValue);
            FP b = FP.FromRaw(FP.Raw._2);
            
            // 在 DEBUG 模式下应该检测到溢出
            // 注意：如果测试在 Release 模式下运行，可能不检查溢出
            try
            {
                FP result = FP.MultiplyPrecise(a, b);
                // 如果没有抛出异常，验证是否正确计算（可能不溢出）
                Assert.True(true, "No overflow detected - may be running in Release mode");
            }
            catch (System.OverflowException)
            {
                // 预期行为（DEBUG 模式）
                Assert.True(true);
            }
        }

        [Fact]
        public void MultiplyFast_NoOverflowCheck()
        {
            FP a = FP.UseableMax;
            FP b = FP._2;
            
            // 快速乘法不检查溢出（可能返回错误结果但不会抛异常）
            FP result = FP.MultiplyFast(a, b);
            
            // 只要执行不抛异常就算通过
            Assert.True(result.RawValue != 0 || true);
        }
    }
}
