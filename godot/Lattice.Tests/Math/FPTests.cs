using Xunit;
using Lattice.Math;

namespace Lattice.Tests.Math;

/// <summary>
/// FP 定点数基础功能测试
/// </summary>
public class FPTests
{
    #region 构造与转换

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 65536)]           // 1.0 * 65536
    [InlineData(-1, -65536)]         // -1.0 * 65536
    [InlineData(0.5, 32768)]         // 0.5 * 65536
    [InlineData(1.5, 98304)]         // 1.5 * 65536 = 65536 + 32768
    public void FromRaw_ConvertsCorrectly(double expected, long raw)
    {
        FP fp = FP.FromRaw(raw);
        // 验证：RawValue / 65536.0 ≈ expected
        double actual = fp.RawValue / (double)FP.ONE;
        Assert.Equal(expected, actual, 4);
    }

    [Fact]
    public void ImplicitInt_ShouldShiftBy16()
    {
        FP fp = 5;
        Assert.Equal(5 * FP.ONE, fp.RawValue);
    }

    [Fact]
    public void ImplicitLong_ShouldShiftBy16()
    {
        FP fp = 1000L;
        Assert.Equal(1000L * FP.ONE, fp.RawValue);
    }

    [Fact]
    public void ExplicitToInt_ShouldTruncate()
    {
        FP fp = FP.FromRaw(98304);  // 1.5
        int result = (int)fp;
        Assert.Equal(1, result);
    }

    #endregion

    #region 算术运算

    [Theory]
    [InlineData(65536, 32768, 98304)]     // 1.0 + 0.5 = 1.5
    [InlineData(0, 0, 0)]
    [InlineData(-65536, 65536, 0)]        // -1.0 + 1.0 = 0
    public void Addition_ShouldAddRawValues(long a, long b, long expected)
    {
        FP result = FP.FromRaw(a) + FP.FromRaw(b);
        Assert.Equal(expected, result.RawValue);
    }

    [Theory]
    [InlineData(98304, 32768, 65536)]     // 1.5 - 0.5 = 1.0
    [InlineData(0, 65536, -65536)]        // 0 - 1.0 = -1.0
    public void Subtraction_ShouldSubtractRawValues(long a, long b, long expected)
    {
        FP result = FP.FromRaw(a) - FP.FromRaw(b);
        Assert.Equal(expected, result.RawValue);
    }

    [Theory]
    [InlineData(65536, 65536, 65536)]     // 1.0 * 1.0 = 1.0
    [InlineData(32768, 65536, 32768)]     // 0.5 * 1.0 = 0.5
    [InlineData(32768, 32768, 16384)]     // 0.5 * 0.5 = 0.25
    [InlineData(131072, 65536, 131072)]   // 2.0 * 1.0 = 2.0
    public void Multiplication_ShouldAdjustScale(long a, long b, long expected)
    {
        FP result = FP.FromRaw(a) * FP.FromRaw(b);
        Assert.Equal(expected, result.RawValue);
    }

    [Theory]
    [InlineData(98304, 65536, 98304)]     // 1.5 / 1.0 = 1.5
    [InlineData(65536, 32768, 131072)]    // 1.0 / 0.5 = 2.0
    [InlineData(131072, 65536, 131072)]   // 2.0 / 1.0 = 2.0
    public void Division_ShouldAdjustScale(long a, long b, long expected)
    {
        FP result = FP.FromRaw(a) / FP.FromRaw(b);
        Assert.Equal(expected, result.RawValue);
    }

    [Fact]
    public void Multiplication_ByInt_ShouldNotAdjustScale()
    {
        FP fp = FP._1;           // 1.0
        FP result = fp * 5;      // 1.0 * 5 = 5.0
        Assert.Equal(5 * FP.ONE, result.RawValue);
    }

    [Fact]
    public void Division_ByInt_ShouldNotAdjustScale()
    {
        FP fp = FP.FromRaw(10 * FP.ONE);  // 10.0
        FP result = fp / 2;                // 10.0 / 2 = 5.0
        Assert.Equal(5 * FP.ONE, result.RawValue);
    }

    #endregion

    #region 一元运算

    [Theory]
    [InlineData(65536, -65536)]     // -(1.0) = -1.0
    [InlineData(-65536, 65536)]     // -(-1.0) = 1.0
    [InlineData(0, 0)]              // -(0) = 0
    public void Negation_ShouldInvertSign(long input, long expected)
    {
        FP result = -FP.FromRaw(input);
        Assert.Equal(expected, result.RawValue);
    }

    #endregion

    #region 比较运算

    [Theory]
    [InlineData(65536, 32768, false)]   // 1.0 > 0.5
    [InlineData(32768, 65536, true)]    // 0.5 < 1.0
    [InlineData(65536, 65536, false)]   // 1.0 !< 1.0
    public void LessThan_ShouldCompareRawValues(long a, long b, bool expected)
    {
        bool result = FP.FromRaw(a) < FP.FromRaw(b);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Equality_ShouldBeTrueForSameRawValue()
    {
        FP a = FP._1;
        FP b = FP.FromRaw(FP.ONE);
        Assert.True(a == b);
        Assert.True(a.Equals(b));
    }

    [Fact]
    public void GetHashCode_ShouldBeSameForEqualValues()
    {
        FP a = FP._1;
        FP b = FP.FromRaw(FP.ONE);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    #endregion

    #region 工具方法

    [Theory]
    [InlineData(65536, 65536)]          // Abs(1.0) = 1.0
    [InlineData(-65536, 65536)]         // Abs(-1.0) = 1.0
    [InlineData(0, 0)]                  // Abs(0) = 0
    public void Abs_ShouldReturnAbsoluteValue(long input, long expected)
    {
        FP result = FP.Abs(FP.FromRaw(input));
        Assert.Equal(expected, result.RawValue);
    }

    [Fact]
    public void Min_ShouldReturnSmaller()
    {
        FP a = FP._0;
        FP b = FP._1;
        Assert.Equal(FP._0, FP.Min(a, b));
    }

    [Fact]
    public void Max_ShouldReturnLarger()
    {
        FP a = FP._0;
        FP b = FP._1;
        Assert.Equal(FP._1, FP.Max(a, b));
    }

    [Theory]
    [InlineData(-1, 0, 1, 0)]           // Clamp(-1, 0, 1) = 0
    [InlineData(2, 0, 1, 1)]            // Clamp(2, 0, 1) = 1
    [InlineData(0.5, 0, 1, 0.5)]        // Clamp(0.5, 0, 1) = 0.5
    public void Clamp_ShouldClampValue(double value, double min, double max, double expected)
    {
        FP v = FP.FromRaw((long)(value * FP.ONE));
        FP mn = FP.FromRaw((long)(min * FP.ONE));
        FP mx = FP.FromRaw((long)(max * FP.ONE));
        FP exp = FP.FromRaw((long)(expected * FP.ONE));

        FP result = FP.Clamp(v, mn, mx);
        Assert.Equal(exp.RawValue, result.RawValue);
    }

    [Fact]
    public void Lerp_ShouldInterpolate()
    {
        FP a = FP._0;
        FP b = FP._1;
        FP t = FP._0_50;  // 0.5

        FP result = FP.Lerp(a, b, t);
        Assert.Equal(FP._0_50.RawValue, result.RawValue);
    }

    [Fact]
    public void MultiplyPrecise_ShouldRoundCorrectly()
    {
        // 测试四舍五入是否更准确
        // 0.1 * 0.2 = 0.02，截断版本可能有误差
        FP a = FP._0_10;   // 0.1
        FP b = FP._0_10;   // 0.2  

        FP precise = FP.MultiplyPrecise(a, b);
        FP normal = a * b;

        // 期望结果：0.02 * 65536 = 1310.72 ≈ 1310 (截断) 或 1311 (四舍五入)
        // 四舍五入应该更接近真实值
        FP expected = FP.FromRaw(1311);  // 0.02 的精确表示

        long diffPrecise = FP.Abs(precise - expected).RawValue;
        long diffNormal = FP.Abs(normal - expected).RawValue;

        Assert.True(diffPrecise <= diffNormal, 
            $"Precise should be closer: precise={precise.RawValue}, normal={normal.RawValue}, expected={expected.RawValue}");
    }

    #endregion

    #region 常量验证

    [Fact]
    public void Pi_ShouldBeApproximate()
    {
        double actual = FP.Pi.RawValue / (double)FP.ONE;
        Assert.True(System.Math.Abs(actual - System.Math.PI) < 0.001);
    }

    [Fact]
    public void Constants_ShouldHaveCorrectRawValues()
    {
        Assert.Equal(0, FP._0.RawValue);
        Assert.Equal(FP.ONE, FP._1.RawValue);
        Assert.Equal(FP.ONE * 2, FP._2.RawValue);
        Assert.Equal(FP.ONE >> 1, FP._0_50.RawValue);
    }

    [Fact]
    public void RawConstants_ShouldMatchStaticProperties()
    {
        Assert.Equal(FP.Raw._0, FP._0.RawValue);
        Assert.Equal(FP.Raw._1, FP._1.RawValue);
        Assert.Equal(FP.Raw._0_50, FP._0_50.RawValue);
        Assert.Equal(FP.Raw._PI, FP.Pi.RawValue);
    }

    #endregion

    #region 字符串解析

    [Theory]
    [InlineData("0", 0)]
    [InlineData("1", 65536)]
    [InlineData("1.5", 98304)]
    [InlineData("0.5", 32768)]
    [InlineData("-1", -65536)]
    [InlineData("-1.5", -98304)]
    [InlineData("123.456", 8090812)]  // 123.456 * 65536 ≈ 8090427
    public void FromString_ShouldParseCorrectly(string input, long expectedRaw)
    {
        FP result = FP.FromString(input);
        // 允许小误差（截断误差）
        long diff = System.Math.Abs(result.RawValue - expectedRaw);
        Assert.True(diff <= 1, $"Expected {expectedRaw}, got {result.RawValue}");
    }

    [Theory]
    [InlineData(0, "0.0000")]
    [InlineData(65536, "1.0000")]
    [InlineData(32768, "0.5000")]
    public void ToString_ShouldFormatCorrectly(long raw, string expected)
    {
        FP fp = FP.FromRaw(raw);
        string result = fp.ToString();
        Assert.Equal(expected, result);
    }

    #endregion
}
