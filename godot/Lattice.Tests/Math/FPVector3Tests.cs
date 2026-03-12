using Xunit;
using Lattice.Math;

namespace Lattice.Tests.Math
{
    /// <summary>
    /// FPVector3 单元测试
    /// </summary>
    public class FPVector3Tests
    {
        #region 构造函数和常量

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
            var v = new FPVector3(3, 4, 5);
            Assert.Equal(3 * FP.ONE, v.X.RawValue);
            Assert.Equal(4 * FP.ONE, v.Y.RawValue);
            Assert.Equal(5 * FP.ONE, v.Z.RawValue);
        }

        [Fact]
        public void Constants_ShouldBeCorrect()
        {
            Assert.Equal(FP._0.RawValue, FPVector3.Zero.X.RawValue);
            Assert.Equal(FP._0.RawValue, FPVector3.Zero.Y.RawValue);
            Assert.Equal(FP._0.RawValue, FPVector3.Zero.Z.RawValue);

            Assert.Equal(FP._1.RawValue, FPVector3.Right.X.RawValue);
            Assert.Equal(FP._0.RawValue, FPVector3.Right.Y.RawValue);
            Assert.Equal(FP._0.RawValue, FPVector3.Right.Z.RawValue);

            Assert.Equal(FP._0.RawValue, FPVector3.Up.X.RawValue);
            Assert.Equal(FP._1.RawValue, FPVector3.Up.Y.RawValue);
            Assert.Equal(FP._0.RawValue, FPVector3.Up.Z.RawValue);

            Assert.Equal(FP._0.RawValue, FPVector3.Forward.X.RawValue);
            Assert.Equal(FP._0.RawValue, FPVector3.Forward.Y.RawValue);
            Assert.Equal(FP._1.RawValue, FPVector3.Forward.Z.RawValue);
        }

        [Fact]
        public void ImplicitConversion_FromFPVector2_ShouldSetZToZero()
        {
            FPVector2 v2 = new FPVector2(1, 2);
            FPVector3 v3 = v2;  // 隐式转换
            Assert.Equal(v2.X.RawValue, v3.X.RawValue);
            Assert.Equal(v2.Y.RawValue, v3.Y.RawValue);
            Assert.Equal(0L, v3.Z.RawValue);
        }

        [Fact]
        public void ExplicitConversion_ToFPVector2_ShouldDiscardZ()
        {
            FPVector3 v3 = new FPVector3(1, 2, 3);
            FPVector2 v2 = (FPVector2)v3;  // 显式转换
            Assert.Equal(v3.X.RawValue, v2.X.RawValue);
            Assert.Equal(v3.Y.RawValue, v2.Y.RawValue);
        }

        #endregion

        #region Swizzle (3D → 2D)

        [Fact]
        public void Swizzle_XX_ShouldReturnCorrectValue()
        {
            var v = new FPVector3(1, 2, 3);
            var swizzled = v.XX;
            Assert.Equal(v.X.RawValue, swizzled.X.RawValue);
            Assert.Equal(v.X.RawValue, swizzled.Y.RawValue);
        }

        [Fact]
        public void Swizzle_XY_ShouldReturnXY()
        {
            var v = new FPVector3(1, 2, 3);
            var swizzled = v.XY;
            Assert.Equal(v.X.RawValue, swizzled.X.RawValue);
            Assert.Equal(v.Y.RawValue, swizzled.Y.RawValue);
        }

        [Fact]
        public void Swizzle_ZY_ShouldSwapAndSelect()
        {
            var v = new FPVector3(1, 2, 3);
            var swizzled = v.ZY;
            Assert.Equal(v.Z.RawValue, swizzled.X.RawValue);
            Assert.Equal(v.Y.RawValue, swizzled.Y.RawValue);
        }

        #endregion

        #region Swizzle (3D → 3D)

        [Fact]
        public void Swizzle_XXX_ShouldReturnAllX()
        {
            var v = new FPVector3(1, 2, 3);
            var swizzled = v.XXX;
            Assert.Equal(v.X.RawValue, swizzled.X.RawValue);
            Assert.Equal(v.X.RawValue, swizzled.Y.RawValue);
            Assert.Equal(v.X.RawValue, swizzled.Z.RawValue);
        }

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

        [Fact]
        public void Swizzle_YXZ_ShouldSwapXY()
        {
            var v = new FPVector3(1, 2, 3);
            var swizzled = v.YXZ;
            Assert.Equal(v.Y.RawValue, swizzled.X.RawValue);
            Assert.Equal(v.X.RawValue, swizzled.Y.RawValue);
            Assert.Equal(v.Z.RawValue, swizzled.Z.RawValue);
        }

        #endregion

        #region 属性

        [Fact]
        public void SqrMagnitude_1_2_2_ShouldBe9()
        {
            var v = new FPVector3(1, 2, 2);  // 1+4+4=9
            FP sqrMag = v.SqrMagnitude;
            Assert.Equal(9 * FP.ONE, sqrMag.RawValue);
        }

        [Fact]
        public void Magnitude_3_4_0_ShouldBe5()
        {
            var v = new FPVector3(3, 4, 0);  // (3,4,0) 长度应为 5
            FP mag = v.Magnitude;
            Assert.True(FP.Abs(mag - 5).RawValue < 100, $"Magnitude should be ~5, got {mag.RawValue}");
        }

        [Fact]
        public void Normalized_UnitVector_ShouldBeSame()
        {
            var v = new FPVector3(1, 0, 0);
            var n = v.Normalized;
            Assert.True(FP.Abs(n.X - FP._1).RawValue < 100);
            Assert.True(FP.Abs(n.Y).RawValue < 100);
            Assert.True(FP.Abs(n.Z).RawValue < 100);
        }

        [Fact]
        public void Normalized_3_4_0_ShouldBeAbout_0_6_0_8_0()
        {
            var v = new FPVector3(3, 4, 0);
            var n = v.Normalized;
            Assert.True(FP.Abs(n.X.RawValue - 39322) < 200, $"X should be ~0.6, got {n.X.RawValue}");
            Assert.True(FP.Abs(n.Y.RawValue - 52429) < 200, $"Y should be ~0.8, got {n.Y.RawValue}");
            Assert.True(FP.Abs(n.Z).RawValue < 100, $"Z should be ~0, got {n.Z.RawValue}");
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

        #endregion

        #region 运算符

        [Fact]
        public void Operator_Add_ShouldAddComponents()
        {
            var a = new FPVector3(1, 2, 3);
            var b = new FPVector3(4, 5, 6);
            var c = a + b;
            Assert.Equal(5 * FP.ONE, c.X.RawValue);
            Assert.Equal(7 * FP.ONE, c.Y.RawValue);
            Assert.Equal(9 * FP.ONE, c.Z.RawValue);
        }

        [Fact]
        public void Operator_Sub_ShouldSubtractComponents()
        {
            var a = new FPVector3(5, 6, 7);
            var b = new FPVector3(2, 3, 4);
            var c = a - b;
            Assert.Equal(3 * FP.ONE, c.X.RawValue);
            Assert.Equal(3 * FP.ONE, c.Y.RawValue);
            Assert.Equal(3 * FP.ONE, c.Z.RawValue);
        }

        [Fact]
        public void Operator_MultiplyByScalar_ShouldScale()
        {
            var v = new FPVector3(1, 2, 3);
            var s = FP._2;
            var r = v * s;
            Assert.Equal(2 * FP.ONE, r.X.RawValue);
            Assert.Equal(4 * FP.ONE, r.Y.RawValue);
            Assert.Equal(6 * FP.ONE, r.Z.RawValue);
        }

        #endregion

        #region 静态方法

        [Fact]
        public void Dot_OrthogonalVectors_ShouldBeZero()
        {
            var a = FPVector3.Right;   // (1,0,0)
            var b = FPVector3.Up;      // (0,1,0)
            FP dot = FPVector3.Dot(a, b);
            Assert.True(FP.Abs(dot).RawValue < 10, $"Dot of orthogonal vectors should be ~0, got {dot.RawValue}");
        }

        [Fact]
        public void Dot_SameDirection_ShouldBeProductOfMagnitudes()
        {
            var a = new FPVector3(2, 0, 0);
            var b = new FPVector3(3, 0, 0);
            FP dot = FPVector3.Dot(a, b);
            Assert.Equal(6 * FP.ONE, dot.RawValue);  // 2*3=6
        }

        [Fact]
        public void Cross_RightUp_ShouldBeForward()
        {
            var a = FPVector3.Right;   // (1,0,0)
            var b = FPVector3.Up;      // (0,1,0)
            var cross = FPVector3.Cross(a, b);
            // Cross((1,0,0), (0,1,0)) = (0,0,1)
            Assert.True(FP.Abs(cross.X).RawValue < 100);
            Assert.True(FP.Abs(cross.Y).RawValue < 100);
            Assert.True(cross.Z.RawValue > 0, "Cross of Right x Up should be Forward (positive Z)");
        }

        [Fact]
        public void Cross_UpRight_ShouldBeBack()
        {
            var a = FPVector3.Up;      // (0,1,0)
            var b = FPVector3.Right;   // (1,0,0)
            var cross = FPVector3.Cross(a, b);
            // Cross((0,1,0), (1,0,0)) = (0,0,-1)
            Assert.True(FP.Abs(cross.X).RawValue < 100);
            Assert.True(FP.Abs(cross.Y).RawValue < 100);
            Assert.True(cross.Z.RawValue < 0, "Cross of Up x Right should be Back (negative Z)");
        }

        [Fact]
        public void Distance_3D_Pythagorean()
        {
            var a = new FPVector3(0, 0, 0);
            var b = new FPVector3(1, 2, 2);  // sqrt(1+4+4) = 3
            FP dist = FPVector3.Distance(a, b);
            Assert.True(FP.Abs(dist - 3).RawValue < 100, $"Distance should be ~3, got {dist.RawValue}");
        }

        [Fact]
        public void Lerp_T0_5_ShouldBeMiddle()
        {
            var a = new FPVector3(0, 0, 0);
            var b = new FPVector3(2, 4, 6);
            var r = FPVector3.Lerp(a, b, FP._0_50);
            Assert.Equal(FP._1.RawValue, r.X.RawValue);
            Assert.Equal(2 * FP.ONE, r.Y.RawValue);
            Assert.Equal(3 * FP.ONE, r.Z.RawValue);
        }

        [Fact]
        public void Reflect_45DegreesXY_ShouldReflectCorrectly()
        {
            var direction = new FPVector3(1, -1, 0).Normalized;
            var normal = FPVector3.Up;
            var reflected = FPVector3.Reflect(direction, normal);
            Assert.True(reflected.Y.RawValue > 0, "Reflected Y should be positive");
        }

        [Fact]
        public void Project_OnXAxis_ShouldGiveXComponent()
        {
            var v = new FPVector3(3, 4, 5);
            var axis = FPVector3.Right;
            var proj = FPVector3.Project(v, axis);
            Assert.Equal(3 * FP.ONE, proj.X.RawValue);
            Assert.Equal(0L, proj.Y.RawValue);
            Assert.Equal(0L, proj.Z.RawValue);
        }

        [Fact]
        public void ProjectOnPlane_XYPlane_ShouldRemoveZ()
        {
            var v = new FPVector3(3, 4, 5);
            var planeNormal = FPVector3.Forward;  // XY 平面的法线
            var proj = FPVector3.ProjectOnPlane(v, planeNormal);
            Assert.Equal(3 * FP.ONE, proj.X.RawValue);
            Assert.Equal(4 * FP.ONE, proj.Y.RawValue);
            Assert.True(FP.Abs(proj.Z).RawValue < 100, "Z should be ~0");
        }

        [Fact]
        public void Scale_ShouldMultiplyComponents()
        {
            var a = new FPVector3(2, 3, 4);
            var b = new FPVector3(5, 6, 7);
            var r = FPVector3.Scale(a, b);
            Assert.Equal(10 * FP.ONE, r.X.RawValue);
            Assert.Equal(18 * FP.ONE, r.Y.RawValue);
            Assert.Equal(28 * FP.ONE, r.Z.RawValue);
        }

        [Fact]
        public void Min_ShouldTakeComponentWiseMin()
        {
            var a = new FPVector3(1, 5, 3);
            var b = new FPVector3(4, 2, 6);
            var r = FPVector3.Min(a, b);
            Assert.Equal(FP._1.RawValue, r.X.RawValue);
            Assert.Equal(2 * FP.ONE, r.Y.RawValue);
            Assert.Equal(3 * FP.ONE, r.Z.RawValue);
        }

        [Fact]
        public void Max_ShouldTakeComponentWiseMax()
        {
            var a = new FPVector3(1, 5, 3);
            var b = new FPVector3(4, 2, 6);
            var r = FPVector3.Max(a, b);
            Assert.Equal(4 * FP.ONE, r.X.RawValue);
            Assert.Equal(5 * FP.ONE, r.Y.RawValue);
            Assert.Equal(6 * FP.ONE, r.Z.RawValue);
        }

        #endregion

        #region 确定性测试

        [Fact]
        public void Normalized_ShouldBeDeterministic()
        {
            var v = new FPVector3(3, 4, 0);
            var n1 = v.Normalized;
            var n2 = v.Normalized;
            Assert.Equal(n1.X.RawValue, n2.X.RawValue);
            Assert.Equal(n1.Y.RawValue, n2.Y.RawValue);
            Assert.Equal(n1.Z.RawValue, n2.Z.RawValue);
        }

        [Fact]
        public void Cross_ShouldBeDeterministic()
        {
            var a = FPVector3.Right;
            var b = FPVector3.Up;
            var c1 = FPVector3.Cross(a, b);
            var c2 = FPVector3.Cross(a, b);
            Assert.Equal(c1.X.RawValue, c2.X.RawValue);
            Assert.Equal(c1.Y.RawValue, c2.Y.RawValue);
            Assert.Equal(c1.Z.RawValue, c2.Z.RawValue);
        }

        [Fact]
        public void Swizzle_ShouldBeDeterministic()
        {
            var v = new FPVector3(1, 2, 3);
            var s1 = v.ZYX;
            var s2 = v.ZYX;
            Assert.Equal(s1.X.RawValue, s2.X.RawValue);
            Assert.Equal(s1.Y.RawValue, s2.Y.RawValue);
            Assert.Equal(s1.Z.RawValue, s2.Z.RawValue);
        }

        #endregion

        #region 边缘情况

        [Fact]
        public void VerySmallVector_Normalized_ShouldHandleGracefully()
        {
            var v = new FPVector3(FP.Epsilon, FP.Epsilon, FP.Epsilon);
            var n = v.Normalized;
            // 不应该抛出异常
            Assert.True(n.X.RawValue >= 0);
            Assert.True(n.Y.RawValue >= 0);
            Assert.True(n.Z.RawValue >= 0);
        }

        [Fact]
        public void LargeVector_Magnitude_ShouldNotOverflow()
        {
            var v = new FPVector3(10000, 10000, 10000);
            FP mag = v.Magnitude;
            Assert.True(mag.RawValue > 0, "Magnitude should be positive");
        }

        #endregion
    }
}
