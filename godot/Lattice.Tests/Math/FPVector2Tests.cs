using Xunit;
using Lattice.Math;

namespace Lattice.Tests.Math
{
    /// <summary>
    /// FPVector2 单元测试
    /// </summary>
    public class FPVector2Tests
    {
        #region 构造函数和常量

        [Fact]
        public void Constructor_FromFP_ShouldSetComponents()
        {
            var v = new FPVector2(FP._1, FP._2);
            Assert.Equal(FP._1.RawValue, v.X.RawValue);
            Assert.Equal(FP._2.RawValue, v.Y.RawValue);
        }

        [Fact]
        public void Constructor_FromInt_ShouldConvertToFP()
        {
            var v = new FPVector2(3, 4);
            Assert.Equal(3 * FP.ONE, v.X.RawValue);
            Assert.Equal(4 * FP.ONE, v.Y.RawValue);
        }

        [Fact]
        public void Constants_ShouldBeCorrect()
        {
            Assert.Equal(FP._0.RawValue, FPVector2.Zero.X.RawValue);
            Assert.Equal(FP._0.RawValue, FPVector2.Zero.Y.RawValue);

            Assert.Equal(FP._1.RawValue, FPVector2.One.X.RawValue);
            Assert.Equal(FP._1.RawValue, FPVector2.One.Y.RawValue);

            Assert.Equal(FP._1.RawValue, FPVector2.Right.X.RawValue);
            Assert.Equal(FP._0.RawValue, FPVector2.Right.Y.RawValue);

            Assert.Equal(FP._0.RawValue, FPVector2.Up.X.RawValue);
            Assert.Equal(FP._1.RawValue, FPVector2.Up.Y.RawValue);
        }

        #endregion

        #region Swizzle

        [Theory]
        [InlineData(1, 2)]
        [InlineData(3, -4)]
        [InlineData(-5, -6)]
        public void Swizzle_XX_ShouldReturnCorrectValue(int x, int y)
        {
            var v = new FPVector2(x, y);
            var swizzled = v.XX;
            Assert.Equal(v.X.RawValue, swizzled.X.RawValue);
            Assert.Equal(v.X.RawValue, swizzled.Y.RawValue);
        }

        [Theory]
        [InlineData(1, 2)]
        [InlineData(3, -4)]
        public void Swizzle_XY_ShouldReturnSameVector(int x, int y)
        {
            var v = new FPVector2(x, y);
            var swizzled = v.XY;
            Assert.Equal(v.X.RawValue, swizzled.X.RawValue);
            Assert.Equal(v.Y.RawValue, swizzled.Y.RawValue);
        }

        [Theory]
        [InlineData(1, 2)]
        [InlineData(3, -4)]
        public void Swizzle_YX_ShouldSwapComponents(int x, int y)
        {
            var v = new FPVector2(x, y);
            var swizzled = v.YX;
            Assert.Equal(v.Y.RawValue, swizzled.X.RawValue);
            Assert.Equal(v.X.RawValue, swizzled.Y.RawValue);
        }

        [Theory]
        [InlineData(1, 2)]
        [InlineData(3, -4)]
        public void Swizzle_YY_ShouldReturnCorrectValue(int x, int y)
        {
            var v = new FPVector2(x, y);
            var swizzled = v.YY;
            Assert.Equal(v.Y.RawValue, swizzled.X.RawValue);
            Assert.Equal(v.Y.RawValue, swizzled.Y.RawValue);
        }

        [Theory]
        [InlineData(1, 2)]
        [InlineData(3, -4)]
        public void Swizzle_XYO_ShouldReturn3D(int x, int y)
        {
            var v = new FPVector2(x, y);
            var swizzled = v.XYO;
            Assert.Equal(v.X.RawValue, swizzled.X.RawValue);
            Assert.Equal(v.Y.RawValue, swizzled.Y.RawValue);
            Assert.Equal(0L, swizzled.Z.RawValue);
        }

        #endregion

        #region 属性

        [Fact]
        public void SqrMagnitude_ShouldBeSumOfSquares()
        {
            var v = new FPVector2(3, 4);  // (3,4) -> 9+16=25
            FP sqrMag = v.SqrMagnitude;
            Assert.Equal(25 * FP.ONE, sqrMag.RawValue);
        }

        [Fact]
        public void Magnitude_3_4_5_ShouldBe5()
        {
            var v = new FPVector2(3, 4);  // (3,4) 长度应为 5
            FP mag = v.Magnitude;
            Assert.True(FP.Abs(mag - 5).RawValue < 100, $"Magnitude should be ~5, got {mag.RawValue}");
        }

        [Fact]
        public void Normalized_UnitVector_ShouldBeSame()
        {
            var v = new FPVector2(1, 0);
            var n = v.Normalized;
            Assert.True(FP.Abs(n.X - FP._1).RawValue < 100);
            Assert.True(FP.Abs(n.Y).RawValue < 100);
        }

        [Fact]
        public void Normalized_3_4_ShouldBeAbout_0_6_0_8()
        {
            var v = new FPVector2(3, 4);  // 归一化后约 (0.6, 0.8)
            var n = v.Normalized;
            // 0.6 * 65536 ≈ 39322, 0.8 * 65536 ≈ 52429
            Assert.True(FP.Abs(n.X.RawValue - 39322) < 200, $"X should be ~0.6, got {n.X.RawValue}");
            Assert.True(FP.Abs(n.Y.RawValue - 52429) < 200, $"Y should be ~0.8, got {n.Y.RawValue}");
        }

        [Fact]
        public void Normalized_ZeroVector_ShouldReturnZero()
        {
            var v = FPVector2.Zero;
            var n = v.Normalized;
            Assert.Equal(0L, n.X.RawValue);
            Assert.Equal(0L, n.Y.RawValue);
        }

        #endregion

        #region 运算符

        [Fact]
        public void Operator_Add_ShouldAddComponents()
        {
            var a = new FPVector2(1, 2);
            var b = new FPVector2(3, 4);
            var c = a + b;
            Assert.Equal(4 * FP.ONE, c.X.RawValue);
            Assert.Equal(6 * FP.ONE, c.Y.RawValue);
        }

        [Fact]
        public void Operator_Sub_ShouldSubtractComponents()
        {
            var a = new FPVector2(5, 6);
            var b = new FPVector2(2, 3);
            var c = a - b;
            Assert.Equal(3 * FP.ONE, c.X.RawValue);
            Assert.Equal(3 * FP.ONE, c.Y.RawValue);
        }

        [Fact]
        public void Operator_Negate_ShouldNegateComponents()
        {
            var v = new FPVector2(1, -2);
            var n = -v;
            Assert.Equal(-FP._1.RawValue, n.X.RawValue);
            Assert.Equal(2 * FP.ONE, n.Y.RawValue);
        }

        [Fact]
        public void Operator_MultiplyByScalar_ShouldScale()
        {
            var v = new FPVector2(2, 3);
            var s = FP._2;
            var r = v * s;
            Assert.Equal(4 * FP.ONE, r.X.RawValue);
            Assert.Equal(6 * FP.ONE, r.Y.RawValue);
        }

        [Fact]
        public void Operator_MultiplyScalarByVector_ShouldScale()
        {
            var v = new FPVector2(2, 3);
            var s = FP._2;
            var r = s * v;
            Assert.Equal(4 * FP.ONE, r.X.RawValue);
            Assert.Equal(6 * FP.ONE, r.Y.RawValue);
        }

        [Fact]
        public void Operator_DivideByScalar_ShouldScaleDown()
        {
            var v = new FPVector2(6, 8);
            var s = FP._2;
            var r = v / s;
            Assert.Equal(3 * FP.ONE, r.X.RawValue);
            Assert.Equal(4 * FP.ONE, r.Y.RawValue);
        }

        [Fact]
        public void Operator_Equal_ShouldCompareComponents()
        {
            var a = new FPVector2(1, 2);
            var b = new FPVector2(1, 2);
            var c = new FPVector2(1, 3);
            Assert.True(a == b);
            Assert.False(a == c);
        }

        #endregion

        #region 静态方法

        [Fact]
        public void Dot_PerpendicularVectors_ShouldBeZero()
        {
            var a = FPVector2.Right;  // (1,0)
            var b = FPVector2.Up;     // (0,1)
            FP dot = FPVector2.Dot(a, b);
            Assert.True(FP.Abs(dot).RawValue < 10, $"Dot of perpendicular vectors should be ~0, got {dot.RawValue}");
        }

        [Fact]
        public void Dot_SameDirection_ShouldBeProductOfMagnitudes()
        {
            var a = new FPVector2(2, 0);
            var b = new FPVector2(3, 0);
            FP dot = FPVector2.Dot(a, b);
            Assert.Equal(6 * FP.ONE, dot.RawValue);  // 2*3=6
        }

        [Fact]
        public void Cross_RightUp_ShouldBePositive()
        {
            var a = FPVector2.Right;  // (1,0)
            var b = FPVector2.Up;     // (0,1)
            FP cross = FPVector2.Cross(a, b);
            // Cross((1,0), (0,1)) = 1*1 - 0*0 = 1
            Assert.True(cross.RawValue > 0, "Cross of Right x Up should be positive (counterclockwise)");
        }

        [Fact]
        public void Distance_3_4_5_Triangle()
        {
            var a = new FPVector2(0, 0);
            var b = new FPVector2(3, 4);
            FP dist = FPVector2.Distance(a, b);
            Assert.True(FP.Abs(dist - 5).RawValue < 100, $"Distance should be ~5, got {dist.RawValue}");
        }

        [Fact]
        public void DistanceSquared_ShouldBeFasterThanDistance()
        {
            var a = new FPVector2(0, 0);
            var b = new FPVector2(3, 4);
            FP distSqr = FPVector2.DistanceSquared(a, b);
            Assert.Equal(25 * FP.ONE, distSqr.RawValue);  // 3²+4²=25
        }

        [Fact]
        public void Lerp_T0_ShouldBeStart()
        {
            var a = new FPVector2(1, 2);
            var b = new FPVector2(3, 4);
            var r = FPVector2.Lerp(a, b, FP._0);
            Assert.Equal(a.X.RawValue, r.X.RawValue);
            Assert.Equal(a.Y.RawValue, r.Y.RawValue);
        }

        [Fact]
        public void Lerp_T1_ShouldBeEnd()
        {
            var a = new FPVector2(1, 2);
            var b = new FPVector2(3, 4);
            var r = FPVector2.Lerp(a, b, FP._1);
            Assert.Equal(b.X.RawValue, r.X.RawValue);
            Assert.Equal(b.Y.RawValue, r.Y.RawValue);
        }

        [Fact]
        public void Lerp_T0_5_ShouldBeMiddle()
        {
            var a = new FPVector2(0, 0);
            var b = new FPVector2(2, 4);
            var r = FPVector2.Lerp(a, b, FP._0_50);
            Assert.Equal(FP._1.RawValue, r.X.RawValue);
            Assert.Equal(2 * FP.ONE, r.Y.RawValue);
        }

        [Fact]
        public void ClampMagnitude_LongVector_ShouldClamp()
        {
            var v = new FPVector2(10, 0);
            var clamped = FPVector2.ClampMagnitude(v, FP._5);
            Assert.True(FP.Abs(clamped.Magnitude - FP._5).RawValue < 100);
        }

        [Fact]
        public void ClampMagnitude_ShortVector_ShouldNotChange()
        {
            var v = new FPVector2(3, 0);
            var clamped = FPVector2.ClampMagnitude(v, FP._5);
            Assert.Equal(3 * FP.ONE, clamped.X.RawValue);
        }

        [Fact]
        public void Reflect_45Degrees_ShouldReflectCorrectly()
        {
            var direction = new FPVector2(1, -1).Normalized;  // 向下 45 度
            var normal = FPVector2.Up;  // 向上法线
            var reflected = FPVector2.Reflect(direction, normal);
            // 反射后应该是向上 45 度
            Assert.True(reflected.Y.RawValue > 0, "Reflected vector should point up");
        }

        [Fact]
        public void Project_OnXAxis_ShouldGiveXComponent()
        {
            var v = new FPVector2(3, 4);
            var axis = FPVector2.Right;
            var proj = FPVector2.Project(v, axis);
            Assert.Equal(3 * FP.ONE, proj.X.RawValue);
            Assert.Equal(0L, proj.Y.RawValue);
        }

        [Fact]
        public void Perpendicular_ShouldRotate90Degrees()
        {
            var v = FPVector2.Right;
            var p = FPVector2.Perpendicular(v);
            // 逆时针旋转 90 度，Right -> Up
            Assert.True(p.Y.RawValue > 0, "Perpendicular should point up");
            Assert.True(FP.Abs(p.X).RawValue < 100, "Perpendicular X should be ~0");
        }

        [Fact]
        public void Rotate_90Degrees_ShouldRotateCorrectly()
        {
            var v = FPVector2.Right;
            var rotated = FPVector2.Rotate(v, FP.PiHalf);
            Assert.True(FP.Abs(rotated.X).RawValue < 1000, $"X should be ~0, got {rotated.X.RawValue}");
            Assert.True(rotated.Y.RawValue > 0, "Y should be positive");
        }

        [Fact]
        public void Scale_ShouldMultiplyComponents()
        {
            var a = new FPVector2(2, 3);
            var b = new FPVector2(4, 5);
            var r = FPVector2.Scale(a, b);
            Assert.Equal(8 * FP.ONE, r.X.RawValue);
            Assert.Equal(15 * FP.ONE, r.Y.RawValue);
        }

        [Fact]
        public void Min_ShouldTakeComponentWiseMin()
        {
            var a = new FPVector2(1, 5);
            var b = new FPVector2(3, 2);
            var r = FPVector2.Min(a, b);
            Assert.Equal(FP._1.RawValue, r.X.RawValue);
            Assert.Equal(2 * FP.ONE, r.Y.RawValue);
        }

        [Fact]
        public void Max_ShouldTakeComponentWiseMax()
        {
            var a = new FPVector2(1, 5);
            var b = new FPVector2(3, 2);
            var r = FPVector2.Max(a, b);
            Assert.Equal(3 * FP.ONE, r.X.RawValue);
            Assert.Equal(5 * FP.ONE, r.Y.RawValue);
        }

        #endregion

        #region 确定性测试

        [Fact]
        public void Normalized_ShouldBeDeterministic()
        {
            var v = new FPVector2(3, 4);
            var n1 = v.Normalized;
            var n2 = v.Normalized;
            Assert.Equal(n1.X.RawValue, n2.X.RawValue);
            Assert.Equal(n1.Y.RawValue, n2.Y.RawValue);
        }

        [Fact]
        public void Dot_ShouldBeDeterministic()
        {
            var a = new FPVector2(1, 2);
            var b = new FPVector2(3, 4);
            var d1 = FPVector2.Dot(a, b);
            var d2 = FPVector2.Dot(a, b);
            Assert.Equal(d1.RawValue, d2.RawValue);
        }

        #endregion

        #region 边缘情况

        [Fact]
        public void VerySmallVector_Normalized_ShouldHandleGracefully()
        {
            var v = new FPVector2(FP.Epsilon, FP.Epsilon);
            var n = v.Normalized;
            // 不应该抛出异常或返回 NaN
            Assert.True(n.X.RawValue >= 0);
            Assert.True(n.Y.RawValue >= 0);
        }

        [Fact]
        public void LargeVector_Magnitude_ShouldNotOverflow()
        {
            var v = new FPVector2(10000, 10000);
            FP mag = v.Magnitude;
            Assert.True(mag.RawValue > 0, "Magnitude should be positive");
        }

        #endregion
    }
}
