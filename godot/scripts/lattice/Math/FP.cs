// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

#nullable enable

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Lattice.Math;

/// <summary>
/// 定点数 (Q48.16)：48位整数 + 16位小数
/// 
/// 参考 FrameSyncEngine / Photon Quantum 设计
/// 用于确定性帧同步，替代 float/double
/// </summary>
public readonly struct FP : IEquatable<FP>, IComparable<FP>
{
    #region 常量定义

    /// <summary>小数位数量</summary>
    public const int FRACTIONAL_BITS = 16;
    
    /// <summary>比例因子 = 2^16 = 65536</summary>
    public const long ONE = 1L << FRACTIONAL_BITS;
    
    /// <summary>安全乘法上限：sqrt(long.MaxValue) ≈ 21亿</summary>
    /// <remarks>两个 UseableMax 相乘不会溢出 long</remarks>
    public static readonly FP UseableMax = new(2147483647L);   // ~32767.999
    
    /// <summary>安全乘法下限</summary>
    public static readonly FP UseableMin = new(-2147483648L);  // -32768
    
    /// <summary>最小精度：1/65536 ≈ 0.000015</summary>
    public static readonly FP Epsilon = new(1L);
    
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
    /// </summary>
    public static class Raw
    {
        public const long _0 = 0;
        public const long _1 = ONE;
        public const long _2 = ONE * 2;
        public const long _3 = ONE * 3;
        public const long _4 = ONE * 4;
        public const long _5 = ONE * 5;
        public const long _10 = ONE * 10;
        public const long _100 = ONE * 100;
        public const long _1000 = ONE * 1000;
        
        public const long _0_01 = 655;
        public const long _0_05 = 3277;
        public const long _0_10 = 6554;
        public const long _0_25 = 16384;
        public const long _0_50 = ONE >> 1;
        public const long _0_75 = (ONE >> 1) + (ONE >> 2);
        public const long _1_50 = ONE + (ONE >> 1);
        
        public const long _PI = 205887;
        public const long _2Pi = 411774;
        public const long _PiHalf = 102943;
    }

    #endregion

    #region 存储与构造

    /// <summary>内部原始值 (Q48.16 格式)</summary>
    public readonly long RawValue;

    /// <summary>
    /// 私有构造函数，强制使用命名工厂方法
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private FP(long raw) => RawValue = raw;

    /// <summary>
    /// 唯一安全的构造入口（从预计算的 Raw 值）
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator int(FP value) => (int)(value.RawValue >> FRACTIONAL_BITS);

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
    /// 使用截断（向零取整），性能优先
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FP operator *(FP a, FP b)
    {
        // FrameSyncEngine / Quantum: 直接截断，性能优先
        long result = (a.RawValue * b.RawValue) >> FRACTIONAL_BITS;
        return new(result);
    }

    /// <summary>
    /// 定点数除法 (a * 65536 / b)
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FP operator /(FP a, FP b)
    {
        Debug.Assert(b.RawValue != 0, "FP division by zero");
        long result = (a.RawValue << FRACTIONAL_BITS) / b.RawValue;
        return new(result);
    }

    // ==================== 与整数的混合运算（优化） ====================

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FP operator *(FP a, int b) => new(a.RawValue * b);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FP operator *(int a, FP b) => new(b.RawValue * a);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FP operator /(FP a, int b)
    {
        Debug.Assert(b != 0, "FP division by int zero");
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

    #region 实用方法

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FP Abs(FP a) => new(a.RawValue < 0 ? -a.RawValue : a.RawValue);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FP Min(FP a, FP b) => a.RawValue < b.RawValue ? a : b;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FP Max(FP a, FP b) => a.RawValue > b.RawValue ? a : b;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FP Clamp(FP value, FP min, FP max)
    {
        if (value.RawValue < min.RawValue) return min;
        if (value.RawValue > max.RawValue) return max;
        return value;
    }

    /// <summary>
    /// 线性插值：a + (b - a) * t
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FP Lerp(FP a, FP b, FP t) => a + (b - a) * t;

    /// <summary>
    /// 高精度乘法（四舍五入，比 * 运算符慢但精度稍高）
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FP MultiplyPrecise(FP a, FP b)
    {
        // 加 0.5 后截断 = 四舍五入
        long result = (a.RawValue * b.RawValue + (ONE >> 1)) >> FRACTIONAL_BITS;
        return new(result);
    }

    /// <summary>
    /// 近似相等（考虑精度误差）
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Approximately(FP a, FP b, FP epsilon)
    {
        long diff = a.RawValue - b.RawValue;
        if (diff < 0) diff = -diff;
        return diff <= epsilon.RawValue;
    }

    #endregion

    #region 字符串与序列化

    /// <summary>
    /// 确定性字符串解析（安全，可用于运行时加载配置）
    /// 格式：英文数字，如 "123.456" 或 "-10.5"
    /// 不经过 float，完全确定性
    /// </summary>
    public static FP FromString(string s)
    {
        if (string.IsNullOrEmpty(s))
            throw new ArgumentException("String is null or empty", nameof(s));

        // 解析符号
        int start = 0;
        bool negative = false;
        if (s[0] == '-')
        {
            negative = true;
            start = 1;
        }
        else if (s[0] == '+')
        {
            start = 1;
        }

        // 分割整数和小数部分
        int dotIndex = s.IndexOf('.', start);
        
        // 解析整数部分
        long integerPart = 0;
        int end = dotIndex >= 0 ? dotIndex : s.Length;
        for (int i = start; i < end; i++)
        {
            if (s[i] < '0' || s[i] > '9')
                throw new FormatException($"Invalid character '{s[i]}' in FP string");
            integerPart = integerPart * 10 + (s[i] - '0');
        }

        // 解析小数部分（最多 5 位，超过部分截断）
        long fractionalPart = 0;
        if (dotIndex >= 0 && dotIndex + 1 < s.Length)
        {
            int fracStart = dotIndex + 1;
            int fracLength = System.Math.Min(5, s.Length - fracStart);
            
            long fracValue = 0;
            long divisor = 1;
            for (int i = 0; i < fracLength; i++)
            {
                char c = s[fracStart + i];
                if (c < '0' || c > '9')
                    throw new FormatException($"Invalid character '{c}' in FP fractional part");
                fracValue = fracValue * 10 + (c - '0');
                divisor *= 10;
            }
            
            // 将小数部分转换为定点数：fracValue / divisor * ONE
            fractionalPart = (fracValue * ONE) / divisor;
        }

        long result = (integerPart << FRACTIONAL_BITS) + fractionalPart;
        if (negative) result = -result;

        return new(result);
    }

#if DEBUG
    [Obsolete("FromString_UNSAFE 内部使用 float.Parse，只能在编辑器使用", true)]
#endif
    public static FP FromString_UNSAFE(string s) => FromFloat_UNSAFE(float.Parse(s));

    /// <summary>
    /// 调试输出（非确定性，仅用于显示）
    /// </summary>
    public override string ToString() => (RawValue / (double)ONE).ToString("F4");

    /// <summary>
    /// 格式化输出
    /// </summary>
    public string ToString(string format) => (RawValue / (double)ONE).ToString(format);

    #endregion
}
