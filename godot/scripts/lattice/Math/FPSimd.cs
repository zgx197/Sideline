// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

#nullable enable

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Lattice.Math;

/// <summary>
/// SIMD 整数运算加速 - 仅使用 Vector&lt;long&gt;/Vector&lt;int&gt;
/// <para>为定点数类型提供批量 SIMD 运算支持，确保跨平台确定性</para>
/// </summary>
/// <remarks>
/// <para><b>⚠️ 重要警告 - 浮点 SIMD 被禁止：</b></para>
/// <para>
/// 本文件<strong>仅使用整数 SIMD</strong>（<see cref="Vector{T}"/> 配合 long/int 类型）。
/// <strong>浮点 SIMD（Vector&lt;float&gt;, Vector&lt;double&gt;）被明确禁止</strong>，原因如下：
/// </para>
/// <list type="number">
///   <item><b>跨平台差异：</b>x86/ARM 处理浮点舍入和特殊情况（NaN、无穷大）的方式不同</item>
///   <item><b>编译器差异：</b>不同编译器对浮点表达式的优化策略不同</item>
///   <item><b>确定性要求：</b>帧同步游戏需要所有客户端产生完全一致的计算结果</item>
/// </list>
/// 
/// <para><b>定点数 SIMD 运算原理：</b></para>
/// <list type="bullet">
///   <item><b>格式：</b>FP 类型使用 Q48.16 格式（64位有符号整数，16位小数）</item>
///   <item><b>加法/减法：</b>直接对原始 long 值进行 SIMD 运算，无需额外处理</item>
///   <item><b>乘法：</b>需要处理定点数精度转换 (a * b + 32768) &gt;&gt; 16</item>
///   <item><b>取反/绝对值：</b>使用位运算实现无分支 SIMD 操作</item>
/// </list>
/// 
/// <para><b>性能特性：</b></para>
/// <list type="bullet">
///   <item>128-bit SIMD (SSE2/NEON): 每次处理 2 个 FP 值</item>
///   <item>256-bit SIMD (AVX2): 每次处理 4 个 FP 值</item>
///   <item>512-bit SIMD (AVX-512): 每次处理 8 个 FP 值（未来扩展）</item>
/// </list>
/// </remarks>
public static class FPSimd
{
    #region 常量

    /// <summary>
    /// Vector&lt;T&gt; 中 long (64位) 的向量宽度
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    ///   <item>128-bit SIMD: VectorLongCount = 2</item>
    ///   <item>256-bit SIMD: VectorLongCount = 4</item>
    ///   <item>512-bit SIMD: VectorLongCount = 8 (未来扩展)</item>
    /// </list>
    /// </remarks>
    public static readonly int VectorLongCount = Vector<long>.Count;

    /// <summary>
    /// Vector&lt;T&gt; 中 int (32位) 的向量宽度
    /// </summary>
    /// <remarks>
    /// 当前主要用于 32 位定点数运算的潜在扩展，64 位 FP 运算主要使用 <see cref="VectorLongCount"/>。
    /// </remarks>
    public static readonly int VectorIntCount = Vector<int>.Count;

    /// <summary>
    /// 硬件 SIMD 加速支持检测
    /// </summary>
    /// <remarks>
    /// 当 CPU 支持 SSE2/AVX/AVX2/NEON 等指令集时返回 true。
    /// 即使返回 false，整数 SIMD 操作仍会正确执行（使用软件回退）。
    /// 实际性能提升取决于数据规模和具体 CPU 架构。
    /// </remarks>
    public static bool IsHardwareAccelerated => Vector.IsHardwareAccelerated;

    #endregion

    #region FP 批量加法

    /// <summary>
    /// 批量加法：result[i] = a[i] + b[i]
    /// </summary>
    /// <param name="a">输入数组 A</param>
    /// <param name="b">输入数组 B</param>
    /// <param name="result">输出数组</param>
    /// <exception cref="ArgumentException">数组长度不匹配时抛出</exception>
    /// <remarks>
    /// <para><b>实现说明：</b></para>
    /// 使用整数 SIMD 实现确定性加法运算。将 FP 数组重新解释为 long 数组后直接进行向量加法，
    /// 充分利用 SIMD 的并行计算能力。
    /// 
    /// <para><b>性能特性：</b></para>
    /// <list type="bullet">
    ///   <item>小数组（&lt; VectorLongCount）：纯标量运算，无 SIMD 开销</item>
    ///   <item>大数组：SIMD 加速比约为 VectorLongCount:1</item>
    ///   <item>内存带宽限制：大规模数据可能受限于内存而非 CPU</item>
    /// </list>
    /// 
    /// <para><b>前置条件：</b></para>
    /// <list type="bullet">
    ///   <item>a.Length == b.Length</item>
    ///   <item>result.Length >= a.Length</item>
    /// </list>
    /// </remarks>
    /// <example>
    /// <code>
    /// FP[] a = { FP._1, FP._2, FP._3, FP._4 };
    /// FP[] b = { FP._1, FP._1, FP._1, FP._1 };
    /// FP[] result = new FP[4];
    /// 
    /// FPSimd.AddBatch(a, b, result);
    /// // result: { 2, 3, 4, 5 }
    /// </code>
    /// </example>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AddBatch(ReadOnlySpan<FP> a, ReadOnlySpan<FP> b, Span<FP> result)
    {
        int count = a.Length;
        if (b.Length != count || result.Length < count)
            throw new ArgumentException("数组长度不匹配");

        // 将 FP 数组转换为 long 原始值数组进行 SIMD 处理
        ReadOnlySpan<long> aRaw = MemoryMarshal.Cast<FP, long>(a);
        ReadOnlySpan<long> bRaw = MemoryMarshal.Cast<FP, long>(b);
        Span<long> resultRaw = MemoryMarshal.Cast<FP, long>(result);

        int i = 0;

        // SIMD 主循环：每次处理 VectorLongCount 个元素
        if (Vector.IsHardwareAccelerated && count >= VectorLongCount)
        {
            int simdLimit = count - (count % VectorLongCount);
            for (; i < simdLimit; i += VectorLongCount)
            {
                Vector<long> va = new Vector<long>(aRaw.Slice(i));
                Vector<long> vb = new Vector<long>(bRaw.Slice(i));
                Vector<long> vr = va + vb;
                vr.CopyTo(resultRaw.Slice(i));
            }
        }

        // 标量收尾：处理剩余元素
        for (; i < count; i++)
        {
            resultRaw[i] = aRaw[i] + bRaw[i];
        }
    }

    #endregion

    #region FP 批量减法

    /// <summary>
    /// 批量减法：result[i] = a[i] - b[i]
    /// </summary>
    /// <param name="a">输入数组 A（被减数）</param>
    /// <param name="b">输入数组 B（减数）</param>
    /// <param name="result">输出数组</param>
    /// <exception cref="ArgumentException">数组长度不匹配时抛出</exception>
    /// <remarks>
    /// <para><b>实现说明：</b></para>
    /// 使用整数 SIMD 实现确定性减法运算。将 FP 数组重新解释为 long 数组后直接进行向量减法。
    /// 
    /// <para><b>性能特性：</b></para>
    /// <list type="bullet">
    ///   <item>与 <see cref="AddBatch"/> 性能特征相同</item>
    ///   <item>标量版本使用简单的 long 减法，无额外开销</item>
    /// </list>
    /// 
    /// <para><b>前置条件：</b></para>
    /// <list type="bullet">
    ///   <item>a.Length == b.Length</item>
    ///   <item>result.Length >= a.Length</item>
    /// </list>
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SubtractBatch(ReadOnlySpan<FP> a, ReadOnlySpan<FP> b, Span<FP> result)
    {
        int count = a.Length;
        if (b.Length != count || result.Length < count)
            throw new ArgumentException("数组长度不匹配");

        ReadOnlySpan<long> aRaw = MemoryMarshal.Cast<FP, long>(a);
        ReadOnlySpan<long> bRaw = MemoryMarshal.Cast<FP, long>(b);
        Span<long> resultRaw = MemoryMarshal.Cast<FP, long>(result);

        int i = 0;

        if (Vector.IsHardwareAccelerated && count >= VectorLongCount)
        {
            int simdLimit = count - (count % VectorLongCount);
            for (; i < simdLimit; i += VectorLongCount)
            {
                Vector<long> va = new Vector<long>(aRaw.Slice(i));
                Vector<long> vb = new Vector<long>(bRaw.Slice(i));
                Vector<long> vr = va - vb;
                vr.CopyTo(resultRaw.Slice(i));
            }
        }

        for (; i < count; i++)
        {
            resultRaw[i] = aRaw[i] - bRaw[i];
        }
    }

    #endregion

    #region FP 批量乘法

    /// <summary>
    /// 批量乘法：result[i] = a[i] * b[i]（四舍五入）
    /// </summary>
    /// <param name="a">输入数组 A</param>
    /// <param name="b">输入数组 B</param>
    /// <param name="result">输出数组</param>
    /// <exception cref="ArgumentException">数组长度不匹配时抛出</exception>
    /// <remarks>
    /// <para><b>定点数乘法公式：</b>(a.Raw * b.Raw + 32768) &gt;&gt; 16</para>
    /// 
    /// <para><b>⚠️ SIMD 限制说明：</b></para>
    /// 当前实现使用标量运算，原因如下：
    /// <list type="number">
    ///   <item>64位乘法在 SIMD 中会产生 128 位结果，Vector&lt;long&gt; 不直接支持</item>
    ///   <item>精度处理需要额外的舍入步骤，SIMD 实现复杂</item>
    ///   <item>定点数乘法的溢出问题需要特殊处理</item>
    /// </list>
    /// 未来优化方向：使用 32 位 SIMD 分拆计算（需验证溢出安全）。
    /// 
    /// <para><b>前置条件：</b></para>
    /// <list type="bullet">
    ///   <item>a.Length == b.Length</item>
    ///   <item>result.Length >= a.Length</item>
    /// </list>
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void MultiplyBatch(ReadOnlySpan<FP> a, ReadOnlySpan<FP> b, Span<FP> result)
    {
        int count = a.Length;
        if (b.Length != count || result.Length < count)
            throw new ArgumentException("数组长度不匹配");

        ReadOnlySpan<long> aRaw = MemoryMarshal.Cast<FP, long>(a);
        ReadOnlySpan<long> bRaw = MemoryMarshal.Cast<FP, long>(b);
        Span<long> resultRaw = MemoryMarshal.Cast<FP, long>(result);

        const long MulRound = 1L << (FP.FRACTIONAL_BITS - 1); // 32768

        // 标量实现：64位乘法需要特殊处理，暂不使用 SIMD
        // 原因：Vector&lt;long&gt; 的乘法操作可能产生溢出或平台差异
        for (int i = 0; i < count; i++)
        {
            long product = aRaw[i] * bRaw[i];
            resultRaw[i] = (product + MulRound) >> FP.FRACTIONAL_BITS;
        }
    }

    #endregion

    #region FP 批量取反

    /// <summary>
    /// 批量取反：result[i] = -input[i]
    /// </summary>
    /// <param name="input">输入数组</param>
    /// <param name="result">输出数组</param>
    /// <exception cref="ArgumentException">输出数组容量不足时抛出</exception>
    /// <remarks>
    /// <para><b>实现说明：</b></para>
    /// 使用 SIMD 整数减法实现：0 - value。利用 Vector.Zero 作为被减数，
    /// 对输入值进行逐元素取反。
    /// 
    /// <para><b>性能特性：</b></para>
    /// <list type="bullet">
    ///   <item>SIMD 版本：利用零向量减法，单指令多数据并行处理</item>
    ///   <item>标量版本：直接使用一元负号运算符</item>
    /// </list>
    /// 
    /// <para><b>前置条件：</b></para>
    /// <list type="bullet">
    ///   <item>result.Length >= input.Length</item>
    /// </list>
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void NegateBatch(ReadOnlySpan<FP> input, Span<FP> result)
    {
        int count = input.Length;
        if (result.Length < count)
            throw new ArgumentException("输出数组容量不足");

        ReadOnlySpan<long> inputRaw = MemoryMarshal.Cast<FP, long>(input);
        Span<long> resultRaw = MemoryMarshal.Cast<FP, long>(result);

        int i = 0;

        if (Vector.IsHardwareAccelerated && count >= VectorLongCount)
        {
            Vector<long> zero = Vector<long>.Zero;
            int simdLimit = count - (count % VectorLongCount);
            for (; i < simdLimit; i += VectorLongCount)
            {
                Vector<long> v = new Vector<long>(inputRaw.Slice(i));
                Vector<long> vr = zero - v;
                vr.CopyTo(resultRaw.Slice(i));
            }
        }

        for (; i < count; i++)
        {
            resultRaw[i] = -inputRaw[i];
        }
    }

    #endregion

    #region FP 批量绝对值

    /// <summary>
    /// 批量绝对值：result[i] = Abs(input[i])
    /// </summary>
    /// <param name="input">输入数组</param>
    /// <param name="result">输出数组</param>
    /// <exception cref="ArgumentException">输出数组容量不足时抛出</exception>
    /// <remarks>
    /// <para><b>实现说明：</b></para>
    /// 使用无分支位运算实现 SIMD 绝对值：
    /// <code>Abs(x) = (x ^ (x &gt;&gt; 63)) - (x &gt;&gt; 63)</code>
    /// 
    /// 利用算术右移扩展符号位，然后通过异或和减法计算绝对值，
    /// 整个过程无分支，适合 SIMD 并行执行。
    /// 
    /// <para><b>⚠️ 边界情况：</b></para>
    /// long.MinValue 的绝对值仍为 long.MinValue（溢出），
    /// 但在 FP 正常使用范围内不会发生。
    /// 
    /// <para><b>前置条件：</b></para>
    /// <list type="bullet">
    ///   <item>result.Length >= input.Length</item>
    /// </list>
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AbsBatch(ReadOnlySpan<FP> input, Span<FP> result)
    {
        int count = input.Length;
        if (result.Length < count)
            throw new ArgumentException("输出数组容量不足");

        ReadOnlySpan<long> inputRaw = MemoryMarshal.Cast<FP, long>(input);
        Span<long> resultRaw = MemoryMarshal.Cast<FP, long>(result);

        int i = 0;

        if (Vector.IsHardwareAccelerated && count >= VectorLongCount)
        {
            int simdLimit = count - (count % VectorLongCount);
            for (; i < simdLimit; i += VectorLongCount)
            {
                Vector<long> v = new Vector<long>(inputRaw.Slice(i));
                // 无分支绝对值：(v ^ (v >> 63)) - (v >> 63)
                Vector<long> sign = v >> 63;
                Vector<long> vr = (v ^ sign) - sign;
                vr.CopyTo(resultRaw.Slice(i));
            }
        }

        for (; i < count; i++)
        {
            long v = inputRaw[i];
            resultRaw[i] = v < 0 ? -v : v;
        }
    }

    #endregion

    #region FPVector2 SIMD 运算

    /// <summary>
    /// 批量 FPVector2 加法：result[i] = a[i] + b[i]
    /// </summary>
    /// <param name="a">输入向量数组 A</param>
    /// <param name="b">输入向量数组 B</param>
    /// <param name="result">输出向量数组</param>
    /// <exception cref="ArgumentException">数组长度不匹配时抛出</exception>
    /// <remarks>
    /// <para><b>内存布局：</b></para>
    /// FPVector2 内存布局为 X(8字节) + Y(8字节) = 16字节。
    /// SIMD 处理时将两个 FPVector2 展开为 4 个 long 进行并行处理。
    /// 
    /// <para><b>性能特性：</b></para>
    /// <list type="bullet">
    ///   <item>每个 FPVector2 包含 2 个 FP 值（16 字节）</item>
    ///   <item>128-bit SIMD: 每次处理 2 个 FPVector2</item>
    ///   <item>256-bit SIMD: 每次处理 4 个 FPVector2</item>
    /// </list>
    /// 
    /// <para><b>前置条件：</b></para>
    /// <list type="bullet">
    ///   <item>a.Length == b.Length</item>
    ///   <item>result.Length >= a.Length</item>
    /// </list>
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AddBatchVector2(ReadOnlySpan<FPVector2> a, ReadOnlySpan<FPVector2> b, Span<FPVector2> result)
    {
        int count = a.Length;
        if (b.Length != count || result.Length < count)
            throw new ArgumentException("数组长度不匹配");

        // 将 FPVector2 数组转换为 long 数组（每个向量2个long，共 2*count 个long）
        ReadOnlySpan<long> aRaw = MemoryMarshal.Cast<FPVector2, long>(a);
        ReadOnlySpan<long> bRaw = MemoryMarshal.Cast<FPVector2, long>(b);
        Span<long> resultRaw = MemoryMarshal.Cast<FPVector2, long>(result);

        int totalLongs = count * 2;
        int i = 0;

        if (Vector.IsHardwareAccelerated && totalLongs >= VectorLongCount)
        {
            int simdLimit = totalLongs - (totalLongs % VectorLongCount);
            for (; i < simdLimit; i += VectorLongCount)
            {
                Vector<long> va = new Vector<long>(aRaw.Slice(i));
                Vector<long> vb = new Vector<long>(bRaw.Slice(i));
                Vector<long> vr = va + vb;
                vr.CopyTo(resultRaw.Slice(i));
            }
        }

        for (; i < totalLongs; i++)
        {
            resultRaw[i] = aRaw[i] + bRaw[i];
        }
    }

    /// <summary>
    /// 批量 FPVector2 减法：result[i] = a[i] - b[i]
    /// </summary>
    /// <param name="a">输入向量数组 A（被减数）</param>
    /// <param name="b">输入向量数组 B（减数）</param>
    /// <param name="result">输出向量数组</param>
    /// <exception cref="ArgumentException">数组长度不匹配时抛出</exception>
    /// <remarks>
    /// <para><b>性能特性：</b></para>
    /// 与 <see cref="AddBatchVector2"/> 性能特征相同。
    /// 
    /// <para><b>前置条件：</b></para>
    /// <list type="bullet">
    ///   <item>a.Length == b.Length</item>
    ///   <item>result.Length >= a.Length</item>
    /// </list>
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SubtractBatchVector2(ReadOnlySpan<FPVector2> a, ReadOnlySpan<FPVector2> b, Span<FPVector2> result)
    {
        int count = a.Length;
        if (b.Length != count || result.Length < count)
            throw new ArgumentException("数组长度不匹配");

        ReadOnlySpan<long> aRaw = MemoryMarshal.Cast<FPVector2, long>(a);
        ReadOnlySpan<long> bRaw = MemoryMarshal.Cast<FPVector2, long>(b);
        Span<long> resultRaw = MemoryMarshal.Cast<FPVector2, long>(result);

        int totalLongs = count * 2;
        int i = 0;

        if (Vector.IsHardwareAccelerated && totalLongs >= VectorLongCount)
        {
            int simdLimit = totalLongs - (totalLongs % VectorLongCount);
            for (; i < simdLimit; i += VectorLongCount)
            {
                Vector<long> va = new Vector<long>(aRaw.Slice(i));
                Vector<long> vb = new Vector<long>(bRaw.Slice(i));
                Vector<long> vr = va - vb;
                vr.CopyTo(resultRaw.Slice(i));
            }
        }

        for (; i < totalLongs; i++)
        {
            resultRaw[i] = aRaw[i] - bRaw[i];
        }
    }

    /// <summary>
    /// 批量 FPVector2 取反：result[i] = -input[i]
    /// </summary>
    /// <param name="input">输入向量数组</param>
    /// <param name="result">输出向量数组</param>
    /// <exception cref="ArgumentException">输出数组容量不足时抛出</exception>
    /// <remarks>
    /// <para><b>前置条件：</b></para>
    /// <list type="bullet">
    ///   <item>result.Length >= input.Length</item>
    /// </list>
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void NegateBatchVector2(ReadOnlySpan<FPVector2> input, Span<FPVector2> result)
    {
        int count = input.Length;
        if (result.Length < count)
            throw new ArgumentException("输出数组容量不足");

        ReadOnlySpan<long> inputRaw = MemoryMarshal.Cast<FPVector2, long>(input);
        Span<long> resultRaw = MemoryMarshal.Cast<FPVector2, long>(result);

        int totalLongs = count * 2;
        int i = 0;

        if (Vector.IsHardwareAccelerated && totalLongs >= VectorLongCount)
        {
            Vector<long> zero = Vector<long>.Zero;
            int simdLimit = totalLongs - (totalLongs % VectorLongCount);
            for (; i < simdLimit; i += VectorLongCount)
            {
                Vector<long> v = new Vector<long>(inputRaw.Slice(i));
                Vector<long> vr = zero - v;
                vr.CopyTo(resultRaw.Slice(i));
            }
        }

        for (; i < totalLongs; i++)
        {
            resultRaw[i] = -inputRaw[i];
        }
    }

    /// <summary>
    /// 批量 FPVector2 标量乘法：result[i] = input[i] * scalar
    /// </summary>
    /// <param name="input">输入向量数组</param>
    /// <param name="scalar">标量乘数</param>
    /// <param name="result">输出向量数组</param>
    /// <exception cref="ArgumentException">输出数组容量不足时抛出</exception>
    /// <remarks>
    /// <para><b>⚠️ 实现限制：</b></para>
    /// 当前使用标量实现，因为定点数乘法需要精度转换 (a * b + 32768) &gt;&gt; 16。
    /// SIMD 实现需要特殊的 64 位乘法分解，将在后续版本优化。
    /// 
    /// <para><b>前置条件：</b></para>
    /// <list type="bullet">
    ///   <item>result.Length >= input.Length</item>
    /// </list>
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void MultiplyBatchVector2(ReadOnlySpan<FPVector2> input, FP scalar, Span<FPVector2> result)
    {
        int count = input.Length;
        if (result.Length < count)
            throw new ArgumentException("输出数组容量不足");

        long s = scalar.RawValue;
        const long MulRound = 1L << (FP.FRACTIONAL_BITS - 1);

        for (int i = 0; i < count; i++)
        {
            FPVector2 v = input[i];
            long x = (v.X.RawValue * s + MulRound) >> FP.FRACTIONAL_BITS;
            long y = (v.Y.RawValue * s + MulRound) >> FP.FRACTIONAL_BITS;
            result[i] = new FPVector2(FP.FromRaw(x), FP.FromRaw(y));
        }
    }

    #endregion

    #region 性能工具方法

    /// <summary>
    /// 获取适合 SIMD 处理的批次大小（向上取整到 VectorLongCount 的倍数）
    /// </summary>
    /// <param name="minSize">最小需要处理的元素数量</param>
    /// <returns>对齐后的批次大小</returns>
    /// <remarks>
    /// <para><b>使用场景：</b></para>
    /// 用于预分配数组或缓冲区，确保能够充分利用 SIMD 处理能力。
    /// 
    /// <para><b>计算示例：</b></para>
    /// <code>
    /// // 假设 VectorLongCount = 4（256-bit SIMD）
    /// int aligned = FPSimd.GetAlignedBatchSize(5);  // 返回 8
    /// int aligned = FPSimd.GetAlignedBatchSize(4);  // 返回 4
    /// int aligned = FPSimd.GetAlignedBatchSize(1);  // 返回 4
    /// </code>
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetAlignedBatchSize(int minSize)
    {
        return (minSize + VectorLongCount - 1) & ~(VectorLongCount - 1);
    }

    /// <summary>
    /// 检查指定数组长度是否适合使用 SIMD 加速
    /// </summary>
    /// <param name="count">元素数量</param>
    /// <returns>如果适合 SIMD 加速返回 true</returns>
    /// <remarks>
    /// <para><b>判断标准：</b></para>
    /// <list type="bullet">
    ///   <item>硬件支持 SIMD（<see cref="IsHardwareAccelerated"/> 为 true）</item>
    ///   <item>元素数量 >= VectorLongCount</item>
    /// </list>
    /// 
    /// 当返回 false 时，批量操作将使用纯标量实现，性能可能不如逐个调用普通方法。
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsSimdBeneficial(int count)
    {
        return Vector.IsHardwareAccelerated && count >= VectorLongCount;
    }

    #endregion
}
