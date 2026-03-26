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
    /// 2D 定点数向量
    /// <para>参考 FrameSyncEngine 设计，高性能实现</para>
    /// <para>内存布局：连续 16 字节（2 * 8 bytes）</para>
    /// </summary>
    /// <remarks>
    /// <para><b>Swizzle 属性：</b></para>
    /// 本结构使用 <see cref="GenerateSwizzleAttribute"/> Source Generator 自动生成 Swizzle 属性。
    /// 可用属性包括：XX, XY, YX, YY, XXX, XYX, XYY, YXX, YXY, YYX, YYY, XXXY, XXYZ 等。
    /// 
    /// <para><b>内存布局：</b></para>
    /// 使用 <see cref="StructLayoutAttribute"/> 显式控制内存布局，确保跨平台一致性。
    /// X 分量在偏移 0，Y 分量在偏移 8。
    /// </remarks>
    [StructLayout(LayoutKind.Explicit, Size = 16)]  // 2 * 8 bytes, Explicit for cross-platform consistency
    [GenerateSwizzle(MaxDimension = 3, IncludeZero = true)]
    public readonly partial struct FPVector2 : IEquatable<FPVector2>
    {
        #region 字段

        /// <summary>X 分量</summary>
        [FieldOffset(0)]
        public readonly FP X;

        /// <summary>Y 分量</summary>
        [FieldOffset(8)]
        public readonly FP Y;

        #endregion

        #region 构造函数

        /// <summary>
        /// 从两个 FP 构造
        /// </summary>
        /// <param name="x">X 分量</param>
        /// <param name="y">Y 分量</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FPVector2(FP x, FP y)
        {
            X = x;
            Y = y;
        }

        /// <summary>
        /// 从两个 int 构造（隐式转换）
        /// </summary>
        /// <param name="x">X 分量</param>
        /// <param name="y">Y 分量</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FPVector2(int x, int y)
        {
            X = (FP)x;
            Y = (FP)y;
        }

        /// <summary>
        /// 从单个 FP 构造（两个分量相同）
        /// </summary>
        /// <param name="value">X 和 Y 分量的值</param>
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
        /// 向量长度的平方（不开方，速度快）- 精确版
        /// <para>使用四舍五入，精度更高</para>
        /// </summary>
        /// <remarks>
        /// 计算公式：(X*X + Y*Y) / ONE，+32768 实现四舍五入
        /// </remarks>
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
                // 直接截断，无 +32768 开销
                long x2 = (X.RawValue * X.RawValue) >> 16;
                long y2 = (Y.RawValue * Y.RawValue) >> 16;
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
        /// <param name="magnitude">输出原始长度</param>
        /// <returns>归一化后的向量</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly FPVector2 NormalizedWithMagnitude(out FP magnitude)
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
        /// <param name="value">要归一化的向量</param>
        /// <param name="magnitude">输出原始长度</param>
        /// <returns>归一化后的向量</returns>
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
        /// <param name="a">第一个向量</param>
        /// <param name="b">第二个向量</param>
        /// <returns>点积结果</returns>
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
        /// <param name="a">第一个向量</param>
        /// <param name="b">第二个向量</param>
        /// <returns>叉积结果（标量）</returns>
        /// <remarks>
        /// 2D 叉积等价于 a.X * b.Y - a.Y * b.X，表示两个向量张成的平行四边形的有向面积。
        /// 结果为正表示 b 在 a 的逆时针方向，为负表示顺时针方向。
        /// </remarks>
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
        /// <param name="a">第一个向量</param>
        /// <param name="b">第二个向量</param>
        /// <returns>欧几里得距离</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP Distance(FPVector2 a, FPVector2 b)
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
        public static FP DistanceSquared(FPVector2 a, FPVector2 b)
            => (a - b).SqrMagnitude;

        /// <summary>
        /// 线性插值（t 限制在 [0,1]）
        /// </summary>
        /// <param name="a">起始向量</param>
        /// <param name="b">目标向量</param>
        /// <param name="t">插值系数 [0, 1]</param>
        /// <returns>插值结果</returns>
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
        /// <param name="a">起始向量</param>
        /// <param name="b">目标向量</param>
        /// <param name="t">插值系数（任意值）</param>
        /// <returns>插值结果</returns>
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
        /// <param name="vector">要限制的向量</param>
        /// <param name="maxLength">最大长度</param>
        /// <returns>限制后的向量</returns>
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
        /// <param name="direction">入射方向</param>
        /// <param name="normal">法线（需归一化）</param>
        /// <returns>反射后的方向</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FPVector2 Reflect(FPVector2 direction, FPVector2 normal)
        {
            FP twoDot = Dot(direction, normal) * 2;
            return direction - normal * twoDot;
        }

        /// <summary>
        /// 投影向量到指定法线上
        /// </summary>
        /// <param name="vector">要投影的向量</param>
        /// <param name="onNormal">投影目标法线</param>
        /// <returns>投影后的向量</returns>
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
        /// <param name="vector">输入向量</param>
        /// <returns>逆时针旋转 90 度后的向量</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FPVector2 Perpendicular(FPVector2 vector)
            => new(-vector.Y, vector.X);

        /// <summary>
        /// 旋转向量（逆时针）
        /// </summary>
        /// <param name="vector">要旋转的向量</param>
        /// <param name="radians">旋转角度（弧度）</param>
        /// <returns>旋转后的向量</returns>
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
        /// <param name="vector">要旋转的向量</param>
        /// <param name="sin">正弦值</param>
        /// <param name="cos">余弦值</param>
        /// <returns>旋转后的向量</returns>
        /// <remarks>
        /// 适用于需要多次旋转或 sin/cos 已预计算的场景，避免重复计算三角函数。
        /// </remarks>
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
        /// <param name="a">第一个向量</param>
        /// <param name="b">第二个向量</param>
        /// <returns>逐元素乘积</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FPVector2 Scale(FPVector2 a, FPVector2 b)
            => new(a.X * b.X, a.Y * b.Y);

        /// <summary>
        /// 取各分量的最小值
        /// </summary>
        /// <param name="a">第一个向量</param>
        /// <param name="b">第二个向量</param>
        /// <returns>各分量的最小值</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FPVector2 Min(FPVector2 a, FPVector2 b)
            => new(FPMath.Min(a.X, b.X), FPMath.Min(a.Y, b.Y));

        /// <summary>
        /// 取各分量的最大值
        /// </summary>
        /// <param name="a">第一个向量</param>
        /// <param name="b">第二个向量</param>
        /// <returns>各分量的最大值</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FPVector2 Max(FPVector2 a, FPVector2 b)
            => new(FPMath.Max(a.X, b.X), FPMath.Max(a.Y, b.Y));

        #region 几何工具函数（参考 FrameSync 设计）

        /// <summary>
        /// 计算两个向量之间的夹角（弧度）- 优化版
        /// <para>范围：[0, π]</para>
        /// <para>优化：合并平方根计算，减少一次 Sqrt</para>
        /// </summary>
        /// <param name="a">第一个向量</param>
        /// <param name="b">第二个向量</param>
        /// <returns>夹角（弧度）</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP Angle(FPVector2 a, FPVector2 b)
        {
            FP dot = Dot(a, b);
            FP sqrMagA = a.SqrMagnitude;
            FP sqrMagB = b.SqrMagnitude;
            if (sqrMagA.RawValue == 0 || sqrMagB.RawValue == 0) return FP.Zero;

            // 优化：sqrt(|a|² * |b|²) = |a| * |b|，只需一次 Sqrt
            FP cos = dot / FPMath.Sqrt(sqrMagA * sqrMagB);
            cos = FPMath.Clamp(cos, -FP._1, FP._1);
            return FP.Acos(cos);
        }

        /// <summary>
        /// 计算有符号夹角（弧度）
        /// <para>范围：[-π, π]，从 a 到 b 逆时针为正</para>
        /// </summary>
        /// <param name="a">起始向量</param>
        /// <param name="b">目标向量</param>
        /// <returns>有符号夹角（弧度）</returns>
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
        /// <param name="vector">当前向量</param>
        /// <param name="target">目标向量</param>
        /// <param name="maxDistanceDelta">最大移动距离</param>
        /// <returns>移动后的向量</returns>
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
        /// <param name="current">当前位置</param>
        /// <param name="target">目标位置</param>
        /// <param name="velocity">当前速度（引用，会被修改）</param>
        /// <param name="smoothTime">平滑时间</param>
        /// <param name="deltaTime">时间增量</param>
        /// <returns>平滑后的位置</returns>
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
        /// <param name="p">要判断的点</param>
        /// <param name="a">三角形顶点 A</param>
        /// <param name="b">三角形顶点 B</param>
        /// <param name="c">三角形顶点 C</param>
        /// <returns>如果点在三角形内（包括边上）返回 true</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
        /// <param name="other">要比较的向量</param>
        /// <returns>相等返回 true</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool Equals(FPVector2 other)
            => this == other;

        /// <summary>
        /// 与对象相等判断
        /// </summary>
        /// <param name="obj">要比较的对象</param>
        /// <returns>相等返回 true</returns>
        public override readonly bool Equals(object? obj)
            => obj is FPVector2 other && Equals(other);

        /// <summary>
        /// 获取哈希码
        /// </summary>
        /// <returns>哈希码</returns>
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
        /// <returns>字符串表示 "(X, Y)"</returns>
        public override readonly string ToString()
            => $"({X}, {Y})";

        #endregion
    }
}
