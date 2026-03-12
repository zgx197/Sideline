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
    /// <para>参考 FrameSyncEngine 设计，提供高性能数学运算</para>
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

        #region 查找表

        /// <summary>
        /// 数学查找表（延迟初始化）
        /// </summary>
        public static class Lut
        {
            /// <summary>平方根查找表 [0, 65536]，结果格式 Q16.16</summary>
            public static readonly ushort[] Sqrt = InitSqrtLut();

            private static ushort[] InitSqrtLut()
            {
                var table = new ushort[65537];
                for (int i = 0; i <= 65536; i++)
                {
                    // sqrt(i / 65536) * 65536，四舍五入
                    table[i] = (ushort)(System.Math.Sqrt(i / 65536.0) * 65536.0 + 0.5);
                }
                return table;
            }
        }

        #endregion

        #region Sqrt 实现（简化版，使用系统 Math.Sqrt 后转换）

        /// <summary>
        /// 计算平方根
        /// <para>使用 double 计算后转换（简化实现，后续可优化为纯整数算法）</para>
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP Sqrt(FP value)
        {
            if (value.RawValue <= 0) return FP.Zero;
            
            // 转换为 double 计算，再转回 FP
            // 注意：这使用了浮点运算，但在初始化/计算时是可接受的
            // 对于严格的帧同步，应该使用纯整数算法
            double d = value.RawValue / 65536.0;
            double sqrt = System.Math.Sqrt(d);
            return FP.FromRaw((long)(sqrt * 65536.0));
        }

        /// <summary>
        /// 获取平方根的近似分解（用于免 Sqrt 归一化）
        /// <para>FrameSync 风格算法</para>
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static SqrtDecomp GetSqrtDecomp(ulong x)
        {
            // 计算平方根（简化版）
            double d = x / 65536.0;
            double sqrt = System.Math.Sqrt(d);
            long sqrtRaw = (long)(sqrt * 65536.0);
            
            // 分解为 mantissa * 2^exponent
            int log2 = BitOperations.Log2((ulong)sqrtRaw);
            int exponent = log2 - 16;
            if (exponent < 0) exponent = 0;
            int mantissa = (int)(sqrtRaw >> exponent);
            if (mantissa < 65536) mantissa = 65536; // 至少为 1.0
            
            return new SqrtDecomp(exponent, mantissa);
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
