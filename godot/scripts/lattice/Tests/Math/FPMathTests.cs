using Xunit;
using Lattice.Math;

namespace Lattice.Tests.Math
{
    /// <summary>
    /// FPMath 单元测试
    /// </summary>
    public class FPMathTests
    {
        #region Sqrt

        [Fact]
        public void Sqrt_Zero_ShouldReturnZero()
        {
            FP result = FPMath.Sqrt(FP._0);
            Assert.Equal(0L, result.RawValue);
        }

        [Fact]
        public void Sqrt_One_ShouldReturnOne()
        {
            FP result = FPMath.Sqrt(FP._1);
            Assert.Equal(FP._1.RawValue, result.RawValue);
        }

        [Fact]
        public void Sqrt_Four_ShouldReturnTwo()
        {
            FP result = FPMath.Sqrt((FP)4);
            Assert.Equal(FP._2.RawValue, result.RawValue);
        }

        [Fact]
        public void Sqrt_Nine_ShouldReturnThree()
        {
            FP result = FPMath.Sqrt((FP)9);
            Assert.Equal(3 * FP.ONE, result.RawValue);
        }

        [Fact]
        public void Sqrt_Two_ShouldBeAbout_1_414()
        {
            FP result = FPMath.Sqrt((FP)2);
            // sqrt(2) ≈ 1.4142, * 65536 ≈ 92681
            long expected = (long)(1.4142 * 65536);
            long diff = result.RawValue - expected;
            Assert.True(System.Math.Abs(diff) < 100, $"Sqrt(2) should be ~1.414, got {result.RawValue / 65536.0:F4}");
        }

        [Fact]
        public void Sqrt_Quarter_ShouldReturnHalf()
        {
            FP result = FPMath.Sqrt(FP._0_25);
            Assert.True(FP.Abs(result - FP._0_50).RawValue < 10, $"Sqrt(0.25) should be ~0.5, got {result.RawValue}");
        }

        [Fact]
        public void Sqrt_LargeNumber_ShouldNotOverflow()
        {
            FP large = (FP)1000000;  // 100万
            FP result = FPMath.Sqrt(large);
            Assert.True(result.RawValue > 0, "Sqrt of large number should be positive");
            // sqrt(1000000) = 1000
            Assert.True(FP.Abs(result - 1000).RawValue < 1000, $"Sqrt(1000000) should be ~1000, got {result.RawValue}");
        }

        [Fact]
        public void Sqrt_ShouldBeDeterministic()
        {
            FP value = (FP)2;
            FP r1 = FPMath.Sqrt(value);
            FP r2 = FPMath.Sqrt(value);
            Assert.Equal(r1.RawValue, r2.RawValue);
        }

        #endregion

        #region Sign

        [Theory]
        [InlineData(5, 1)]
        [InlineData(-5, -1)]
        [InlineData(0, 0)]
        public void Sign_ShouldReturnCorrectValue(int input, int expected)
        {
            FP value = (FP)input;
            int result = FPMath.Sign(value);
            Assert.Equal(expected, result);
        }

        #endregion

        #region Abs

        [Theory]
        [InlineData(5, 5)]
        [InlineData(-5, 5)]
        [InlineData(0, 0)]
        public void Abs_ShouldReturnAbsoluteValue(int input, int expected)
        {
            FP value = (FP)input;
            FP result = FPMath.Abs(value);
            Assert.Equal(expected * FP.ONE, result.RawValue);
        }

        #endregion

        #region Min / Max

        [Fact]
        public void Min_ShouldReturnSmaller()
        {
            FP a = (FP)3;
            FP b = (FP)5;
            FP result = FPMath.Min(a, b);
            Assert.Equal(3 * FP.ONE, result.RawValue);
        }

        [Fact]
        public void Max_ShouldReturnLarger()
        {
            FP a = (FP)3;
            FP b = (FP)5;
            FP result = FPMath.Max(a, b);
            Assert.Equal(5 * FP.ONE, result.RawValue);
        }

        #endregion

        #region Clamp

        [Theory]
        [InlineData(5, 0, 10, 5)]    // 在范围内
        [InlineData(-5, 0, 10, 0)]   // 小于最小值
        [InlineData(15, 0, 10, 10)]  // 大于最大值
        [InlineData(0, 0, 10, 0)]    // 等于最小值
        [InlineData(10, 0, 10, 10)]  // 等于最大值
        public void Clamp_ShouldRestrictToRange(int value, int min, int max, int expected)
        {
            FP result = FPMath.Clamp((FP)value, (FP)min, (FP)max);
            Assert.Equal(expected * FP.ONE, result.RawValue);
        }

        [Theory]
        [InlineData(0.5, 0.5)]       // 在范围内
        [InlineData(-0.5, 0)]        // 小于 0
        [InlineData(1.5, 1)]         // 大于 1
        public void Clamp01_ShouldRestrictToZeroOne(double value, double expected)
        {
            FP input = FP.FromRaw((long)(value * 65536));
            FP result = FPMath.Clamp01(input);
            FP expectedFp = FP.FromRaw((long)(expected * 65536));
            Assert.True(FP.Abs(result - expectedFp).RawValue < 10);
        }

        #endregion

        #region Lerp

        [Fact]
        public void Lerp_T0_ShouldBeStart()
        {
            FP a = (FP)10;
            FP b = (FP)20;
            FP result = FPMath.Lerp(a, b, FP._0);
            Assert.Equal(10 * FP.ONE, result.RawValue);
        }

        [Fact]
        public void Lerp_T1_ShouldBeEnd()
        {
            FP a = (FP)10;
            FP b = (FP)20;
            FP result = FPMath.Lerp(a, b, FP._1);
            Assert.Equal(20 * FP.ONE, result.RawValue);
        }

        [Fact]
        public void Lerp_T0_5_ShouldBeMiddle()
        {
            FP a = (FP)10;
            FP b = (FP)20;
            FP result = FPMath.Lerp(a, b, FP._0_50);
            Assert.Equal(15 * FP.ONE, result.RawValue);
        }

        [Fact]
        public void Lerp_Clamped_ShouldNotExceedRange()
        {
            FP a = (FP)10;
            FP b = (FP)20;
            FP result = FPMath.Lerp(a, b, (FP)2);  // t=2，但应该被限制到 1
            Assert.True(result.RawValue <= 20 * FP.ONE, "Lerp should be clamped to end");
        }

        [Fact]
        public void LerpUnclamped_CanExceedRange()
        {
            FP a = (FP)10;
            FP b = (FP)20;
            FP result = FPMath.LerpUnclamped(a, b, (FP)2);  // t=2，不限制
            Assert.Equal(30 * FP.ONE, result.RawValue);  // 10 + (20-10)*2 = 30
        }

        #endregion

        #region Floor / Ceiling / Round

        [Theory]
        [InlineData(3.7, 3)]
        [InlineData(3.2, 3)]
        [InlineData(-3.7, -4)]
        public void Floor_ShouldReturnLargestIntegerLessThanOrEqual(double value, int expected)
        {
            FP input = FP.FromRaw((long)(value * 65536));
            FP result = FPMath.Floor(input);
            Assert.Equal(expected * FP.ONE, result.RawValue);
        }

        [Theory]
        [InlineData(3.7, 4)]
        [InlineData(3.2, 4)]
        [InlineData(-3.7, -3)]
        public void Ceiling_ShouldReturnSmallestIntegerGreaterThanOrEqual(double value, int expected)
        {
            FP input = FP.FromRaw((long)(value * 65536));
            FP result = FPMath.Ceiling(input);
            Assert.Equal(expected * FP.ONE, result.RawValue);
        }

        [Theory]
        [InlineData(3.7, 4)]
        [InlineData(3.2, 3)]
        [InlineData(3.5, 4)]  // 向偶数取整
        [InlineData(2.5, 2)]  // 向偶数取整
        public void Round_ShouldReturnNearestInteger(double value, int expected)
        {
            FP input = FP.FromRaw((long)(value * 65536));
            FP result = FPMath.Round(input);
            Assert.Equal(expected * FP.ONE, result.RawValue);
        }

        #endregion

        #region 确定性测试

        [Fact]
        public void AllOperations_ShouldBeDeterministic()
        {
            FP value = FP.FromRaw((long)(123.456 * 65536));
            
            FP sqrt1 = FPMath.Sqrt(value);
            FP sqrt2 = FPMath.Sqrt(value);
            Assert.Equal(sqrt1.RawValue, sqrt2.RawValue);

            FP abs1 = FPMath.Abs(value);
            FP abs2 = FPMath.Abs(value);
            Assert.Equal(abs1.RawValue, abs2.RawValue);

            FP floor1 = FPMath.Floor(value);
            FP floor2 = FPMath.Floor(value);
            Assert.Equal(floor1.RawValue, floor2.RawValue);
        }

        #endregion
    }
}
