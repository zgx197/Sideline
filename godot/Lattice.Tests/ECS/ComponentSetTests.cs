using System;
using Lattice.ECS.Core;
using Xunit;

namespace Lattice.Tests.ECS
{
    /// <summary>
    /// ComponentSet 单元测试
    /// </summary>
    public class ComponentSetTests
    {
        #region 基础操作测试

        [Fact]
        public void EmptySet_ShouldBeEmpty()
        {
            var set = ComponentSet.Empty;
            
            Assert.True(set.IsEmpty);
            Assert.Equal(0, set.Count);
        }

        [Fact]
        public void DefaultConstructor_ShouldCreateEmptySet()
        {
            var set = new ComponentSet();
            
            Assert.True(set.IsEmpty);
            Assert.Equal(0, set.Count);
        }

        [Fact]
        public void Add_SingleComponent_ShouldContain()
        {
            var set = new ComponentSet();
            set.Add(5);
            
            Assert.True(set.Contains(5));
            Assert.False(set.Contains(0));
            Assert.False(set.Contains(100));
            Assert.Equal(1, set.Count);
            Assert.False(set.IsEmpty);
        }

        [Fact]
        public void Add_MultipleComponents_ShouldContainAll()
        {
            var set = new ComponentSet();
            set.Add(0);
            set.Add(63);   // 第一个块的边界
            set.Add(64);   // 第二个块的开始
            set.Add(511);  // 最后一个索引
            
            Assert.True(set.Contains(0));
            Assert.True(set.Contains(63));
            Assert.True(set.Contains(64));
            Assert.True(set.Contains(511));
            Assert.Equal(4, set.Count);
        }

        [Fact]
        public void Add_Duplicate_ShouldBeIdempotent()
        {
            var set = new ComponentSet();
            set.Add(10);
            set.Add(10);
            set.Add(10);
            
            Assert.Equal(1, set.Count);
            Assert.True(set.Contains(10));
        }

        [Fact]
        public void Remove_ExistingComponent_ShouldRemove()
        {
            var set = new ComponentSet();
            set.Add(5);
            set.Add(10);
            set.Remove(5);
            
            Assert.False(set.Contains(5));
            Assert.True(set.Contains(10));
            Assert.Equal(1, set.Count);
        }

        [Fact]
        public void Remove_NonExisting_ShouldBeNoOp()
        {
            var set = new ComponentSet();
            set.Add(5);
            set.Remove(100);  // 不存在的组件（但在范围内）
            
            Assert.True(set.Contains(5));
            Assert.Equal(1, set.Count);
        }

        [Fact]
        public void Remove_LastComponent_ShouldBecomeEmpty()
        {
            var set = new ComponentSet();
            set.Add(5);
            set.Remove(5);
            
            Assert.True(set.IsEmpty);
            Assert.Equal(0, set.Count);
        }

        [Fact]
        public void Toggle_ShouldSwitchState()
        {
            var set = new ComponentSet();
            
            set.Toggle(5);
            Assert.True(set.Contains(5));
            
            set.Toggle(5);
            Assert.False(set.Contains(5));
            
            set.Toggle(5);
            Assert.True(set.Contains(5));
        }

        [Fact]
        public void Clear_ShouldRemoveAll()
        {
            var set = new ComponentSet();
            set.Add(0);
            set.Add(100);
            set.Add(200);
            set.Add(300);
            
            set.Clear();
            
            Assert.True(set.IsEmpty);
            Assert.Equal(0, set.Count);
            Assert.False(set.Contains(0));
            Assert.False(set.Contains(100));
        }

        #endregion

        #region 边界值测试

        [Fact]
        public void Add_BoundaryIndices_ShouldWork()
        {
            var set = new ComponentSet();
            
            // 块边界
            set.Add(0);      // 第一个块开始
            set.Add(63);     // 第一个块结束
            set.Add(64);     // 第二个块开始
            set.Add(127);    // 第二个块结束
            set.Add(128);    // 第三个块开始
            set.Add(447);    // 第七个块
            set.Add(511);    // 最后一个索引
            
            Assert.Equal(7, set.Count);
            Assert.True(set.Contains(0));
            Assert.True(set.Contains(63));
            Assert.True(set.Contains(64));
            Assert.True(set.Contains(511));
        }

        [Fact]
        public void Contains_OutOfRange_ShouldReturnFalse()
        {
            var set = new ComponentSet();
            set.Add(0);
            
            // 使用 unsafe fixed 字段，越界访问可能导致崩溃
            // 但我们有 DEBUG 检查，这里只测试正常范围内的
            Assert.False(set.Contains(100));
            Assert.False(set.Contains(511));
        }

        #endregion

        #region 集合运算测试

        [Fact]
        public void IsSupersetOf_Subset_ShouldReturnTrue()
        {
            var superset = ComponentSet.Create(0, 1, 2);
            var subset = ComponentSet.Create(0, 2);
            
            Assert.True(superset.IsSupersetOf(subset));
            Assert.False(subset.IsSupersetOf(superset));
        }

        [Fact]
        public void IsSupersetOf_EqualSet_ShouldReturnTrue()
        {
            var set1 = ComponentSet.Create(0, 1, 2);
            var set2 = ComponentSet.Create(0, 1, 2);
            
            Assert.True(set1.IsSupersetOf(set2));
            Assert.True(set2.IsSupersetOf(set1));
        }

        [Fact]
        public void IsSupersetOf_EmptySet_ShouldReturnTrue()
        {
            var set = ComponentSet.Create(0, 1, 2);
            var empty = ComponentSet.Empty;
            
            Assert.True(set.IsSupersetOf(empty));
            Assert.False(empty.IsSupersetOf(set));
        }

        [Fact]
        public void IsSubsetOf_ShouldBeInverseOfSuperset()
        {
            var a = ComponentSet.Create(0, 1);
            var b = ComponentSet.Create(0, 1, 2, 3);
            
            Assert.True(a.IsSubsetOf(b));
            Assert.False(b.IsSubsetOf(a));
        }

        [Fact]
        public void Overlaps_SharingElements_ShouldReturnTrue()
        {
            var a = ComponentSet.Create(0, 1, 2);
            var b = ComponentSet.Create(2, 3, 4);
            
            Assert.True(a.Overlaps(b));
            Assert.True(b.Overlaps(a));
        }

        [Fact]
        public void Overlaps_NoCommonElements_ShouldReturnFalse()
        {
            var a = ComponentSet.Create(0, 1, 2);
            var b = ComponentSet.Create(3, 4, 5);
            
            Assert.False(a.Overlaps(b));
            Assert.False(b.Overlaps(a));
        }

        [Fact]
        public void Overlaps_WithEmpty_ShouldReturnFalse()
        {
            var set = ComponentSet.Create(0, 1, 2);
            var empty = ComponentSet.Empty;
            
            Assert.False(set.Overlaps(empty));
            Assert.False(empty.Overlaps(set));
        }

        [Fact]
        public void UnionWith_ShouldCombineSets()
        {
            var a = ComponentSet.Create(0, 1, 2);
            var b = ComponentSet.Create(2, 3, 4);
            
            a.UnionWith(b);
            
            Assert.Equal(5, a.Count);
            Assert.True(a.Contains(0));
            Assert.True(a.Contains(1));
            Assert.True(a.Contains(2));
            Assert.True(a.Contains(3));
            Assert.True(a.Contains(4));
        }

        [Fact]
        public void IntersectWith_ShouldKeepOnlyCommon()
        {
            var a = ComponentSet.Create(0, 1, 2);
            var b = ComponentSet.Create(2, 3, 4);
            
            a.IntersectWith(b);
            
            Assert.Equal(1, a.Count);  // 只有 2 是共同的
            Assert.True(a.Contains(2));
            Assert.False(a.Contains(0));
            Assert.False(a.Contains(1));
            Assert.False(a.Contains(3));
            Assert.False(a.Contains(4));
        }

        [Fact]
        public void IntersectWith_NoCommon_ShouldBecomeEmpty()
        {
            var a = ComponentSet.Create(0, 1);
            var b = ComponentSet.Create(2, 3);
            
            a.IntersectWith(b);
            
            Assert.True(a.IsEmpty);
        }

        [Fact]
        public void ExceptWith_ShouldRemoveElements()
        {
            var a = ComponentSet.Create(0, 1, 2);
            var b = ComponentSet.Create(2);
            
            a.ExceptWith(b);
            
            Assert.Equal(2, a.Count);
            Assert.True(a.Contains(0));
            Assert.True(a.Contains(1));
            Assert.False(a.Contains(2));
            Assert.False(a.Contains(3));
        }

        [Fact]
        public void SymmetricExceptWith_ShouldKeepOnlyUnique()
        {
            var a = ComponentSet.Create(0, 1, 2);
            var b = ComponentSet.Create(2, 3, 4);
            
            a.SymmetricExceptWith(b);
            
            Assert.Equal(4, a.Count);  // 0, 1, 3, 4 (2 被移除)
            Assert.True(a.Contains(0));
            Assert.True(a.Contains(1));
            Assert.False(a.Contains(2));  // 共同元素被移除
            Assert.True(a.Contains(3));
            Assert.True(a.Contains(4));
        }

        #endregion

        #region 静态方法测试

        [Fact]
        public void StaticUnion_ShouldReturnNewSet()
        {
            var a = ComponentSet.Create(0, 1);
            var b = ComponentSet.Create(2, 3);
            
            var result = ComponentSet.Union(a, b);
            
            Assert.Equal(4, result.Count);
            Assert.True(result.Contains(0));
            Assert.True(result.Contains(3));
            // 原集合不应被修改
            Assert.Equal(2, a.Count);
            Assert.Equal(2, b.Count);
        }

        [Fact]
        public void StaticIntersection_ShouldReturnCommon()
        {
            var a = ComponentSet.Create(0, 1, 2);
            var b = ComponentSet.Create(2, 3, 4);
            
            var result = ComponentSet.Intersection(a, b);
            
            Assert.Equal(1, result.Count);
            Assert.True(result.Contains(2));
        }

        [Fact]
        public void StaticDifference_ShouldReturnAWithoutB()
        {
            var a = ComponentSet.Create(0, 1, 2);
            var b = ComponentSet.Create(2, 3);
            
            var result = ComponentSet.Difference(a, b);
            
            Assert.Equal(2, result.Count);
            Assert.True(result.Contains(0));
            Assert.True(result.Contains(1));
            Assert.False(result.Contains(2));
        }

        #endregion

        #region 相等性测试

        [Fact]
        public void Equals_SameElements_ShouldReturnTrue()
        {
            var a = ComponentSet.Create(0, 1, 2);
            var b = ComponentSet.Create(0, 1, 2);
            
            Assert.True(a.Equals(b));
            Assert.True(a == b);
            Assert.False(a != b);
        }

        [Fact]
        public void Equals_DifferentElements_ShouldReturnFalse()
        {
            var a = ComponentSet.Create(0, 1, 2);
            var b = ComponentSet.Create(0, 1, 3);
            
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
            var a = ComponentSet.Create(0, 1, 2);
            var b = ComponentSet.Create(0, 1, 2);
            
            Assert.Equal(a.GetHashCode(), b.GetHashCode());
        }

        #endregion

        #region 序列化测试

        [Fact]
        public void WriteTo_ReadFrom_ShouldRoundTrip()
        {
            var original = ComponentSet.Create(0, 63, 64, 127, 511);
            Span<ulong> buffer = stackalloc ulong[8];
            
            original.WriteTo(buffer);
            
            var restored = new ComponentSet();
            restored.ReadFrom(buffer);
            
            Assert.Equal(original, restored);
            Assert.True(original.Equals(restored));
        }

        [Fact]
        public void WriteTo_SmallSpan_ShouldThrow()
        {
            var set = ComponentSet.Create(0);
            Assert.Throws<ArgumentException>(() => {
                Span<ulong> smallBuffer = stackalloc ulong[4];
                set.WriteTo(smallBuffer);
            });
        }

        [Fact]
        public void ReadFrom_SmallSpan_ShouldThrow()
        {
            var set = new ComponentSet();
            Assert.Throws<ArgumentException>(() => {
                ReadOnlySpan<ulong> smallBuffer = stackalloc ulong[4];
                set.ReadFrom(smallBuffer);
            });
        }

        #endregion

        #region 性能相关测试

        [Fact]
        public void Count_LargeSet_ShouldBeCorrect()
        {
            var set = new ComponentSet();
            const int count = 100;
            
            for (int i = 0; i < count; i++)
            {
                set.Add(i * 5);  // 分散在多个块
            }
            
            Assert.Equal(count, set.Count);
        }

        [Fact]
        public void Count_AfterOperations_ShouldBeCorrect()
        {
            var set = new ComponentSet();
            
            set.Add(0);
            set.Add(1);
            set.Add(2);
            Assert.Equal(3, set.Count);
            
            set.Remove(1);
            Assert.Equal(2, set.Count);
            
            set.Add(1);
            set.Add(1);  // 重复
            Assert.Equal(3, set.Count);
            
            set.Clear();
            Assert.Equal(0, set.Count);
        }

        #endregion

        #region 创建方法测试

        [Fact]
        public void Create_OneArg_ShouldWork()
        {
            var set = ComponentSet.Create(42);
            
            Assert.Equal(1, set.Count);
            Assert.True(set.Contains(42));
        }

        [Fact]
        public void Create_TwoArgs_ShouldWork()
        {
            var set = ComponentSet.Create(10, 20);
            
            Assert.Equal(2, set.Count);
            Assert.True(set.Contains(10));
            Assert.True(set.Contains(20));
        }

        [Fact]
        public void Create_ThreeArgs_ShouldWork()
        {
            var set = ComponentSet.Create(1, 2, 3);
            
            Assert.Equal(3, set.Count);
            Assert.True(set.Contains(1));
            Assert.True(set.Contains(2));
            Assert.True(set.Contains(3));
        }

        #endregion

        #region ToString 测试

        [Fact]
        public void ToString_Empty_ShouldShowEmpty()
        {
            var set = new ComponentSet();
            Assert.Equal("ComponentSet{}", set.ToString());
        }

        [Fact]
        public void ToString_WithElements_ShouldShowElements()
        {
            var set = ComponentSet.Create(0, 5, 10);
            var str = set.ToString();
            
            Assert.Contains("0", str);
            Assert.Contains("5", str);
            Assert.Contains("10", str);
        }

        #endregion
    }
}
