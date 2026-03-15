// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

using System;
using System.Runtime.InteropServices;
using Lattice.ECS.Core;
using Xunit;

namespace Lattice.Tests.ECS
{
    /// <summary>
    /// ComponentSet 跨平台一致性测试
    /// 验证在不同 CPU 架构上行为一致
    /// </summary>
    public class ComponentSetCrossPlatformTests
    {
        #region 字节序测试

        [Fact]
        public void Serialization_LittleEndian_ShouldBeConsistent()
        {
            var set = ComponentSet.Empty.With(0).With(63).With(64).With(511);

            // 序列化为小端字节
            var bytes = set.ToLittleEndianBytes();

            // 验证大小
            Assert.Equal(64, bytes.Length);

            // 验证特定位置（小端序：低位在前）
            // Set[0] = 0b0000_0001 | 0b1000_0000_0000_0000 = 0x8000_0000_0000_0001
            Assert.Equal(0x01, bytes[0]);  // 第 0 位
            Assert.Equal(0x80, bytes[7]);  // 第 63 位

            // 反序列化
            var restored = ComponentSet.FromLittleEndianBytes(bytes);

            Assert.True(restored.Contains(0));
            Assert.True(restored.Contains(63));
            Assert.True(restored.Contains(64));
            Assert.True(restored.Contains(511));
        }

        [Fact]
        public void Serialization_AllIndices_ShouldRoundTrip()
        {
            for (int index = 0; index < ComponentSet.MAX_COMPONENTS; index += 64)
            {
                var original = ComponentSet.Empty.With(index);
                var bytes = original.ToLittleEndianBytes();
                var restored = ComponentSet.FromLittleEndianBytes(bytes);

                Assert.True(restored.Contains(index), $"Index {index} should round-trip");
            }
        }

        #endregion

        #region 内存布局测试

        [Fact]
        public void MemoryLayout_SizeShouldBe64Bytes()
        {
            Assert.Equal(64, ComponentSet.SIZE);
            Assert.Equal(64, Marshal.SizeOf<ComponentSet>());
        }

        [Fact]
        public void MemoryLayout_BlockCountShouldBe8()
        {
            Assert.Equal(8, ComponentSet.BLOCK_COUNT);
        }

        [Fact]
        public void MemoryLayout_MaxComponentsShouldBe512()
        {
            Assert.Equal(512, ComponentSet.MAX_COMPONENTS);
        }

        #endregion

        #region 平台特性检测测试

        [Fact]
        public void PlatformInfo_ShouldNotThrow()
        {
            var info = ComponentSet.GetPlatformInfo();
            Assert.NotNull(info);
            Assert.Contains("Vector", info);
        }

        #endregion

        #region 边界值一致性测试

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(63)]
        [InlineData(64)]
        [InlineData(127)]
        [InlineData(128)]
        [InlineData(255)]
        [InlineData(256)]
        [InlineData(447)]
        [InlineData(511)]
        public void AllBoundaryIndices_ShouldWork(int index)
        {
            var set = ComponentSet.Empty.With(index);
            Assert.True(set.Contains(index));
        }

        [Fact]
        public void InvalidIndices_ShouldReturnFalse()
        {
            var set = new ComponentSet();

            Assert.False(set.Contains(-1));
            Assert.False(set.Contains(512));
            Assert.False(set.Contains(1000));
            Assert.False(set.Contains(int.MinValue));
            Assert.False(set.Contains(int.MaxValue));
        }

        [Fact]
        public void InvalidIndices_AddShouldNotCrash()
        {
            var set = new ComponentSet();

            // DEBUG 模式下会抛出异常，RELEASE 下静默忽略
#if DEBUG
            Assert.Throws<ArgumentOutOfRangeException>(() => set.Add(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => set.Add(512));
#else
            set.Add(-1);  // 应该被忽略
            set.Add(512); // 应该被忽略
            Assert.True(set.IsEmpty);
#endif
        }

        #endregion

        #region 确定性测试（联机同步关键）

        [Fact]
        public void Determinism_EqualSets_ShouldHaveSameHashCode()
        {
            var set1 = ComponentSet.Empty.With(1).With(5).With(10);
            var set2 = ComponentSet.Empty.With(1).With(5).With(10);

            Assert.Equal(set1.GetHashCode(), set2.GetHashCode());
        }

        [Fact]
        public void Determinism_SetOperations_ShouldBeConsistent()
        {
            var a = ComponentSet.Empty.With(0).With(1).With(2);
            var b = ComponentSet.Empty.With(2).With(3).With(4);

            // 多次执行应该得到相同结果
            for (int i = 0; i < 100; i++)
            {
                var union = a.Union(b);
                var intersection = a.Intersection(b);
                var difference = a.Difference(b);

                Assert.Equal(5, union.CountBits());
                Assert.Equal(1, intersection.CountBits());
                Assert.True(intersection.Contains(2));
                Assert.Equal(2, difference.CountBits());
                Assert.True(difference.Contains(0));
                Assert.True(difference.Contains(1));
            }
        }

        [Fact]
        public void Determinism_CountBits_ShouldBeConsistent()
        {
            var set = ComponentSet.Empty;
            var indices = new[] { 0, 5, 10, 50, 100, 200, 300, 400, 500, 511 };

            foreach (var idx in indices)
            {
                set = set.With(idx);
            }

            // 多次计数应该相同
            for (int i = 0; i < 1000; i++)
            {
                Assert.Equal(indices.Length, set.CountBits());
            }
        }

        #endregion

        #region 集合代数一致性测试

        [Fact]
        public void Algebra_IdempotentLaw()
        {
            var a = ComponentSet.Empty.With(0).With(1);

            // A ∪ A = A
            Assert.Equal(a, a.Union(a));

            // A ∩ A = A
            Assert.Equal(a, a.Intersection(a));
        }

        [Fact]
        public void Algebra_CommutativeLaw()
        {
            var a = ComponentSet.Empty.With(0).With(1);
            var b = ComponentSet.Empty.With(1).With(2);

            // A ∪ B = B ∪ A
            Assert.Equal(a.Union(b), b.Union(a));

            // A ∩ B = B ∩ A
            Assert.Equal(a.Intersection(b), b.Intersection(a));
        }

        [Fact]
        public void Algebra_AssociativeLaw()
        {
            var a = ComponentSet.Empty.With(0);
            var b = ComponentSet.Empty.With(1);
            var c = ComponentSet.Empty.With(2);

            // (A ∪ B) ∪ C = A ∪ (B ∪ C)
            Assert.Equal((a.Union(b)).Union(c), a.Union(b.Union(c)));

            // (A ∩ B) ∩ C = A ∩ (B ∩ C)
            Assert.Equal((a.Intersection(b)).Intersection(c), a.Intersection(b.Intersection(c)));
        }

        [Fact]
        public void Algebra_DeMorganLaw()
        {
            var a = ComponentSet.Empty.With(0).With(1);
            var b = ComponentSet.Empty.With(1).With(2);

            // ~(A ∪ B) = ~A ∩ ~B
            var notUnion = ~(a | b);
            var notAAndNotB = (~a) & (~b);
            Assert.Equal(notUnion, notAAndNotB);

            // ~(A ∩ B) = ~A ∪ ~B
            var notIntersection = ~(a & b);
            var notAOrNotB = (~a) | (~b);
            Assert.Equal(notIntersection, notAOrNotB);
        }

        [Fact]
        public void Algebra_AbsorptionLaw()
        {
            var a = ComponentSet.Empty.With(0).With(1);
            var b = ComponentSet.Empty.With(1).With(2);

            // A ∪ (A ∩ B) = A
            Assert.Equal(a, a.Union(a.Intersection(b)));

            // A ∩ (A ∪ B) = A
            Assert.Equal(a, a.Intersection(a.Union(b)));
        }

        #endregion

        #region 空集和全集测试

        [Fact]
        public void EmptySet_Properties()
        {
            var empty = ComponentSet.Empty;

            Assert.True(empty.IsEmpty);
            Assert.Equal(0, empty.CountBits());
            Assert.Equal(0, empty.GetHashCode()); // 空集哈希码应该为 0
        }

        [Fact]
        public void EmptySet_Identity()
        {
            var a = ComponentSet.Empty.With(0).With(1);
            var empty = ComponentSet.Empty;

            // A ∪ ∅ = A
            Assert.Equal(a, a.Union(empty));

            // A ∩ ∅ = ∅
            Assert.True(a.Intersection(empty).IsEmpty);

            // A - ∅ = A
            Assert.Equal(a, a.Difference(empty));

            // ∅ - A = ∅
            Assert.True(empty.Difference(a).IsEmpty);
        }

        [Fact]
        public void Inverse_DoubleInverse_ShouldBeOriginal()
        {
            var a = ComponentSet.Empty.With(0).With(1).With(2);

            // ~~A = A（理论上，但实际上受限于 512 位）
            // 注意：Inverse 只反转移位，不是数学上的补集
            var doubleInverse = ~(~a);

            // 在 512 位范围内应该相等
            for (int i = 0; i < ComponentSet.MAX_COMPONENTS; i++)
            {
                Assert.Equal(a.Contains(i), doubleInverse.Contains(i));
            }
        }

        #endregion
    }
}
