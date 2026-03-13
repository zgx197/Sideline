// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

#nullable enable

using System.Diagnostics;

namespace Lattice.Math
{
    /// <summary>
    /// FP 数学常量定义
    /// <para>所有魔数集中管理，附带推导说明</para>
    /// </summary>
    internal static class FPMathConstants
    {
        #region LUT 索引常量

        /// <summary>Sin/Cos LUT 大小：4096 条目</summary>
        /// <remarks>
        /// 计算：2π / 4096 ≈ 0.00153 弧度精度
        /// 内存：4096 × 8 bytes = 32 KB
        /// </remarks>
        public const int SinCosLutSize = 4096;

        /// <summary>Sin/Cos 索引掩码：12 位 (log2(4096))</summary>
        public const int SinCosLutMask = SinCosLutSize - 1;

        /// <summary>Sin/Cos 索引位移：16 - 12 = 4</summary>
        public const int SinCosIndexShift = 4;

        /// <summary>Sqrt LUT 大小：65536 条目</summary>
        /// <remarks>
        /// 覆盖输入范围：0 到 65535（即 0.0 到 0.9999 in Q16.16）
        /// 内存：65536 × 4 bytes = 256 KB
        /// </remarks>
        public const int SqrtLutSize = 65536;

        /// <summary>Sqrt 索引掩码：16 位</summary>
        public const int SqrtLutMask = SqrtLutSize - 1;

        /// <summary>Acos LUT 大小：65537 条目（包含 -1 到 1）</summary>
        public const int AcosLutSize = 65537;

        /// <summary>Acos 索引偏移：32768（将 [-1, 1] 映射到 [0, 65536]）</summary>
        public const int AcosIndexOffset = 32768;

        #endregion

        #region 归一化常量

        /// <summary>
        /// 归一化倒数计算的魔数：2^44
        /// </summary>
        /// <remarks>
        /// 推导：
        /// - 我们需要计算 1/sqrt(x) ≈ 2^k / sqrt(mantissa)
        /// - k = 44/2 = 22 (sqrt 精度)
        /// - 加上 Q16.16 格式需要 16 位
        /// - 加上额外 6 位精度
        /// - 总计：22 + 16 + 6 = 44 位
        /// 
        /// 值：17,592,186,044,416
        /// </remarks>
        public const long ReciprocalNormalizationFactor = 1L << 44;

        /// <summary>2^22 = 4,194,304（sqrt 精度基准）</summary>
        public const long SqrtPrecisionBase = 1L << 22;

        /// <summary>2^38 = 274,877,906,944（sqrt 中间计算）</summary>
        public const long SqrtIntermediateShift = 1L << 38;

        #endregion

        #region 乘法常量

        /// <summary>
        /// 乘法舍入常量：0.5 LSB = 2^15 = 32768
        /// </summary>
        /// <remarks>
        /// 用于四舍五入乘法：(a * b + MulRound) >> 16
        /// 相比截断，精度误差减半
        /// </remarks>
        public const long MulRound = 1L << (FP.FRACTIONAL_BITS - 1);

        /// <summary>最大安全被除数：long.MaxValue >> 16</summary>
        /// <remarks>
        /// 约 140,737,488,355,327（140万亿）
        /// 超过此值的 FP 除法会溢出
        /// </remarks>
        public const long MaxSafeDividend = long.MaxValue >> FP.FRACTIONAL_BITS;

        #endregion

        #region 角度/弧度转换

        /// <summary>π 的 FP 表示：205887</summary>
        public const long PiRaw = 205887L;

        /// <summary>2π 的 FP 表示：411774</summary>
        public const long TwoPiRaw = 411774L;

        /// <summary>π/2 的 FP 表示：102943</summary>
        public const long PiHalfRaw = 102943L;

        /// <summary>角度转弧度：π/180 ≈ 1144</summary>
        public const long Deg2RadRaw = 1144L;

        /// <summary>弧度转角度：180/π ≈ 3,754,936</summary>
        public const long Rad2DegRaw = 3754936L;

        #endregion

        #region 验证方法

        /// <summary>
        /// 验证所有常量计算正确
        /// </summary>
        [Conditional("DEBUG")]
        public static void Validate()
        {
            // 验证 LUT 大小是 2 的幂
            Debug.Assert((SinCosLutSize & SinCosLutMask) == 0, "SinCosLutSize must be power of 2");
            Debug.Assert((SqrtLutSize & SqrtLutMask) == 0, "SqrtLutSize must be power of 2");

            // 验证掩码计算
            Debug.Assert(SinCosLutMask == 4095, "SinCosLutMask = 4096 - 1");
            Debug.Assert(SqrtLutMask == 65535, "SqrtLutMask = 65536 - 1");

            // 验证归一化因子
            Debug.Assert(ReciprocalNormalizationFactor == 17592186044416L, "ReciprocalNormalizationFactor = 2^44");

            // 验证舍入常量
            Debug.Assert(MulRound == 32768L, "MulRound = 2^15");

            // 验证角度常量
            Debug.Assert(PiRaw == (long)(System.Math.PI * FP.ONE), "PiRaw mismatch");
            Debug.Assert(TwoPiRaw == PiRaw * 2, "TwoPiRaw mismatch");
            Debug.Assert(PiHalfRaw == PiRaw / 2, "PiHalfRaw mismatch");
        }

        #endregion
    }
}
