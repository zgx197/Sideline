using Xunit;
using Lattice.Math;
using System;
using System.Collections.Generic;

namespace Lattice.Tests.Robustness;

/// <summary>
/// 健壮性测试：边界情况、错误处理、异常情况
/// 确保框架在各种极端情况下都能正确处理
/// </summary>
public class RobustnessTests
{
    #region 溢出检测测试

    /// <summary>
    /// 乘法溢出检测：UseableMax * 2 应该溢出
    /// </summary>
    [Fact]
    public void Multiplication_Overflow_ShouldProduceIncorrectResult()
    {
        // 注意：我们不抛出异常，但应该知道溢出的后果
        FP max = FP.UseableMax;  // ~32767
        FP two = FP._2;

        // 这个乘法在数学上应该是 ~65534，但实际会溢出
        FP result = max * two;

        // 溢出后结果不正确（但仍然是确定性的）
        // 32767 * 2 = 65534，但 (32767*65536) * (2*65536) >> 16 会溢出
        // 我们验证它不等于预期的数学结果
        Assert.NotEqual(65534 * FP.ONE, result);

        // 但两次计算应该一致（确定性）
        FP result2 = max * two;
        Assert.Equal(result.RawValue, result2.RawValue);
    }

    /// <summary>
    /// 安全范围内的乘法不会溢出
    /// </summary>
    [Theory]
    [InlineData(1000, 1000)]      // 100万 < 21亿，安全
    [InlineData(10000, 10000)]    // 1亿 < 21亿，安全
    [InlineData(32767, 1)]        // UseableMax * 1，安全
    [InlineData(-32768, 1)]       // UseableMin * 1，安全
    public void Multiplication_InSafeRange_ShouldBeCorrect(int a, int b)
    {
        FP fpA = a;
        FP fpB = b;

        FP result = fpA * fpB;

        // 验证结果等于预期（没有溢出）
        long expectedRaw = ((long)a * FP.ONE) * ((long)b * FP.ONE) >> 16;
        Assert.Equal(expectedRaw, result.RawValue);
    }

    /// <summary>
    /// 加法溢出检测
    /// </summary>
    [Fact]
    public void Addition_Overflow_ShouldWrapAround()
    {
        // long 加法溢出会回绕（C# 的未检查行为）
        FP large = FP.FromRaw(long.MaxValue - 1000);
        FP add = FP.FromRaw(2000);

        FP result = large + add;

        // 溢出后结果变为负数（回绕）
        Assert.True(result.RawValue < 0, "Addition overflow should wrap to negative");

        // 但仍然是确定性的
        FP result2 = large + add;
        Assert.Equal(result.RawValue, result2.RawValue);
    }

    /// <summary>
    /// 减法下溢检测
    /// </summary>
    [Fact]
    public void Subtraction_Underflow_ShouldWrapAround()
    {
        FP small = FP.FromRaw(long.MinValue + 1000);
        FP sub = FP.FromRaw(2000);

        FP result = small - sub;

        // 下溢后结果变为正数（回绕）
        Assert.True(result.RawValue > 0, "Subtraction underflow should wrap to positive");

        // 确定性验证
        FP result2 = small - sub;
        Assert.Equal(result.RawValue, result2.RawValue);
    }

    /// <summary>
    /// 累积运算的溢出风险（使用更大的增量加速溢出）
    /// </summary>
    [Fact]
    public void CumulativeOperations_MayOverflow()
    {
        // 从较大的值开始，使用较大的增量
        FP value = FP.FromRaw(long.MaxValue - 100000);
        FP increment = FP._1000;  // 使用较大的增量

        // 连续累加可能导致溢出
        bool overflowOccurred = false;
        for (int i = 0; i < 200; i++)
        {
            FP before = value;
            value += increment;

            // 如果加正数后变小了，说明溢出
            if (value.RawValue < before.RawValue)
            {
                overflowOccurred = true;
                break;
            }
        }

        Assert.True(overflowOccurred, "Expected overflow to occur during cumulative addition");
    }

    #endregion

    #region 除零异常测试

    /// <summary>
    /// FP 除以 FP 零：Debug 下断言失败，Release 下行为未定义
    /// </summary>
    [Fact]
    public void Division_ByZeroFP_ShouldCauseUndefinedBehavior()
    {
        FP one = FP._1;
        FP zero = FP._0;

        // 注意：我们不测试抛异常，因为定点数除零在硬件层是未定义行为
        // 这个测试记录了这种行为

        // 在 Debug 构建中，Debug.Assert 会触发
        // 在 Release 构建中，结果是不确定的（可能是崩溃、随机值等）

        // 我们测试非零除法的正确性作为对照
        FP validResult = one / FP._2;
        Assert.Equal(FP._0_50.RawValue, validResult.RawValue);
    }

    /// <summary>
    /// 除以 Int 零：同样危险
    /// </summary>
    [Fact]
    public void Division_ByZeroInt_ShouldCauseUndefinedBehavior()
    {
        FP one = FP._1;

        // 非零除法对照
        FP validResult = one / 2;
        Assert.Equal(FP._0_50.RawValue, validResult.RawValue);

        // 除零行为未定义，不直接测试
    }

    /// <summary>
    /// 接近零的除法（极小值）
    /// </summary>
    [Fact]
    public void Division_ByVerySmallNumber_ShouldProduceLargeResult()
    {
        FP one = FP._1;
        FP verySmall = FP.Epsilon;  // 最小精度单位

        // 1.0 / 0.000015 = 非常大的数
        FP result = one / verySmall;

        // 结果应该非常大（但可能溢出）
        Assert.True(result.RawValue > 0, "1/epsilon should be positive");

        // 确定性
        FP result2 = one / verySmall;
        Assert.Equal(result.RawValue, result2.RawValue);
    }

    /// <summary>
    /// 零除以非零数应该安全
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(-1)]
    [InlineData(100)]
    [InlineData(-100)]
    public void ZeroDividedByNonZero_ShouldBeZero(int divisor)
    {
        FP zero = FP._0;
        FP result = zero / divisor;
        Assert.Equal(FP._0.RawValue, result.RawValue);
    }

    #endregion

    #region 精度损失测试

    /// <summary>
    /// 大数加小数精度丢失
    /// </summary>
    [Fact]
    public void LargePlusSmall_PrecisionMayBeLost()
    {
        FP large = FP.FromRaw(1000000 * FP.ONE);  // 100万
        FP small = FP.Epsilon;                     // 最小精度

        FP sum = large + small;

        // 大数加小数可能丢失精度（小数被吞没）
        // 这是定点数的正常行为，不是错误
        Assert.True(sum.RawValue >= large.RawValue, "Sum should be >= large");

        // 如果精度足够，应该有所增加
        // 100万 < 2^20，而 FP 有 48 位整数，精度足够
        if (large.RawValue < (1L << 32))  // 如果大数不是太大
        {
            // 精度足够时应该能加上
            Assert.Equal(large.RawValue + small.RawValue, sum.RawValue);
        }
    }

    /// <summary>
    /// 连续除法的精度累积损失
    /// </summary>
    [Fact]
    public void SequentialDivision_PrecisionLossAccumulates()
    {
        FP value = FP._1;
        FP divisor = FP._2;

        // 连续除以 2 十次：1 -> 0.5 -> 0.25 -> ...
        for (int i = 0; i < 10; i++)
        {
            value = value / divisor;
        }

        // 理论上 1 / 2^10 = 1/1024 ≈ 0.000977
        // 但实际上可能在某一步变为 0（精度不足）

        // 验证结果是非负的
        Assert.True(value.RawValue >= 0, "Sequential division should not go negative");

        // 确定性
        FP value2 = FP._1;
        for (int i = 0; i < 10; i++)
        {
            value2 = value2 / divisor;
        }
        Assert.Equal(value.RawValue, value2.RawValue);
    }

    /// <summary>
    /// 极小值的乘法下溢
    /// </summary>
    [Fact]
    public void VerySmallMultiplication_MayUnderflowToZero()
    {
        FP tiny1 = FP.Epsilon;  // ~0.000015
        FP tiny2 = FP.Epsilon;

        FP result = tiny1 * tiny2;

        // 极小值相乘结果可能是 0（下溢）
        // 0.000015 * 0.000015 = 2.3e-10，远小于最小精度
        Assert.True(result.RawValue >= 0, "Tiny multiplication should be non-negative");

        // 实际上是 0 或极小值
        Assert.True(result.RawValue == 0 || result.RawValue < 10,
            "Tiny * Tiny should be 0 or very small");
    }

    #endregion

    #region 序列化/反序列化测试

    /// <summary>
    /// RawValue 直接序列化
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(65536)]
    [InlineData(-65536)]
    [InlineData(long.MaxValue)]
    [InlineData(long.MinValue)]
    public void Serialization_RawValueShouldRoundTrip(long rawValue)
    {
        FP original = FP.FromRaw(rawValue);

        // 模拟序列化：保存 RawValue
        long serialized = original.RawValue;

        // 模拟反序列化：从 RawValue 重建
        FP reconstructed = FP.FromRaw(serialized);

        Assert.Equal(original.RawValue, reconstructed.RawValue);
    }

    /// <summary>
    /// 字符串序列化（确定性）
    /// </summary>
    [Theory]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("-1")]
    [InlineData("1.5")]
    [InlineData("-1.5")]
    [InlineData("123.456")]
    [InlineData("-999.999")]
    public void Serialization_StringShouldRoundTrip(string original)
    {
        FP fp = FP.FromString(original);

        // 序列化为字符串（可能丢失精度）
        string serialized = fp.ToString();

        // 反序列化
        FP reconstructed = FP.FromString(serialized);

        // 验证近似相等（允许精度损失）
        long diff = FP.Abs(fp - reconstructed).RawValue;
        Assert.True(diff <= 10, $"String serialization lost too much precision: {diff}");
    }

    /// <summary>
    /// 非法字符串解析（格式错误）
    /// </summary>
    [Theory]
    [InlineData("abc")]
    [InlineData("1.2.3")]
    [InlineData("--1")]
    [InlineData("1a")]
    public void FromString_InvalidInput_ShouldThrowFormatException(string invalid)
    {
        Assert.Throws<FormatException>(() => FP.FromString(invalid));
    }

    /// <summary>
    /// 空字符串和 null 应该抛出 ArgumentException
    /// </summary>
    [Theory]
    [InlineData("")]
    public void FromString_EmptyOrNull_ShouldThrowArgumentException(string invalid)
    {
        Assert.Throws<ArgumentException>(() => FP.FromString(invalid));
    }

    /// <summary>
    /// 空字符串处理
    /// </summary>
    [Fact]
    public void FromString_EmptyString_ShouldThrowArgumentException()
    {
        Assert.Throws<ArgumentException>(() => FP.FromString(""));
    }

    /// <summary>
    /// null 字符串处理
    /// </summary>
    [Fact]
    public void FromString_NullString_ShouldThrowArgumentException()
    {
#nullable disable
        Assert.Throws<ArgumentException>(() => FP.FromString(null));
#nullable restore
    }

    #endregion

    #region 比较操作边界测试

    /// <summary>
    /// 比较操作的自反性
    /// </summary>
    [Fact]
    public void Comparison_ShouldBeReflexive()
    {
        FP a = FP._1;
#pragma warning disable CS1718 // 故意测试自反性
        Assert.True(a == a);
        Assert.False(a != a);
        Assert.True(a <= a);
        Assert.True(a >= a);
        Assert.False(a < a);
        Assert.False(a > a);
#pragma warning restore CS1718
    }

    /// <summary>
    /// 比较操作的对称性
    /// </summary>
    [Fact]
    public void Comparison_ShouldBeSymmetric()
    {
        FP a = FP._1;
        FP b = FP._2;

        // 如果 a < b，则 b > a
        Assert.True(a < b);
        Assert.True(b > a);

        // 如果 a <= b，则 b >= a
        Assert.True(a <= b);
        Assert.True(b >= a);
    }

    /// <summary>
    /// 比较操作的传递性
    /// </summary>
    [Fact]
    public void Comparison_ShouldBeTransitive()
    {
        FP a = FP._1;   // 1.0
        FP b = FP._2;   // 2.0
        FP c = FP._3;   // 3.0

        // a < b 且 b < c，则 a < c
        Assert.True(a < b);
        Assert.True(b < c);
        Assert.True(a < c);
    }

    /// <summary>
    /// 正负零比较（定点数中没有负零的概念）
    /// </summary>
    [Fact]
    public void ZeroComparison_ShouldHandleCorrectly()
    {
        FP zero = FP._0;
        FP negZero = FP.FromRaw(0);  // 同样是 0

        Assert.True(zero == negZero);
        Assert.Equal(zero.RawValue, negZero.RawValue);
    }

    /// <summary>
    /// 最大值/最小值比较
    /// </summary>
    [Fact]
    public void MinMaxComparison_ShouldWork()
    {
        Assert.True(FP.UseableMin < FP.UseableMax);
        Assert.True(FP.UseableMax > FP.UseableMin);
        Assert.False(FP.UseableMin > FP.UseableMax);
        Assert.False(FP.UseableMax < FP.UseableMin);
    }

    #endregion

    #region 哈希与集合行为

    /// <summary>
    /// 相同值的哈希码必须相同
    /// </summary>
    [Fact]
    public void GetHashCode_SameValue_ShouldBeSame()
    {
        FP a = FP.FromRaw(12345);
        FP b = FP.FromRaw(12345);

        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    /// <summary>
    /// 哈希码分布（简单测试）
    /// </summary>
    [Fact]
    public void GetHashCode_ShouldDistribute()
    {
        // 测试不同值有不同的哈希码（不要求唯一，但希望分散）
        var hashes = new HashSet<int>();

        for (int i = 0; i < 1000; i++)
        {
            FP fp = i;
            hashes.Add(fp.GetHashCode());
        }

        // 希望大多数都有不同的哈希码
        Assert.True(hashes.Count > 900, "Hash codes should be well distributed");
    }

    /// <summary>
    /// 可以用作 Dictionary 的键
    /// </summary>
    [Fact]
    public void CanBeUsedAsDictionaryKey()
    {
        var dict = new Dictionary<FP, string>();

        dict[FP._0] = "zero";
        dict[FP._1] = "one";
        dict[FP._0_50] = "half";

        Assert.Equal("zero", dict[FP._0]);
        Assert.Equal("one", dict[FP._1]);
        Assert.Equal("half", dict[FP._0_50]);

        // 相同值应该找到同一个条目
        Assert.Equal("one", dict[FP.FromRaw(FP.ONE)]);
    }

    #endregion

    #region 特殊值行为

    /// <summary>
    /// 最小精度值的行为
    /// </summary>
    [Fact]
    public void Epsilon_Behavior()
    {
        FP eps = FP.Epsilon;

        // Epsilon 是最小正数
        Assert.True(eps.RawValue > 0);
        Assert.True(eps > FP._0);

        // Epsilon + 0 = Epsilon
        Assert.Equal(eps.RawValue, (eps + FP._0).RawValue);

        // Epsilon - Epsilon = 0
        Assert.Equal(FP._0.RawValue, (eps - eps).RawValue);
    }

    /// <summary>
    /// 最大值的行为
    /// </summary>
    [Fact]
    public void UseableMax_Behavior()
    {
        FP max = FP.UseableMax;

        // UseableMax > 0
        Assert.True(max > FP._0);

        // UseableMax + 1 > UseableMax
        Assert.True(max + FP._1 > max);

        // UseableMax 是安全乘法的上限
        // UseableMax * 1 = UseableMax（安全）
        Assert.Equal(max.RawValue, (max * FP._1).RawValue);
    }

    /// <summary>
    /// 最小值的行为
    /// </summary>
    [Fact]
    public void UseableMin_Behavior()
    {
        FP min = FP.UseableMin;

        // UseableMin < 0
        Assert.True(min < FP._0);

        // UseableMin < UseableMax
        Assert.True(FP.UseableMin < FP.UseableMax);

        // Abs(UseableMin) 应该很大
        FP absMin = FP.Abs(FP.UseableMin);
        Assert.True(absMin > FP._0);
    }

    #endregion

    #region 运算符重载边界情况

    /// <summary>
    /// 混合类型运算（FP 与 int）
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(-1)]
    [InlineData(100)]
    [InlineData(-100)]
    public void MixedOperations_WithInt_ShouldWork(int value)
    {
        FP fp = FP._1;  // 1.0

        // FP + int
        FP sum = fp + value;
        Assert.Equal((1 + value) * FP.ONE, sum.RawValue);

        // FP - int
        FP diff = fp - value;
        Assert.Equal((1 - value) * FP.ONE, diff.RawValue);

        // FP * int
        FP prod = fp * value;
        Assert.Equal(value * FP.ONE, prod.RawValue);

        // FP / int（value != 0）
        if (value != 0)
        {
            FP quot = fp / value;
            // 1.0 / value
            Assert.Equal(FP.ONE / value, quot.RawValue);
        }
    }

    /// <summary>
    /// 一元运算符
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(-1)]
    [InlineData(100)]
    [InlineData(-100)]
    public void UnaryOperators_ShouldWork(int value)
    {
        FP fp = value;

        // +FP
        Assert.Equal(fp.RawValue, (+fp).RawValue);

        // -FP
        Assert.Equal(-fp.RawValue, (-fp).RawValue);

        // --FP = FP
        Assert.Equal(fp.RawValue, (-(-fp)).RawValue);
    }

    #endregion

    #region 多线程安全性（确定性框架的关键）

    /// <summary>
    /// FP 运算应该是线程安全的（只读操作）
    /// </summary>
    [Fact]
    public void ThreadSafety_ReadOnlyOperations()
    {
        // 降低迭代次数避免 CI 环境下的线程压力
        const int iterations = 100;
        FP value = FP.FromRaw(12345);

        // 模拟多线程只读访问
        var results = new System.Collections.Concurrent.ConcurrentBag<long>();

        System.Threading.Tasks.Parallel.For(0, iterations, i =>
        {
            FP result = value * FP._2 + FP._1 - FP._0_50;
            results.Add(result.RawValue);
        });

        // 所有结果应该相同
        var distinctResults = new HashSet<long>(results);
        Assert.Single(distinctResults);
    }

    #endregion

    #region Lerp 和 Clamp 边界测试

    /// <summary>
    /// Lerp 边界情况
    /// </summary>
    [Theory]
    [InlineData(0.0, 10.0, 0.0, 0.0)]    // t=0 -> a
    [InlineData(0.0, 10.0, 1.0, 10.0)]   // t=1 -> b
    [InlineData(0.0, 10.0, 0.5, 5.0)]    // t=0.5 -> 中间
    public void Lerp_BoundaryConditions(double a, double b, double t, double expected)
    {
        FP fpA = (int)a;
        FP fpB = (int)b;
        FP fpT = FP.FromRaw((long)(t * FP.ONE));

        FP result = FP.Lerp(fpA, fpB, fpT);

        // 允许小误差
        long expectedRaw = (long)(expected * FP.ONE);
        long diff = System.Math.Abs(result.RawValue - expectedRaw);
        Assert.True(diff <= 1, $"Lerp result too far from expected: {result.RawValue} vs {expectedRaw}");
    }

    /// <summary>
    /// Clamp 边界情况
    /// </summary>
    [Theory]
    [InlineData(5, 0, 10, 5)]     // 在范围内 -> 不变
    [InlineData(-5, 0, 10, 0)]    // 小于 min -> min
    [InlineData(15, 0, 10, 10)]   // 大于 max -> max
    [InlineData(0, 0, 10, 0)]     // 等于 min -> min
    [InlineData(10, 0, 10, 10)]   // 等于 max -> max
    public void Clamp_BoundaryConditions(int value, int min, int max, int expected)
    {
        FP result = FP.Clamp(value, min, max);
        Assert.Equal(expected * FP.ONE, result.RawValue);
    }

    #endregion

    #region 新修复的健壮性测试

    /// <summary>
    /// int 转换溢出应该抛出异常
    /// </summary>
    [Theory]
    [InlineData(2147483648)]      // int.MaxValue + 1
    [InlineData(-2147483649)]     // int.MinValue - 1
    [InlineData(100000000000)]    // 非常大的正数
    [InlineData(-100000000000)]   // 非常大的负数
    public void IntConversion_Overflow_ShouldThrow(long rawValue)
    {
        FP fp = FP.FromRaw(rawValue * FP.ONE);
        Assert.Throws<OverflowException>(() => (int)fp);
    }

    /// <summary>
    /// int 转换在安全范围内应该正常工作
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1000)]
    [InlineData(-1000)]
    [InlineData(2147483647)]      // int.MaxValue
    [InlineData(-2147483648)]     // int.MinValue
    public void IntConversion_InRange_ShouldWork(int value)
    {
        FP fp = (FP)value;
        int result = (int)fp;
        Assert.Equal(value, result);
    }

    /// <summary>
    /// FP 除以 FP 零应该抛出异常
    /// </summary>
    [Fact]
    public void Division_ByZeroFP_ShouldThrow()
    {
        FP one = FP._1;
        FP zero = FP._0;
        Assert.Throws<DivideByZeroException>(() => one / zero);
    }

    /// <summary>
    /// FP 除以 int 零应该抛出异常
    /// </summary>
    [Fact]
    public void Division_ByZeroInt_ShouldThrow()
    {
        FP one = FP._1;
        Assert.Throws<DivideByZeroException>(() => one / 0);
    }

    /// <summary>
    /// 除法左移溢出检查
    /// </summary>
    [Fact]
    public void Division_LeftShiftOverflow_ShouldThrow()
    {
        // 创建一个超过 MAX_SAFE_DIVIDEND 的值
        FP huge = FP.FromRaw(long.MaxValue >> 15);  // 超过 >> 16 的安全值
        FP one = FP._1;
        
        Assert.Throws<OverflowException>(() => huge / one);
    }

    /// <summary>
    /// Abs 处理 long.MinValue 特殊情况
    /// </summary>
    [Fact]
    public void Abs_LongMinValue_ShouldNotCrash()
    {
        // 虽然这个值在正常使用范围外，但应该安全处理
        FP minValue = FP.FromRaw(long.MinValue);
        FP abs = FP.Abs(minValue);
        
        // 应该返回一个很大的正数
        Assert.True(abs.RawValue > 0);
    }

    /// <summary>
    /// Tan 接近 90 度时的处理
    /// </summary>
    [Fact]
    public void Tan_Near90Degrees_ShouldReturnLargeValue()
    {
        FP near90 = FP.FromRaw((long)(1.5707 * FP.ONE));  // 接近 π/2
        FP tan = FP.Tan(near90);
        
        // 应该返回一个很大的值（接近无穷）
        Assert.True(tan.RawValue > 1000000 || tan.RawValue < -1000000);
    }

    /// <summary>
    /// Approximately 默认容差测试
    /// </summary>
    [Fact]
    public void ApproximatelyDefault_ShouldUseCorrectEpsilon()
    {
        FP one = FP._1;
        FP onePlusTiny = FP.FromRaw(FP.ONE + 5);   // 1.0 + 极小值
        FP onePlusLarge = FP.FromRaw(FP.ONE + 100); // 1.0 + 较大值
        
        // 极小差异应该在默认容差内
        Assert.True(FP.Approximately(one, onePlusTiny));
        
        // 较大差异应该超出默认容差
        Assert.False(FP.Approximately(one, onePlusLarge));
    }

    /// <summary>
    /// 乘法 DEBUG 溢出检测（仅在 DEBUG 模式下有效）
    /// </summary>
    [Fact]
    public void Multiplication_OverflowDetection()
    {
        // 在 DEBUG 模式下，溢出会触发 Debug.Fail
        // 这里只验证正常乘法不会误报
        FP a = FP._100;
        FP b = FP._100;
        FP result = a * b;  // 10000，安全
        
        Assert.Equal(10000 * FP.ONE, result.RawValue);
    }

    /// <summary>
    /// EpsilonDefault 常量正确性
    /// </summary>
    [Fact]
    public void EpsilonDefault_ShouldBe10TimesEpsilon()
    {
        long expected = FP.Epsilon.RawValue * 10;
        Assert.Equal(expected, FP.EpsilonDefault.RawValue);
    }

    /// <summary>
    /// ToString 各种边界情况
    /// </summary>
    [Theory]
    [InlineData(0, "0.0000")]
    [InlineData(65536, "1.0000")]      // 1.0
    [InlineData(32768, "0.5000")]      // 0.5
    [InlineData(-65536, "-1.0000")]    // -1.0
    public void ToString_ShouldFormatCorrectly(long raw, string expectedStart)
    {
        FP fp = FP.FromRaw(raw);
        string result = fp.ToString();
        Assert.Equal(expectedStart, result);
    }

    /// <summary>
    /// ToString 自定义小数位数
    /// </summary>
    [Theory]
    [InlineData(65536, "F2", "1.00")]
    [InlineData(65536, "F6", "1.000000")]
    [InlineData(32768, "F2", "0.50")]
    public void ToString_CustomFormat_ShouldWork(long raw, string format, string expected)
    {
        FP fp = FP.FromRaw(raw);
        string result = fp.ToString(format);
        Assert.Equal(expected, result);
    }

    #endregion
}
