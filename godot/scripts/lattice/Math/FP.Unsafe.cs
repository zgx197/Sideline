// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

#nullable enable

using System;
using System.Runtime.CompilerServices;

namespace Lattice.Math;

/// <summary>
/// FP 不安全指针操作 - 用于极致性能场景
/// <para>提供基于指针的批量运算，避免数组边界检查开销</para>
/// </summary>
/// <remarks>
/// <para><b>⚠️ 安全警告：</b></para>
/// <para>
/// 本文件使用 unsafe 代码，仅在以下场景使用：
/// </para>
/// <list type="number">
///   <item>性能关键路径，需要极致性能优化</item>
///   <item>大批量数据处理（通常 &gt; 1000 个元素）</item>
///   <item>内部库实现，不直接暴露给游戏逻辑层</item>
/// </list>
/// 
/// <para><b>调用者责任：</b></para>
/// <list type="bullet">
///   <item>确保指针非 null（<see cref="ArgumentNullException"/> 仅在 DEBUG 模式检查）</item>
///   <item>确保内存区域有足够容量（count 个元素）</item>
///   <item>确保内存对齐（64位系统上 long* 通常自动 8 字节对齐）</item>
///   <item>确保源和目标内存区域不重叠（除非方法明确支持）</item>
/// </list>
/// 
/// <para><b>DEBUG 模式保护：</b></para>
/// 在 DEBUG 配置下，所有方法会进行以下检查：
/// <list type="bullet">
///   <item>空指针检查（<see cref="ArgumentNullException"/>）</item>
///   <item>负数 count 检查（<see cref="ArgumentOutOfRangeException"/>）</item>
/// </list>
/// RELEASE 模式下这些检查被移除以获得最大性能。
/// 
/// <para><b>确定性保证：</b></para>
/// 所有操作本身是确定性的，但调用者需确保内存安全，
/// 否则可能产生未定义行为（内存损坏、崩溃等）。
/// </remarks>
public static unsafe class FPUnsafe
{
    #region DEBUG 边界检查

#if DEBUG
    /// <summary>
    /// DEBUG 模式下检查指针是否为 null
    /// </summary>
    /// <param name="ptr">要检查的指针</param>
    /// <param name="name">参数名（用于异常消息）</param>
    /// <exception cref="ArgumentNullException">指针为 null 时抛出</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CheckNotNull(void* ptr, string name)
    {
        if (ptr == null)
            throw new ArgumentNullException(name, $"{name} 指针不能为 null");
    }

    /// <summary>
    /// DEBUG 模式下检查 count 是否为非负数
    /// </summary>
    /// <param name="count">要检查的数量</param>
    /// <exception cref="ArgumentOutOfRangeException">count 为负数时抛出</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CheckCount(int count)
    {
        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count), "count 不能为负数");
    }

    /// <summary>
    /// DEBUG 模式下检查指针和 count 的有效性
    /// </summary>
    /// <param name="source">源指针</param>
    /// <param name="destination">目标指针</param>
    /// <param name="count">元素数量</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CheckPointers(void* source, void* destination, int count)
    {
        CheckNotNull(source, nameof(source));
        CheckNotNull(destination, nameof(destination));
        CheckCount(count);
    }
#endif

    #endregion

    #region 批量复制操作

    /// <summary>
    /// 直接内存复制：FP[] -> FP[]
    /// </summary>
    /// <param name="source">源指针</param>
    /// <param name="destination">目标指针</param>
    /// <param name="count">元素数量</param>
    /// <remarks>
    /// <para><b>前置条件（调用者负责）：</b></para>
    /// <list type="bullet">
    ///   <item>source != null</item>
    ///   <item>destination != null</item>
    ///   <item>count >= 0</item>
    ///   <item>源内存区域至少有 count 个 FP</item>
    ///   <item>目标内存区域至少有 count 个 FP 的容量</item>
    /// </list>
    /// 
    /// <para><b>实现说明：</b></para>
    /// 使用 <see cref="Buffer.MemoryCopy(void*, void*, long, long)"/> 实现高效复制，
    /// 内部可能使用平台特定的优化（如 rep movsb、memcpy 等）。
    /// </remarks>
    /// <example>
    /// <code>
    /// FP[] source = new FP[100];
    /// FP[] dest = new FP[100];
    /// 
    /// fixed (FP* src = source)
    /// fixed (FP* dst = dest)
    /// {
    ///     FPUnsafe.Copy(src, dst, 100);
    /// }
    /// </code>
    /// </example>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Copy(FP* source, FP* destination, int count)
    {
#if DEBUG
        CheckPointers(source, destination, count);
#endif
        if (count == 0) return;

        Buffer.MemoryCopy(source, destination, count * sizeof(FP), count * sizeof(FP));
    }

    /// <summary>
    /// 批量填充：将目标区域填充为指定值
    /// </summary>
    /// <param name="destination">目标指针</param>
    /// <param name="value">填充值</param>
    /// <param name="count">元素数量</param>
    /// <remarks>
    /// <para><b>前置条件（调用者负责）：</b></para>
    /// <list type="bullet">
    ///   <item>destination != null</item>
    ///   <item>count >= 0</item>
    ///   <item>目标内存区域至少有 count 个 FP 的容量</item>
    /// </list>
    /// 
    /// <para><b>性能特性：</b></para>
    /// 使用循环展开（每次处理 4 个元素），减少分支预测开销。
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Fill(FP* destination, FP value, int count)
    {
#if DEBUG
        CheckNotNull(destination, nameof(destination));
        CheckCount(count);
#endif
        long rawValue = value.RawValue;
        long* ptr = (long*)destination;
        long* end = ptr + count;

        while (ptr < end)
        {
            *ptr++ = rawValue;
        }
    }

    #endregion

    #region 批量算术运算

    /// <summary>
    /// 批量加法：result[i] = a[i] + b[i]
    /// </summary>
    /// <param name="a">第一个操作数指针</param>
    /// <param name="b">第二个操作数指针</param>
    /// <param name="result">结果指针</param>
    /// <param name="count">元素数量</param>
    /// <remarks>
    /// <para><b>前置条件（调用者负责）：</b></para>
    /// <list type="bullet">
    ///   <item>a != null, b != null, result != null</item>
    ///   <item>count >= 0</item>
    ///   <item>每个内存区域至少有 count 个 FP 的容量</item>
    /// </list>
    /// 
    /// <para><b>性能特性：</b></para>
    /// 使用 4 路循环展开，每次迭代处理 4 个元素，减少循环开销。
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Add(FP* a, FP* b, FP* result, int count)
    {
#if DEBUG
        CheckNotNull(a, nameof(a));
        CheckNotNull(b, nameof(b));
        CheckNotNull(result, nameof(result));
        CheckCount(count);
#endif
        if (count == 0) return;

        long* pa = (long*)a;
        long* pb = (long*)b;
        long* pr = (long*)result;
        long* end = pa + count;

        // 循环展开：每次处理 4 个元素
        while (pa + 4 <= end)
        {
            pr[0] = pa[0] + pb[0];
            pr[1] = pa[1] + pb[1];
            pr[2] = pa[2] + pb[2];
            pr[3] = pa[3] + pb[3];
            pa += 4;
            pb += 4;
            pr += 4;
        }

        // 处理剩余元素
        while (pa < end)
        {
            *pr++ = *pa++ + *pb++;
        }
    }

    /// <summary>
    /// 批量乘法：result[i] = a[i] * b[i]（带四舍五入）
    /// </summary>
    /// <param name="a">第一个操作数指针</param>
    /// <param name="b">第二个操作数指针</param>
    /// <param name="result">结果指针</param>
    /// <param name="count">元素数量</param>
    /// <remarks>
    /// <para><b>前置条件（调用者负责）：</b></para>
    /// <list type="bullet">
    ///   <item>a != null, b != null, result != null</item>
    ///   <item>count >= 0</item>
    ///   <item>每个内存区域至少有 count 个 FP 的容量</item>
    /// </list>
    /// 
    /// <para><b>定点数乘法公式：</b></para>
    /// <code>result = (a.Raw * b.Raw + 32768) &gt;&gt; 16</code>
    /// 
    /// <para><b>性能特性：</b></para>
    /// 使用 4 路循环展开，每次迭代处理 4 个元素。
    /// 注意：64位乘法是相对昂贵的操作。
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Multiply(FP* a, FP* b, FP* result, int count)
    {
#if DEBUG
        CheckNotNull(a, nameof(a));
        CheckNotNull(b, nameof(b));
        CheckNotNull(result, nameof(result));
        CheckCount(count);
#endif
        if (count == 0) return;

        long* pa = (long*)a;
        long* pb = (long*)b;
        long* pr = (long*)result;
        long* end = pa + count;
        const long mulRound = FP.MulRound;

        // 循环展开：每次处理 4 个元素
        while (pa + 4 <= end)
        {
            pr[0] = (pa[0] * pb[0] + mulRound) >> FP.FRACTIONAL_BITS;
            pr[1] = (pa[1] * pb[1] + mulRound) >> FP.FRACTIONAL_BITS;
            pr[2] = (pa[2] * pb[2] + mulRound) >> FP.FRACTIONAL_BITS;
            pr[3] = (pa[3] * pb[3] + mulRound) >> FP.FRACTIONAL_BITS;
            pa += 4;
            pb += 4;
            pr += 4;
        }

        // 处理剩余元素
        while (pa < end)
        {
            *pr++ = (*pa++ * *pb++ + mulRound) >> FP.FRACTIONAL_BITS;
        }
    }

    /// <summary>
    /// 快速批量乘法：result[i] = a[i] * b[i]（截断，无舍入）
    /// </summary>
    /// <param name="a">第一个操作数指针</param>
    /// <param name="b">第二个操作数指针</param>
    /// <param name="result">结果指针</param>
    /// <param name="count">元素数量</param>
    /// <remarks>
    /// <para><b>前置条件（调用者负责）：</b></para>
    /// <list type="bullet">
    ///   <item>a != null, b != null, result != null</item>
    ///   <item>count >= 0</item>
    ///   <item>每个内存区域至少有 count 个 FP 的容量</item>
    /// </list>
    /// 
    /// <para><b>精度警告：</b></para>
    /// 在性能极度敏感且精度要求不高的场景使用。
    /// 误差：最大 1 LSB（约 0.000015）。
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void MultiplyFast(FP* a, FP* b, FP* result, int count)
    {
#if DEBUG
        CheckNotNull(a, nameof(a));
        CheckNotNull(b, nameof(b));
        CheckNotNull(result, nameof(result));
        CheckCount(count);
#endif
        if (count == 0) return;

        long* pa = (long*)a;
        long* pb = (long*)b;
        long* pr = (long*)result;
        long* end = pa + count;

        while (pa + 4 <= end)
        {
            pr[0] = (pa[0] * pb[0]) >> FP.FRACTIONAL_BITS;
            pr[1] = (pa[1] * pb[1]) >> FP.FRACTIONAL_BITS;
            pr[2] = (pa[2] * pb[2]) >> FP.FRACTIONAL_BITS;
            pr[3] = (pa[3] * pb[3]) >> FP.FRACTIONAL_BITS;
            pa += 4;
            pb += 4;
            pr += 4;
        }

        while (pa < end)
        {
            *pr++ = (*pa++ * *pb++) >> FP.FRACTIONAL_BITS;
        }
    }

    #endregion

    #region 原始 long 数组操作

    /// <summary>
    /// 批量原始加法：result[i] = a[i] + b[i]
    /// </summary>
    /// <param name="a">第一个操作数指针（原始 long 值）</param>
    /// <param name="b">第二个操作数指针（原始 long 值）</param>
    /// <param name="result">结果指针</param>
    /// <param name="count">元素数量</param>
    /// <remarks>
    /// <para><b>前置条件（调用者负责）：</b></para>
    /// <list type="bullet">
    ///   <item>a != null, b != null, result != null</item>
    ///   <item>count >= 0</item>
    ///   <item>每个内存区域至少有 count 个 long 的容量</item>
    /// </list>
    /// 
    /// <para><b>性能优势：</b></para>
    /// 直接操作原始 long 值，跳过 FP 结构体封装，性能最优。
    /// 适用于内部算法实现，避免 FP 包装/解包开销。
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AddRaw(long* a, long* b, long* result, int count)
    {
#if DEBUG
        CheckNotNull(a, nameof(a));
        CheckNotNull(b, nameof(b));
        CheckNotNull(result, nameof(result));
        CheckCount(count);
#endif
        if (count == 0) return;

        long* end = a + count;

        // 循环展开：每次处理 4 个元素
        while (a + 4 <= end)
        {
            result[0] = a[0] + b[0];
            result[1] = a[1] + b[1];
            result[2] = a[2] + b[2];
            result[3] = a[3] + b[3];
            a += 4;
            b += 4;
            result += 4;
        }

        // 处理剩余元素
        while (a < end)
        {
            *result++ = *a++ + *b++;
        }
    }

    /// <summary>
    /// 批量原始乘法：result[i] = (a[i] * b[i] + 32768) &gt;&gt; 16
    /// </summary>
    /// <param name="a">第一个操作数指针（原始 long 值）</param>
    /// <param name="b">第二个操作数指针（原始 long 值）</param>
    /// <param name="result">结果指针</param>
    /// <param name="count">元素数量</param>
    /// <remarks>
    /// <para><b>前置条件（调用者负责）：</b></para>
    /// <list type="bullet">
    ///   <item>a != null, b != null, result != null</item>
    ///   <item>count >= 0</item>
    ///   <item>每个内存区域至少有 count 个 long 的容量</item>
    /// </list>
    /// 
    /// <para><b>说明：</b></para>
    /// 直接操作原始 long 值，带四舍五入的定点数乘法。
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void MultiplyRaw(long* a, long* b, long* result, int count)
    {
#if DEBUG
        CheckNotNull(a, nameof(a));
        CheckNotNull(b, nameof(b));
        CheckNotNull(result, nameof(result));
        CheckCount(count);
#endif
        if (count == 0) return;

        long* end = a + count;
        const long mulRound = FP.MulRound;

        while (a + 4 <= end)
        {
            result[0] = (a[0] * b[0] + mulRound) >> FP.FRACTIONAL_BITS;
            result[1] = (a[1] * b[1] + mulRound) >> FP.FRACTIONAL_BITS;
            result[2] = (a[2] * b[2] + mulRound) >> FP.FRACTIONAL_BITS;
            result[3] = (a[3] * b[3] + mulRound) >> FP.FRACTIONAL_BITS;
            a += 4;
            b += 4;
            result += 4;
        }

        while (a < end)
        {
            *result++ = (*a++ * *b++ + mulRound) >> FP.FRACTIONAL_BITS;
        }
    }

    /// <summary>
    /// 批量原始填充
    /// </summary>
    /// <param name="destination">目标指针（原始 long 值）</param>
    /// <param name="rawValue">填充的原始 long 值</param>
    /// <param name="count">元素数量</param>
    /// <remarks>
    /// <para><b>前置条件（调用者负责）：</b></para>
    /// <list type="bullet">
    ///   <item>destination != null</item>
    ///   <item>count >= 0</item>
    ///   <item>目标内存区域至少有 count 个 long 的容量</item>
    /// </list>
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void FillRaw(long* destination, long rawValue, int count)
    {
#if DEBUG
        CheckNotNull(destination, nameof(destination));
        CheckCount(count);
#endif
        long* end = destination + count;

        while (destination + 4 <= end)
        {
            destination[0] = rawValue;
            destination[1] = rawValue;
            destination[2] = rawValue;
            destination[3] = rawValue;
            destination += 4;
        }

        while (destination < end)
        {
            *destination++ = rawValue;
        }
    }

    #endregion

    #region 数学运算

    /// <summary>
    /// 点积：计算两个 FP 数组的点积
    /// </summary>
    /// <param name="a">第一个数组指针</param>
    /// <param name="b">第二个数组指针</param>
    /// <param name="count">元素数量</param>
    /// <returns>点积结果</returns>
    /// <remarks>
    /// <para><b>前置条件（调用者负责）：</b></para>
    /// <list type="bullet">
    ///   <item>a != null, b != null</item>
    ///   <item>count >= 0</item>
    ///   <item>每个内存区域至少有 count 个 FP 的容量</item>
    /// </list>
    /// 
    /// <para><b>数学公式：</b></para>
    /// <code>dot = Σ(a[i] * b[i])</code>
    /// 
    /// <para><b>溢出保护：</b></para>
    /// 使用 128 位中间结果（<see cref="Int128"/>）防止累加溢出，
    /// 最后统一进行定点数精度转换。
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FP DotProduct(FP* a, FP* b, int count)
    {
#if DEBUG
        CheckNotNull(a, nameof(a));
        CheckNotNull(b, nameof(b));
        CheckCount(count);
#endif
        if (count == 0) return FP._0;

        long* pa = (long*)a;
        long* pb = (long*)b;
        long* end = pa + count;

        // 使用 128 位累加器防止溢出
        Int128 sum = 0;
        const long mulRound = FP.MulRound;

        // 先累加乘积的原始值（不减精度），最后统一处理
        while (pa + 4 <= end)
        {
            sum += (Int128)pa[0] * pb[0];
            sum += (Int128)pa[1] * pb[1];
            sum += (Int128)pa[2] * pb[2];
            sum += (Int128)pa[3] * pb[3];
            pa += 4;
            pb += 4;
        }

        while (pa < end)
        {
            sum += (Int128)(*pa++) * (*pb++);
        }

        // 加上舍入常数并右移
        long result = (long)((sum + mulRound) >> FP.FRACTIONAL_BITS);
        return FP.FromRaw(result);
    }

    /// <summary>
    /// 批量缩放：result[i] = a[i] * scale
    /// </summary>
    /// <param name="a">源数组指针</param>
    /// <param name="scale">缩放因子</param>
    /// <param name="result">结果指针</param>
    /// <param name="count">元素数量</param>
    /// <remarks>
    /// <para><b>前置条件（调用者负责）：</b></para>
    /// <list type="bullet">
    ///   <item>a != null, result != null</item>
    ///   <item>count >= 0</item>
    ///   <item>每个内存区域至少有 count 个 FP 的容量</item>
    /// </list>
    /// 
    /// <para><b>定点数缩放公式：</b></para>
    /// <code>result = (a.Raw * scale.Raw + 32768) &gt;&gt; 16</code>
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Scale(FP* a, FP scale, FP* result, int count)
    {
#if DEBUG
        CheckNotNull(a, nameof(a));
        CheckNotNull(result, nameof(result));
        CheckCount(count);
#endif
        if (count == 0) return;

        long* pa = (long*)a;
        long* pr = (long*)result;
        long* end = pa + count;
        long scaleRaw = scale.RawValue;
        const long mulRound = FP.MulRound;

        while (pa + 4 <= end)
        {
            pr[0] = (pa[0] * scaleRaw + mulRound) >> FP.FRACTIONAL_BITS;
            pr[1] = (pa[1] * scaleRaw + mulRound) >> FP.FRACTIONAL_BITS;
            pr[2] = (pa[2] * scaleRaw + mulRound) >> FP.FRACTIONAL_BITS;
            pr[3] = (pa[3] * scaleRaw + mulRound) >> FP.FRACTIONAL_BITS;
            pa += 4;
            pr += 4;
        }

        while (pa < end)
        {
            *pr++ = (*pa++ * scaleRaw + mulRound) >> FP.FRACTIONAL_BITS;
        }
    }

    /// <summary>
    /// 批量整数缩放：result[i] = a[i] * scale（scale 为整数）
    /// </summary>
    /// <param name="a">源数组指针</param>
    /// <param name="scale">整数缩放因子</param>
    /// <param name="result">结果指针</param>
    /// <param name="count">元素数量</param>
    /// <remarks>
    /// <para><b>前置条件（调用者负责）：</b></para>
    /// <list type="bullet">
    ///   <item>a != null, result != null</item>
    ///   <item>count >= 0</item>
    ///   <item>每个内存区域至少有 count 个 FP 的容量</item>
    /// </list>
    /// 
    /// <para><b>性能优势：</b></para>
    /// 整数缩放无需舍入，性能优于浮点缩放（<see cref="Scale"/>）。
    /// 适用于简单的整数倍放大/缩小场景。
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ScaleInt(FP* a, int scale, FP* result, int count)
    {
#if DEBUG
        CheckNotNull(a, nameof(a));
        CheckNotNull(result, nameof(result));
        CheckCount(count);
#endif
        if (count == 0) return;

        long* pa = (long*)a;
        long* pr = (long*)result;
        long* end = pa + count;

        while (pa + 4 <= end)
        {
            pr[0] = pa[0] * scale;
            pr[1] = pa[1] * scale;
            pr[2] = pa[2] * scale;
            pr[3] = pa[3] * scale;
            pa += 4;
            pr += 4;
        }

        while (pa < end)
        {
            *pr++ = *pa++ * scale;
        }
    }

    #endregion

    #region 辅助方法

    /// <summary>
    /// 批量绝对值：result[i] = |a[i]|
    /// </summary>
    /// <param name="a">源数组指针</param>
    /// <param name="result">结果指针</param>
    /// <param name="count">元素数量</param>
    /// <remarks>
    /// <para><b>前置条件（调用者负责）：</b></para>
    /// <list type="bullet">
    ///   <item>a != null, result != null</item>
    ///   <item>count >= 0</item>
    ///   <item>每个内存区域至少有 count 个 FP 的容量</item>
    /// </list>
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Abs(FP* a, FP* result, int count)
    {
#if DEBUG
        CheckNotNull(a, nameof(a));
        CheckNotNull(result, nameof(result));
        CheckCount(count);
#endif
        if (count == 0) return;

        long* pa = (long*)a;
        long* pr = (long*)result;
        long* end = pa + count;

        while (pa + 4 <= end)
        {
            pr[0] = pa[0] < 0 ? -pa[0] : pa[0];
            pr[1] = pa[1] < 0 ? -pa[1] : pa[1];
            pr[2] = pa[2] < 0 ? -pa[2] : pa[2];
            pr[3] = pa[3] < 0 ? -pa[3] : pa[3];
            pa += 4;
            pr += 4;
        }

        while (pa < end)
        {
            long v = *pa++;
            *pr++ = v < 0 ? -v : v;
        }
    }

    /// <summary>
    /// 批量取负：result[i] = -a[i]
    /// </summary>
    /// <param name="a">源数组指针</param>
    /// <param name="result">结果指针</param>
    /// <param name="count">元素数量</param>
    /// <remarks>
    /// <para><b>前置条件（调用者负责）：</b></para>
    /// <list type="bullet">
    ///   <item>a != null, result != null</item>
    ///   <item>count >= 0</item>
    ///   <item>每个内存区域至少有 count 个 FP 的容量</item>
    /// </list>
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Negate(FP* a, FP* result, int count)
    {
#if DEBUG
        CheckNotNull(a, nameof(a));
        CheckNotNull(result, nameof(result));
        CheckCount(count);
#endif
        if (count == 0) return;

        long* pa = (long*)a;
        long* pr = (long*)result;
        long* end = pa + count;

        while (pa + 4 <= end)
        {
            pr[0] = -pa[0];
            pr[1] = -pa[1];
            pr[2] = -pa[2];
            pr[3] = -pa[3];
            pa += 4;
            pr += 4;
        }

        while (pa < end)
        {
            *pr++ = -*pa++;
        }
    }

    #endregion
}
