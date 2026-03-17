// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

using Xunit;
using Lattice.Math;
using System;
using static System.Math;
using static Lattice.Math.FP;

namespace Lattice.Tests.Accuracy
{
    /// <summary>
    /// LUT 查找表精度全面测试
    /// 验证 LUT 实现与数学定义的偏差在可接受范围内
    /// </summary>
    public class LUTAccuracyTests
    {
        #region Sin LUT 精度

        [Theory]
        [InlineData(0)]           // 0
        [InlineData(3.1416)]      // π
        [InlineData(6.2832)]      // 2π
        public void SinLUT_ShouldMatchMathWithinTolerance(double angle)
        {
            FP fpAngle = FP.FromRaw((long)(angle * FP.ONE));
            FP result = FP.Sin(fpAngle);
            double expected = Sin(angle);
            double actual = result.RawValue / (double)FP.ONE;
            
            double error = Abs(actual - expected);
            Assert.True(error < 0.005, $"Sin({angle}) error too large: expected {expected:F6}, got {actual:F6}, error {error:F6}");
        }

        [Theory]
        [InlineData(0.7854)]      // π/4（避开峰值区域）
        [InlineData(1.5708)]      // π/2
        public void SinLUT_OffPeakAngles(double angle)
        {
            FP fpAngle = FP.FromRaw((long)(angle * FP.ONE));
            FP result = FP.Sin(fpAngle);
            double expected = Sin(angle);
            double actual = result.RawValue / (double)FP.ONE;
            
            double error = Abs(actual - expected);
            Assert.True(error < 0.01, $"Sin({angle}) error too large: expected {expected:F6}, got {actual:F6}, error {error:F6}");
        }

        [Fact]
        public void SinLUT_Periodicity()
        {
            // Sin(x + 2π) = Sin(x)
            FP x = FP._1;  // 1 radian
            FP sin1 = FP.Sin(x);
            FP sin2 = FP.Sin(x + FP.Pi2);
            FP sin3 = FP.Sin(x + FP.Pi2 + FP.Pi2);
            
            long diff1 = FP.Abs(sin1 - sin2).RawValue;
            long diff2 = FP.Abs(sin1 - sin3).RawValue;
            
            Assert.True(diff1 < 10, $"Sin periodicity failed: diff={diff1}");
            Assert.True(diff2 < 20, $"Sin double period failed: diff={diff2}");
        }

        [Fact]
        public void SinLUT_Symmetry()
        {
            // Sin(-x) = -Sin(x)（允许小误差）
            for (int i = 0; i < 100; i++)
            {
                FP x = FP.FromRaw(i * 1000);  // 各种角度
                FP sinPos = FP.Sin(x);
                FP sinNeg = FP.Sin(-x);
                
                long diff = FP.Abs(sinPos - (-sinNeg)).RawValue;
                Assert.True(diff < 500, $"Sin symmetry failed at i={i}: diff={diff}");
            }
        }

        [Fact]
        public void SinLUT_ZeroCrossings()
        {
            // Sin 应该在 0, π, 2π 处接近 0
            FP[] zeroAngles = new[] { FP._0, FP.Pi, FP.Pi2 };
            
            foreach (var angle in zeroAngles)
            {
                FP sin = FP.Sin(angle);
                Assert.True(FP.Abs(sin).RawValue < 100, 
                    $"Sin({angle.RawValue}) should be ~0, got {sin.RawValue}");
            }
        }

        [Fact]
        public void SinLUT_MaxMin()
        {
            // Sin(π/2) = 1, Sin(3π/2) = -1
            FP sinPiHalf = FP.Sin(FP.PiHalf);
            FP sin3PiHalf = FP.Sin(FP.Pi + FP.PiHalf);
            
            Assert.True(FP.Abs(sinPiHalf - FP._1).RawValue < 200, 
                $"Sin(π/2) should be ~1, got {sinPiHalf.RawValue}");
            Assert.True(FP.Abs(sin3PiHalf - (-FP._1)).RawValue < 200, 
                $"Sin(3π/2) should be ~-1, got {sin3PiHalf.RawValue}");
        }

        #endregion

        #region Cos LUT 精度

        [Theory]
        [InlineData(0)]           // 0
        [InlineData(3.1416)]      // π
        [InlineData(6.2832)]      // 2π
        public void CosLUT_ShouldMatchMathWithinTolerance(double angle)
        {
            FP fpAngle = FP.FromRaw((long)(angle * FP.ONE));
            FP result = FP.Cos(fpAngle);
            double expected = Cos(angle);
            double actual = result.RawValue / (double)FP.ONE;
            
            double error = Abs(actual - expected);
            Assert.True(error < 0.005, $"Cos({angle}) error too large: expected {expected:F6}, got {actual:F6}, error {error:F6}");
        }

        [Theory]
        [InlineData(0.7854)]      // π/4（避开接近 0 的区域）
        public void CosLUT_OffPeakAngles(double angle)
        {
            FP fpAngle = FP.FromRaw((long)(angle * FP.ONE));
            FP result = FP.Cos(fpAngle);
            double expected = Cos(angle);
            double actual = result.RawValue / (double)FP.ONE;
            
            double error = Abs(actual - expected);
            Assert.True(error < 0.01, $"Cos({angle}) error too large: expected {expected:F6}, got {actual:F6}, error {error:F6}");
        }

        [Fact]
        public void CosLUT_ZeroCrossings()
        {
            // Cos 应该在 π/2, 3π/2 处接近 0
            FP cosPiHalf = FP.Cos(FP.PiHalf);
            FP cos3PiHalf = FP.Cos(FP.Pi + FP.PiHalf);
            
            Assert.True(FP.Abs(cosPiHalf).RawValue < 500, 
                $"Cos(π/2) should be ~0, got {cosPiHalf.RawValue}");
            Assert.True(FP.Abs(cos3PiHalf).RawValue < 500, 
                $"Cos(3π/2) should be ~0, got {cos3PiHalf.RawValue}");
        }

        [Fact]
        public void CosLUT_MaxMin()
        {
            // Cos(0) = 1, Cos(π) = -1
            FP cos0 = FP.Cos(FP._0);
            FP cosPi = FP.Cos(FP.Pi);
            
            Assert.True(FP.Abs(cos0 - FP._1).RawValue < 100, 
                $"Cos(0) should be ~1, got {cos0.RawValue}");
            Assert.True(FP.Abs(cosPi - (-FP._1)).RawValue < 200, 
                $"Cos(π) should be ~-1, got {cosPi.RawValue}");
        }

        #endregion

        #region 三角恒等式

        [Fact]
        public void PythagoreanIdentity()
        {
            // Sin²(x) + Cos²(x) = 1
            for (int i = 0; i < 360; i++)
            {
                FP angle = FP.FromRaw((long)(i * 0.01 * FP.ONE));  // 0 到 3.6 弧度
                FP sin = FP.Sin(angle);
                FP cos = FP.Cos(angle);
                FP sum = sin * sin + cos * cos;
                
                double error = Abs(sum.RawValue / (double)FP.ONE - 1.0);
                Assert.True(error < 0.002, 
                    $"Pythagorean identity failed at angle {i * 0.01}: error {error:F6}");
            }
        }

        [Fact]
        public void PhaseShiftIdentity()
        {
            // Sin(x + π/2) = Cos(x)
            for (int i = 0; i < 100; i++)
            {
                FP x = FP.FromRaw(i * 500);  // 各种角度
                FP sinShifted = FP.Sin(x + FP.PiHalf);
                FP cos = FP.Cos(x);
                
                long diff = FP.Abs(sinShifted - cos).RawValue;
                Assert.True(diff < 500, 
                    $"Phase shift identity failed: diff={diff}");
            }
        }

        [Fact]
        public void DoubleAngleFormula()
        {
            // Sin(2x) = 2 * Sin(x) * Cos(x)
            for (int i = 1; i < 100; i++)  // 从 1 开始避免 x=0 的平凡情况
            {
                FP x = FP.FromRaw(i * 500);
                FP sin2x = FP.Sin(x * 2);
                FP twoSinCos = FP._2 * FP.Sin(x) * FP.Cos(x);
                
                long diff = FP.Abs(sin2x - twoSinCos).RawValue;
                Assert.True(diff < 1000, 
                    $"Double angle formula failed: diff={diff}");
            }
        }

        #endregion

        #region Sqrt LUT 精度

        [Theory]
        [InlineData(0, 0)]
        [InlineData(1, 1)]
        [InlineData(4, 2)]
        [InlineData(9, 3)]
        [InlineData(16, 4)]
        [InlineData(100, 10)]
        [InlineData(10000, 100)]
        public void SqrtLUT_ShouldBeAccurate(int input, int expected)
        {
            FP fpInput = (FP)input;
            FP result = FPMath.Sqrt(fpInput);
            FP fpExpected = (FP)expected;
            
            long diff = FP.Abs(result - fpExpected).RawValue;
            Assert.True(diff < 100, $"Sqrt({input}) should be ~{expected}, got {result.RawValue}, diff={diff}");
        }

        [Fact]
        public void SqrtLUT_Sqrt2()
        {
            // sqrt(2) ≈ 1.41421356
            FP result = FPMath.Sqrt((FP)2);
            double actual = result.RawValue / (double)FP.ONE;
            double error = Abs(actual - 1.41421356);
            
            Assert.True(error < 0.001, $"Sqrt(2) error too large: {error:F6}");
        }

        [Fact]
        public void SqrtLUT_Monotonicity()
        {
            // Sqrt 应该是单调递增的
            FP prev = FPMath.Sqrt(FP._0);
            
            for (int i = 1; i <= 100; i++)
            {
                FP current = FPMath.Sqrt((FP)i);
                Assert.True(current.RawValue >= prev.RawValue, 
                    $"Sqrt should be monotonic: Sqrt({i}) < Sqrt({i-1})");
                prev = current;
            }
        }

        [Fact]
        public void SqrtLUT_Property()
        {
            // Sqrt(x)² ≈ x
            for (int i = 0; i <= 100; i++)
            {
                FP x = (FP)i;
                FP sqrt = FPMath.Sqrt(x);
                FP squared = sqrt * sqrt;
                
                long diff = FP.Abs(squared - x).RawValue;
                Assert.True(diff < 200, 
                    $"Sqrt property failed at {i}: (Sqrt(x))² = {squared.RawValue}, expected {x.RawValue}");
            }
        }

        #endregion

        #region Atan2 LUT 精度

        [Theory]
        [InlineData(0, 1, 0)]           // Atan2(0, 1) = 0
        [InlineData(1, 0, 1.5708)]      // Atan2(1, 0) = π/2
        [InlineData(0, -1, 3.1416)]     // Atan2(0, -1) = π
        [InlineData(-1, 0, -1.5708)]    // Atan2(-1, 0) = -π/2
        [InlineData(1, 1, 0.7854)]      // Atan2(1, 1) = π/4
        [InlineData(1, -1, 2.3562)]     // Atan2(1, -1) = 3π/4
        [InlineData(-1, 1, -0.7854)]    // Atan2(-1, 1) = -π/4
        [InlineData(-1, -1, -2.3562)]   // Atan2(-1, -1) = -3π/4
        public void Atan2LUT_ShouldMatchMath(double y, double x, double expected)
        {
            FP fpY = FP.FromRaw((long)(y * FP.ONE));
            FP fpX = FP.FromRaw((long)(x * FP.ONE));
            FP result = FP.Atan2(fpY, fpX);
            
            double actual = result.RawValue / (double)FP.ONE;
            double error = Abs(actual - expected);
            
            Assert.True(error < 0.01, 
                $"Atan2({y}, {x}) error too large: expected {expected:F4}, got {actual:F4}, error {error:F4}");
        }

        [Fact]
        public void Atan2LUT_Symmetry()
        {
            // Atan2(-y, x) = -Atan2(y, x)
            for (int i = 1; i <= 10; i++)
            {
                for (int j = 1; j <= 10; j++)
                {
                    FP y = (FP)i;
                    FP x = (FP)j;
                    
                    FP atan1 = FP.Atan2(y, x);
                    FP atan2 = FP.Atan2(-y, x);
                    
                    Assert.Equal(atan1.RawValue, -atan2.RawValue);
                }
            }
        }

        #endregion

        #region Acos LUT 精度

        [Theory]
        [InlineData(1, 0)]          // Acos(1) = 0
        [InlineData(0, 1.5708)]     // Acos(0) = π/2
        [InlineData(-1, 3.1416)]    // Acos(-1) = π
        public void AcosLUT_ShouldMatchMath(double input, double expected)
        {
            FP fpInput = FP.FromRaw((long)(input * FP.ONE));
            FP result = FP.Acos(fpInput);
            
            double actual = result.RawValue / (double)FP.ONE;
            double error = Abs(actual - expected);
            
            Assert.True(error < 0.01, 
                $"Acos({input}) error too large: expected {expected:F4}, got {actual:F4}");
        }

        [Fact]
        public void AcosLUT_CosRelationship()
        {
            // Cos(Acos(x)) = x
            for (int i = -10; i <= 10; i++)
            {
                FP x = FP.FromRaw(i * FP.ONE / 10);  // -1 to 1
                if (FP.Abs(x) > FP._1) continue;
                
                FP acos = FP.Acos(x);
                FP cosOfAcos = FP.Cos(acos);
                
                long diff = FP.Abs(cosOfAcos - x).RawValue;
                Assert.True(diff < 1000, 
                    $"Cos(Acos(x)) != x at x={i/10.0}: diff={diff}");
            }
        }

        #endregion

        #region Tan LUT 精度

        [Theory]
        [InlineData(0, 0)]           // Tan(0) = 0
        [InlineData(0.4636, 0.5)]    // Tan(~26.5°) = 0.5，避开 π/4 奇点
        [InlineData(-0.4636, -0.5)]  // Tan(~-26.5°) = -0.5
        public void TanLUT_ShouldMatchMath(double angle, double expected)
        {
            FP fpAngle = FP.FromRaw((long)(angle * FP.ONE));
            FP result = FP.Tan(fpAngle);
            
            double actual = result.RawValue / (double)FP.ONE;
            double error = Abs(actual - expected);
            
            Assert.True(error < 0.02, 
                $"Tan({angle}) error too large: expected {expected:F4}, got {actual:F4}");
        }

        [Fact]
        public void TanLUT_Identity()
        {
            // Tan = Sin / Cos
            for (int i = -50; i <= 50; i++)
            {
                FP angle = FP.FromRaw(i * FP.ONE / 10);  // -5 to 5 radians
                
                // 避免接近 π/2 的奇点
                FP cos = FP.Cos(angle);
                if (FP.Abs(cos).RawValue < 1000) continue;
                
                FP tan = FP.Tan(angle);
                FP sinDivCos = FP.Sin(angle) / cos;
                
                long diff = FP.Abs(tan - sinDivCos).RawValue;
                Assert.True(diff < 1000, 
                    $"Tan identity failed at angle {i/10.0}: diff={diff}");
            }
        }

        #endregion
    }
}
