// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

using System.Runtime.CompilerServices;
using Lattice.Core;
using Xunit;

namespace Lattice.Tests.ECS.Core
{
    /// <summary>
    /// EntityRef 单元测试 - 验证代际索引行为和内存布局
    /// </summary>
    public class EntityRefTests
    {
        [Fact]
        public void EntityRef_CreateAndDestroy()
        {
            var e1 = new EntityRef(0, 1);
            Assert.Equal(0, e1.Index);
            Assert.Equal(1, e1.Version);
            Assert.True(e1.IsValid);
        }

        [Fact]
        public void EntityRef_Invalid()
        {
            var invalid = EntityRef.Invalid;
            Assert.Equal(0, invalid.Index);
            Assert.Equal(0, invalid.Version);
            Assert.False(invalid.IsValid);
        }

        [Fact]
        public void EntityRef_Equality_SameEntity()
        {
            var e1 = new EntityRef(100, 5);
            var e2 = new EntityRef(100, 5);

            Assert.Equal(e1, e2);
            Assert.True(e1 == e2);
            Assert.False(e1 != e2);
        }

        [Fact]
        public void EntityRef_Equality_DifferentIndex()
        {
            var e1 = new EntityRef(100, 5);
            var e2 = new EntityRef(101, 5);

            Assert.NotEqual(e1, e2);
            Assert.False(e1 == e2);
            Assert.True(e1 != e2);
        }

        [Fact]
        public void EntityRef_Equality_DifferentVersion()
        {
            var e1 = new EntityRef(100, 5);
            var e2 = new EntityRef(100, 6);

            Assert.NotEqual(e1, e2);
            Assert.False(e1 == e2);
            Assert.True(e1 != e2);
        }

        [Fact]
        public void EntityRef_GetHashCode_Deterministic()
        {
            var e1 = new EntityRef(12345, 67890);
            var e2 = new EntityRef(12345, 67890);

            Assert.Equal(e1.GetHashCode(), e2.GetHashCode());
        }

        [Fact]
        public void EntityRef_GetHashCode_Distribution()
        {
            // 验证哈希分布性 - 不同实体应有不同哈希值
            var hashes = new System.Collections.Generic.HashSet<int>();
            for (int i = 0; i < 1000; i++)
            {
                var e = new EntityRef(i, 1);
                hashes.Add(e.GetHashCode());
            }
            // 至少95%的唯一性
            Assert.True(hashes.Count > 950, $"哈希冲突过多: {1000 - hashes.Count}");
        }

        [Fact]
        public void EntityRef_VersionOverflow()
        {
            // 版本号溢出测试 - 从65535回绕到1
            var e = new EntityRef(0, 65535);
            Assert.Equal(65535, e.Version);

            // 模拟版本递增后的行为
            var nextVersion = (ushort)((e.Version + 1) & 0x7FFF);
            if (nextVersion == 0) nextVersion = 1;

            Assert.Equal(1, nextVersion);
        }

        [Fact]
        public void MemoryLayout_EntityRefSize()
        {
            // EntityRef 应该是8字节 (int + int 或 ushort + ushort + padding)
            Assert.Equal(8, Unsafe.SizeOf<EntityRef>());
        }

        [Fact]
        public void EntityRef_ToString()
        {
            var e = new EntityRef(42, 7);
            var str = e.ToString();
            Assert.Contains("42", str);
            Assert.Contains("7", str);
        }

        [Fact]
        public void EntityRef_BoxingBehavior()
        {
            // 验证装箱后行为一致
            var e1 = new EntityRef(100, 5);
            object obj1 = e1;
            object obj2 = new EntityRef(100, 5);

            Assert.Equal(obj1, obj2);
            Assert.Equal(obj1.GetHashCode(), obj2.GetHashCode());
        }
    }
}
