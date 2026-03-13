// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

#nullable enable

using System;
using System.Runtime.CompilerServices;

namespace Lattice.Math;

/// <summary>
/// FP 数学工具方法（部分类分离）
/// <para>包含 Abs、Min、Max、Clamp、Lerp、Approximately 等常用数学函数</para>
/// <para>提供分支版本和无分支版本（Branchless），适用于不同性能场景</para>
/// </summary>
/// <remarks>
/// <para><b>分支版本 vs 无分支版本：</b></para>
/// <list type="bullet">
///   <item><b>分支版本（如 <see cref="Abs(FP)"/>）：</b>使用条件语句实现，代码可读性好。当数据分布可预测时（如大部分值为正），现代 CPU 的分支预测能提供良好性能。</item>
///   <item><b>无分支版本（如 <see cref="AbsBranchless"/>）：</b>使用位运算实现，避免分支预测失败惩罚。适合数据分布随机、不可预测的热点代码路径。</item>
/// </list>
/// <para>性能建议：在性能敏感场景进行基准测试后选择合适版本。</para>
/// </remarks>
public readonly partial struct FP
{
    #region 基础数学函数

    /// <summary>
    /// 绝对值（分支版本）
    /// <para>注意：long.MinValue 的绝对值仍是 long.MinValue（溢出），但在 FP 使用范围内不会发生</para>
    /// </summary>
    /// <param name="a">输入值</param>
    /// <returns>绝对值结果</returns>
    /// <remarks>
    /// 使用条件分支实现，当输入值的符号可预测时性能较好。
    /// 对于随机分布的数据，考虑使用 <see cref="AbsBranchless"/>。
    /// </remarks>
    /// <seealso cref="AbsBranchless"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FP Abs(FP a)
    {
        long v = a.RawValue;
        // 处理 long.MinValue 的特殊情况（虽然 FP 使用范围内不会发生）
        if (v == long.MinValue)
            return new(long.MaxValue); // 返回最大正数作为近似
        return new(v < 0 ? -v : v);

    }

    /// <summary>
    /// 绝对值（无分支版本）
    /// <para>使用位运算实现，避免分支预测失败惩罚，适合热点代码路径</para>
    /// </summary>
    /// <param name="a">输入值</param>
    /// <returns>绝对值结果</returns>
    /// <remarks>
    /// <para><b>适用场景：</b>数据分布随机、不可预测（如物理模拟、AI计算），且在紧循环中频繁调用</para>
    /// <para><b>性能特性：</b>恒定执行时间，无分支预测风险，但在现代 CPU 上分支预测成功时可能略慢于分支版本</para>
    /// <para><b>实现原理：</b>Abs(x) = (x ^ (x &gt;&gt; 63)) - (x &gt;&gt; 63)</para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // 在物理引擎的紧循环中使用无分支版本
    /// for (int i = 0; i &lt; particles.Count; i++)
    /// {
    ///     FP velocity = particle.Velocity;
    ///     FP damping = FP.AbsBranchless(velocity) * friction;
    /// }
    /// </code>
    /// </example>
    /// <seealso cref="Abs(FP)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FP AbsBranchless(FP a)
    {
        long v = a.RawValue;
        long mask = v >> 63;  // 符号位扩展：负数=-1，正数/零=0
        return new((v ^ mask) - mask);  // (v ^ -1) - (-1) = ~v + 1 = -v
    }

    /// <summary>
    /// 取两个值中的较小值（分支版本）
    /// </summary>
    /// <param name="a">第一个值</param>
    /// <param name="b">第二个值</param>
    /// <returns>较小值</returns>
    /// <remarks>
    /// 使用条件分支实现，当两个值的大小关系可预测时性能较好。
    /// 对于随机分布的数据，考虑使用 <see cref="MinBranchless"/>。
    /// </remarks>
    /// <seealso cref="MinBranchless"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FP Min(FP a, FP b)
    {
        return a.RawValue < b.RawValue ? a : b;

    }

    /// <summary>
    /// 取两个值中的较小值（无分支版本）
    /// <para>使用位运算实现，避免分支预测失败惩罚，适合热点代码路径</para>
    /// </summary>
    /// <param name="a">第一个值</param>
    /// <param name="b">第二个值</param>
    /// <returns>较小值</returns>
    /// <remarks>
    /// <para><b>适用场景：</b>数据分布随机、不可预测（如物理模拟、排序算法），且在紧循环中频繁调用</para>
    /// <para><b>性能特性：</b>恒定执行时间，无分支预测风险，但在现代 CPU 上分支预测成功时可能略慢于分支版本</para>
    /// <para><b>实现原理：</b>利用差值的符号位作为掩码选择值</para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // 在碰撞检测的 AABB 检查中使用
    /// FP minX = FP.MinBranchless(a.Min.X, b.Min.X);
    /// FP minY = FP.MinBranchless(a.Min.Y, b.Min.Y);
    /// </code>
    /// </example>
    /// <seealso cref="Min(FP, FP)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FP MinBranchless(FP a, FP b)
    {
        long diff = a.RawValue - b.RawValue;
        long mask = diff >> 63;  // a < b 时 mask = -1，否则 mask = 0
        return new((a.RawValue & mask) | (b.RawValue & ~mask));  // mask = -1 时选 a，否则选 b
    }

    /// <summary>
    /// 取两个值中的较大值（分支版本）
    /// </summary>
    /// <param name="a">第一个值</param>
    /// <param name="b">第二个值</param>
    /// <returns>较大值</returns>
    /// <remarks>
    /// 使用条件分支实现，当两个值的大小关系可预测时性能较好。
    /// 对于随机分布的数据，考虑使用 <see cref="MaxBranchless"/>。
    /// </remarks>
    /// <seealso cref="MaxBranchless"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FP Max(FP a, FP b)
    {
        return a.RawValue > b.RawValue ? a : b;

    }

    /// <summary>
    /// 取两个值中的较大值（无分支版本）
    /// <para>使用位运算实现，避免分支预测失败惩罚，适合热点代码路径</para>
    /// </summary>
    /// <param name="a">第一个值</param>
    /// <param name="b">第二个值</param>
    /// <returns>较大值</returns>
    /// <remarks>
    /// <para><b>适用场景：</b>数据分布随机、不可预测（如物理模拟、碰撞检测），且在紧循环中频繁调用</para>
    /// <para><b>性能特性：</b>恒定执行时间，无分支预测风险，但在现代 CPU 上分支预测成功时可能略慢于分支版本</para>
    /// <para><b>实现原理：</b>利用差值的符号位作为掩码选择值</para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // 在动画系统中混合多个值
    /// FP maxWeight = FP.MaxBranchless(weight1, FP.MaxBranchless(weight2, weight3));
    /// </code>
    /// </example>
    /// <seealso cref="Max(FP, FP)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FP MaxBranchless(FP a, FP b)
    {
        long diff = a.RawValue - b.RawValue;
        long mask = diff >> 63;  // a < b 时 mask = -1，否则 mask = 0
        return new((b.RawValue & mask) | (a.RawValue & ~mask));  // mask = -1 时选 b，否则选 a
    }

    /// <summary>
    /// 将值限制在 [min, max] 范围内（分支版本）
    /// <para>如果 value &lt; min 返回 min，如果 value &gt; max 返回 max，否则返回 value</para>
    /// </summary>
    /// <param name="value">要限制的值</param>
    /// <param name="min">最小值</param>
    /// <param name="max">最大值</param>
    /// <returns>限制后的值</returns>
    /// <remarks>
    /// 使用条件分支实现，当 value 通常已在范围内时性能较好。
    /// 对于随机分布的数据，考虑使用 <see cref="ClampBranchless"/>。
    /// </remarks>
    /// <seealso cref="ClampBranchless"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FP Clamp(FP value, FP min, FP max)
    {
        if (value.RawValue < min.RawValue) return min;
        if (value.RawValue > max.RawValue) return max;
        return value;

    }

    /// <summary>
    /// 将值限制在 [min, max] 范围内（无分支版本）
    /// <para>使用位运算实现，避免分支预测失败惩罚，适合热点代码路径</para>
    /// </summary>
    /// <param name="value">要限制的值</param>
    /// <param name="min">最小值</param>
    /// <param name="max">最大值</param>
    /// <returns>限制后的值</returns>
    /// <remarks>
    /// <para><b>适用场景：</b>数据分布随机、不可预测（如输入处理、动画系统），且在紧循环中频繁调用</para>
    /// <para><b>性能特性：</b>恒定执行时间，无分支预测风险，但在现代 CPU 上分支预测成功时可能略慢于分支版本</para>
    /// <para><b>实现原理：</b>ClampBranchless(v, min, max) = MinBranchless(MaxBranchless(v, min), max)</para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // 在输入处理中限制摇杆值
    /// FP clampedX = FP.ClampBranchless(input.X, -FP._1, FP._1);
    /// FP clampedY = FP.ClampBranchless(input.Y, -FP._1, FP._1);
    /// </code>
    /// </example>
    /// <seealso cref="Clamp(FP, FP, FP)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FP ClampBranchless(FP value, FP min, FP max)
    {
        return MinBranchless(MaxBranchless(value, min), max);
    }

    /// <summary>
    /// 线性插值：a + (b - a) * t
    /// <para>结果范围取决于 t，不限制 t 的范围</para>
    /// </summary>
    /// <param name="a">起始值</param>
    /// <param name="b">结束值</param>
    /// <param name="t">插值系数（0 返回 a，1 返回 b）</param>
    /// <returns>插值结果</returns>
    /// <example>
    /// <code>
    /// // 在 100 和 200 之间插值，t=0.5 返回 150
    /// FP result = FP.Lerp(FP._100, FP._200, FP._0_50);
    /// </code>
    /// </example>
    /// <seealso cref="FPVector2.Lerp(FPVector2, FPVector2, FP)"/>
    /// <seealso cref="FPVector3.Lerp(FPVector3, FPVector3, FP)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FP Lerp(FP a, FP b, FP t)
    {
        return a + (b - a) * t;
    }

    /// <summary>
    /// 近似相等（使用默认容差 EpsilonDefault ≈ 0.00015）
    /// <para>用于处理定点数运算累积的微小误差</para>
    /// </summary>
    /// <param name="a">第一个值</param>
    /// <param name="b">第二个值</param>
    /// <returns>如果两值之差小于等于默认容差则返回 true</returns>
    /// <remarks>
    /// 定点数运算会产生微小精度误差，使用此方法而非 == 进行比较。
    /// 默认容差适用于大多数场景，需要自定义容差时使用 <see cref="Approximately(FP, FP, FP)"/>。
    /// </remarks>
    /// <example>
    /// <code>
    /// // 检查是否到达目标位置（考虑浮点误差）
    /// if (FP.Approximately(currentPos, targetPos))
    /// {
    ///     // 到达目标
    /// }
    /// </code>
    /// </example>
    /// <seealso cref="Approximately(FP, FP, FP)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Approximately(FP a, FP b)
    {
        return Approximately(a, b, EpsilonDefault);
    }

    /// <summary>
    /// 近似相等（自定义容差）
    /// <para>用于处理定点数运算累积的微小误差</para>
    /// </summary>
    /// <param name="a">第一个值</param>
    /// <param name="b">第二个值</param>
    /// <param name="epsilon">容差值</param>
    /// <returns>如果两值之差小于等于 epsilon 则返回 true</returns>
    /// <example>
    /// <code>
    /// // 使用较大容差检查速度是否接近零
    /// if (FP.Approximately(velocity, FP._0, (FP)0.01))
    /// {
    ///     velocity = FP._0; // 完全停止
    /// }
    /// </code>
    /// </example>
    /// <seealso cref="Approximately(FP, FP)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Approximately(FP a, FP b, FP epsilon)
    {
        long diff = a.RawValue - b.RawValue;
        if (diff < 0) diff = -diff;
        return diff <= epsilon.RawValue;
    }

    #endregion
}
