using System;
using Lattice.ECS.Core;
using Xunit;

namespace Lattice.Tests.ECS
{
    /// <summary>
    /// ComponentSet 单元测试 - 适配 FrameSync 风格重构后的 API
    /// </summary>
    public class ComponentSetTests
    {
        // 测试用组件类型
        private struct TestComp1 : IComponent { }
        private struct TestComp2 : IComponent { }
        private struct TestComp3 : IComponent { }
        private struct TestComp4 : IComponent { }
        #region 基础操作测试

        [Fact]
        public void EmptySet_ShouldBeEmpty()
        {
            var set = ComponentSet.Empty;

            Assert.True(set.IsEmpty);
            Assert.Equal(0, set.CountBits());
        }

        [Fact]
        public void DefaultConstructor_ShouldCreateEmptySet()
        {
            var set = new ComponentSet();

            Assert.True(set.IsEmpty);
            Assert.Equal(0, set.CountBits());
        }

        [Fact]
        public void Add_SingleComponent_ShouldContain()
        {
            var set = ComponentSet.Empty;
            set.Add(5);

            Assert.True(set.Contains(5));
            Assert.False(set.Contains(0));
            Assert.False(set.Contains(100));
            Assert.Equal(1, set.CountBits());
            Assert.False(set.IsEmpty);
        }

        [Fact]
        public void Add_MultipleComponents_ShouldContainAll()
        {
            var set = ComponentSet.Empty;
            set.Add(0);
            set.Add(63);   // 第一个 ulong 的边界
            set.Add(64);   // 第二个 ulong 的开始
            set.Add(511);  // 最后一个索引

            Assert.True(set.Contains(0));
            Assert.True(set.Contains(63));
            Assert.True(set.Contains(64));
            Assert.True(set.Contains(511));
            Assert.Equal(4, set.CountBits());
        }

        [Fact]
        public void Add_Duplicate_ShouldBeIdempotent()
        {
            var set = ComponentSet.Empty;
            set.Add(10);
            set.Add(10);
            set.Add(10);

            Assert.Equal(1, set.CountBits());
            Assert.True(set.Contains(10));
        }

        [Fact]
        public void Remove_ExistingComponent_ShouldRemove()
        {
            var set = ComponentSet.Empty;
            set.Add(5);
            set.Add(10);
            set.Remove(5);

            Assert.False(set.Contains(5));
            Assert.True(set.Contains(10));
            Assert.Equal(1, set.CountBits());
        }

        [Fact]
        public void Remove_NonExisting_ShouldBeNoOp()
        {
            var set = ComponentSet.Empty;
            set.Add(5);
            set.Remove(100);  // 不存在的组件

            Assert.True(set.Contains(5));
            Assert.Equal(1, set.CountBits());
        }

        [Fact]
        public void Remove_LastComponent_ShouldBecomeEmpty()
        {
            var set = ComponentSet.Empty;
            set.Add(5);
            set.Remove(5);

            Assert.True(set.IsEmpty);
            Assert.Equal(0, set.CountBits());
        }

        [Fact]
        public void Union_ShouldCombineSets()
        {
            var a = ComponentSet.Create<TestComp1, TestComp2>();
            var b = ComponentSet.Create<TestComp3, TestComp4>();

            var result = a.Union(b);

            Assert.Equal(4, result.CountBits());
            Assert.True(result.Contains<TestComp1>());
            Assert.True(result.Contains<TestComp4>());
            // 原集合不应被修改（值语义）
            Assert.Equal(2, a.CountBits());
            Assert.Equal(2, b.CountBits());
        }

        [Fact]
        public void Intersection_ShouldReturnCommon()
        {
            var a = new ComponentSet();
            a.Add(0);
            a.Add(1);
            a.Add(2);
            var b = new ComponentSet();
            b.Add(2);
            b.Add(3);
            b.Add(4);

            var result = a.Intersection(b);

            Assert.Equal(1, result.CountBits());
            Assert.True(result.Contains(2));
            Assert.False(result.Contains(0));
            Assert.False(result.Contains(3));
        }

        [Fact]
        public void Difference_ShouldReturnAWithoutB()
        {
            var a = new ComponentSet();
            a.Add(0);
            a.Add(1);
            a.Add(2);
            var b = new ComponentSet();
            b.Add(2);
            b.Add(3);

            var result = a.Difference(b);

            Assert.Equal(2, result.CountBits());
            Assert.True(result.Contains(0));
            Assert.True(result.Contains(1));
            Assert.False(result.Contains(2));
            Assert.False(result.Contains(3));
        }

        [Fact]
        public void Inverse_ShouldFlipAllBits()
        {
            var set = new ComponentSet();
            set.Add(0);
            set.Add(1);

            var result = set.Inverse();

            Assert.False(result.Contains(0));
            Assert.False(result.Contains(1));
            Assert.True(result.Contains(2));  // 原来不在的现在在了
            Assert.True(result.Contains(511));
        }

        #endregion

        #region 边界值测试

        [Fact]
        public void Add_BoundaryIndices_ShouldWork()
        {
            var set = ComponentSet.Empty;

            // ulong 边界
            set.Add(0);      // 第一个 ulong 开始
            set.Add(63);     // 第一个 ulong 结束
            set.Add(64);     // 第二个 ulong 开始
            set.Add(127);    // 第二个 ulong 结束
            set.Add(128);    // 第三个 ulong 开始
            set.Add(447);    // 第七个 ulong
            set.Add(511);    // 最后一个索引

            Assert.Equal(7, set.CountBits());
            Assert.True(set.Contains(0));
            Assert.True(set.Contains(63));
            Assert.True(set.Contains(64));
            Assert.True(set.Contains(511));
        }

        [Fact]
        public void Contains_OutOfRange_ShouldReturnFalse()
        {
            var set = ComponentSet.Create<TestComp1>();

            // 使用 fixed buffer，越界访问是安全的（返回 false）
            Assert.False(set.Contains(1000));
        }

        #endregion

        #region 集合运算测试

        [Fact]
        public void IsSupersetOf_Subset_ShouldReturnTrue()
        {
            var superset = new ComponentSet();
            superset.Add(0);
            superset.Add(1);
            superset.Add(2);
            var subset = new ComponentSet();
            subset.Add(0);
            subset.Add(2);

            Assert.True(superset.IsSupersetOf(subset));
            Assert.False(subset.IsSupersetOf(superset));
        }

        [Fact]
        public void IsSupersetOf_EqualSet_ShouldReturnTrue()
        {
            var set1 = new ComponentSet();
            set1.Add(0);
            set1.Add(1);
            set1.Add(2);
            var set2 = new ComponentSet();
            set2.Add(0);
            set2.Add(1);
            set2.Add(2);

            Assert.True(set1.IsSupersetOf(set2));
            Assert.True(set2.IsSupersetOf(set1));
        }

        [Fact]
        public void IsSupersetOf_EmptySet_ShouldReturnTrue()
        {
            var set = new ComponentSet();
            set.Add(0);
            set.Add(1);
            set.Add(2);
            var empty = ComponentSet.Empty;

            Assert.True(set.IsSupersetOf(empty));
            // 空集不是任何非空集的超集
            Assert.False(empty.IsSupersetOf(set));
        }

        [Fact]
        public void Intersects_SharingElements_ShouldReturnTrue()
        {
            var a = new ComponentSet();
            a.Add(0);
            a.Add(1);
            a.Add(2);
            var b = new ComponentSet();
            b.Add(2);
            b.Add(3);
            b.Add(4);

            Assert.True(a.Intersects(b));
            Assert.True(b.Intersects(a));
        }

        [Fact]
        public void Intersects_NoCommonElements_ShouldReturnFalse()
        {
            var a = new ComponentSet();
            a.Add(0);
            a.Add(1);
            a.Add(2);
            var b = new ComponentSet();
            b.Add(3);
            b.Add(4);
            b.Add(5);

            Assert.False(a.Intersects(b));
            Assert.False(b.Intersects(a));
        }

        [Fact]
        public void Intersects_WithEmpty_ShouldReturnFalse()
        {
            var set = new ComponentSet();
            set.Add(0);
            set.Add(1);
            set.Add(2);
            var empty = ComponentSet.Empty;

            Assert.False(set.Intersects(empty));
            Assert.False(empty.Intersects(set));
        }

        [Fact]
        public void Intersection_NoCommon_ShouldBecomeEmpty()
        {
            var a = new ComponentSet();
            a.Add(0);
            a.Add(1);
            var b = new ComponentSet();
            b.Add(2);
            b.Add(3);

            var result = a.Intersection(b);

            Assert.True(result.IsEmpty);
        }

        [Fact]
        public void Union_WithEmpty_ShouldReturnOriginal()
        {
            var set = ComponentSet.Empty.With(0).With(1).With(2);
            var empty = ComponentSet.Empty;

            var result = set.Union(empty);

            Assert.Equal(3, result.CountBits());
            Assert.True(result.Contains(0));
            Assert.True(result.Contains(2));
        }

        #endregion

        #region 运算符测试

        [Fact]
        public void Operator_Or_ShouldUnion()
        {
            var a = ComponentSet.Empty.With(0).With(1);
            var b = ComponentSet.Empty.With(2).With(3);

            var result = a | b;

            Assert.Equal(4, result.CountBits());
        }

        [Fact]
        public void Operator_And_ShouldIntersect()
        {
            var a = ComponentSet.Empty.With(0).With(1).With(2);
            var b = ComponentSet.Empty.With(2).With(3).With(4);

            var result = a & b;

            Assert.Equal(1, result.CountBits());
            Assert.True(result.Contains(2));
        }

        [Fact]
        public void Operator_Minus_ShouldDifference()
        {
            var a = ComponentSet.Empty.With(0).With(1).With(2);
            var b = ComponentSet.Empty.With(2);

            var result = a - b;

            Assert.Equal(2, result.CountBits());
            Assert.True(result.Contains(0));
            Assert.True(result.Contains(1));
            Assert.False(result.Contains(2));
        }

        [Fact]
        public void Operator_Not_ShouldInverse()
        {
            var set = ComponentSet.Empty.With(0).With(1);

            var result = ~set;

            Assert.False(result.Contains(0));
            Assert.False(result.Contains(1));
            Assert.True(result.Contains(2));
        }

        [Fact]
        public void Operator_Equality_SameElements_ShouldBeEqual()
        {
            var a = ComponentSet.Empty.With(0).With(1).With(2);
            var b = ComponentSet.Empty.With(0).With(1).With(2);

            Assert.True(a == b);
            Assert.False(a != b);
        }

        [Fact]
        public void Operator_Equality_DifferentElements_ShouldNotBeEqual()
        {
            var a = ComponentSet.Empty.With(0).With(1).With(2);
            var b = ComponentSet.Empty.With(0).With(1).With(3);

            Assert.False(a == b);
            Assert.True(a != b);
        }

        #endregion

        #region 相等性测试

        [Fact]
        public void Equals_SameElements_ShouldReturnTrue()
        {
            var a = ComponentSet.Empty.With(0).With(1).With(2);
            var b = ComponentSet.Empty.With(0).With(1).With(2);

            Assert.True(a.Equals(b));
            Assert.True(a == b);
            Assert.False(a != b);
        }

        [Fact]
        public void Equals_DifferentElements_ShouldReturnFalse()
        {
            var a = ComponentSet.Empty.With(0).With(1).With(2);
            var b = ComponentSet.Empty.With(0).With(1).With(3);

            Assert.False(a.Equals(b));
            Assert.False(a == b);
            Assert.True(a != b);
        }

        [Fact]
        public void Equals_EmptySets_ShouldReturnTrue()
        {
            var a = new ComponentSet();
            var b = ComponentSet.Empty;

            Assert.True(a.Equals(b));
            Assert.True(a == b);
        }

        [Fact]
        public void GetHashCode_SameElements_ShouldBeEqual()
        {
            var a = ComponentSet.Empty.With(0).With(1).With(2);
            var b = ComponentSet.Empty.With(0).With(1).With(2);

            Assert.Equal(a.GetHashCode(), b.GetHashCode());
        }

        #endregion

        #region 性能相关测试

        [Fact]
        public void CountBits_LargeSet_ShouldBeCorrect()
        {
            var set = ComponentSet.Empty;
            const int count = 100;

            for (int i = 0; i < count; i++)
            {
                set = set.With(i * 5);  // 分散在多个 ulong
            }

            Assert.Equal(count, set.CountBits());
        }

        [Fact]
        public void CountBits_AfterOperations_ShouldBeCorrect()
        {
            var set = ComponentSet.Empty;

            set = set.With(0);
            set = set.With(1);
            set = set.With(2);
            Assert.Equal(3, set.CountBits());

            set = set.Without(1);
            Assert.Equal(2, set.CountBits());

            set = set.With(1);
            set = set.With(1);  // 重复（值语义，每次都是新对象）
            Assert.Equal(3, set.CountBits());  // 仍然 3，因为值已存在
        }

        #endregion

        #region 链式调用测试

        [Fact]
        public void ChainedAdd_ShouldWork()
        {
            var set = ComponentSet.Empty.With(10).With(20).With(30);

            Assert.Equal(3, set.CountBits());
            Assert.True(set.Contains(10));
            Assert.True(set.Contains(20));
            Assert.True(set.Contains(30));
        }

        [Fact]
        public void ChainedOperations_ShouldWork()
        {
            var set = ComponentSet.Empty.With(1).With(2).With(3).Without(2);

            Assert.Equal(2, set.CountBits());
            Assert.True(set.Contains(1));
            Assert.False(set.Contains(2));
            Assert.True(set.Contains(3));
        }

        #endregion

        #region ToString 测试

        [Fact]
        public void ToString_ShouldContainCount()
        {
            var set = new ComponentSet();
            Assert.Contains("0", set.ToString());  // 包含计数

            set = set.With(0).With(5).With(10);
            var str = set.ToString();
            Assert.Contains("3", str);  // 包含计数 3
        }

        #endregion
    }
}
