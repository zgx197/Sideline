// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

#nullable enable

using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Lattice.Math
{
    /// <summary>
    /// 定点数数学辅助类
    /// <para>采用 FrameSyncEngine 纯整数算法，无浮点运算，严格跨平台确定性</para>
    /// </summary>
    public static class FPMath
    {
        #region 内部结构

        /// <summary>
        /// 平方根分解结果：sqrt(x) = Mantissa * 2^Exponent
        /// </summary>
        internal readonly struct SqrtDecomp
        {
            public readonly int Exponent;
            public readonly int Mantissa;

            public SqrtDecomp(int exponent, int mantissa)
            {
                Exponent = exponent;
                Mantissa = mantissa;
            }
        }

        #endregion

        #region Sqrt 实现（FrameSync 风格纯整数算法）

        /// <summary>
        /// 计算平方根 - 纯整数查表法
        /// <para>FrameSync 风格实现：查表 + 指数分解，无浮点运算</para>
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP Sqrt(FP value)
        {
            if (value.RawValue <= 0) return FP.Zero;
            return FP.FromRaw(SqrtRaw(value.RawValue));
        }

        /// <summary>
        /// 计算平方根（原始值）- 纯整数算法
        /// <para>核心算法来自 FrameSyncEngine:</para>
        /// <para>1. 小值 (<= 65536): 直接查表，结果右移 6 位</para>
        /// <para>2. 大值: 分解为 mantissa * 2^exponent，分别处理</para>
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long SqrtRaw(long x)
        {
            if (x <= 65536L)
            {
                if (x < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(x), $"平方根输入不能为负数: {x}");
                }
                // 小值直接查表，右移 6 位去除额外精度
                return FPSqrtLut.Table[x] >> FPSqrtLut.AdditionalPrecisionBits;
            }

            // 大值处理：分解尾数和指数
            // x = raw * 2^exponent, 其中 raw 在 [0, 65536] 范围内
            long raw = x;
            int log2 = 0;

            // 手动计算 log2（找最高有效位）
            if ((raw >> 32) != 0L) { raw >>= 32; log2 += 32; }
            if ((raw >> 16) != 0L) { raw >>= 16; log2 += 16; }
            if ((raw >> 8) != 0L) { raw >>= 8; log2 += 8; }
            if ((raw >> 4) != 0L) { raw >>= 4; log2 += 4; }
            if ((raw >> 2) != 0L) { log2 += 2; }

            // 计算指数偏移，使 x >> exponent 落在查找表范围内
            // log2 - 16: 因为查找表覆盖 16 位小数
            // + 2: FrameSync 的调整因子
            int exponent = log2 - 16 + 2;
            
            // 查表获取尾数的平方根（带额外精度）
            int mantissaSqrt = FPSqrtLut.Table[x >> exponent];
            
            // 结果 = 尾数平方根 << (exponent / 2)
            // exponent >> 1 相当于 exponent / 2
            long result = (long)mantissaSqrt << (exponent >> 1);
            
            // 右移 6 位去除额外精度
            return result >> FPSqrtLut.AdditionalPrecisionBits;
        }

        /// <summary>
        /// 获取平方根的指数-尾数分解（用于免 Sqrt 归一化）
        /// <para>FrameSync 风格算法，纯整数实现</para>
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static SqrtDecomp GetSqrtDecomp(ulong x)
        {
            if (x <= 65536UL)
            {
                return new SqrtDecomp(
                    exponent: 0,
                    mantissa: FPSqrtLut.Table[x]);
            }

            // 计算 log2（找最高有效位）
            ulong raw = x;
            int log2 = 0;
            
            if ((raw >> 32) != 0UL) { raw >>= 32; log2 += 32; }
            if ((raw >> 16) != 0UL) { raw >>= 16; log2 += 16; }
            if ((raw >> 8) != 0UL) { raw >>= 8; log2 += 8; }
            if ((raw >> 4) != 0UL) { raw >>= 4; log2 += 4; }
            if ((raw >> 2) != 0UL) { log2 += 2; }

            int exponent = log2 - 16 + 2;
            
            return new SqrtDecomp(
                exponent: exponent >> 1,
                mantissa: FPSqrtLut.Table[x >> exponent]);
        }

        #endregion

        #region 常用数学函数

        /// <summary>
        /// 符号函数：返回 -1, 0, 1
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Sign(FP value)
        {
            if (value.RawValue > 0) return 1;
            if (value.RawValue < 0) return -1;
            return 0;
        }

        /// <summary>
        /// 取绝对值
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP Abs(FP value)
        {
            return value.RawValue < 0 ? new FP(-value.RawValue) : value;
        }

        /// <summary>
        /// 取最小值
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP Min(FP a, FP b)
        {
            return a.RawValue < b.RawValue ? a : b;
        }

        /// <summary>
        /// 取最大值
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP Max(FP a, FP b)
        {
            return a.RawValue > b.RawValue ? a : b;
        }

        /// <summary>
        /// 限制在 [min, max] 范围
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP Clamp(FP value, FP min, FP max)
        {
            if (value.RawValue < min.RawValue) return min;
            if (value.RawValue > max.RawValue) return max;
            return value;
        }

        /// <summary>
        /// 限制在 [0, 1] 范围
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP Clamp01(FP value)
        {
            if (value.RawValue < 0) return FP.Zero;
            if (value.RawValue > FP.ONE) return FP.One;
            return value;
        }

        /// <summary>
        /// 线性插值（已限制 t 在 [0,1]）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP Lerp(FP a, FP b, FP t)
        {
            t = Clamp01(t);
            return new FP(a.RawValue + ((b.RawValue - a.RawValue) * t.RawValue + 32768 >> 16));
        }

        /// <summary>
        /// 线性插值（不限制 t）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP LerpUnclamped(FP a, FP b, FP t)
        {
            return new FP(a.RawValue + ((b.RawValue - a.RawValue) * t.RawValue + 32768 >> 16));
        }

        /// <summary>
        /// 向下取整
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP Floor(FP value)
        {
            return new FP(value.RawValue & -65536L);
        }

        /// <summary>
        /// 向上取整
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP Ceiling(FP value)
        {
            if ((value.RawValue & 0xFFFF) != 0)
                return new FP((value.RawValue & -65536L) + 65536);
            return value;
        }

        /// <summary>
        /// 四舍五入
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP Round(FP value)
        {
            long fractional = value.RawValue & 0xFFFF;
            if (fractional < 32768)
                return Floor(value);
            if (fractional > 32768)
                return Ceiling(value);
            // 正好是 0.5，向偶数取整
            return (value.RawValue & 0x10000) != 0 ? Ceiling(value) : Floor(value);
        }

        #endregion
    }
}
