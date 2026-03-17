// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

using System;
using System.Runtime.InteropServices;
using Lattice.Core;
using Lattice.ECS.Core;
using Lattice.Math;
using Xunit;

namespace Lattice.Tests
{
    /// <summary>
    /// 确定性验证测试
    /// 
    /// 这些测试验证跨平台、跨运行时的行为一致性
    /// </summary>
    public class DeterminismTests
    {
        #region EntityRef 确定性测试

        [Fact]
        public void EntityRef_HashCode_Deterministic()
        {
            // 给定相同的输入，哈希码必须相同
            var e1 = new EntityRef(123, 456);
            var e2 = new EntityRef(123, 456);

            Assert.Equal(e1.GetHashCode(), e2.GetHashCode());

            // 期望值应该是确定的
            int expectedHash = DeterministicHash.GetHashCode(e1);
            Assert.Equal(expectedHash, e1.GetHashCode());
        }

        [Theory]
        [InlineData(0, 0)]
        [InlineData(1, 1)]
        [InlineData(100, 200)]
        [InlineData(999999, 1234567)]
        public void EntityRef_HashCode_ConsistentAcrossValues(int index, int version)
        {
            var entity = new EntityRef(index, version);
            var hash1 = entity.GetHashCode();
            var hash2 = entity.GetHashCode();

            Assert.Equal(hash1, hash2);
        }

        [Fact]
        public void EntityRef_RawValue_Deterministic()
        {
            // Raw 值必须可预测
            var entity = new EntityRef(100, 200);
            ulong expectedRaw = (ulong)(uint)100 | ((ulong)(uint)200 << 32);
            
            Assert.Equal(expectedRaw, entity.Raw);
        }

        [Fact]
        public void EntityRef_Equality_Deterministic()
        {
            var e1 = new EntityRef(123, 456);
            var e2 = new EntityRef(123, 456);
            var e3 = new EntityRef(123, 457); // 不同版本

            Assert.True(e1 == e2);
            Assert.True(e1.Equals(e2));
            Assert.False(e1 == e3);
        }

        #endregion

        #region ComponentSet 确定性测试

        [Fact]
        public void ComponentSetNet8_Operations_Deterministic()
        {
            var set1 = new ComponentSetNet8();
            var set2 = new ComponentSetNet8();

            // 相同的操作序列必须产生相同结果
            set1.Add(5);
            set1.Add(10);
            set1.Add(100);

            set2.Add(5);
            set2.Add(10);
            set2.Add(100);

            Assert.True(set1.Equals(set2));
            Assert.Equal(set1.GetHashCode(), set2.GetHashCode());

            // 检查位是否正确设置
            Assert.True(set1.IsSet(5));
            Assert.True(set1.IsSet(10));
            Assert.True(set1.IsSet(100));
            Assert.False(set1.IsSet(50));
        }

        [Fact]
        public void ComponentSetNet8_Superset_Deterministic()
        {
            var set1 = new ComponentSetNet8();
            set1.Add(1);
            set1.Add(2);
            set1.Add(3);

            var set2 = new ComponentSetNet8();
            set2.Add(1);
            set2.Add(2);

            // set1 是 set2 的超集
            Assert.True(set1.IsSupersetOf(ref set2));
            Assert.False(set2.IsSupersetOf(ref set1));
        }

        [Fact]
        public void ComponentSetNet8_Overlaps_Deterministic()
        {
            var set1 = new ComponentSetNet8();
            set1.Add(1);
            set1.Add(2);

            var set2 = new ComponentSetNet8();
            set2.Add(2);
            set2.Add(3);

            // 有交集
            Assert.True(set1.Overlaps(ref set2));
        }

        [Fact]
        public void ComponentSetNet8_Union_Deterministic()
        {
            var set1 = new ComponentSetNet8();
            set1.Add(1);
            set1.Add(2);

            var set2 = new ComponentSetNet8();
            set2.Add(3);
            set2.Add(4);

            set1.UnionWith(ref set2);

            Assert.True(set1.IsSet(1));
            Assert.True(set1.IsSet(2));
            Assert.True(set1.IsSet(3));
            Assert.True(set1.IsSet(4));
        }

        [Fact]
        public void ComponentSetNet8_Clear_Deterministic()
        {
            var set = new ComponentSetNet8();
            set.Add(1);
            set.Add(2);

            Assert.False(set.IsEmpty);

            set.Clear();

            Assert.True(set.IsEmpty);
            Assert.False(set.IsSet(1));
        }

        #endregion

        #region 序列化确定性测试

        [Fact]
        public void Serialization_FP_Deterministic()
        {
            // FP 序列化必须跨平台一致
            FP fp = 42;  // 使用隐式转换
            
            Span<byte> buffer = new byte[8];
            int offset = 0;
            
            DeterministicSerialization.WriteFP(buffer, fp, ref offset);

            // 验证序列化结果
            Assert.Equal(8, offset);
            
            // 反序列化
            offset = 0;
            var fp2 = DeterministicSerialization.ReadFP(buffer, ref offset);
            
            Assert.Equal(fp.RawValue, fp2.RawValue);
        }

        [Fact]
        public void Serialization_Int32_Deterministic()
        {
            Span<byte> buffer = new byte[4];
            int offset = 0;
            
            DeterministicSerialization.WriteInt32(buffer, 0x12345678, ref offset);

            // 小端序验证
            Assert.Equal(0x78, buffer[0]);
            Assert.Equal(0x56, buffer[1]);
            Assert.Equal(0x34, buffer[2]);
            Assert.Equal(0x12, buffer[3]);

            // 反序列化
            offset = 0;
            var value = DeterministicSerialization.ReadInt32(buffer, ref offset);
            
            Assert.Equal(0x12345678, value);
        }

        #endregion

        #region 哈希算法确定性测试

        [Theory]
        [InlineData(new byte[] { 1, 2, 3, 4, 5 })]
        [InlineData(new byte[] { 0xFF, 0xEE, 0xDD, 0xCC })]
        [InlineData(new byte[] { 0 })]
        public void DeterministicHash_Fnv1a32_Deterministic(byte[] data)
        {
            var hash1 = DeterministicHash.Fnv1a32(data);
            var hash2 = DeterministicHash.Fnv1a32(data);

            Assert.Equal(hash1, hash2);
        }

        [Fact]
        public void DeterministicHash_Combine_Deterministic()
        {
            int h1 = 123;
            int h2 = 456;
            int h3 = 789;

            var combined1 = DeterministicHash.Combine(h1, h2, h3);
            var combined2 = DeterministicHash.Combine(h1, h2, h3);

            Assert.Equal(combined1, combined2);
        }

        #endregion

        #region SIMD 操作确定性测试

        [Fact]
        public void ComponentSetNet8_SIMD_Deterministic()
        {
            // SIMD 和标量操作结果必须一致
            var set1 = new ComponentSetNet8();
            set1.Add(0);
            set1.Add(63);
            set1.Add(64);
            set1.Add(127);
            set1.Add(128);
            set1.Add(511);

            var set2 = new ComponentSetNet8();
            set2.Add(0);
            set2.Add(63);
            set2.Add(64);

            // SIMD 超集检查
            bool simdResult = set1.IsSupersetOf(ref set2);

            // 手动检查（标量）
            bool scalarResult = true;
            for (int i = 0; i < 512; i++)
            {
                if (set2.IsSet(i) && !set1.IsSet(i))
                {
                    scalarResult = false;
                    break;
                }
            }

            Assert.Equal(scalarResult, simdResult);
        }

        #endregion

        #region 跨平台假设验证

        [Fact]
        public void Platform_Assumptions()
        {
            // 验证我们依赖的平台特性
            Assert.True(BitConverter.IsLittleEndian, 
                "代码假设小端序平台。如果是大端序，需要修改序列化代码");
            
            // 验证指针大小
            Assert.True(IntPtr.Size == 8, 
                "代码针对 64 位平台优化");
        }

        [Fact]
        public void MemoryLayout_StructSizes()
        {
            // 验证关键结构体大小
            Assert.Equal(8, Marshal.SizeOf<EntityRef>());
            
            // ComponentSetNet8 使用 InlineArray(8) * 8 bytes = 64 bytes
            // 但实际大小可能因为布局略有不同
            var actualSize = Marshal.SizeOf<ComponentSetNet8>();
            Assert.True(actualSize >= 64, $"ComponentSetNet8 size should be >= 64, but was {actualSize}");
        }

        #endregion
    }
}
