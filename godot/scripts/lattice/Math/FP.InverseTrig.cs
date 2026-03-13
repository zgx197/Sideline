// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

#nullable enable

using System;
using System.Runtime.CompilerServices;

namespace Lattice.Math;

/// <summary>
/// FP 反三角函数部分 - 使用 LUT 实现确定性计算
/// </summary>
public readonly partial struct FP
{
    #region 反三角函数

    /// <summary>
    /// 反余弦函数 Acos(x)，x ∈ [-1, 1]，返回值 ∈ [0, π]
    /// <para>使用预生成查找表，纯整数运算</para>
    /// </summary>
    /// <param name="x">输入值，必须在 [-1, 1] 范围内</param>
    /// <returns>反余弦值，范围 [0, π]</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FP Acos(FP x)
    {
        // 限制在 [-1, 1]
        long clamped = x.RawValue;
        if (clamped < -FP.ONE) clamped = -FP.ONE;
        if (clamped > FP.ONE) clamped = FP.ONE;

        // 使用查表：索引 = (x + 1) * 32768，结果在 [0, π]
        int index = (int)((clamped + FP.ONE) * 32768 >> 16);
        if (index < 0) index = 0;
        if (index > 65536) index = 65536;

        // 使用预生成 LUT
        return new FP(FPAcosLut.Table[index]);
    }

    /// <summary>
    /// 反正弦函数 Asin(x)，x ∈ [-1, 1]，返回值 ∈ [-π/2, π/2]
    /// <para>利用 Asin(x) = π/2 - Acos(x) 关系</para>
    /// </summary>
    /// <param name="x">输入值，必须在 [-1, 1] 范围内</param>
    /// <returns>反正弦值，范围 [-π/2, π/2]</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FP Asin(FP x)
    {
        // Asin(x) = π/2 - Acos(x)
        return PiHalf - Acos(x);
    }

    #endregion
}
