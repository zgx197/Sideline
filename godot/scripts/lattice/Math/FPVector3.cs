// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

#nullable enable

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Lattice.Generators;

namespace Lattice.Math
{
    /// <summary>
    /// 3D 定点数向量
    /// <para>参考 FrameSyncEngine 设计，高性能实现</para>
    /// <para>内存布局：连续 24 字节（3 * 8 bytes）</para>
    /// </summary>
    /// <remarks>
    /// <para><b>Swizzle 属性：</b></para>
    /// 本结构使用 <see cref="GenerateSwizzleAttribute"/> Source Generator 自动生成 Swizzle 属性。
    /// 可用属性包括：XX, XY, XZ, YX, YY, YZ, ZX, ZY, ZZ, XXX, XXY, XYZ, XZX, XZY, XZZ 等。
    /// 
    /// <para><b>内存布局：</b></para>
    /// 使用 <see cref="StructLayoutAttribute"/> 显式控制内存布局，确保跨平台一致性。
    /// X 分量在偏移 0，Y 分量在偏移 8，Z 分量在偏移 16。
    /// </remarks>
    [StructLayout(LayoutKind.Explicit, Size = 24)]  // 3 * 8 bytes, Explicit for cross-platform consistency
    [GenerateSwizzle(MaxDimension = 4, IncludeZero = true)]
    public readonly partial struct FPVector3 : IEquatable<FPVector3>
    {
        #region 字段

        /// <summary>X 分量</summary>
        [FieldOffset(0)]
        public readonly FP X;

        /// <summary>Y 分量</summary>
        [FieldOffset(8)]
        public readonly FP Y;

        /// <summary>Z 分量</summary>
        [FieldOffset(16)]
        public readonly FP Z;

        #endregion

        #region 构造函数

        /// <summary>
        /// 从三个 FP 构造
        /// </summary>
        /// <param name="x">X 分量</param>
        /// <param name="y">Y 分量</param>
        /// <param name="z">Z 分量</param>
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
        /// <param name="x">X 分量</param>
        /// <param name="y">Y 分量</param>
        /// <param name="z">Z 分量</param>
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
        /// <param name="x">X 分量</param>
        /// <param name="y">Y 分量</param>
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
        /// <param name="value">X、Y、Z 分量的值</param>
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

        // Swizzle 属性由 Source Generator 自动生成
        // 参见 Tools/SwizzleGenerator

        #region 属性

        /// <summary>
        /// 向量长度的平方 - 精确版（四舍五入）
        /// </summary>
        /// <remarks>
        /// 计算公式：(X*X + Y*Y + Z*Z) / ONE，+32768 实现四舍五入
        /// </remarks>
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
        /// 向量长度的平方 - 快速版（截断）
        /// <para>无四舍五入开销，适合性能敏感场景</para>
        /// </summary>
        /// <remarks>
        /// 误差：最大 1 LSB（约 0.000015）
        /// </remarks>
        public readonly FP SqrMagnitudeFast
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                long x2 = (X.RawValue * X.RawValue) >> 16;
                long y2 = (Y.RawValue * Y.RawValue) >> 16;
                long z2 = (Z.RawValue * Z.RawValue) >> 16;
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
        /// <param name="magnitude">输出原始长度</param>
        /// <returns>归一化后的向量</returns>
        public readonly FPVector3 NormalizedWithMagnitude(out FP magnitude)
        {
            return Normalize(this, out magnitude);
        }

        /// <summary>
        /// 归一化向量（静态方法，FrameSync 优化）
        /// <para>使用指数-尾数分解 + 倒数乘法，避免除法</para>
        /// </summary>
        /// <param name="value">要归一化的向量</param>
        /// <returns>归一化后的向量</returns>
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
        /// <param name="value">要归一化的向量</param>
        /// <param name="magnitude">输出原始长度</param>
        /// <returns>归一化后的向量</returns>
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
        /// <param name="a">第一个向量</param>
        /// <param name="b">第二个向量</param>
        /// <returns>点积结果</returns>
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
        /// <param name="a">第一个向量</param>
        /// <param name="b">第二个向量</param>
        /// <returns>叉积向量</returns>
        /// <remarks>
        /// 叉积结果垂直于 a 和 b 所在的平面，方向由右手定则确定。
        /// 结果长度等于 |a| * |b| * sin(θ)，即两个向量张成的平行四边形的面积。
        /// </remarks>
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
        /// <param name="a">第一个向量</param>
        /// <param name="b">第二个向量</param>
        /// <returns>欧几里得距离</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP Distance(FPVector3 a, FPVector3 b)
            => (a - b).Magnitude;

        /// <summary>
        /// 两个向量之间的距离平方（更快）
        /// </summary>
        /// <param name="a">第一个向量</param>
        /// <param name="b">第二个向量</param>
        /// <returns>距离平方</returns>
        /// <remarks>
        /// 适用于比较距离大小而不需要精确值的场景（如范围检测）。
        /// 比 <see cref="Distance"/> 快约 3-5 倍（省去了平方根运算）。
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP DistanceSquared(FPVector3 a, FPVector3 b)
            => (a - b).SqrMagnitude;

        /// <summary>
        /// 线性插值（t 限制在 [0,1]）
        /// </summary>
        /// <param name="a">起始向量</param>
        /// <param name="b">目标向量</param>
        /// <param name="t">插值系数 [0, 1]</param>
        /// <returns>插值结果</returns>
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
        /// <param name="a">起始向量</param>
        /// <param name="b">目标向量</param>
        /// <param name="t">插值系数（任意值）</param>
        /// <returns>插值结果</returns>
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
        /// <param name="a">起始向量（建议归一化）</param>
        /// <param name="b">目标向量（建议归一化）</param>
        /// <param name="t">插值系数 [0, 1]</param>
        /// <returns>球面插值结果</returns>
        /// <remarks>
        /// Slerp 在球面上进行插值，保持恒定角速度。
        /// 适用于相机旋转、球面移动等场景。
        /// </remarks>
        public static FPVector3 Slerp(FPVector3 a, FPVector3 b, FP t)
        {
            t = FPMath.Clamp01(t);
            return SlerpUnclamped(a, b, t);
        }

        /// <summary>
        /// 球面插值（不限制 t）
        /// </summary>
        /// <param name="a">起始向量（建议归一化）</param>
        /// <param name="b">目标向量（建议归一化）</param>
        /// <param name="t">插值系数（任意值）</param>
        /// <returns>球面插值结果</returns>
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
        /// <param name="vector">要限制的向量</param>
        /// <param name="maxLength">最大长度</param>
        /// <returns>限制后的向量</returns>
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
        /// <param name="direction">入射方向</param>
        /// <param name="normal">法线（需归一化）</param>
        /// <returns>反射后的方向</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FPVector3 Reflect(FPVector3 direction, FPVector3 normal)
        {
            FP twoDot = Dot(direction, normal) * 2;
            return direction - normal * twoDot;
        }

        /// <summary>
        /// 投影向量
        /// </summary>
        /// <param name="vector">要投影的向量</param>
        /// <param name="onNormal">投影目标法线</param>
        /// <returns>投影后的向量</returns>
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
        /// <param name="vector">要投影的向量</param>
        /// <param name="planeNormal">平面法线</param>
        /// <returns>投影到平面后的向量</returns>
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
        /// <param name="a">第一个向量</param>
        /// <param name="b">第二个向量</param>
        /// <returns>逐元素乘积</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FPVector3 Scale(FPVector3 a, FPVector3 b)
            => new(a.X * b.X, a.Y * b.Y, a.Z * b.Z);

        /// <summary>
        /// 取各分量的最小值
        /// </summary>
        /// <param name="a">第一个向量</param>
        /// <param name="b">第二个向量</param>
        /// <returns>各分量的最小值</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FPVector3 Min(FPVector3 a, FPVector3 b)
            => new(FPMath.Min(a.X, b.X), FPMath.Min(a.Y, b.Y), FPMath.Min(a.Z, b.Z));

        /// <summary>
        /// 取各分量的最大值
        /// </summary>
        /// <param name="a">第一个向量</param>
        /// <param name="b">第二个向量</param>
        /// <returns>各分量的最大值</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FPVector3 Max(FPVector3 a, FPVector3 b)
            => new(FPMath.Max(a.X, b.X), FPMath.Max(a.Y, b.Y), FPMath.Max(a.Z, b.Z));

        #endregion

        #region 实例方法

        /// <summary>
        /// 与另一个向量相等判断
        /// </summary>
        /// <param name="other">要比较的向量</param>
        /// <returns>相等返回 true</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool Equals(FPVector3 other)
            => this == other;

        /// <summary>
        /// 与对象相等判断
        /// </summary>
        /// <param name="obj">要比较的对象</param>
        /// <returns>相等返回 true</returns>
        public override readonly bool Equals(object? obj)
            => obj is FPVector3 other && Equals(other);

        /// <summary>
        /// 获取哈希码
        /// </summary>
        /// <returns>哈希码</returns>
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
        /// <returns>字符串表示 "(X, Y, Z)"</returns>
        public override readonly string ToString()
            => $"({X}, {Y}, {Z})";

        #endregion
    }
}
