// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

#nullable enable

using System;
using System.Runtime.CompilerServices;

namespace Lattice.Math
{
    /// <summary>
    /// 3D 定点数向量
    /// <para>参考 FrameSyncEngine 设计，高性能实现</para>
    /// </summary>
    public readonly struct FPVector3 : IEquatable<FPVector3>
    {
        #region 字段

        /// <summary>X 分量</summary>
        public readonly FP X;

        /// <summary>Y 分量</summary>
        public readonly FP Y;

        /// <summary>Z 分量</summary>
        public readonly FP Z;

        #endregion

        #region 构造函数

        /// <summary>
        /// 从三个 FP 构造
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FPVector3(FP x, FP y, FP z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        /// <summary>
        /// 从三个 int 构造
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FPVector3(int x, int y, int z)
        {
            X = (FP)x;
            Y = (FP)y;
            Z = (FP)z;
        }

        /// <summary>
        /// 从两个 FP 构造（Z=0）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FPVector3(FP x, FP y)
        {
            X = x;
            Y = y;
            Z = FP._0;
        }

        /// <summary>
        /// 从单个 FP 构造（三个分量相同）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FPVector3(FP value)
        {
            X = value;
            Y = value;
            Z = value;
        }

        #endregion

        #region 常量

        /// <summary>零向量 (0, 0, 0)</summary>
        public static FPVector3 Zero => default;

        /// <summary>单位向量 (1, 1, 1)</summary>
        public static FPVector3 One => new(FP._1, FP._1, FP._1);

        /// <summary>右向量 (1, 0, 0)</summary>
        public static FPVector3 Right => new(FP._1, FP._0, FP._0);

        /// <summary>左向量 (-1, 0, 0)</summary>
        public static FPVector3 Left => new(-FP._1, FP._0, FP._0);

        /// <summary>上向量 (0, 1, 0)</summary>
        public static FPVector3 Up => new(FP._0, FP._1, FP._0);

        /// <summary>下向量 (0, -1, 0)</summary>
        public static FPVector3 Down => new(FP._0, -FP._1, FP._0);

        /// <summary>前向量 (0, 0, 1)</summary>
        public static FPVector3 Forward => new(FP._0, FP._0, FP._1);

        /// <summary>后向量 (0, 0, -1)</summary>
        public static FPVector3 Back => new(FP._0, FP._0, -FP._1);

        /// <summary>最大值向量</summary>
        public static FPVector3 MaxValue => new(new FP(long.MaxValue), new FP(long.MaxValue), new FP(long.MaxValue));

        /// <summary>最小值向量</summary>
        public static FPVector3 MinValue => new(new FP(long.MinValue), new FP(long.MinValue), new FP(long.MinValue));

        #endregion

        #region Swizzle (3D → 2D) - 9个

        /// <summary>(X, X)</summary>
        public readonly FPVector2 XX => new(X, X);

        /// <summary>(X, Y)</summary>
        public readonly FPVector2 XY => new(X, Y);

        /// <summary>(X, Z)</summary>
        public readonly FPVector2 XZ => new(X, Z);

        /// <summary>(Y, X)</summary>
        public readonly FPVector2 YX => new(Y, X);

        /// <summary>(Y, Y)</summary>
        public readonly FPVector2 YY => new(Y, Y);

        /// <summary>(Y, Z)</summary>
        public readonly FPVector2 YZ => new(Y, Z);

        /// <summary>(Z, X)</summary>
        public readonly FPVector2 ZX => new(Z, X);

        /// <summary>(Z, Y)</summary>
        public readonly FPVector2 ZY => new(Z, Y);

        /// <summary>(Z, Z)</summary>
        public readonly FPVector2 ZZ => new(Z, Z);

        #endregion

        #region Swizzle (3D → 3D) - X开头 (9个)

        /// <summary>(X, X, X)</summary>
        public readonly FPVector3 XXX => new(X, X, X);

        /// <summary>(X, X, Y)</summary>
        public readonly FPVector3 XXY => new(X, X, Y);

        /// <summary>(X, X, Z)</summary>
        public readonly FPVector3 XXZ => new(X, X, Z);

        /// <summary>(X, Y, X)</summary>
        public readonly FPVector3 XYX => new(X, Y, X);

        /// <summary>(X, Y, Y)</summary>
        public readonly FPVector3 XYY => new(X, Y, Y);

        /// <summary>(X, Y, Z) - 自身</summary>
        public readonly FPVector3 XYZ => this;

        /// <summary>(X, Z, X)</summary>
        public readonly FPVector3 XZX => new(X, Z, X);

        /// <summary>(X, Z, Y)</summary>
        public readonly FPVector3 XZY => new(X, Z, Y);

        /// <summary>(X, Z, Z)</summary>
        public readonly FPVector3 XZZ => new(X, Z, Z);

        #endregion

        #region Swizzle (3D → 3D) - Y开头 (9个)

        /// <summary>(Y, X, X)</summary>
        public readonly FPVector3 YXX => new(Y, X, X);

        /// <summary>(Y, X, Y)</summary>
        public readonly FPVector3 YXY => new(Y, X, Y);

        /// <summary>(Y, X, Z)</summary>
        public readonly FPVector3 YXZ => new(Y, X, Z);

        /// <summary>(Y, Y, X)</summary>
        public readonly FPVector3 YYX => new(Y, Y, X);

        /// <summary>(Y, Y, Y)</summary>
        public readonly FPVector3 YYY => new(Y, Y, Y);

        /// <summary>(Y, Y, Z)</summary>
        public readonly FPVector3 YYZ => new(Y, Y, Z);

        /// <summary>(Y, Z, X)</summary>
        public readonly FPVector3 YZX => new(Y, Z, X);

        /// <summary>(Y, Z, Y)</summary>
        public readonly FPVector3 YZY => new(Y, Z, Y);

        /// <summary>(Y, Z, Z)</summary>
        public readonly FPVector3 YZZ => new(Y, Z, Z);

        #endregion

        #region Swizzle (3D → 3D) - Z开头 (9个)

        /// <summary>(Z, X, X)</summary>
        public readonly FPVector3 ZXX => new(Z, X, X);

        /// <summary>(Z, X, Y)</summary>
        public readonly FPVector3 ZXY => new(Z, X, Y);

        /// <summary>(Z, X, Z)</summary>
        public readonly FPVector3 ZXZ => new(Z, X, Z);

        /// <summary>(Z, Y, X)</summary>
        public readonly FPVector3 ZYX => new(Z, Y, X);

        /// <summary>(Z, Y, Y)</summary>
        public readonly FPVector3 ZYY => new(Z, Y, Y);

        /// <summary>(Z, Y, Z)</summary>
        public readonly FPVector3 ZYZ => new(Z, Y, Z);

        /// <summary>(Z, Z, X)</summary>
        public readonly FPVector3 ZZX => new(Z, Z, X);

        /// <summary>(Z, Z, Y)</summary>
        public readonly FPVector3 ZZY => new(Z, Z, Y);

        /// <summary>(Z, Z, Z)</summary>
        public readonly FPVector3 ZZZ => new(Z, Z, Z);

        #endregion

        #region 属性

        /// <summary>
        /// 向量长度的平方
        /// </summary>
        public readonly FP SqrMagnitude
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                long x2 = (X.RawValue * X.RawValue + 32768) >> 16;
                long y2 = (Y.RawValue * Y.RawValue + 32768) >> 16;
                long z2 = (Z.RawValue * Z.RawValue + 32768) >> 16;
                return new FP(x2 + y2 + z2);
            }
        }

        /// <summary>
        /// 向量长度
        /// </summary>
        public readonly FP Magnitude
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => FPMath.Sqrt(SqrMagnitude);
        }

        /// <summary>
        /// 归一化向量（FrameSync 免除法优化）
        /// <para>使用倒数乘法而非除法，速度快约 2-3 倍</para>
        /// </summary>
        public readonly FPVector3 Normalized
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Normalize(this);
        }

        /// <summary>
        /// 归一化向量并返回原始长度
        /// <para>FrameSync 风格免除法实现</para>
        /// </summary>
        public readonly FPVector3 NormalizedWithMagnitude(out FP magnitude)
        {
            return Normalize(this, out magnitude);
        }

        /// <summary>
        /// 归一化向量（静态方法，FrameSync 优化）
        /// <para>使用指数-尾数分解 + 倒数乘法，避免除法</para>
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FPVector3 Normalize(FPVector3 value)
        {
            ulong sqrmag = (ulong)(value.X.RawValue * value.X.RawValue + value.Y.RawValue * value.Y.RawValue + value.Z.RawValue * value.Z.RawValue);
            if (sqrmag == 0) return Zero;
            
            var (reciprocal, shift) = FPMath.GetReciprocalForNormalize(sqrmag);
            
            return new FPVector3(
                new FP(value.X.RawValue * reciprocal >> shift),
                new FP(value.Y.RawValue * reciprocal >> shift),
                new FP(value.Z.RawValue * reciprocal >> shift)
            );
        }

        /// <summary>
        /// 归一化向量并返回原始长度（FrameSync 优化）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FPVector3 Normalize(FPVector3 value, out FP magnitude)
        {
            ulong sqrmag = (ulong)(value.X.RawValue * value.X.RawValue + value.Y.RawValue * value.Y.RawValue + value.Z.RawValue * value.Z.RawValue);
            if (sqrmag == 0)
            {
                magnitude = FP.Zero;
                return Zero;
            }
            
            var sqrt = FPMath.GetSqrtDecomp(sqrmag);
            var (reciprocal, shift) = FPMath.GetReciprocalForNormalize(sqrmag);
            
            magnitude = FP.FromRaw((long)sqrt.Mantissa << sqrt.Exponent >> 14);
            
            return new FPVector3(
                new FP(value.X.RawValue * reciprocal >> shift),
                new FP(value.Y.RawValue * reciprocal >> shift),
                new FP(value.Z.RawValue * reciprocal >> shift)
            );
        }

        #endregion

        #region 运算符

        /// <summary>向量加法</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FPVector3 operator +(FPVector3 a, FPVector3 b)
            => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

        /// <summary>向量减法</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FPVector3 operator -(FPVector3 a, FPVector3 b)
            => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

        /// <summary>向量取反</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FPVector3 operator -(FPVector3 v)
            => new(-v.X, -v.Y, -v.Z);

        /// <summary>向量 * 标量</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FPVector3 operator *(FPVector3 v, FP s)
            => new(v.X * s, v.Y * s, v.Z * s);

        /// <summary>标量 * 向量</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FPVector3 operator *(FP s, FPVector3 v)
            => v * s;

        /// <summary>向量 / 标量</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FPVector3 operator /(FPVector3 v, FP s)
            => new(v.X / s, v.Y / s, v.Z / s);

        /// <summary>相等比较</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(FPVector3 a, FPVector3 b)
            => a.X.RawValue == b.X.RawValue && a.Y.RawValue == b.Y.RawValue && a.Z.RawValue == b.Z.RawValue;

        /// <summary>不等比较</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(FPVector3 a, FPVector3 b)
            => !(a == b);

        /// <summary>隐式从 FPVector2 转换（Z=0）</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator FPVector3(FPVector2 v)
            => new(v.X, v.Y, FP._0);

        /// <summary>显式转换为 FPVector2（丢弃 Z）</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static explicit operator FPVector2(FPVector3 v)
            => new(v.X, v.Y);

        #endregion

        #region 静态方法

        /// <summary>
        /// 点积
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP Dot(FPVector3 a, FPVector3 b)
        {
            long x = (a.X.RawValue * b.X.RawValue + 32768) >> 16;
            long y = (a.Y.RawValue * b.Y.RawValue + 32768) >> 16;
            long z = (a.Z.RawValue * b.Z.RawValue + 32768) >> 16;
            return new FP(x + y + z);
        }

        /// <summary>
        /// 叉积
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FPVector3 Cross(FPVector3 a, FPVector3 b)
        {
            long x = (a.Y.RawValue * b.Z.RawValue - a.Z.RawValue * b.Y.RawValue + 32768) >> 16;
            long y = (a.Z.RawValue * b.X.RawValue - a.X.RawValue * b.Z.RawValue + 32768) >> 16;
            long z = (a.X.RawValue * b.Y.RawValue - a.Y.RawValue * b.X.RawValue + 32768) >> 16;
            return new FPVector3(new FP(x), new FP(y), new FP(z));
        }

        /// <summary>
        /// 两个向量之间的距离
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP Distance(FPVector3 a, FPVector3 b)
            => (a - b).Magnitude;

        /// <summary>
        /// 两个向量之间的距离平方（更快）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP DistanceSquared(FPVector3 a, FPVector3 b)
            => (a - b).SqrMagnitude;

        /// <summary>
        /// 线性插值（t 限制在 [0,1]）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FPVector3 Lerp(FPVector3 a, FPVector3 b, FP t)
        {
            t = FPMath.Clamp01(t);
            return new FPVector3(
                new FP(a.X.RawValue + ((b.X.RawValue - a.X.RawValue) * t.RawValue + 32768 >> 16)),
                new FP(a.Y.RawValue + ((b.Y.RawValue - a.Y.RawValue) * t.RawValue + 32768 >> 16)),
                new FP(a.Z.RawValue + ((b.Z.RawValue - a.Z.RawValue) * t.RawValue + 32768 >> 16))
            );
        }

        /// <summary>
        /// 线性插值（t 不限制）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FPVector3 LerpUnclamped(FPVector3 a, FPVector3 b, FP t)
        {
            return new FPVector3(
                new FP(a.X.RawValue + ((b.X.RawValue - a.X.RawValue) * t.RawValue + 32768 >> 16)),
                new FP(a.Y.RawValue + ((b.Y.RawValue - a.Y.RawValue) * t.RawValue + 32768 >> 16)),
                new FP(a.Z.RawValue + ((b.Z.RawValue - a.Z.RawValue) * t.RawValue + 32768 >> 16))
            );
        }

        /// <summary>
        /// 球面插值（Slerp）
        /// </summary>
        public static FPVector3 Slerp(FPVector3 a, FPVector3 b, FP t)
        {
            t = FPMath.Clamp01(t);
            return SlerpUnclamped(a, b, t);
        }

        /// <summary>
        /// 球面插值（不限制 t）
        /// </summary>
        public static FPVector3 SlerpUnclamped(FPVector3 a, FPVector3 b, FP t)
        {
            FP dot = Dot(a, b);

            // 如果点积为负，反转一个向量以走最短路径
            if (dot.RawValue < 0)
            {
                dot = -dot;
                b = -b;
            }

            // 限制 dot 在 [-1, 1] 内
            dot = FPMath.Clamp(dot, -FP._1, FP._1);

            // 如果点积接近 1，使用线性插值（避免除以接近 0 的 sin）
            if (dot.RawValue > 65536 - 100)  // 1 - epsilon
                return LerpUnclamped(a, b, t);

            // 计算角度和 sin
            FP theta = FP.Acos(dot);           // a 和 b 之间的角度
            FP sinTheta = FP.Sin(theta);       // sin(角度)

            // 计算权重
            FP wa = FP.Sin((FP._1 - t) * theta) / sinTheta;
            FP wb = FP.Sin(t * theta) / sinTheta;

            return a * wa + b * wb;
        }

        /// <summary>
        /// 限制向量长度
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FPVector3 ClampMagnitude(FPVector3 vector, FP maxLength)
        {
            FP sqrMag = vector.SqrMagnitude;
            FP maxSqr = maxLength * maxLength;
            if (sqrMag.RawValue > maxSqr.RawValue)
                return vector.Normalized * maxLength;
            return vector;
        }

        /// <summary>
        /// 反射向量
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FPVector3 Reflect(FPVector3 direction, FPVector3 normal)
        {
            FP twoDot = Dot(direction, normal) * 2;
            return direction - normal * twoDot;
        }

        /// <summary>
        /// 投影向量
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FPVector3 Project(FPVector3 vector, FPVector3 onNormal)
        {
            FP sqrMag = onNormal.SqrMagnitude;
            if (sqrMag.RawValue == 0) return Zero;
            FP dot = Dot(vector, onNormal);
            return onNormal * (dot / sqrMag);
        }

        /// <summary>
        /// 投影到平面上
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FPVector3 ProjectOnPlane(FPVector3 vector, FPVector3 planeNormal)
        {
            FP sqrMag = planeNormal.SqrMagnitude;
            if (sqrMag.RawValue == 0) return vector;
            FP dot = Dot(vector, planeNormal);
            return vector - planeNormal * (dot / sqrMag);
        }

        /// <summary>
        /// 分量逐元素相乘
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FPVector3 Scale(FPVector3 a, FPVector3 b)
            => new(a.X * b.X, a.Y * b.Y, a.Z * b.Z);

        /// <summary>
        /// 取各分量的最小值
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FPVector3 Min(FPVector3 a, FPVector3 b)
            => new(FPMath.Min(a.X, b.X), FPMath.Min(a.Y, b.Y), FPMath.Min(a.Z, b.Z));

        /// <summary>
        /// 取各分量的最大值
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FPVector3 Max(FPVector3 a, FPVector3 b)
            => new(FPMath.Max(a.X, b.X), FPMath.Max(a.Y, b.Y), FPMath.Max(a.Z, b.Z));

        #endregion

        #region 实例方法

        /// <summary>
        /// 与另一个向量相等判断
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool Equals(FPVector3 other)
            => this == other;

        /// <summary>
        /// 与对象相等判断
        /// </summary>
        public override readonly bool Equals(object? obj)
            => obj is FPVector3 other && Equals(other);

        /// <summary>
        /// 获取哈希码
        /// </summary>
        public override readonly int GetHashCode()
        {
            int hash = 17;
            hash = hash * 31 + X.GetHashCode();
            hash = hash * 31 + Y.GetHashCode();
            hash = hash * 31 + Z.GetHashCode();
            return hash;
        }

        /// <summary>
        /// 转换为字符串
        /// </summary>
        public override readonly string ToString()
            => $"({X}, {Y}, {Z})";

        #endregion
    }
}
