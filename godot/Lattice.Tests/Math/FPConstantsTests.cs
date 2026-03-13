// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

using Xunit;
using Lattice.Math;

namespace Lattice.Tests.Math
{
    /// <summary>
    /// FP 常量测试
    /// </summary>
    public class FPConstantsTests
    {
        [Theory]
        [InlineData(0.01, FP.Raw._0_01)]
        [InlineData(0.02, FP.Raw._0_02)]
        [InlineData(0.05, FP.Raw._0_05)]
        [InlineData(0.33, FP.Raw._0_33)]
        [InlineData(0.50, FP.Raw._0_50)]
        [InlineData(0.75, FP.Raw._0_75)]
        [InlineData(0.99, FP.Raw._0_99)]
        [InlineData(1.01, FP.Raw._1_01)]
        [InlineData(1.25, FP.Raw._1_25)]
        [InlineData(1.50, FP.Raw._1_50)]
        public void DecimalConstants_AreCorrect(double expected, long raw)
        {
            FP fp = FP.FromRaw(raw);
            double actual = fp.RawValue / (double)FP.ONE;
            
            // 允许 2% 误差（某些近似值如 0.33）
            double error = expected == 0 ? System.Math.Abs(actual) : System.Math.Abs(actual - expected) / expected;
            Assert.True(error < 0.02, $"Constant mismatch: expected {expected}, got {actual}, error {error:P}");
        }

        [Theory]
        [InlineData(1.0, FP.Raw._1)]
        [InlineData(2.0, FP.Raw._2)]
        [InlineData(10.0, FP.Raw._10)]
        [InlineData(100.0, FP.Raw._100)]
        [InlineData(1000.0, FP.Raw._1000)]
        public void IntegerConstants_AreExact(int expected, long raw)
        {
            FP fp = FP.FromRaw(raw);
            int actual = (int)fp;
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void AngleConstants_AreCorrect()
        {
            // 90 degrees in radians ≈ 1.5708
            double rad90 = FP.Raw.Rad_90 / (double)FP.ONE;
            Assert.True(System.Math.Abs(rad90 - 1.5708) < 0.001);
            
            // 45 degrees in radians ≈ 0.7854
            double rad45 = FP.Raw.Rad_45 / (double)FP.ONE;
            Assert.True(System.Math.Abs(rad45 - 0.7854) < 0.001);
            
            // 30 degrees in radians ≈ 0.5236
            double rad30 = FP.Raw.Rad_30 / (double)FP.ONE;
            Assert.True(System.Math.Abs(rad30 - 0.5236) < 0.001);
        }

        [Fact]
        public void EpsilonConstants_AreOrdered()
        {
            // EN1 > EN2 > EN3 > EN4 > EN5
            Assert.True(FP.Raw.EN1 > FP.Raw.EN2);
            Assert.True(FP.Raw.EN2 > FP.Raw.EN3);
            Assert.True(FP.Raw.EN3 > FP.Raw.EN4);
            Assert.True(FP.Raw.EN4 > FP.Raw.EN5);
            Assert.True(FP.Raw.EN5 > 0);
        }

        [Fact]
        public void PiConstants_AreConsistent()
        {
            // 2π = π * 2
            Assert.Equal(FP.Raw._2Pi, FP.Raw._PI * 2);
            
            // π/2 = π / 2
            Assert.Equal(FP.Raw._PiHalf, FP.Raw._PI / 2);
            
            // Rad_180 = π
            Assert.Equal(FP.Raw.Rad_180, FP.Raw._PI);
            
            // Rad_90 ≈ π/2 (允许 1 的误差，因为常量可能是独立计算的)
            long diff = FP.Raw.Rad_90 - FP.Raw._PiHalf;
            Assert.True(System.Math.Abs(diff) <= 1, $"Rad_90 ({FP.Raw.Rad_90}) should be close to _PiHalf ({FP.Raw._PiHalf})");
        }
    }
}
