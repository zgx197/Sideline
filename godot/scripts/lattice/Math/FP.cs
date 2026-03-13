// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

#nullable enable

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Lattice.Math;

/// <summary>
/// 定点数 (Q48.16)：48位整数 + 16位小数
/// </summary>
/// <remarks>
/// <para>Lattice 定点数系统使用 64 位有符号整数存储，其中：</para>
/// <list type="bullet">
///   <item>48 位：整数部分（范围约 ±140万亿）</item>
///   <item>16 位：小数部分（精度约 0.000015）</item>
/// </list>
/// <para>安全乘法范围：±32,768（两个操作数都需在此范围内）</para>
/// <para>参考：FrameSyncEngine / Photon Quantum 设计</para>
/// <para>用途：确定性帧同步，完全替代 float/double</para>
/// </remarks>
/// <example>
/// 基本使用：
/// <code>
/// // 初始化 LUT（游戏启动时）
/// FPLut.InitializeBuiltIn();
/// 
/// // 创建定点数
/// FP a = 3.5;                    // 隐式转换
/// FP b = FP.FromRaw(FP.Raw._1_50);  // 使用原始常量
/// 
/// // 数学运算
/// FP c = a * b;                // 乘法（四舍五入）
/// FP d = FP.Sqrt(c);           // 平方根
/// FP e = FP.Sin(a);            // 正弦
/// </code>
/// </example>
[StructLayout(LayoutKind.Explicit, Size = 8)]  // 8 bytes, Explicit for cross-platform consistency
public readonly partial struct FP : IEquatable<FP>, IComparable<FP>
{
    #region 常量定义

    /// <summary>小数位数量</summary>
    public const int FRACTIONAL_BITS = 16;
    
    /// <summary>比例因子 = 2^16 = 65536</summary>
    public const long ONE = 1L << FRACTIONAL_BITS;
    
    /// <summary>乘法舍入常量：0.5 LSB = 2^15</summary>
    /// <remarks>
    /// 用于乘法四舍五入：(a * b + MulRound) >> 16
    /// 相比截断，精度误差减半
    /// </remarks>
    internal const long MulRound = 1L << (FRACTIONAL_BITS - 1);  // 32768
    
    /// <summary>
    /// 安全乘法上限：sqrt(long.MaxValue) ≈ 3,037,000,499
    /// 对应真实值约 46,340
    /// <para>两个 FP 都 ≤ UseableMax 时，乘法不会溢出 long</para>
    /// </summary>
    public static readonly FP UseableMax = new(3037000499L);
    
    /// <summary>安全乘法下限（对应真实值约 -46,340）</summary>
    public static readonly FP UseableMin = new(-3037000499L);
    
    /// <summary>
    /// 最小精度：1/65536 ≈ 0.000015
    /// <para>注意：这是 Q48.16 能表示的最小单位，不是 float.Epsilon 的概念</para>
    /// </summary>
    public static readonly FP Epsilon = new(1L);
    
    /// <summary>
    /// 默认近似比较容差（10倍精度，约 0.00015）
    /// <para>用于 Approximately 的默认参数</para>
    /// </summary>
    public static readonly FP EpsilonDefault = new(10L);
    
    /// <summary>零</summary>
    public static FP Zero => new(0);
    
    /// <summary>一</summary>
    public static FP One => new(ONE);
    
    /// <summary>二</summary>
    public static FP Two => new(ONE * 2);
    
    /// <summary>一半 (0.5)</summary>
    public static FP Half => new(ONE >> 1);
    
    /// <summary>π (3.14159...)</summary>
    public static FP Pi => new(205887L);       // 3.1415863037109375
    
    /// <summary>2π</summary>
    public static FP Pi2 => new(411774L);      // 6.283172607421875
    
    /// <summary>π/2</summary>
    public static FP PiHalf => new(102943L);   // 1.57079315185546875
    
    // 快捷常量命名（参考 Quantum 风格）
    public static FP _0 => new(0);
    public static FP _1 => new(ONE);
    public static FP _2 => new(ONE * 2);
    public static FP _3 => new(ONE * 3);
    public static FP _4 => new(ONE * 4);
    public static FP _5 => new(ONE * 5);
    public static FP _10 => new(ONE * 10);
    public static FP _100 => new(ONE * 100);
    public static FP _1000 => new(ONE * 1000);
    
    public static FP _0_01 => new(655);        // 0.01 (655.36 截断)
    public static FP _0_05 => new(3277);       // 0.05 (3276.8 四舍五入)
    public static FP _0_10 => new(6554);       // 0.1
    public static FP _0_25 => new(16384);      // 0.25 (精确)
    public static FP _0_50 => new(ONE >> 1);   // 0.5
    public static FP _0_75 => new((ONE >> 1) + (ONE >> 2)); // 0.75
    public static FP _1_50 => new(ONE + (ONE >> 1));        // 1.5

    /// <summary>
    /// Raw 常量：用于编译期确定的场景
    /// <para>命名规范：_整数 或 _整数_小数</para>
    /// </summary>
    public static class Raw
    {
        // ==================== 整数常量 ====================
        public const long _0 = 0;
        public const long _1 = ONE;
        public const long _2 = ONE * 2;
        public const long _3 = ONE * 3;
        public const long _4 = ONE * 4;
        public const long _5 = ONE * 5;
        public const long _6 = ONE * 6;
        public const long _7 = ONE * 7;
        public const long _8 = ONE * 8;
        public const long _9 = ONE * 9;
        public const long _10 = ONE * 10;
        public const long _99 = 6488064L;      // 99
        public const long _100 = ONE * 100;
        public const long _180 = 11796480L;    // 180
        public const long _200 = 13107200L;    // 200
        public const long _360 = 23592960L;    // 360
        public const long _1000 = ONE * 1000;
        public const long _10000 = 655360000L; // 10000
        
        // ==================== 小数常量 (0.01 ~ 0.99) ====================
        public const long _0_01 = 655L;        // 0.01
        public const long _0_02 = 1311L;       // 0.02
        public const long _0_03 = 1966L;       // 0.03
        public const long _0_04 = 2621L;       // 0.04
        public const long _0_05 = 3277L;       // 0.05
        public const long _0_10 = 6554L;       // 0.1
        public const long _0_20 = 13107L;      // 0.2
        public const long _0_25 = 16384L;      // 0.25 (精确)
        public const long _0_33 = 21845L;      // 0.33 (1/3 近似)
        public const long _0_50 = ONE >> 1;    // 0.5
        public const long _0_66 = 43691L;      // 0.66 (2/3 近似)
        public const long _0_75 = (ONE >> 1) + (ONE >> 2); // 0.75
        public const long _0_99 = 64881L;      // 0.99
        
        // ==================== 大于1的常量 ====================
        public const long _1_01 = 66191L;      // 1.01
        public const long _1_02 = 66847L;      // 1.02
        public const long _1_03 = 67502L;      // 1.03
        public const long _1_04 = 68157L;      // 1.04
        public const long _1_05 = 68813L;      // 1.05
        public const long _1_10 = 72090L;      // 1.10
        public const long _1_20 = 78643L;      // 1.20
        public const long _1_25 = 81920L;      // 1.25
        public const long _1_33 = 87381L;      // 1.33 (4/3 近似)
        public const long _1_50 = ONE + (ONE >> 1); // 1.5
        public const long _1_75 = 114688L;     // 1.75
        public const long _1_99 = 130417L;     // 1.99
        
        // ==================== 角度弧度常量 ====================
        public const long _PI = 205887;        // π ≈ 3.14159
        public const long _2Pi = 411774;       // 2π
        public const long _PiHalf = 102943;    // π/2
        public const long _PiInv = 20861L;     // 1/π
        public const long _PiOver2Inv = 41722L;// 2/π
        public const long Rad_360 = 411775L;   // 2π (360度)
        public const long Rad_180 = 205887L;   // π (180度)
        public const long Rad_90 = 102944L;    // π/2 (90度)
        public const long Rad_45 = 51472L;     // π/4 (45度)
        public const long Rad_30 = 34315L;     // π/6 (30度)
        public const long Rad_22_50 = 25736L;  // π/8 (22.5度)
        
        // ==================== 角度转换常量 ====================
        public const long _Deg2Rad = 1144;      // π/180 ≈ 0.0174533
        public const long _Rad2Deg = 3754936;   // 180/π ≈ 57.2958
        
        // ==================== 极小量常量 (EN = Epsilon N) ====================
        public const long EN1 = 6554L;         // 0.1
        public const long EN2 = 655L;          // 0.01
        public const long EN3 = 66L;           // 0.001 (Epsilon)
        public const long EN4 = 7L;            // 0.0001
        public const long EN5 = 1L;            // 0.000015 (最小精度)
        
        // ==================== 数学常量 ====================
        public const long _E = 178145L;        // e ≈ 2.71828
        public const long Log2_E = 94548L;     // log2(e)
        public const long Log2_10 = 217706L;   // log2(10)
        
        // ==================== 极值常量 ====================
        public const long _MaxValue = 3037000499L;  // UseableMax
        public const long _MinValue = -3037000499L; // UseableMin
        public const long _Epsilon = 1;             // 最小精度
        public const long Minus_1 = -65536L;        // -1
    }

    #endregion

    #region 存储与构造

    /// <summary>内部原始值 (Q48.16 格式)</summary>
    [FieldOffset(0)]
    public readonly long RawValue;

    /// <summary>
    /// 内部构造函数，允许同程序集内的类型创建实例
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal FP(long raw) => RawValue = raw;

    /// <summary>
    /// 从原始值构造 FP（安全的公开 API）
    /// </summary>
    /// <example>
    /// FP angle = FP.FromRaw(FP.Raw._PI);
    /// </example>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FP FromRaw(long raw) => new(raw);

    #endregion

    #region 类型转换

    // ==================== 整数（隐式，确定性安全） ====================

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator FP(int value) => new((long)value << FRACTIONAL_BITS);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator FP(long value) => new(value << FRACTIONAL_BITS);

    /// <summary>
    /// 转换为 int（截断小数部分）
    /// <para>如果值超出 int 范围（±2,147,483,647），将抛出 OverflowException</para>
    /// </summary>
    /// <exception cref="OverflowException">当值超出 int 范围时抛出</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator int(FP value)
    {
        long intPart = value.RawValue >> FRACTIONAL_BITS;
        if (intPart > int.MaxValue || intPart < int.MinValue)
            throw new OverflowException($"FP value {intPart} cannot be converted to int (range: {int.MinValue} to {int.MaxValue})");
        return (int)intPart;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator long(FP value) => value.RawValue >> FRACTIONAL_BITS;

    // ==================== 浮点数（显式 UNSAFE，禁止隐式） ====================

#if DEBUG
    [Obsolete("警告：FromFloat_UNSAFE 只能在编辑器/配置阶段使用！" +
              "模拟/运行时使用会导致非确定性。请使用预计算的 Raw 值，如 FP.FromRaw(FP.Raw._1_50)", true)]
#endif
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FP FromFloat_UNSAFE(float f) => new((long)(f * ONE));

#if DEBUG
    [Obsolete("警告：FromDouble_UNSAFE 只能在编辑器/配置阶段使用！" +
              "模拟/运行时使用会导致非确定性。", true)]
#endif
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FP FromDouble_UNSAFE(double d) => new((long)(d * ONE));

#if DEBUG
    [Obsolete("警告：ToFloat_UNSAFE 只能在调试/渲染中使用！" +
              "模拟/运行时使用会导致非确定性。", true)]
#endif
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float ToFloat_UNSAFE() => RawValue / (float)ONE;

#if DEBUG
    [Obsolete("警告：ToDouble_UNSAFE 只能在调试/渲染中使用！", true)]
#endif
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double ToDouble_UNSAFE() => RawValue / (double)ONE;

    // 显式运算符（会抛出异常，作为最后一道防线）
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator FP(float value)
    {
        throw new InvalidOperationException(
            "禁止隐式从 float 转换为 FP！请使用 FP.FromRaw(FP.Raw.xxx) 或 FP.FromFloat_UNSAFE()（仅在编辑器使用）");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator FP(double value)
    {
        throw new InvalidOperationException(
            "禁止隐式从 double 转换为 FP！请使用 FP.FromRaw(FP.Raw.xxx) 或 FP.FromDouble_UNSAFE()（仅在编辑器使用）");
    }

    #endregion

    #region 算术运算

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FP operator +(FP a, FP b) => new(a.RawValue + b.RawValue);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FP operator -(FP a, FP b) => new(a.RawValue - b.RawValue);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FP operator -(FP a) => new(-a.RawValue);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FP operator +(FP a) => a;  // 一元正号

    /// <summary>
    /// 定点数乘法 (a * b / 65536)
    /// 使用四舍五入，精度更高
    /// </summary>
    /// <remarks>
    /// 相比截断版本，精度误差减半（约 0.5 LSB）
    /// 性能损失：1 次加法（可忽略）
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FP operator *(FP a, FP b)
    {
        long product = a.RawValue * b.RawValue;
        
#if DEBUG
        // 检查舍入溢出（当 product 接近 long.MaxValue/MinValue 时）
        // 这种情况在正常使用中极不可能发生（需要数值 > 2^63 / 2^16 ≈ 140万亿）
        if (product > long.MaxValue - MulRound || product < long.MinValue + MulRound)
        {
            Debug.Fail($"FP multiplication overflow: {a.RawValue} * {b.RawValue}. Use MultiplyPrecise for overflow checking.");
        }
#endif
        // 四舍五入：加上 0.5 LSB 后截断
        return new((product + MulRound) >> FRACTIONAL_BITS);
    }
    
    /// <summary>
    /// 快速乘法（截断，无舍入开销）
    /// </summary>
    /// <remarks>
    /// 在性能极度敏感且精度要求不高的场景使用
    /// 误差：最大 1 LSB（约 0.000015）
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FP MultiplyFast(FP a, FP b)
    {
        return new((a.RawValue * b.RawValue) >> FRACTIONAL_BITS);
    }
    
    /// <summary>
    /// 高精度乘法（带溢出检测）
    /// </summary>
    /// <exception cref="OverflowException">乘法溢出时抛出</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FP MultiplyPrecise(FP a, FP b)
    {
        long product = a.RawValue * b.RawValue;
        long rounded = product + MulRound;
        
        // 检测舍入溢出
        if ((product > 0 && rounded < product) || (product < 0 && rounded > product))
            throw new OverflowException($"FP multiplication overflow: {a} * {b}");
        
        return new(rounded >> FRACTIONAL_BITS);
    }

    /// <summary>
    /// 定点数除法 (a * 65536 / b)
    /// <para>注意：除零会抛出 <see cref="DivideByZeroException"/></para>
    /// </summary>
    /// <exception cref="DivideByZeroException">当 b 为零时抛出</exception>
    /// <exception cref="OverflowException">当左移操作溢出时抛出</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FP operator /(FP a, FP b)
    {
        if (b.RawValue == 0)
            throw new DivideByZeroException("FP division by zero");
        
        // 检查左移是否会导致溢出：|a| > long.MaxValue >> 16 ≈ 140万亿
        // 对于正常游戏数值（通常 < 100万），永远不会触发
        const long MAX_SAFE_DIVIDEND = long.MaxValue >> FRACTIONAL_BITS; // ≈ 140,737,488,355,327
        if (a.RawValue > MAX_SAFE_DIVIDEND || a.RawValue < -MAX_SAFE_DIVIDEND)
            throw new OverflowException($"FP division overflow: dividend {a.RawValue} is too large for division");
        
        long result = (a.RawValue << FRACTIONAL_BITS) / b.RawValue;
        return new(result);
    }

    // ==================== 与整数的混合运算（优化） ====================

    // int 运算符
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FP operator *(FP a, int b) => new(a.RawValue * b);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FP operator *(int a, FP b) => new(b.RawValue * a);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FP operator /(FP a, int b)
    {
        if (b == 0)
            throw new DivideByZeroException("FP division by int zero");
        return new(a.RawValue / b);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FP operator +(FP a, int b) => new(a.RawValue + ((long)b << FRACTIONAL_BITS));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FP operator +(int a, FP b) => b + a;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FP operator -(FP a, int b) => new(a.RawValue - ((long)b << FRACTIONAL_BITS));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FP operator -(int a, FP b) => new(((long)a << FRACTIONAL_BITS) - b.RawValue);

    // long 运算符（P1 优化：避免 int 溢出）
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FP operator *(FP a, long b) => new(a.RawValue * b);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FP operator *(long a, FP b) => new(b.RawValue * a);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FP operator /(FP a, long b)
    {
        if (b == 0)
            throw new DivideByZeroException("FP division by long zero");
        return new(a.RawValue / b);
    }

    #endregion

    #region 比较运算

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(FP a, FP b) => a.RawValue == b.RawValue;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(FP a, FP b) => a.RawValue != b.RawValue;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(FP a, FP b) => a.RawValue < b.RawValue;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(FP a, FP b) => a.RawValue > b.RawValue;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(FP a, FP b) => a.RawValue <= b.RawValue;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(FP a, FP b) => a.RawValue >= b.RawValue;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(FP other) => RawValue.CompareTo(other.RawValue);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(FP other) => RawValue == other.RawValue;

    public override bool Equals(object? obj) => obj is FP other && Equals(other);

    public override int GetHashCode() => RawValue.GetHashCode();

    #endregion
}
