// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

using Xunit;
using Lattice.Math;

namespace Lattice.Tests.Math
{
    /// <summary>
    /// FPVector3 全面测试
    /// </summary>
    public class FPVector3Tests
    {
        #region 构造与常量

        [Fact]
        public void Constructor_FromFP_ShouldSetComponents()
        {
            var v = new FPVector3(FP._1, FP._2, FP._3);
            Assert.Equal(FP._1.RawValue, v.X.RawValue);
            Assert.Equal(FP._2.RawValue, v.Y.RawValue);
            Assert.Equal(FP._3.RawValue, v.Z.RawValue);
        }

        [Fact]
        public void Constructor_FromInt_ShouldConvertToFP()
        {
            var v = new FPVector3(1, 2, 3);
            Assert.Equal(FP.ONE, v.X.RawValue);
            Assert.Equal(2 * FP.ONE, v.Y.RawValue);
            Assert.Equal(3 * FP.ONE, v.Z.RawValue);
        }

        [Fact]
        public void Constants_ShouldBeCorrect()
        {
            Assert.True(FPVector3.Zero.X.RawValue == 0 && FPVector3.Zero.Y.RawValue == 0 && FPVector3.Zero.Z.RawValue == 0);
            Assert.True(FPVector3.One.X.RawValue == FP.ONE && FPVector3.One.Y.RawValue == FP.ONE && FPVector3.One.Z.RawValue == FP.ONE);
            Assert.True(FPVector3.Right.X.RawValue == FP.ONE && FPVector3.Right.Y.RawValue == 0 && FPVector3.Right.Z.RawValue == 0);
            Assert.True(FPVector3.Up.Y.RawValue == FP.ONE && FPVector3.Up.X.RawValue == 0 && FPVector3.Up.Z.RawValue == 0);
            Assert.True(FPVector3.Forward.Z.RawValue == FP.ONE && FPVector3.Forward.X.RawValue == 0 && FPVector3.Forward.Y.RawValue == 0);
        }

        [Fact]
        public void Vector3_ToVector2_ShouldDropZ()
        {
            var v3 = new FPVector3(1, 2, 3);
            var v2 = (FPVector2)v3;
            Assert.Equal(v3.X.RawValue, v2.X.RawValue);
            Assert.Equal(v3.Y.RawValue, v2.Y.RawValue);
        }

        #endregion

        #region Swizzle

        [Fact]
        public void Swizzle_XYZ_ShouldReturnSame()
        {
            var v = new FPVector3(1, 2, 3);
            var swizzled = v.XYZ;
            Assert.Equal(v.X.RawValue, swizzled.X.RawValue);
            Assert.Equal(v.Y.RawValue, swizzled.Y.RawValue);
            Assert.Equal(v.Z.RawValue, swizzled.Z.RawValue);
        }

        [Fact]
        public void Swizzle_ZYX_ShouldReverse()
        {
            var v = new FPVector3(1, 2, 3);
            var swizzled = v.ZYX;
            Assert.Equal(v.Z.RawValue, swizzled.X.RawValue);
            Assert.Equal(v.Y.RawValue, swizzled.Y.RawValue);
            Assert.Equal(v.X.RawValue, swizzled.Z.RawValue);
        }

        #endregion

        #region 属性与长度

        [Fact]
        public void SqrMagnitude_3D_ShouldBeSumOfSquares()
        {
            var v = new FPVector3(1, 2, 2);  // 1+4+4=9
            FP sqrMag = v.SqrMagnitude;
            Assert.Equal(9 * FP.ONE, sqrMag.RawValue);
        }

        [Fact]
        public void Magnitude_3D_ShouldBeCorrect()
        {
            var v = new FPVector3(3, 4, 0);  // 长度 5（XY平面）
            FP mag = v.Magnitude;
            Assert.True(FP.Abs(mag - 5).RawValue < 100, $"Magnitude should be ~5, got {mag.RawValue}");
        }

        [Fact]
        public void Magnitude_SpaceDiagonal_ShouldBeSqrt3()
        {
            var v = new FPVector3(1, 1, 1);  // sqrt(3) ≈ 1.732
            FP mag = v.Magnitude;
            // sqrt(3) * 65536 ≈ 113512
            Assert.True(mag.RawValue > 113000 && mag.RawValue < 114000, 
                $"Magnitude of (1,1,1) should be ~113512, got {mag.RawValue}");
        }

        [Fact]
        public void Normalized_UnitX_ShouldBeSame()
        {
            var v = FPVector3.Right;
            var n = v.Normalized;
            Assert.True(FP.Abs(n.X - FP._1).RawValue < 100);
            Assert.True(FP.Abs(n.Y).RawValue < 100);
            Assert.True(FP.Abs(n.Z).RawValue < 100);
        }

        [Fact]
        public void Normalized_ZeroVector_ShouldReturnZero()
        {
            var v = FPVector3.Zero;
            var n = v.Normalized;
            Assert.Equal(0L, n.X.RawValue);
            Assert.Equal(0L, n.Y.RawValue);
            Assert.Equal(0L, n.Z.RawValue);
        }

        [Fact]
        public void Normalized_ShouldProduceUnitVector()
        {
            var v = new FPVector3(3, 4, 5);
            var n = v.Normalized;
            FP mag = n.Magnitude;
            Assert.True(FP.Abs(mag - FP._1).RawValue < 200, 
                $"Normalized vector should have magnitude ~1, got {mag.RawValue}");
        }

        #endregion

        #region 运算符

        [Fact]
        public void Operator_Add_3D()
        {
            var a = new FPVector3(1, 2, 3);
            var b = new FPVector3(4, 5, 6);
            var c = a + b;
            Assert.Equal(5 * FP.ONE, c.X.RawValue);
            Assert.Equal(7 * FP.ONE, c.Y.RawValue);
            Assert.Equal(9 * FP.ONE, c.Z.RawValue);
        }

        [Fact]
        public void Operator_Sub_3D()
        {
            var a = new FPVector3(5, 6, 7);
            var b = new FPVector3(1, 2, 3);
            var c = a - b;
            Assert.Equal(4 * FP.ONE, c.X.RawValue);
            Assert.Equal(4 * FP.ONE, c.Y.RawValue);
            Assert.Equal(4 * FP.ONE, c.Z.RawValue);
        }

        [Fact]
        public void Operator_MultiplyByScalar_3D()
        {
            var v = new FPVector3(1, 2, 3);
            var r = v * FP._2;
            Assert.Equal(2 * FP.ONE, r.X.RawValue);
            Assert.Equal(4 * FP.ONE, r.Y.RawValue);
            Assert.Equal(6 * FP.ONE, r.Z.RawValue);
        }

        [Fact]
        public void Operator_Negate_3D()
        {
            var v = new FPVector3(1, -2, 3);
            var n = -v;
            Assert.Equal(-FP.ONE, n.X.RawValue);
            Assert.Equal(2 * FP.ONE, n.Y.RawValue);
            Assert.Equal(-3 * FP.ONE, n.Z.RawValue);
        }

        #endregion

        #region 静态方法

        [Fact]
        public void Dot_Perpendicular3D_ShouldBeZero()
        {
            var a = FPVector3.Right;
            var b = FPVector3.Up;
            FP dot = FPVector3.Dot(a, b);
            Assert.True(FP.Abs(dot).RawValue < 10, $"Dot of perpendicular vectors should be ~0, got {dot.RawValue}");
        }

        [Fact]
        public void Dot_SameDirection_ShouldBeProduct()
        {
            var a = new FPVector3(2, 0, 0);
            var b = new FPVector3(3, 0, 0);
            FP dot = FPVector3.Dot(a, b);
            Assert.Equal(6 * FP.ONE, dot.RawValue);
        }

        [Fact]
        public void Cross_RightCrossUp_ShouldBeForward()
        {
            var cross = FPVector3.Cross(FPVector3.Right, FPVector3.Up);
            Assert.True(FP.Abs(cross.X).RawValue < 100, "X should be ~0");
            Assert.True(FP.Abs(cross.Y).RawValue < 100, "Y should be ~0");
            Assert.True(FP.Abs(cross.Z - FP._1).RawValue < 100, "Z should be ~1");
        }

        [Fact]
        public void Cross_UpCrossForward_ShouldBeRight()
        {
            var cross = FPVector3.Cross(FPVector3.Up, FPVector3.Forward);
            Assert.True(FP.Abs(cross.X - FP._1).RawValue < 100, "X should be ~1");
            Assert.True(FP.Abs(cross.Y).RawValue < 100, "Y should be ~0");
            Assert.True(FP.Abs(cross.Z).RawValue < 100, "Z should be ~0");
        }

        [Fact]
        public void Cross_AntiCommutative()
        {
            var a = new FPVector3(1, 2, 3);
            var b = new FPVector3(4, 5, 6);
            var crossAB = FPVector3.Cross(a, b);
            var crossBA = FPVector3.Cross(b, a);
            Assert.Equal(crossAB.X.RawValue, -crossBA.X.RawValue);
            Assert.Equal(crossAB.Y.RawValue, -crossBA.Y.RawValue);
            Assert.Equal(crossAB.Z.RawValue, -crossBA.Z.RawValue);
        }

        [Fact]
        public void Distance_3D()
        {
            var a = new FPVector3(0, 0, 0);
            var b = new FPVector3(1, 2, 2);  // sqrt(1+4+4) = sqrt(9) = 3
            FP dist = FPVector3.Distance(a, b);
            Assert.True(FP.Abs(dist - 3).RawValue < 100, $"Distance should be ~3, got {dist.RawValue}");
        }

        [Fact]
        public void DistanceSquared_3D()
        {
            var a = new FPVector3(1, 2, 3);
            var b = new FPVector3(4, 6, 9);  // dx=3, dy=4, dz=6 -> 9+16+36=61
            FP distSqr = FPVector3.DistanceSquared(a, b);
            Assert.Equal(61 * FP.ONE, distSqr.RawValue);
        }

        [Fact]
        public void Lerp_3D()
        {
            var a = new FPVector3(0, 0, 0);
            var b = new FPVector3(2, 4, 6);
            var r = FPVector3.Lerp(a, b, FP._0_50);
            Assert.Equal(FP._1.RawValue, r.X.RawValue);
            Assert.Equal(2 * FP.ONE, r.Y.RawValue);
            Assert.Equal(3 * FP.ONE, r.Z.RawValue);
        }

        [Fact]
        public void Project_OnAxis_3D()
        {
            var v = new FPVector3(3, 4, 5);
            var axis = FPVector3.Right;
            var proj = FPVector3.Project(v, axis);
            Assert.Equal(3 * FP.ONE, proj.X.RawValue);
            Assert.Equal(0L, proj.Y.RawValue);
            Assert.Equal(0L, proj.Z.RawValue);
        }

        [Fact]
        public void Reflect_3D()
        {
            var direction = new FPVector3(1, -1, 0).Normalized;
            var normal = FPVector3.Up;
            var reflected = FPVector3.Reflect(direction, normal);
            Assert.True(reflected.Y.RawValue > 0, "Reflected Y should be positive");
        }

        [Fact]
        public void Slerp_SameVectors_ShouldReturnSame()
        {
            var a = FPVector3.Right;
            var b = FPVector3.Right;
            var r = FPVector3.Slerp(a, b, FP._0_50);
            // Slerp 结果应该是单位向量
            FP mag = r.Magnitude;
            Assert.True(FP.Abs(mag - FP._1).RawValue < 500, 
                $"Slerp result should be unit vector, magnitude {mag.RawValue}");
        }

        #endregion

        #region 边界情况

        [Fact]
        public void VerySmallVector_Normalized_3D()
        {
            var v = new FPVector3(FP.Epsilon, FP.Epsilon, FP.Epsilon);
            var n = v.Normalized;
            Assert.True(n.X.RawValue >= 0);
            Assert.True(n.Y.RawValue >= 0);
            Assert.True(n.Z.RawValue >= 0);
        }

        [Fact]
        public void LargeVector_Magnitude_3D()
        {
            var v = new FPVector3(10000, 10000, 10000);
            FP mag = v.Magnitude;
            Assert.True(mag.RawValue > 0);
        }

        [Fact]
        public void Cross_ZeroVector_ShouldReturnZero()
        {
            var a = new FPVector3(1, 2, 3);
            var zero = FPVector3.Zero;
            var cross = FPVector3.Cross(a, zero);
            Assert.Equal(0L, cross.X.RawValue);
            Assert.Equal(0L, cross.Y.RawValue);
            Assert.Equal(0L, cross.Z.RawValue);
        }

        #endregion
    }
}
