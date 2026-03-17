// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

using Lattice.Core;
using Lattice.ECS.Core;
using Xunit;

namespace Lattice.Tests.ECS.Core
{
    /// <summary>
    /// 序列化测试 - 验证确定性序列化行为
    /// </summary>
    public class SerializationTests
    {
        [Fact]
        public void EntityRef_MemoryLayout()
        {
            // EntityRef 是值类型，可以直接内存复制
            var original = new EntityRef(12345, 67890);

            // 模拟序列化（内存复制）
            byte[] data = new byte[8];
            unsafe
            {
                fixed (byte* p = data)
                {
                    *(EntityRef*)p = original;
                }
            }

            // 模拟反序列化
            EntityRef restored;
            unsafe
            {
                fixed (byte* p = data)
                {
                    restored = *(EntityRef*)p;
                }
            }

            Assert.Equal(original, restored);
        }

        [Fact]
        public void EntityRef_MemoryLayout_Bytes()
        {
            // 验证内存布局 - 小端序
            var entity = new EntityRef(0x12345678, 0x1234);
            byte[] data = new byte[8];

            unsafe
            {
                fixed (byte* p = data)
                {
                    *(EntityRef*)p = entity;
                }
            }

            // Index 是 int (4 bytes)
            Assert.Equal(0x78, data[0]); // 小端序 - 最低字节在前
            Assert.Equal(0x56, data[1]);
            Assert.Equal(0x34, data[2]);
            Assert.Equal(0x12, data[3]);

            // Version 是 int (4 bytes)
            Assert.Equal(0x34, data[4]);
            Assert.Equal(0x12, data[5]);
            Assert.Equal(0x00, data[6]);
            Assert.Equal(0x00, data[7]);
        }

        [Fact]
        public void ComponentSet_MemoryLayout()
        {
            // ComponentSet 是 64 字节的位图
            var set = new ComponentSet();
            set.Add(0);
            set.Add(63);
            set.Add(64);
            set.Add(511);

            byte[] data = new byte[64];
            unsafe
            {
                fixed (byte* p = data)
                {
                    *(ComponentSet*)p = set;
                }
            }

            // 第一个字节应该设置了最低位 (bit 0)
            Assert.Equal(0x01, data[0]);

            // 第8个字节应该设置了最高位 (bit 63)
            Assert.Equal(0x80, data[7]);

            // 读取回数据
            ComponentSet restored;
            unsafe
            {
                fixed (byte* p = data)
                {
                    restored = *(ComponentSet*)p;
                }
            }

            Assert.True(restored.IsSet(0));
            Assert.True(restored.IsSet(63));
            Assert.True(restored.IsSet(64));
            Assert.True(restored.IsSet(511));
        }

        [Fact]
        public void ComponentSet_DeterministicMemoryLayout()
        {
            // 验证相同数据产生相同的内存布局
            var set1 = new ComponentSet();
            var set2 = new ComponentSet();

            for (int i = 0; i < 512; i += 17)
            {
                set1.Add(i);
                set2.Add(i);
            }

            byte[] data1 = new byte[64];
            byte[] data2 = new byte[64];

            unsafe
            {
                fixed (byte* p1 = data1)
                fixed (byte* p2 = data2)
                {
                    *(ComponentSet*)p1 = set1;
                    *(ComponentSet*)p2 = set2;
                }
            }

            Assert.Equal(data1, data2);
        }

        [Fact]
        public void DeterministicIdMap_StructLayout()
        {
            // 验证 DeterministicIdMap 可以正确存储和检索结构
            var map = new DeterministicIdMap<ComponentSet>(64);

            var set1 = ComponentSet.Create(1, 2, 3);
            var set2 = ComponentSet.Create(4, 5, 6);

            map.Add(1, set1);
            map.Add(2, set2);

            Assert.True(map.TryGetValue(1, out var restored1));
            Assert.Equal(set1, restored1);

            Assert.True(map.TryGetValue(2, out var restored2));
            Assert.Equal(set2, restored2);
        }
    }
}
