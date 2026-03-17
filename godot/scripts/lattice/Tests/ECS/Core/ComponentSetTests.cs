// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

using System.Runtime.CompilerServices;
using Lattice.ECS.Core;
using Xunit;

namespace Lattice.Tests.ECS.Core
{
    /// <summary>
    /// ComponentSet 单元测试 - 验证位操作和确定性行为
    /// </summary>
    public class ComponentSetNewTests
    {
        [Fact]
        public void ComponentSet_IsSet_InitialState()
        {
            var set = new ComponentSet();
            for (int i = 0; i < 512; i++)
            {
                Assert.False(set.IsSet(i));
            }
        }

        [Fact]
        public void ComponentSet_AddAndIsSet()
        {
            var set = new ComponentSet();
            set.Add(5);
            set.Add(100);
            set.Add(511);

            Assert.True(set.IsSet(5));
            Assert.True(set.IsSet(100));
            Assert.True(set.IsSet(511));
            Assert.False(set.IsSet(0));
            Assert.False(set.IsSet(50));
        }

        [Fact]
        public void ComponentSet_Remove()
        {
            var set = new ComponentSet();
            set.Add(42);
            Assert.True(set.IsSet(42));

            set.Remove(42);
            Assert.False(set.IsSet(42));
        }

        [Fact]
        public void ComponentSet_AddRemoveMultiple()
        {
            var set = new ComponentSet();

            // 添加多个组件
            for (int i = 0; i < 100; i++)
            {
                set.Add(i);
            }

            // 验证都在
            for (int i = 0; i < 100; i++)
            {
                Assert.True(set.IsSet(i));
            }

            // 移除偶数
            for (int i = 0; i < 100; i += 2)
            {
                set.Remove(i);
            }

            // 验证奇数还在，偶数已移除
            for (int i = 0; i < 100; i++)
            {
                if (i % 2 == 0)
                    Assert.False(set.IsSet(i));
                else
                    Assert.True(set.IsSet(i));
            }
        }

        [Fact]
        public void ComponentSet_Clear()
        {
            var set = new ComponentSet();
            set.Add(10);
            set.Add(20);
            set.Add(30);

            set.Clear();

            Assert.False(set.IsSet(10));
            Assert.False(set.IsSet(20));
            Assert.False(set.IsSet(30));
        }

        [Fact]
        public void ComponentSet_Count()
        {
            var set = new ComponentSet();
            Assert.Equal(0, set.Count);

            set.Add(5);
            set.Add(10);
            set.Add(15);
            Assert.Equal(3, set.Count);

            set.Add(5); // 重复添加
            Assert.Equal(3, set.Count);

            set.Remove(10);
            Assert.Equal(2, set.Count);
        }

        [Fact]
        public void ComponentSet_IsEmpty()
        {
            var set = new ComponentSet();
            Assert.True(set.IsEmpty);

            set.Add(1);
            Assert.False(set.IsEmpty);

            set.Clear();
            Assert.True(set.IsEmpty);
        }

        [Fact]
        public void ComponentSet_IsSupersetOf()
        {
            var set1 = new ComponentSet();
            var set2 = new ComponentSet();

            set1.Add(1);
            set1.Add(2);
            set1.Add(3);
            set1.Add(4);

            set2.Add(2);
            set2.Add(3);

            Assert.True(set1.IsSupersetOf(set2));
            Assert.False(set2.IsSupersetOf(set1));
        }

        [Fact]
        public void ComponentSet_IsSupersetOf_Empty()
        {
            var set1 = new ComponentSet();
            var set2 = new ComponentSet();

            // 空集是任何集的超集
            Assert.True(set1.IsSupersetOf(set2));

            set2.Add(1);
            // 空集不是非空集的超集
            Assert.False(set1.IsSupersetOf(set2));
        }

        [Fact]
        public void ComponentSet_IsSubsetOf()
        {
            var set1 = new ComponentSet();
            var set2 = new ComponentSet();

            set1.Add(1);
            set1.Add(2);

            set2.Add(1);
            set2.Add(2);
            set2.Add(3);

            Assert.True(set1.IsSubsetOf(set2));
            Assert.False(set2.IsSubsetOf(set1));
        }

        [Fact]
        public void ComponentSet_Intersection()
        {
            var set1 = new ComponentSet();
            var set2 = new ComponentSet();

            set1.Add(1);
            set1.Add(2);
            set1.Add(3);

            set2.Add(2);
            set2.Add(3);
            set2.Add(4);

            var intersection = ComponentSet.Intersection(set1, set2);

            Assert.True(intersection.IsSet(2));
            Assert.True(intersection.IsSet(3));
            Assert.False(intersection.IsSet(1));
            Assert.False(intersection.IsSet(4));
        }

        [Fact]
        public void ComponentSet_Union()
        {
            var set1 = new ComponentSet();
            var set2 = new ComponentSet();

            set1.Add(1);
            set1.Add(2);

            set2.Add(2);
            set2.Add(3);

            var union = ComponentSet.Union(set1, set2);

            Assert.True(union.IsSet(1));
            Assert.True(union.IsSet(2));
            Assert.True(union.IsSet(3));
        }

        [Fact]
        public void ComponentSet_Difference()
        {
            var set1 = new ComponentSet();
            var set2 = new ComponentSet();

            set1.Add(1);
            set1.Add(2);
            set1.Add(3);

            set2.Add(2);

            var diff = ComponentSet.Difference(set1, set2);

            Assert.True(diff.IsSet(1));
            Assert.False(diff.IsSet(2));
            Assert.True(diff.IsSet(3));
        }

        [Fact]
        public void ComponentSet_Equality()
        {
            var set1 = new ComponentSet();
            var set2 = new ComponentSet();

            set1.Add(1);
            set1.Add(2);

            set2.Add(1);
            set2.Add(2);

            Assert.Equal(set1, set2);
            Assert.True(set1 == set2);
        }

        [Fact]
        public void ComponentSet_Inequality()
        {
            var set1 = new ComponentSet();
            var set2 = new ComponentSet();

            set1.Add(1);
            set2.Add(2);

            Assert.NotEqual(set1, set2);
            Assert.True(set1 != set2);
        }

        [Fact]
        public void ComponentSet_Contains_Alias()
        {
            var set = new ComponentSet();
            set.Add(42);

            Assert.True(set.Contains(42));
            Assert.True(set.IsSet(42));
        }

        [Fact]
        public void ComponentSet_Toggle()
        {
            var set = new ComponentSet();

            set.Toggle(5);
            Assert.True(set.IsSet(5));

            set.Toggle(5);
            Assert.False(set.IsSet(5));
        }

        [Fact]
        public void ComponentSet_Overlaps()
        {
            var set1 = new ComponentSet();
            var set2 = new ComponentSet();

            set1.Add(1);
            set1.Add(2);

            set2.Add(2);
            set2.Add(3);

            Assert.True(set1.Overlaps(set2));

            set2.Remove(2);
            Assert.False(set1.Overlaps(set2));
        }

        [Fact]
        public void MemoryLayout_ComponentSetSize()
        {
            // ComponentSet 应该是 64 字节 (8 * sizeof(ulong))
            Assert.Equal(64, Unsafe.SizeOf<ComponentSet>());
        }

        [Fact]
        public void MemoryLayout_ComponentSet256Size()
        {
            // ComponentSet256 应该是 32 字节 (4 * sizeof(ulong))
            Assert.Equal(32, Unsafe.SizeOf<ComponentSet256>());
        }

        [Fact]
        public void MemoryLayout_ComponentSet64Size()
        {
            // ComponentSet64 应该是 8 字节 (1 * sizeof(ulong))
            Assert.Equal(8, Unsafe.SizeOf<ComponentSet64>());
        }

        [Fact]
        public void ComponentSet_Create_FactoryMethods()
        {
            var set1 = ComponentSet.Create(1);
            Assert.True(set1.IsSet(1));

            var set2 = ComponentSet.Create(1, 2);
            Assert.True(set2.IsSet(1));
            Assert.True(set2.IsSet(2));

            var set3 = ComponentSet.Create(1, 2, 3);
            Assert.True(set3.IsSet(1));
            Assert.True(set3.IsSet(2));
            Assert.True(set3.IsSet(3));

            var set4 = ComponentSet.Create(1, 2, 3, 4);
            Assert.True(set4.IsSet(1));
            Assert.True(set4.IsSet(2));
            Assert.True(set4.IsSet(3));
            Assert.True(set4.IsSet(4));

            var set5 = ComponentSet.Create(1, 2, 3, 4, 5);
            Assert.True(set5.IsSet(1));
            Assert.True(set5.IsSet(2));
            Assert.True(set5.IsSet(3));
            Assert.True(set5.IsSet(4));
            Assert.True(set5.IsSet(5));
        }

        [Fact]
        public void ComponentSet_Empty_Static()
        {
            var empty = ComponentSet.Empty;
            Assert.Equal(0, empty.Count);
            Assert.True(empty.IsEmpty);
        }
    }
}
