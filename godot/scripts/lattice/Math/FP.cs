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
        
        // 角度转换常量
        public const long _Deg2Rad = 1144;      // π/180 ≈ 0.0174533
        public const long _Rad2Deg = 3754936;   // 180/π ≈ 57.2958
        
        // 极值常量
        public const long _MaxValue = 3037000499L;  // UseableMax
        public const long _MinValue = -3037000499L; // UseableMin
        public const long _Epsilon = 1;             // 最小精度
    }

    #endregion

    #region 存储与构造

    /// <summary>内部原始值 (Q48.16 格式)</summary>
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

    /// <summary>
    /// 反余弦函数 Acos(x)，x ∈ [-1, 1]，返回值 ∈ [0, π]
    /// <para>使用预生成查找表，纯整数运算</para>
    /// </summary>
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
    /// 使用截断（向零取整），性能优先
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FP operator *(FP a, FP b)
    {
#if DEBUG
        // 检查溢出：如果 a != 0 且 (a*b)/a != b，说明溢出
        if (a.RawValue != 0)
        {
            long temp = a.RawValue * b.RawValue;
            if (a.RawValue != -1 && temp / a.RawValue != b.RawValue)
                Debug.Fail($"FP multiplication overflow: {a.RawValue} * {b.RawValue}");
        }
#endif
        // FrameSyncEngine / Quantum: 直接截断，性能优先
        long result = (a.RawValue * b.RawValue) >> FRACTIONAL_BITS;
        return new(result);
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

    /// <summary>
    /// 绝对值
    /// <para>注意：long.MinValue 的绝对值仍是 long.MinValue（溢出），但在 FP 使用范围内不会发生</para>
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FP Abs(FP a)
    {
        long v = a.RawValue;
        // 处理 long.MinValue 的特殊情况（虽然 FP 使用范围内不会发生）
        if (v == long.MinValue)
            return new(long.MaxValue); // 返回最大正数作为近似
        return new(v < 0 ? -v : v);
    }

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
    /// 近似相等（使用默认容差 EpsilonDefault ≈ 0.00015）
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Approximately(FP a, FP b) => Approximately(a, b, EpsilonDefault);

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
#pragma warning disable RS0030 // 允许 UNSAFE 方法使用被禁止的 API
    public static FP FromString_UNSAFE(string s) => FromFloat_UNSAFE(float.Parse(s));
#pragma warning restore RS0030

    /// <summary>
    /// 调试输出（纯整数实现，确定性）
    /// <para>格式：固定4位小数</para>
    /// </summary>
    public override string ToString()
    {
        return ToStringInternal(4);
    }

    /// <summary>
    /// 格式化输出（纯整数实现，确定性）
    /// <para>支持格式：F0-F9（小数位数）</para>
    /// </summary>
    public string ToString(string format)
    {
        int decimalPlaces = 4;
        if (!string.IsNullOrEmpty(format) && format.Length > 1 && format[0] == 'F')
        {
            if (int.TryParse(format.Substring(1), out int parsed) && parsed >= 0 && parsed <= 9)
                decimalPlaces = parsed;
        }
        return ToStringInternal(decimalPlaces);
    }

    /// <summary>
    /// 纯整数实现的字符串转换
    /// </summary>
    private string ToStringInternal(int decimalPlaces)
    {
        if (RawValue == 0) return "0." + new string('0', decimalPlaces);
        
        long raw = RawValue;
        bool negative = raw < 0;
        if (negative) raw = -raw;
        
        // 整数部分
        long intPart = raw >> FRACTIONAL_BITS;
        
        // 小数部分（16位）
        long fracPart = raw & (ONE - 1);
        
        // 将小数部分扩展到指定位数
        // fracPart / 65536 * 10^decimalPlaces
        long divisor = ONE;
        for (int i = 0; i < decimalPlaces; i++) divisor /= 10;
        if (divisor == 0) divisor = 1; // 防止除零
        
        long scaledFrac = (fracPart * Pow10(decimalPlaces)) / ONE;
        
        // 格式化
        string intStr = intPart.ToString();
        string fracStr = scaledFrac.ToString().PadLeft(decimalPlaces, '0');
        
        // 截断到指定位数
        if (fracStr.Length > decimalPlaces)
            fracStr = fracStr.Substring(0, decimalPlaces);
        
        return (negative ? "-" : "") + intStr + "." + fracStr;
    }

    /// <summary>10的幂次查找表</summary>
    private static long Pow10(int n)
    {
        return n switch
        {
            0 => 1,
            1 => 10,
            2 => 100,
            3 => 1000,
            4 => 10000,
            5 => 100000,
            6 => 1000000,
            7 => 10000000,
            8 => 100000000,
            9 => 1000000000,
            _ => 10000
        };
    }

    #endregion

    #region 三角函数（双精度 LUT 实现，参考 FrameSync）
    
    // 使用预生成的双精度 LUT（Fast: 1024, Accurate: 4096）
    
    /// <summary>2π 对应的 RawValue</summary>
    private static readonly long TWO_PI_RAW = Pi2.RawValue;

    #region 快速模式 (1024 LUT) - Cache 友好

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

    #region 标准模式 (1024 LUT + 归一化) - 平衡

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

    #region 高精度模式 (4096 LUT) - 3D/物理

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

    #region 使用指南注释

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

    /// <summary>
    /// 反正切函数 Atan2(y, x)，返回角度 [-π, π]
    /// <para>用于计算向量角度，如 MathF.Atan2(y, x)</para>
    /// </summary>
    /// <remarks>
    /// 使用象限归约 + 查表，保证确定性
    /// </remarks>
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
}
