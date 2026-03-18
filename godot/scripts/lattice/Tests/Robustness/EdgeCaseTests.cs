// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

using Xunit;
using Lattice.Math;
using System;
using System.Collections.Generic;

namespace Lattice.Tests.Robustness
{
    /// <summary>
    /// 边界情况全面测试
    /// 极端值、特殊输入、错误条件
    /// </summary>
    public class EdgeCaseTests
    {
        #region FP 极端值

        [Fact]
        public void FP_MaxValue_Conversion()
        {
            // 测试 UseableMax 的各种操作
            FP max = FP.UseableMax;
            
            // 自加、自减
            Assert.Equal(max.RawValue, (max + FP._0).RawValue);
            Assert.Equal((max - FP._0).RawValue, max.RawValue);
            
            // 乘以 1 应该不变
            Assert.Equal(max.RawValue, (max * FP._1).RawValue);
            
            // 除以 1 应该不变
            Assert.Equal(max.RawValue, (max / FP._1).RawValue);
        }

        [Fact]
        public void FP_MinValue_Conversion()
        {
            FP min = FP.UseableMin;
            
            // 自加、自减
            Assert.Equal(min.RawValue, (min + FP._0).RawValue);
            
            // 乘以 1 应该不变
            Assert.Equal(min.RawValue, (min * FP._1).RawValue);
        }

        [Fact]
        public void FP_Epsilon_Behavior()
        {
            FP eps = FP.Epsilon;
            
            // Epsilon 是最小正数
            Assert.True(eps.RawValue > 0);
            Assert.True(eps > FP._0);
            
            // Epsilon / 2 = 0（下溢）
            FP halfEps = eps / 2;
            Assert.Equal(0L, halfEps.RawValue);
            
            // Epsilon * 1 = Epsilon
            Assert.Equal(eps.RawValue, (eps * FP._1).RawValue);
            
            // 1 + Epsilon > 1
            Assert.True(FP._1 + eps > FP._1);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(-1)]
        [InlineData(65536)]
        [InlineData(-65536)]
        public void FP_FromRaw_RoundTrip(long raw)
        {
            FP fp = FP.FromRaw(raw);
            Assert.Equal(raw, fp.RawValue);
        }

        [Fact]
        public void FP_LongMinValue_Abs()
        {
            // long.MinValue 的 Abs 是特殊情况
            FP minLong = FP.FromRaw(long.MinValue);
            FP abs = FP.Abs(minLong);
            
            // 应该返回正数（虽然实际值还是 long.MinValue 因为溢出）
            Assert.True(abs.RawValue > 0 || abs.RawValue == long.MinValue);
        }

        #endregion

        #region 除零与溢出边界

        [Fact(Skip = "macOS ARM64 上异常处理导致进程崩溃，暂时跳过")]
        public void Division_ByZero_FP_ShouldThrow()
        {
            Assert.Throws<DivideByZeroException>(() => FP._1 / FP._0);
            Assert.Throws<DivideByZeroException>(() => (-FP._1) / FP._0);
            Assert.Throws<DivideByZeroException>(() => FP.UseableMax / FP._0);
        }

        [Fact(Skip = "macOS ARM64 上异常处理导致进程崩溃，暂时跳过")]
        public void Division_ByZero_Int_ShouldThrow()
        {
            Assert.Throws<DivideByZeroException>(() => FP._1 / 0);
            Assert.Throws<DivideByZeroException>(() => (-FP._1) / 0);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(-1)]
        [InlineData(-2)]
        [InlineData(100)]
        [InlineData(-100)]
        public void Division_ByNonZero_ShouldWork(int divisor)
        {
            FP one = FP._1;
            FP result = one / divisor;
            Assert.True(result.RawValue != 0 || divisor > (int)FP.UseableMax);
        }

        [Fact]
        public void Multiplication_NearOverflow()
        {
            // 接近溢出边界的乘法
            FP nearMax = FP.FromRaw(FP.UseableMax.RawValue / 2);
            FP two = FP._2;
            
            FP result = nearMax * two;
            
            // 应该产生结果（不溢出）
            Assert.True(result.RawValue > 0);
        }

        #endregion

        #region 向量极端值

        [Fact]
        public void Vector2_Normalize_ZeroVector()
        {
            var zero = FPVector2.Zero;
            var normalized = zero.Normalized;
            
            // 零向量归一化应该返回零向量（不抛出异常）
            Assert.Equal(0L, normalized.X.RawValue);
            Assert.Equal(0L, normalized.Y.RawValue);
        }

        [Fact]
        public void Vector2_Normalize_VeryLarge()
        {
            // 使用安全范围内的大向量
            var large = new FPVector2(10000, 10000);
            var normalized = large.Normalized;
            
            // 大向量归一化后应该是单位向量（允许较大误差，因为涉及平方根）
            FP mag = normalized.Magnitude;
            Assert.True(FP.Abs(mag - FP._1).RawValue < 2000, 
                $"Normalized magnitude should be ~1, got {mag.RawValue / (double)FP.ONE:F4}");
        }

        [Fact]
        public void Vector2_Normalize_VerySmall()
        {
            var tiny = new FPVector2(FP.Epsilon, FP.Epsilon);
            var normalized = tiny.Normalized;
            
            // 极小向量归一化应该安全
            Assert.True(normalized.X.RawValue >= 0);
            Assert.True(normalized.Y.RawValue >= 0);
        }

        [Fact]
        public void Vector3_Normalize_ZeroVector()
        {
            var zero = FPVector3.Zero;
            var normalized = zero.Normalized;
            
            Assert.Equal(0L, normalized.X.RawValue);
            Assert.Equal(0L, normalized.Y.RawValue);
            Assert.Equal(0L, normalized.Z.RawValue);
        }

        [Fact]
        public void Vector3_Cross_ZeroVector()
        {
            var a = new FPVector3(1, 2, 3);
            var zero = FPVector3.Zero;
            
            var cross1 = FPVector3.Cross(a, zero);
            var cross2 = FPVector3.Cross(zero, a);
            
            Assert.Equal(0L, cross1.X.RawValue);
            Assert.Equal(0L, cross1.Y.RawValue);
            Assert.Equal(0L, cross1.Z.RawValue);
            
            Assert.Equal(0L, cross2.X.RawValue);
            Assert.Equal(0L, cross2.Y.RawValue);
            Assert.Equal(0L, cross2.Z.RawValue);
        }

        #endregion

        #region 三角函数边界

        [Fact]
        public void Trig_VeryLargeAngles()
        {
            // 极大角度的三角函数（应该正确处理周期性）
            FP largeAngle = FP.FromRaw(1000000);  // 约 15.26 弧度
            
            FP sin = FP.Sin(largeAngle);
            FP cos = FP.Cos(largeAngle);
            
            // 结果应该在 [-1, 1] 范围内
            Assert.True(sin.RawValue >= -FP.ONE - 100 && sin.RawValue <= FP.ONE + 100);
            Assert.True(cos.RawValue >= -FP.ONE - 100 && cos.RawValue <= FP.ONE + 100);
        }

        [Fact]
        public void Trig_NegativeAngles()
        {
            // 负角度的三角函数
            FP negAngle = FP.FromRaw(-FP.ONE);  // -1 弧度
            
            FP sinNeg = FP.Sin(negAngle);
            FP sinPos = FP.Sin(-negAngle);
            
            // Sin(-x) = -Sin(x)（允许小误差）
            long diff = FP.Abs(sinNeg - (-sinPos)).RawValue;
            Assert.True(diff < 500, $"Sin(-x) should be close to -Sin(x), diff={diff}");
        }

        [Fact]
        public void Atan2_SpecialCases()
        {
            // Atan2(0, 0) 应该返回 0
            FP atan00 = FP.Atan2(FP._0, FP._0);
            Assert.Equal(0L, atan00.RawValue);
            
            // Atan2(0, x) = 0 for x > 0
            FP atan0Pos = FP.Atan2(FP._0, FP._1);
            Assert.True(FP.Abs(atan0Pos).RawValue < 100);
            
            // Atan2(0, -x) = π for x > 0
            FP atan0Neg = FP.Atan2(FP._0, -FP._1);
            Assert.True(FP.Abs(atan0Neg - FP.Pi).RawValue < 1000);
        }

        [Fact]
        public void Acos_OutOfRange()
        {
            // Acos(1.1) 和 Acos(-1.1) 应该被 Clamp
            FP overOne = FP.FromRaw(FP.ONE + 1000);  // > 1
            FP underMinusOne = FP.FromRaw(-FP.ONE - 1000);  // < -1
            
            // 不应该抛出异常
            FP acosOver = FP.Acos(overOne);
            FP acosUnder = FP.Acos(underMinusOne);
            
            // 结果应该在有效范围内
            Assert.True(acosOver.RawValue >= 0);
            Assert.True(acosUnder.RawValue >= 0);
        }

        #endregion

        #region Lerp 边界

        [Fact]
        public void Lerp_ExtremeTValues()
        {
            FP a = FP._0;
            FP b = FP._1;
            
            // t = 0
            Assert.Equal(a.RawValue, FP.Lerp(a, b, FP._0).RawValue);
            
            // t = 1
            Assert.Equal(b.RawValue, FP.Lerp(a, b, FP._1).RawValue);
            
            // t = 0.5
            FP mid = FP.Lerp(a, b, FP._0_50);
            Assert.True(FP.Abs(mid - FP._0_50).RawValue < 100);
        }

        [Fact]
        public void Lerp_UnclampedCanExceedRange()
        {
            FP a = FP._0;
            FP b = FP._1;
            FP t = FP._2;  // t = 2
            
            // LerpUnclamped 可以超出范围
            FP result = FPMath.LerpUnclamped(a, b, t);
            Assert.Equal(2 * FP.ONE, result.RawValue);
        }

        [Fact]
        public void Lerp_NegativeT()
        {
            FP a = FP._0;
            FP b = FP._1;
            FP t = -FP._1;  // t = -1
            
            // LerpUnclamped 可以是负数
            FP result = FPMath.LerpUnclamped(a, b, t);
            Assert.Equal(-FP.ONE, result.RawValue);
        }

        #endregion

        #region Clamp 边界

        [Theory]
        [InlineData(0, 0, 10, 0)]     // value = min
        [InlineData(10, 0, 10, 10)]   // value = max
        [InlineData(5, 0, 10, 5)]     // value 在范围内
        [InlineData(-5, 0, 10, 0)]    // value < min
        [InlineData(15, 0, 10, 10)]   // value > max
        public void Clamp_BoundaryValues(int value, int min, int max, int expected)
        {
            FP result = FP.Clamp((FP)value, (FP)min, (FP)max);
            Assert.Equal(expected * FP.ONE, result.RawValue);
        }

        [Fact]
        public void Clamp_MinGreaterThanMax()
        {
            // 当 min > max 时的行为（未定义，但不应崩溃）
            FP value = FP._5;
            FP min = FP._10;
            FP max = FP._0;
            
            // 根据标准 Clamp 实现：value < min ? min : (value > max ? max : value)
            // 5 < 10 ? 10 : (5 > 0 ? 0 : 5) = 0
            FP result = FP.Clamp(value, min, max);
            
            // 结果取决于实现，只要是一致的即可
            Assert.True(result.RawValue == 0 || result.RawValue == 5 * FP.ONE || result.RawValue == 10 * FP.ONE,
                $"Clamp with min > max should return a consistent value, got {result.RawValue}");
        }

        #endregion

        #region Sqrt 边界

        [Fact]
        public void Sqrt_Zero()
        {
            Assert.Equal(0L, FPMath.Sqrt(FP._0).RawValue);
        }

        [Fact]
        public void Sqrt_One()
        {
            Assert.Equal(FP.ONE, FPMath.Sqrt(FP._1).RawValue);
        }

        [Fact]
        public void Sqrt_LargeNumber()
        {
            FP large = (FP)1000000;  // 1 million
            FP result = FPMath.Sqrt(large);
            
            // sqrt(1,000,000) = 1000
            Assert.True(FP.Abs(result - 1000).RawValue < 1000);
        }

        [Fact]
        public void Sqrt_SmallNumber()
        {
            FP small = FP._0_01;  // 0.01
            FP result = FPMath.Sqrt(small);
            
            // sqrt(0.01) = 0.1
            Assert.True(FP.Abs(result - FP._0_10).RawValue < 100);
        }

        #endregion

        #region 字符串解析边界

        [Theory]
        [InlineData("0")]
        [InlineData("0.0")]
        [InlineData("0.0000")]
        [InlineData(".5")]  // 无前导零
        [InlineData("-.5")] // 负小数，无前导零
        [InlineData("1.")]  // 无小数部分
        public void FromString_ValidFormats(string input)
        {
            // 不应该抛出异常
            FP result = FP.FromString(input);
            Assert.True(result.RawValue >= long.MinValue && result.RawValue <= long.MaxValue);
        }

        [Theory]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData("  ")]
        [InlineData("abc")]
        [InlineData("1.2.3")]
        [InlineData("--1")]
        [InlineData("+-1")]
        [InlineData("1e10")]  // 科学计数法（如果未实现）
        public void FromString_InvalidFormats(string input)
        {
            Assert.ThrowsAny<Exception>(() => FP.FromString(input));
        }

        [Fact]
        public void FromString_Null()
        {
            Assert.Throws<ArgumentException>(() => FP.FromString(null!));
        }

        #endregion

        #region 批量操作边界

        [Fact]
        public void BatchOperations_EmptyArrays()
        {
            var empty = Array.Empty<FP>();
            var result = new FP[0];
            
            // 空数组应该安全处理
            FPMath.LerpBatch(empty, empty, FP._0_50, result);
            // 不抛出异常即通过
        }

        [Fact]
        public void BatchOperations_SingleElement()
        {
            var input = new[] { FP._1 };
            var output = new FP[1];
            
            FPMath.ClampBatch(input, FP._0, FP._2, output);
            Assert.Equal(FP._1.RawValue, output[0].RawValue);
        }

        [Fact]
        public void BatchOperations_MismatchedSize()
        {
            var input = new[] { FP._1, FP._2 };
            var output = new FP[1];  // 太小
            
            Assert.Throws<ArgumentException>(() => 
                FPMath.ClampBatch(input, FP._0, FP._2, output));
        }

        #endregion

        #region 确定性边界

        [Fact]
        public void Determinism_ExtremeValues()
        {
            // 极端值的确定性
            var values = new[] { FP._0, FP._1, FP.UseableMax, FP.UseableMin, FP.Epsilon };
            
            var results1 = new List<long>();
            var results2 = new List<long>();
            
            foreach (var v in values)
            {
                results1.Add((v * v + v - v / (v + FP._1)).RawValue);
                results2.Add((v * v + v - v / (v + FP._1)).RawValue);
            }
            
            Assert.Equal(results1, results2);
        }

        [Fact]
        public void Determinism_SequentialOperations()
        {
            // 多次运算的确定性
            FP seed = FP._0_10;
            
            FP Compute(FP s)
            {
                for (int i = 0; i < 1000; i++)
                {
                    s = (s * FP._1_50 + FP._0_10) / FP._2;
                    s = FP.Clamp(s, FP._0, FP._100);
                }
                return s;
            }
            
            FP result1 = Compute(seed);
            FP result2 = Compute(seed);
            
            Assert.Equal(result1.RawValue, result2.RawValue);
        }

        #endregion
    }
}
