// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

using Lattice.ECS.Core;
using Xunit;

namespace Lattice.Tests.ECS.Core
{
    /// <summary>
    /// DeterministicIdMap 单元测试
    /// </summary>
    public class DeterministicIdMapTests
    {
        [Fact]
        public void IdMap_AddAndGet()
        {
            var map = new DeterministicIdMap<int>(256);
            map.Add(1, 100);
            map.Add(2, 200);
            map.Add(3, 300);

            Assert.True(map.TryGetValue(1, out var v1));
            Assert.Equal(100, v1);

            Assert.True(map.TryGetValue(2, out var v2));
            Assert.Equal(200, v2);

            Assert.True(map.TryGetValue(3, out var v3));
            Assert.Equal(300, v3);
        }

        [Fact]
        public void IdMap_GetNonExistent()
        {
            var map = new DeterministicIdMap<int>(256);
            map.Add(1, 100);

            Assert.False(map.TryGetValue(999, out var value));
            Assert.Equal(0, value);
        }

        [Fact]
        public void IdMap_OverwriteValue()
        {
            var map = new DeterministicIdMap<int>(256);
            map.Add(1, 100);
            map.Add(1, 200);

            Assert.True(map.TryGetValue(1, out var value));
            Assert.Equal(200, value);
        }

        [Fact]
        public void IdMap_Has()
        {
            var map = new DeterministicIdMap<int>(256);
            map.Add(1, 100);

            Assert.True(map.Has(1));
            Assert.False(map.Has(999));
        }

        [Fact]
        public void IdMap_Count()
        {
            var map = new DeterministicIdMap<int>(256);
            Assert.Equal(0, map.Count);

            map.Add(1, 100);
            map.Add(2, 200);
            Assert.Equal(2, map.Count);

            map.Add(1, 150); // 覆盖，计数不变
            Assert.Equal(2, map.Count);
        }

        [Fact]
        public void IdMap_Iteration()
        {
            var map = new DeterministicIdMap<int>(256);
            map.Add(1, 100);
            map.Add(2, 200);
            map.Add(3, 300);

            var keys = new System.Collections.Generic.List<int>();
            var values = new System.Collections.Generic.List<int>();

            foreach (var (key, value) in map.GetAll())
            {
                keys.Add(key);
                values.Add(value);
            }

            Assert.Equal(3, keys.Count);
            Assert.Contains(1, keys);
            Assert.Contains(2, keys);
            Assert.Contains(3, keys);

            Assert.Contains(100, values);
            Assert.Contains(200, values);
            Assert.Contains(300, values);
        }

        [Fact]
        public void IdMap_DeterministicOrder()
        {
            // 验证遍历顺序是确定性的
            var map = new DeterministicIdMap<int>(256);
            map.Add(5, 500);
            map.Add(2, 200);
            map.Add(8, 800);
            map.Add(1, 100);

            var order1 = new System.Collections.Generic.List<int>();
            foreach (var (key, _) in map.GetAll())
            {
                order1.Add(key);
            }

            var order2 = new System.Collections.Generic.List<int>();
            foreach (var (key, _) in map.GetAll())
            {
                order2.Add(key);
            }

            Assert.Equal(order1, order2);
        }

        [Fact]
        public void IdMap_StructType()
        {
            var map = new DeterministicIdMap<System.Guid>(256);
            var guid1 = System.Guid.NewGuid();
            var guid2 = System.Guid.NewGuid();

            map.Add(1, guid1);
            map.Add(2, guid2);

            Assert.True(map.TryGetValue(1, out var g1));
            Assert.Equal(guid1, g1);

            Assert.True(map.TryGetValue(2, out var g2));
            Assert.Equal(guid2, g2);
        }

        [Fact]
        public void IdMap_GetById()
        {
            var map = new DeterministicIdMap<int>(256);
            map.Add(5, 42);

            ref var value = ref map.GetById(5);
            Assert.Equal(42, value);

            value = 100; // 修改引用
            Assert.Equal(100, map.GetById(5));
        }

        [Fact]
        public void IdMap_CapacityExpansion()
        {
            var map = new DeterministicIdMap<int>(4); // 小容量
            map.Add(100, 1000); // 需要扩容

            Assert.True(map.TryGetValue(100, out var value));
            Assert.Equal(1000, value);
        }
    }
}
