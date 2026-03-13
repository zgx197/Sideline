// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

#nullable enable

using System;
using System.Runtime.CompilerServices;
using Lattice.Generators;

namespace Lattice.Math
{
    /// <summary>
    /// 2D 定点数向量
    /// <para>参考 FrameSyncEngine 设计，高性能实现</para>
    /// </summary>
    [GenerateSwizzle(MaxDimension = 3, IncludeZero = true)]
    public readonly partial struct FPVector2 : IEquatable<FPVector2>
    {
        #region 字段

        /// <summary>X 分量</summary>
        public readonly FP X;

        /// <summary>Y 分量</summary>
        public readonly FP Y;

        #endregion

        #region 构造函数

        /// <summary>
        /// 从两个 FP 构造
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FPVector2(FP x, FP y)
        {
            X = x;
            Y = y;
        }

        /// <summary>
        /// 从两个 int 构造（隐式转换）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FPVector2(int x, int y)
        {
            X = (FP)x;
            Y = (FP)y;
        }

        /// <summary>
        /// 从单个 FP 构造（两个分量相同）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FPVector2(FP value)
        {
            X = value;
            Y = value;
        }

        #endregion

        #region 常量

        /// <summary>零向量 (0, 0)</summary>
        public static FPVector2 Zero => default;

        /// <summary>单位向量 (1, 1)</summary>
        public static FPVector2 One => new(FP._1, FP._1);

        /// <summary>右向量 (1, 0)</summary>
        public static FPVector2 Right => new(FP._1, FP._0);

        /// <summary>左向量 (-1, 0)</summary>
        public static FPVector2 Left => new(-FP._1, FP._0);

        /// <summary>上向量 (0, 1)</summary>
        public static FPVector2 Up => new(FP._0, FP._1);

        /// <summary>下向量 (0, -1)</summary>
        public static FPVector2 Down => new(FP._0, -FP._1);

        /// <summary>最大值向量</summary>
        public static FPVector2 MaxValue => new(new FP(long.MaxValue), new FP(long.MaxValue));

        /// <summary>最小值向量</summary>
        public static FPVector2 MinValue => new(new FP(long.MinValue), new FP(long.MinValue));

        #endregion

        // Swizzle 属性由 Source Generator 自动生成
        // 参见 Tools/SwizzleGenerator

        #region 属性

        /// <summary>
        /// 向量长度的平方（不开方，速度快）
        /// <para>用于比较距离时避免开方</para>
        /// </summary>
        public readonly FP SqrMagnitude
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                // (X*X + Y*Y) / ONE，+32768 实现四舍五入
                long x2 = (X.RawValue * X.RawValue + 32768) >> 16;
                long y2 = (Y.RawValue * Y.RawValue + 32768) >> 16;
                return new FP(x2 + y2);
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
        /// <para>零向量返回零向量</para>
        /// <para>使用倒数乘法而非除法，速度快约 2-3 倍</para>
        /// </summary>
        public readonly FPVector2 Normalized
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Normalize(this);
        }

        /// <summary>
        /// 归一化向量并返回原始长度
        /// <para>FrameSync 风格免除法实现</para>
        /// </summary>
        public readonly FPVector2 NormalizedWithMagnitude(out FP magnitude)
        {
            return Normalize(this, out magnitude);
        }

        /// <summary>
        /// 归一化向量（静态方法，FrameSync 优化）
        /// <para>使用指数-尾数分解 + 倒数乘法，避免除法</para>
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FPVector2 Normalize(FPVector2 value)
        {
            ulong sqrmag = (ulong)(value.X.RawValue * value.X.RawValue + value.Y.RawValue * value.Y.RawValue);
            if (sqrmag == 0) return Zero;
            
            var (reciprocal, shift) = FPMath.GetReciprocalForNormalize(sqrmag);
            
            return new FPVector2(
                new FP(value.X.RawValue * reciprocal >> shift),
                new FP(value.Y.RawValue * reciprocal >> shift)
            );
        }

        /// <summary>
        /// 归一化向量并返回原始长度（FrameSync 优化）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FPVector2 Normalize(FPVector2 value, out FP magnitude)
        {
            ulong sqrmag = (ulong)(value.X.RawValue * value.X.RawValue + value.Y.RawValue * value.Y.RawValue);
            if (sqrmag == 0)
            {
                magnitude = FP.Zero;
                return Zero;
            }
            
            var sqrt = FPMath.GetSqrtDecomp(sqrmag);
            var (reciprocal, shift) = FPMath.GetReciprocalForNormalize(sqrmag);
            
            magnitude = FP.FromRaw((long)sqrt.Mantissa << sqrt.Exponent >> 14);
            
            return new FPVector2(
                new FP(value.X.RawValue * reciprocal >> shift),
                new FP(value.Y.RawValue * reciprocal >> shift)
            );
        }

        #endregion

        #region 运算符

        /// <summary>向量加法</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FPVector2 operator +(FPVector2 a, FPVector2 b)
            => new(a.X + b.X, a.Y + b.Y);

        /// <summary>向量减法</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FPVector2 operator -(FPVector2 a, FPVector2 b)
            => new(a.X - b.X, a.Y - b.Y);

        /// <summary>向量取反</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FPVector2 operator -(FPVector2 v)
            => new(-v.X, -v.Y);

        /// <summary>向量 * 标量</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FPVector2 operator *(FPVector2 v, FP s)
            => new(v.X * s, v.Y * s);

        /// <summary>标量 * 向量</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FPVector2 operator *(FP s, FPVector2 v)
            => v * s;

        /// <summary>向量 / 标量</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FPVector2 operator /(FPVector2 v, FP s)
            => new(v.X / s, v.Y / s);

        /// <summary>相等比较</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(FPVector2 a, FPVector2 b)
            => a.X.RawValue == b.X.RawValue && a.Y.RawValue == b.Y.RawValue;

        /// <summary>不等比较</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(FPVector2 a, FPVector2 b)
            => !(a == b);

        #endregion

        #region 静态方法

        /// <summary>
        /// 点积
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP Dot(FPVector2 a, FPVector2 b)
        {
            long x = (a.X.RawValue * b.X.RawValue + 32768) >> 16;
            long y = (a.Y.RawValue * b.Y.RawValue + 32768) >> 16;
            return new FP(x + y);
        }

        /// <summary>
        /// 叉积（2D 叉积返回标量，表示有向面积）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP Cross(FPVector2 a, FPVector2 b)
        {
            long x = (a.X.RawValue * b.Y.RawValue + 32768) >> 16;
            long y = (a.Y.RawValue * b.X.RawValue + 32768) >> 16;
            return new FP(x - y);
        }

        /// <summary>
        /// 两个向量之间的距离
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP Distance(FPVector2 a, FPVector2 b)
            => (a - b).Magnitude;

        /// <summary>
        /// 两个向量之间的距离平方（更快）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP DistanceSquared(FPVector2 a, FPVector2 b)
            => (a - b).SqrMagnitude;

        /// <summary>
        /// 线性插值（t 限制在 [0,1]）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FPVector2 Lerp(FPVector2 a, FPVector2 b, FP t)
        {
            t = FPMath.Clamp01(t);
            return new FPVector2(
                new FP(a.X.RawValue + ((b.X.RawValue - a.X.RawValue) * t.RawValue + 32768 >> 16)),
                new FP(a.Y.RawValue + ((b.Y.RawValue - a.Y.RawValue) * t.RawValue + 32768 >> 16))
            );
        }

        /// <summary>
        /// 线性插值（t 不限制）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FPVector2 LerpUnclamped(FPVector2 a, FPVector2 b, FP t)
        {
            return new FPVector2(
                new FP(a.X.RawValue + ((b.X.RawValue - a.X.RawValue) * t.RawValue + 32768 >> 16)),
                new FP(a.Y.RawValue + ((b.Y.RawValue - a.Y.RawValue) * t.RawValue + 32768 >> 16))
            );
        }

        /// <summary>
        /// 限制向量长度
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FPVector2 ClampMagnitude(FPVector2 vector, FP maxLength)
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
        public static FPVector2 Reflect(FPVector2 direction, FPVector2 normal)
        {
            FP twoDot = Dot(direction, normal) * 2;
            return direction - normal * twoDot;
        }

        /// <summary>
        /// 投影向量到指定法线上
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FPVector2 Project(FPVector2 vector, FPVector2 onNormal)
        {
            FP sqrMag = onNormal.SqrMagnitude;
            if (sqrMag.RawValue == 0) return Zero;
            FP dot = Dot(vector, onNormal);
            return onNormal * (dot / sqrMag);
        }

        /// <summary>
        /// 垂直向量（逆时针旋转 90 度）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FPVector2 Perpendicular(FPVector2 vector)
            => new(-vector.Y, vector.X);

        /// <summary>
        /// 旋转向量（逆时针）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FPVector2 Rotate(FPVector2 vector, FP radians)
        {
            FP sin = FP.Sin(radians);
            FP cos = FP.Cos(radians);
            return Rotate(vector, sin, cos);
        }

        /// <summary>
        /// 旋转向量（传入已计算的 sin/cos）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FPVector2 Rotate(FPVector2 vector, FP sin, FP cos)
        {
            FP x = vector.X * cos - vector.Y * sin;
            FP y = vector.X * sin + vector.Y * cos;
            return new FPVector2(x, y);
        }

        /// <summary>
        /// 分量逐元素相乘
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FPVector2 Scale(FPVector2 a, FPVector2 b)
            => new(a.X * b.X, a.Y * b.Y);

        /// <summary>
        /// 取各分量的最小值
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FPVector2 Min(FPVector2 a, FPVector2 b)
            => new(FPMath.Min(a.X, b.X), FPMath.Min(a.Y, b.Y));

        /// <summary>
        /// 取各分量的最大值
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FPVector2 Max(FPVector2 a, FPVector2 b)
            => new(FPMath.Max(a.X, b.X), FPMath.Max(a.Y, b.Y));

        #region 几何工具函数（参考 FrameSync 设计）

        /// <summary>
        /// 计算两个向量之间的夹角（弧度）
        /// <para>范围：[0, π]</para>
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP Angle(FPVector2 a, FPVector2 b)
        {
            FP dot = Dot(a, b);
            FP magA = a.SqrMagnitude;
            FP magB = b.SqrMagnitude;
            if (magA.RawValue == 0 || magB.RawValue == 0) return FP.Zero;
            
            // cos(θ) = dot / (|a|*|b|)
            FP cos = dot / (FPMath.Sqrt(magA) * FPMath.Sqrt(magB));
            cos = FPMath.Clamp(cos, -FP._1, FP._1);
            return FP.Acos(cos);
        }

        /// <summary>
        /// 计算有符号夹角（弧度）
        /// <para>范围：[-π, π]，从 a 到 b 逆时针为正</para>
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP SignedAngle(FPVector2 a, FPVector2 b)
        {
            FP angle = Angle(a, b);
            FP cross = Cross(a, b);
            return cross.RawValue < 0 ? -angle : angle;
        }

        /// <summary>
        /// 向量插值（按距离）
        /// <para>将 vector 向 target 移动 maxDistanceDelta</para>
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FPVector2 MoveTowards(FPVector2 vector, FPVector2 target, FP maxDistanceDelta)
        {
            FPVector2 diff = target - vector;
            FP dist = diff.Magnitude;
            if (dist.RawValue <= maxDistanceDelta.RawValue || dist.RawValue == 0)
                return target;
            return vector + diff / dist * maxDistanceDelta;
        }

        /// <summary>
        /// 平滑阻尼（SmoothDamp）
        /// <para>FrameSync 风格平滑移动</para>
        /// </summary>
        public static FPVector2 SmoothDamp(FPVector2 current, FPVector2 target, ref FPVector2 velocity, FP smoothTime, FP deltaTime)
        {
            FP omega = 2 / smoothTime;
            FP x = omega * deltaTime;
            FP exp = FP._1 / (FP._1 + x + x * x * FP._0_50); // exp(-x) 近似
            
            FPVector2 change = velocity * deltaTime;
            FPVector2 diff = current - target;
            
            FPVector2 temp = (velocity + omega * diff) * deltaTime;
            velocity = (velocity - omega * temp) * exp;
            
            return target + (diff + change) * exp;
        }

        /// <summary>
        /// 判断点是否在三角形内（重心坐标法）
        /// </summary>
        public static bool PointInTriangle(FPVector2 p, FPVector2 a, FPVector2 b, FPVector2 c)
        {
            FP d1 = Cross(b - a, p - a);
            FP d2 = Cross(c - b, p - b);
            FP d3 = Cross(a - c, p - c);
            
            bool hasNeg = (d1.RawValue < 0) || (d2.RawValue < 0) || (d3.RawValue < 0);
            bool hasPos = (d1.RawValue > 0) || (d2.RawValue > 0) || (d3.RawValue > 0);
            
            return !(hasNeg && hasPos);
        }

        #endregion

        #endregion

        #region 实例方法

        /// <summary>
        /// 与另一个向量相等判断
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool Equals(FPVector2 other)
            => this == other;

        /// <summary>
        /// 与对象相等判断
        /// </summary>
        public override readonly bool Equals(object? obj)
            => obj is FPVector2 other && Equals(other);

        /// <summary>
        /// 获取哈希码
        /// </summary>
        public override readonly int GetHashCode()
        {
            int hash = 17;
            hash = hash * 31 + X.GetHashCode();
            hash = hash * 31 + Y.GetHashCode();
            return hash;
        }

        /// <summary>
        /// 转换为字符串
        /// </summary>
        public override readonly string ToString()
            => $"({X}, {Y})";

        #endregion
    }
}
