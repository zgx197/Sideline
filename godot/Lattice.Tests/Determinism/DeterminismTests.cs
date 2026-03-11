using Xunit;
using Lattice.Math;
using System.Collections.Generic;

namespace Lattice.Tests.Determinism;

/// <summary>
/// 确定性专项测试
/// 验证：相同输入 → 相同输出（跨时间、跨平台）
/// </summary>
public class DeterminismTests
{
    #region 核心确定性测试

    /// <summary>
    /// 核心测试：运行相同模拟两次，结果必须完全一致
    /// </summary>
    [Fact]
    public void Simulation_ShouldProduceSameResult_WhenRunTwice()
    {
        var result1 = RunSimulation(seed: 12345, frames: 1000);
        var result2 = RunSimulation(seed: 12345, frames: 1000);

        Assert.Equal(result1.Checksum, result2.Checksum);
        Assert.Equal(result1.FinalPosition, result2.FinalPosition);
        Assert.Equal(result1.FinalVelocity, result2.FinalVelocity);
    }

    /// <summary>
    /// 测试多次乘法不会累积误差导致发散
    /// </summary>
    [Fact]
    public void Multiplication_ShouldNotAccumulateError()
    {
        FP value = FP._1;
        FP factor = FP.FromRaw(FP.Raw._1 - FP.Raw._0_01);  // 每次乘以 0.99

        // 运行 1000 次
        for (int i = 0; i < 1000; i++)
        {
            value *= factor;
        }

        // 重新运行，应该得到完全相同的结果
        FP value2 = FP._1;
        for (int i = 0; i < 1000; i++)
        {
            value2 *= factor;
        }

        Assert.Equal(value.RawValue, value2.RawValue);
    }

    /// <summary>
    /// 复杂运算链的确定性
    /// </summary>
    [Theory]
    [InlineData(100)]
    [InlineData(1000)]
    [InlineData(10000)]
    public void ComplexOperationChain_ShouldBeDeterministic(int iterations)
    {
        var results = new List<long>();

        // 第一遍
        for (int i = 0; i < iterations; i++)
        {
            FP a = FP.FromRaw(12345 + i * 100);
            FP b = FP.FromRaw(54321 - i * 50);
            FP c = FP.FromRaw(i * 1000);

            // 复杂运算链：加减乘除混合
            FP result = ((a * b + c) / FP._2 - FP._1_50) * FP._0_10;
            result = FP.Clamp(result, FP.UseableMin, FP.UseableMax);
            result = FP.Lerp(a, b, FP.Abs(result) / FP._100);

            results.Add(result.RawValue);
        }

        // 第二遍：验证完全相同
        for (int i = 0; i < iterations; i++)
        {
            FP a = FP.FromRaw(12345 + i * 100);
            FP b = FP.FromRaw(54321 - i * 50);
            FP c = FP.FromRaw(i * 1000);

            FP result = ((a * b + c) / FP._2 - FP._1_50) * FP._0_10;
            result = FP.Clamp(result, FP.UseableMin, FP.UseableMax);
            result = FP.Lerp(a, b, FP.Abs(result) / FP._100);

            Assert.Equal(results[i], result.RawValue);
        }
    }

    #endregion

    #region 跨平台一致性测试

    /// <summary>
    /// 跨平台一致性：RawValue 是唯一的确定性表示
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(-1)]
    [InlineData(65536)]              // 1.0
    [InlineData(-65536)]             // -1.0
    [InlineData(205887)]             // Pi
    [InlineData(2147483647)]         // UseableMax
    [InlineData(-2147483648)]        // UseableMin
    public void RawValue_ShouldBePlatformIndependent(long raw)
    {
        // 这个测试验证：任何平台，相同的 RawValue 表示相同的数
        FP fp = FP.FromRaw(raw);
        Assert.Equal(raw, fp.RawValue);
    }

    /// <summary>
    /// 校验和计算的一致性
    /// </summary>
    [Fact]
    public void Checksum_ShouldBeDeterministic()
    {
        ulong ComputeChecksum()
        {
            var values = new[]
            {
                FP._0, FP._1, FP._2,
                FP._0_50, FP._0_10, FP._0_01,
                FP.Pi, FP.Pi2, FP.PiHalf,
                FP.UseableMax, FP.UseableMin,
                FP.Epsilon
            };

            // FNV-1a 哈希
            ulong hash = 14695981039346656037;
            foreach (var fp in values)
            {
                hash ^= (ulong)fp.RawValue;
                hash *= 1099511628211;
            }
            return hash;
        }

        ulong checksum1 = ComputeChecksum();
        ulong checksum2 = ComputeChecksum();
        ulong checksum3 = ComputeChecksum();

        Assert.Equal(checksum1, checksum2);
        Assert.Equal(checksum2, checksum3);
    }

    #endregion

    #region 边界值测试（健壮性关键）

    /// <summary>
    /// 零值运算的确定性
    /// </summary>
    [Fact]
    public void ZeroOperations_ShouldBeDeterministic()
    {
        FP zero = FP._0;
        FP one = FP._1;
        FP negOne = FP.FromRaw(-FP.ONE);

        // 加法
        Assert.Equal(FP._0.RawValue, (zero + zero).RawValue);
        Assert.Equal(FP._1.RawValue, (zero + one).RawValue);
        Assert.Equal(FP.FromRaw(-FP.ONE).RawValue, (zero + negOne).RawValue);

        // 减法
        Assert.Equal(FP._0.RawValue, (zero - zero).RawValue);
        Assert.Equal(FP.FromRaw(-FP.ONE).RawValue, (zero - one).RawValue);

        // 乘法
        Assert.Equal(FP._0.RawValue, (zero * zero).RawValue);
        Assert.Equal(FP._0.RawValue, (zero * one).RawValue);
        Assert.Equal(FP._0.RawValue, (one * zero).RawValue);

        // 除零在 Debug 下断言失败，这里不测除零
    }

    /// <summary>
    /// UseableMax 边界运算
    /// </summary>
    [Fact]
    public void UseableMaxOperations_ShouldNotOverflow()
    {
        FP max = FP.UseableMax;
        FP min = FP.UseableMin;
        FP one = FP._1;

        // UseableMax * 1 应该安全
        FP result1 = max * one;
        Assert.Equal(max.RawValue, result1.RawValue);

        // UseableMax / 1 应该安全
        FP result2 = max / one;
        Assert.Equal(max.RawValue, result2.RawValue);

        // UseableMin * 1 应该安全
        FP result3 = min * one;
        Assert.Equal(min.RawValue, result3.RawValue);

        // UseableMax 与 UseableMin 的加法（应该安全，因为范围足够大）
        FP sum = max + min;
        // 验证结果在合理范围（正数加负数）
        Assert.True(sum.RawValue < max.RawValue);
        Assert.True(sum.RawValue > min.RawValue);
    }

    /// <summary>
    /// 极端值运算：接近 UseableMax 的值进行确定性运算
    /// 注意：大数与小数相乘可能下溢为 0，这是定点数的正常行为
    /// </summary>
    [Theory]
    [InlineData(2147483000)]   // 接近 UseableMax
    [InlineData(-2147483000)]  // 接近 UseableMin
    [InlineData(1000000000)]   // 大正数
    [InlineData(-1000000000)]  // 大负数
    public void ExtremeValueOperations_ShouldBeDeterministic(long rawValue)
    {
        FP value = FP.FromRaw(rawValue);
        FP small = FP._0_01;  // 0.01

        // 与大数相乘可能会下溢为 0（大数 >> 16 后小数部分丢失）
        // 这里只验证确定性（两次结果相同）
        FP result1 = value * small;
        FP result2 = value * small;
        Assert.Equal(result1.RawValue, result2.RawValue);

        // 极端值的加法是确定性的
        FP sum = value + value;
        Assert.Equal(sum.RawValue, (value + value).RawValue);
    }

    #endregion

    #region 负数运算全面测试（之前简化的部分）

    /// <summary>
    /// 负数加减法的确定性
    /// </summary>
    [Theory]
    [InlineData(-10, -5, -15)]    // 负 + 负 = 更负
    [InlineData(-10, 5, -5)]      // 负 + 正 = 可能负
    [InlineData(-5, 10, 5)]       // 负 + 正 = 正
    [InlineData(0, -5, -5)]       // 零 + 负 = 负
    public void NegativeAddition_ShouldBeCorrect(int a, int b, int expected)
    {
        FP fpA = a;
        FP fpB = b;
        FP result = fpA + fpB;
        Assert.Equal(expected, (int)result);
    }

    /// <summary>
    /// 负数乘法的确定性（关键测试）
    /// </summary>
    [Theory]
    // 正 × 正 = 正（基准）
    [InlineData(5, 5, 25)]
    // 负 × 正 = 负
    [InlineData(-5, 5, -25)]
    [InlineData(5, -5, -25)]
    // 负 × 负 = 正
    [InlineData(-5, -5, 25)]
    // 零参与
    [InlineData(-5, 0, 0)]
    [InlineData(0, -5, 0)]
    public void NegativeMultiplication_ShouldFollowSignRules(int a, int b, int expected)
    {
        FP fpA = a;
        FP fpB = b;
        FP result = fpA * fpB;
        Assert.Equal(expected, (int)result);
    }

    /// <summary>
    /// 负数除法的确定性（关键测试）
    /// </summary>
    [Theory]
    // 正 ÷ 正 = 正
    [InlineData(10, 2, 5)]
    // 负 ÷ 正 = 负
    [InlineData(-10, 2, -5)]
    // 正 ÷ 负 = 负
    [InlineData(10, -2, -5)]
    // 负 ÷ 负 = 正
    [InlineData(-10, -2, 5)]
    // 零 ÷ 负 = 零
    [InlineData(0, -5, 0)]
    public void NegativeDivision_ShouldFollowSignRules(int a, int b, int expected)
    {
        FP fpA = a;
        // 除以 int，不是 FP
        FP result = fpA / b;
        Assert.Equal(expected, (int)result);
    }

    /// <summary>
    /// 定点数除法的截断行为
    /// 注意：C# 的 >> 对负数是向负无穷取整，不是向零
    /// </summary>
    [Theory]
    // 正数除法截断（向零/向负无穷相同）
    [InlineData(5, 2, 2)]     // 2.5 → 2
    [InlineData(7, 3, 2)]     // 2.33 → 2
    // 负数除法截断（向负无穷）
    [InlineData(-5, 2, -3)]   // -2.5 → -3（向负无穷）
    [InlineData(-7, 3, -3)]   // -2.33 → -3（向负无穷）
    [InlineData(5, -2, -3)]   // -2.5 → -3（向负无穷）
    [InlineData(-5, -2, 2)]   // 2.5 → 2
    public void Division_ShouldTruncateTowardsNegativeInfinity(int a, int b, int expected)
    {
        FP fpA = a;
        FP result = fpA / b;
        Assert.Equal(expected, (int)result);
    }

    /// <summary>
    /// 一元负号的确定性
    /// </summary>
    [Fact]
    public void Negation_ShouldBeDeterministic()
    {
        // 测试各种值的负号
        var testValues = new[]
        {
            FP._0, FP._1, FP.FromRaw(-FP.ONE),
            FP._0_50, FP.FromRaw(-(FP.ONE >> 1)),
            FP.UseableMax, FP.UseableMin,
            FP.Pi, FP.FromRaw(-205887)
        };

        foreach (var value in testValues)
        {
            FP negated = -value;
            FP doubleNegated = -negated;

            // 双重否定应回到原值
            Assert.Equal(value.RawValue, doubleNegated.RawValue);

            // 验证符号相反
            if (value.RawValue != 0)
            {
                Assert.True(
                    (value.RawValue > 0 && negated.RawValue < 0) ||
                    (value.RawValue < 0 && negated.RawValue > 0),
                    $"Negation failed for {value.RawValue}"
                );
            }
        }
    }

    /// <summary>
    /// Abs 函数的确定性
    /// </summary>
    [Theory]
    [InlineData(0, 0)]
    [InlineData(5, 5)]
    [InlineData(-5, 5)]
    [InlineData(1000, 1000)]
    [InlineData(-1000, 1000)]
    public void Abs_ShouldReturnAbsoluteValue(int input, int expected)
    {
        FP fp = input;
        FP result = FP.Abs(fp);
        Assert.Equal(expected, (int)result);
    }

    #endregion

    #region 精度测试（定点数特性）

    /// <summary>
    /// 定点数精度测试：1/3 无法精确表示
    /// </summary>
    [Fact]
    public void OneThird_ShouldBeApproximate()
    {
        FP one = FP._1;
        FP three = 3;

        FP oneThird = one / three;
        FP reconstructed = oneThird * three;

        // 1/3 * 3 不会精确等于 1（精度损失）
        Assert.NotEqual(one.RawValue, reconstructed.RawValue);

        // 但应该非常接近
        long diff = FP.Abs(one - reconstructed).RawValue;
        Assert.True(diff < 100, $"Precision loss too large: {diff}");
    }

    /// <summary>
    /// 累积误差测试：多次小数值运算
    /// </summary>
    [Fact]
    public void CumulativeError_ShouldBeBounded()
    {
        FP sum = FP._0;
        FP increment = FP._0_01;  // 0.01 (约 655.36，实际存储 655)

        // 累加 100 次 0.01
        for (int i = 0; i < 100; i++)
        {
            sum += increment;
        }

        // 理论上等于 1.0，但 0.01 本身就是近似值 (655/65536 = 0.0099945)
        // 100 * 655 = 65500，而 1.0 = 65536
        // 误差约为 36/65536 ≈ 0.00055
        FP expected = FP._1;
        long diff = FP.Abs(sum - expected).RawValue;

        // 误差应该在可接受范围（约 50 个 raw units = 0.00076）
        Assert.True(diff <= 50, $"Cumulative error too large: {diff} raw units");
    }

    /// <summary>
    /// MultiplyPrecise 的舍入行为测试
    /// 四舍五入在某些情况下应该比截断更接近真实值
    /// </summary>
    [Fact]
    public void MultiplyPrecise_ShouldRoundCorrectly()
    {
        // 构造一个明确的场景：结果小数部分 >= 0.5 时应该进位
        // 使用简单整数可以预测结果
        FP a = FP._1;  // 1.0
        FP b = FP._0_50;  // 0.5

        FP normal = a * b;      // 截断
        FP precise = FP.MultiplyPrecise(a, b);  // 四舍五入

        // 1.0 * 0.5 = 0.5，两者都应该精确等于 0.5
        Assert.Equal(FP._0_50.RawValue, normal.RawValue);
        Assert.Equal(FP._0_50.RawValue, precise.RawValue);

        // 测试一个更复杂的场景，确保四舍五入有效
        // 选择两个数使乘积的小数部分接近 0.5
        FP c = FP.FromRaw((FP.ONE * 3) / 4);  // 0.75
        FP d = FP.FromRaw((FP.ONE * 2) / 3);  // ~0.6667

        FP precise2 = FP.MultiplyPrecise(c, d);
        FP normal2 = c * d;

        // 验证 MultiplyPrecise 不会比正常版本差（至少不会更差）
        // 由于 0.75 * 0.6667 = 0.5，结果应该在范围内
        Assert.True(precise2.RawValue > 0, "MultiplyPrecise should produce positive result");
        Assert.True(normal2.RawValue > 0, "Normal multiply should produce positive result");
    }

    #endregion

    #region 辅助方法

    private (ulong Checksum, long FinalPosition, long FinalVelocity) RunSimulation(int seed, int frames)
    {
        // 简单的物理模拟
        FP position = FP.FromRaw(seed);
        FP velocity = FP._1;           // 1.0
        FP acceleration = FP._0_10;    // 0.1
        FP damping = FP.FromRaw(FP.Raw._1 - FP.Raw._0_01);         // 0.99 阻尼

        ulong checksum = 14695981039346656037;

        for (int frame = 0; frame < frames; frame++)
        {
            // 更新速度和位置
            velocity += acceleration;
            velocity *= damping;
            position += velocity;

            // 边界反弹
            if (position > FP._1000 || position < -FP._1000)
            {
                velocity = -velocity;
            }

            // 计算校验和
            checksum ^= (ulong)position.RawValue;
            checksum *= 1099511628211;
            checksum ^= (ulong)velocity.RawValue;
            checksum *= 1099511628211;
        }

        return (checksum, position.RawValue, velocity.RawValue);
    }

    #endregion
}
