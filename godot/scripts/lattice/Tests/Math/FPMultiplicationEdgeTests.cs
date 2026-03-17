// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

using Xunit;
using Lattice.Math;

namespace Lattice.Tests.Math
{
    /// <summary>
    /// 乘法舍入边界测试
    /// 验证四舍五入在各种边界条件下的正确性
    /// </summary>
    public class FPMultiplicationEdgeTests
    {
        #region 基本舍入测试

        [Theory]
        [InlineData(0.5, 0.5, 0.25)]           // 精确值
        [InlineData(0.1, 0.1, 0.01)]           // 需要舍入
        [InlineData(0.3, 0.3, 0.09)]           // 0.09 近似
        [InlineData(1.0, 1.0, 1.0)]            // 单位元
        [InlineData(2.5, 2.0, 5.0)]            // 整数结果
        public void Multiplication_RoundingAccuracy(double a, double b, double expected)
        {
            FP fa = FP.FromRaw((long)(a * FP.ONE));
            FP fb = FP.FromRaw((long)(b * FP.ONE));
            FP fexpected = FP.FromRaw((long)(expected * FP.ONE));
            
            FP result = fa * fb;
            
            // 误差应小于 0.5 LSB (0.0000076)
            double error = FP.Abs(result - fexpected).RawValue / (double)FP.ONE;
            Assert.True(error < 0.00001, $"Error {error} too large for {a} * {b}");
        }

        #endregion

        #region 边界值测试

        [Fact]
        public void Multiply_ByZero_ReturnsZero()
        {
            FP a = FP.FromRaw(12345);
            Assert.Equal(FP._0.RawValue, (a * FP._0).RawValue);
            Assert.Equal(FP._0.RawValue, (FP._0 * a).RawValue);
        }

        [Fact]
        public void Multiply_ByOne_ReturnsSame()
        {
            FP a = FP.FromRaw(12345);
            Assert.Equal(a.RawValue, (a * FP._1).RawValue);
            Assert.Equal(a.RawValue, (FP._1 * a).RawValue);
        }

        [Fact]
        public void Multiply_NegativeNumbers()
        {
            FP a = -FP._5;
            FP b = FP._3;
            
            FP result = a * b;
            Assert.True(result.RawValue < 0, "-5 * 3 should be negative");
            
            // 近似 -15
            Assert.True(FP.Abs(result - (-(FP._5 * FP._3))).RawValue < 100);
        }

        [Fact]
        public void Multiply_TwoNegatives_ReturnsPositive()
        {
            FP a = -FP._5;
            FP b = -FP._3;
            
            FP result = a * b;
            Assert.True(result.RawValue > 0, "-5 * -3 should be positive");
        }

        [Fact]
        public void Multiply_SmallestNonZero()
        {
            // Epsilon * 1 = Epsilon
            FP result = FP.Epsilon * FP._1;
            Assert.Equal(FP.Epsilon.RawValue, result.RawValue);
            
            // Epsilon * Epsilon = 0（下溢）
            FP result2 = FP.Epsilon * FP.Epsilon;
            Assert.Equal(0L, result2.RawValue);
        }

        #endregion

        #region 精度边界测试

        [Fact]
        public void Multiply_HalfLSBBoundary()
        {
            // 构造一个需要舍入的场景
            // 0.0000152587890625 * 0.0000152587890625
            // 精确值：0.0000000002328306436538696...
            // 截断：0
            // 四舍五入：0
            
            FP tiny = FP.FromRaw(1);  // 最小精度单位
            FP result = tiny * tiny;
            
            // 应该下溢为 0
            Assert.Equal(0L, result.RawValue);
        }

        [Fact]
        public void Multiply_RoundingVsTruncationComparison()
        {
            // 选择一个乘积小数部分接近 0.5 的值
            // 0.3333... * 3 = 0.9999...
            FP a = FP.FromRaw(FP.Raw._0_33);  // 0.33
            FP b = FP._3;
            
            FP rounded = a * b;
            FP truncated = FP.MultiplyFast(a, b);
            
            // 四舍五入应该更接近 0.99
            FP expected = FP.FromRaw(FP.Raw._0_99);
            long diffRounded = FP.Abs(rounded - expected).RawValue;
            long diffTruncated = FP.Abs(truncated - expected).RawValue;
            
            Assert.True(diffRounded <= diffTruncated + 1, 
                $"Rounding should be closer or equal: rounded diff={diffRounded}, truncated diff={diffTruncated}");
        }

        #endregion

        #region 对称性和一致性

        [Fact]
        public void Multiply_IsCommutative()
        {
            FP a = FP.FromRaw(12345);
            FP b = FP.FromRaw(67890);
            
            FP ab = a * b;
            FP ba = b * a;
            
            Assert.Equal(ab.RawValue, ba.RawValue);
        }

        [Fact]
        public void Multiply_IsAssociative_Approximately()
        {
            // 注意：定点数乘法不满足严格结合律
            // (a * b) * c ≈ a * (b * c)
            
            FP a = FP._2;
            FP b = FP._3;
            FP c = FP._4;
            
            FP abc1 = (a * b) * c;  // (2*3)*4 = 24
            FP abc2 = a * (b * c);  // 2*(3*4) = 24
            
            // 允许小误差
            long diff = FP.Abs(abc1 - abc2).RawValue;
            Assert.True(diff < 10, $"Associative error: {diff}");
        }

        [Fact]
        public void Multiply_Distributive_Approximately()
        {
            // a * (b + c) ≈ a * b + a * c
            FP a = FP._2;
            FP b = FP._3;
            FP c = FP._4;
            
            FP left = a * (b + c);   // 2 * (3+4) = 14
            FP right = a * b + a * c; // 2*3 + 2*4 = 6 + 8 = 14
            
            long diff = FP.Abs(left - right).RawValue;
            Assert.True(diff < 100, $"Distributive error: {diff}");
        }

        #endregion

        #region 溢出测试

        [Theory]
        [InlineData(10000, 10000)]      // 1亿，安全
        [InlineData(30000, 30000)]      // 9亿，接近安全上限
        public void Multiply_SafeRange_NoOverflow(int a, int b)
        {
            FP fa = a;
            FP fb = b;
            
            // 不应抛出异常
            FP result = fa * fb;
            
            // 验证结果正确
            long expectedRaw = ((long)a * FP.ONE) * ((long)b * FP.ONE) >> 16;
            Assert.Equal(expectedRaw, result.RawValue);
        }

        #endregion
    }
}
