// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

#nullable enable

using System;
using System.Runtime.CompilerServices;

namespace Lattice.Math;

/// <summary>
/// FP 三角函数部分 - 使用 LUT 实现确定性计算
/// </summary>
public readonly partial struct FP
{
    #region 常量与辅助

    /// <summary>2π 对应的 RawValue</summary>
    private static readonly long TWO_PI_RAW = Pi2.RawValue;

    #endregion

    #region 快速模式 (1024 LUT)

    /// <summary>
    /// 快速正弦函数（1024 LUT，Cache 友好）
    /// <para>精度：~0.35° 步长，适合一般计算</para>
    /// <para>速度：比 Accurate 模式快，Cache 命中率高</para>
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FP SinFast(FP angle)
    {
        long raw = angle.RawValue;
        
        // 归一化到 [0, 2π)
        if (raw < 0) raw = raw % TWO_PI_RAW + TWO_PI_RAW;
        else if (raw >= TWO_PI_RAW) raw = raw % TWO_PI_RAW;
        
        // 映射到 [0, 1024)：raw * 1024 / 2π
        int index = (int)((raw << 10) / TWO_PI_RAW);  // 10 = log2(1024)
        if (index >= FPSinCosLut.FastSize) index = 0;
        
        return new(FPSinCosLut.SinFast[index]);
    }

    /// <summary>
    /// 快速余弦函数（1024 LUT）
    /// <para>Cos(x) = Sin(x + π/2)</para>
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FP CosFast(FP angle)
    {
        return SinFast(angle + PiHalf);
    }

    #endregion

    #region 标准模式 (1024 LUT)

    /// <summary>
    /// 标准正弦函数（1024 LUT）
    /// <para>默认推荐：平衡精度和性能</para>
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FP Sin(FP angle)
    {
        // 使用快速模式（当前实现相同，未来可添加插值）
        return SinFast(angle);
    }

    /// <summary>
    /// 标准余弦函数（1024 LUT）
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FP Cos(FP angle)
    {
        return Sin(angle + PiHalf);
    }

    #endregion

    #region 高精度模式 (4096 LUT)

    /// <summary>
    /// 高精度正弦函数（4096 LUT）
    /// <para>精度：~0.088° 步长，适合 3D 旋转和物理模拟</para>
    /// <para>内存：比 Fast 模式多 24KB，Cache 压力略大</para>
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FP SinAccurate(FP angle)
    {
        long raw = angle.RawValue;
        
        // 归一化到 [0, 2π)
        if (raw < 0) raw = raw % TWO_PI_RAW + TWO_PI_RAW;
        else if (raw >= TWO_PI_RAW) raw = raw % TWO_PI_RAW;
        
        // 映射到 [0, 4096)：raw * 4096 / 2π
        int index = (int)((raw << 12) / TWO_PI_RAW);  // 12 = log2(4096)
        if (index >= FPSinCosLut.AccurateSize) index = 0;
        
        return new(FPSinCosLut.SinAccurate[index]);
    }

    /// <summary>
    /// 高精度余弦函数（4096 LUT）
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FP CosAccurate(FP angle)
    {
        return SinAccurate(angle + PiHalf);
    }

    #endregion

    #region 其他三角函数

    /// <summary>
    /// 正切函数 Tan(angle) - 使用标准精度
    /// <para>Tan(x) = Sin(x) / Cos(x)</para>
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FP Tan(FP angle)
    {
        FP sin = Sin(angle);
        FP cos = Cos(angle);
        
        // 当 Cos 接近 0 时处理
        long absCos = cos.RawValue < 0 ? -cos.RawValue : cos.RawValue;
        if (absCos < 10) // 接近零
        {
            return sin.RawValue >= 0 ? UseableMax : UseableMin;
        }
        
        return sin / cos;
    }

    /// <summary>
    /// 高精度正切函数 - 使用 4096 LUT
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FP TanAccurate(FP angle)
    {
        FP sin = SinAccurate(angle);
        FP cos = CosAccurate(angle);
        
        long absCos = cos.RawValue < 0 ? -cos.RawValue : cos.RawValue;
        if (absCos < 10)
        {
            return sin.RawValue >= 0 ? UseableMax : UseableMin;
        }
        
        return sin / cos;
    }

    #endregion

    #region 反三角函数

    /// <summary>
    /// 反正切函数 Atan2(y, x)，返回角度 [-π, π]
    /// <para>用于计算向量角度，如 MathF.Atan2(y, x)</para>
    /// </summary>
    /// <remarks>
    /// 使用象限归约 + 查表，保证确定性
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FP Atan2(FP y, FP x)
    {
        // 处理原点和坐标轴上的点
        if (x.RawValue == 0)
        {
            if (y.RawValue > 0) return PiHalf;
            if (y.RawValue < 0) return -PiHalf;
            return Zero;
        }
        if (y.RawValue == 0)
        {
            return x.RawValue > 0 ? Zero : Pi;
        }
        
        FP absY = Abs(y);
        FP absX = Abs(x);
        
        // 使用公式：atan2(y, x) = atan(y/x) (适当调整象限)
        // 但为了精度，当 |y| > |x| 时，使用 atan2(y, x) = π/2 - atan(x/y)
        FP angle;
        if (absY <= absX)
        {
            // |y/x| <= 1，直接计算 atan(|y/x|)
            FP ratio = absY / absX;  // [0, 1]
            angle = AtanSmall(ratio);
        }
        else
        {
            // |y/x| > 1，使用互补角公式
            FP ratio = absX / absY;  // [0, 1]
            angle = PiHalf - AtanSmall(ratio);
        }
        
        // 恢复符号和象限
        // 将结果从第一象限 [0, π/2] 映射到正确的象限
        if (x.RawValue < 0)
        {
            // 第二或第三象限
            angle = y.RawValue > 0 ? Pi - angle : -Pi + angle;
        }
        else if (y.RawValue < 0)
        {
            // 第四象限
            angle = -angle;
        }
        // 否则第一象限，保持不变
        
        return angle;
    }

    /// <summary>
    /// 计算 atan(x) 当 x ∈ [0, 1]
    /// <para>使用预生成查找表 + 线性插值</para>
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static FP AtanSmall(FP x)
    {
        // 使用预生成的 atan 表
        // atan(x) for x in [0, 1], result in [0, π/4]
        return AtanTableLookup(x);
    }

    /// <summary>Atan 表大小常量</summary>
    private const int ATAN_TABLE_SIZE = 256;

    /// <summary>
    /// 查表 + 线性插值
    /// <para>输入 x ∈ [0, 1]，输出 atan(x) ∈ [0, π/4]</para>
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static FP AtanTableLookup(FP x)
    {
        long r = x.RawValue;
        if (r <= 0) return Zero;
        if (r >= ONE) return new FP(FPAtanLut.Table[ATAN_TABLE_SIZE]);
        
        // 映射到表索引
        long idxLong = (r * ATAN_TABLE_SIZE) / ONE;
        int idx = (int)idxLong;
        if (idx >= ATAN_TABLE_SIZE) return new FP(FPAtanLut.Table[ATAN_TABLE_SIZE]);
        
        // 线性插值
        long lower = FPAtanLut.Table[idx];
        long upper = FPAtanLut.Table[idx + 1];
        long frac = (r * ATAN_TABLE_SIZE) % ONE;
        long interpolated = lower + ((upper - lower) * frac) / ONE;
        
        return new FP(interpolated);
    }

    #endregion

    #region 角度转换

    // 180° 对应的 FP 常量（180 * 65536 = 11796480）
    private static readonly FP DEG_180 = new(11796480L);

    /// <summary>
    /// 角度转弧度（当输入是角度时）
    /// <para>例如：Deg2Rad(90) = π/2</para>
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FP Deg2Rad(FP degrees)
    {
        // radians = degrees * π / 180
        return degrees * Pi / DEG_180;
    }

    /// <summary>
    /// 弧度转角度
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FP Rad2Deg(FP radians)
    {
        // degrees = radians * 180 / π
        return radians * DEG_180 / Pi;
    }

    #endregion

    #region 使用指南

    /*
     * 精度选择指南：
     * 
     * 1. Fast 模式 (1024 LUT):
     *    - 场景：粒子效果、UI 动画、非关键逻辑
     *    - 优势：Cache 命中率高，适合高频调用
     *    - 精度：±0.35° 最大误差
     * 
     * 2. 标准模式 (1024 LUT):
     *    - 场景：2D 游戏、一般移动计算
     *    - 优势：平衡的性能和精度
     *    - 精度：±0.35° 最大误差
     * 
     * 3. Accurate 模式 (4096 LUT):
     *    - 场景：3D 旋转、物理模拟、长时间积分
     *    - 优势：高精度减少误差累积
     *    - 精度：±0.088° 最大误差
     * 
     * 内存占用：
     * - Fast (1024)：Sin + Cos = 16 KB
     * - Accurate (4096)：Sin + Cos = 64 KB
     * - 总计：80 KB（可接受）
     */

    #endregion
}
