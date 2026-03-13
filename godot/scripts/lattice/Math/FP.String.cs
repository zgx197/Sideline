// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

#nullable enable

using System;
using System.Runtime.CompilerServices;

namespace Lattice.Math;

/// <summary>
/// 定点数字符串解析与格式化（部分类）
/// </summary>
/// <remarks>
/// <para>本文件包含 FP 结构体的字符串相关方法：</para>
/// <list type="bullet">
///   <item>FromString - 确定性解析（安全，可用于运行时）</item>
///   <item>FromString_UNSAFE - 基于 float.Parse（仅编辑器使用）</item>
///   <item>ToString - 格式化输出（纯整数实现）</item>
/// </list>
/// </remarks>
public readonly partial struct FP
{
    #region 字符串解析

    /// <summary>
    /// 确定性字符串解析（安全，可用于运行时加载配置）
    /// <para>格式：英文数字，如 "123.456" 或 "-10.5"</para>
    /// <para>不经过 float，完全确定性</para>
    /// </summary>
    /// <param name="s">要解析的字符串</param>
    /// <returns>解析后的 FP 值</returns>
    /// <exception cref="ArgumentException">字符串为空或 null 时抛出</exception>
    /// <exception cref="FormatException">字符串格式无效时抛出</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

    /// <summary>
    /// 字符串解析（UNSAFE，基于 float.Parse）
    /// <para>警告：此方法内部使用 float.Parse，只能在编辑器/配置阶段使用！</para>
    /// <para>模拟/运行时使用会导致非确定性。请使用 FromString 或预计算的 Raw 值。</para>
    /// </summary>
    /// <param name="s">要解析的字符串</param>
    /// <returns>解析后的 FP 值</returns>
    /// <remarks>
    /// 此方法仅在非 DEBUG 构建中可用。DEBUG 构建中标记为 [Obsolete(error: true)]。
    /// </remarks>
#if DEBUG
    [Obsolete("FromString_UNSAFE 内部使用 float.Parse，只能在编辑器使用。生产环境请使用 FromString()", error: true)]
#endif
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#pragma warning disable RS0030 // 允许 UNSAFE 方法使用被禁止的 API
    public static FP FromString_UNSAFE(string s) => FromFloat_UNSAFE(float.Parse(s));
#pragma warning restore RS0030

    #endregion

    #region 字符串格式化

    /// <summary>
    /// 默认格式化输出（纯整数实现，确定性）
    /// <para>格式：固定4位小数</para>
    /// </summary>
    /// <returns>格式化后的字符串，如 "123.4560" 或 "-10.5000"</returns>
    public override string ToString()
    {
        return ToStringInternal(4);
    }

    /// <summary>
    /// 自定义格式输出（纯整数实现，确定性）
    /// <para>支持格式：F0-F9（小数位数）</para>
    /// </summary>
    /// <param name="format">格式字符串，如 "F2" 表示保留2位小数</param>
    /// <returns>格式化后的字符串</returns>
    /// <example>
    /// <code>
    /// FP value = FP.FromRaw(FP.Raw._1_50);  // 1.5
    /// string s1 = value.ToString("F2");    // "1.50"
    /// string s2 = value.ToString("F0");    // "2"（四舍五入）
    /// </code>
    /// </example>
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
    /// 纯整数实现的字符串转换（内部方法）
    /// <para>将定点数转换为指定小数位数的字符串表示</para>
    /// </summary>
    /// <param name="decimalPlaces">小数位数（0-9）</param>
    /// <returns>格式化后的字符串</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

    #endregion

    #region 辅助方法

    /// <summary>
    /// 10的幂次查找表
    /// <para>用于字符串格式化时快速计算 10^n</para>
    /// </summary>
    /// <param name="n">指数（0-9）</param>
    /// <returns>10^n 的值</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
}
