// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

using Lattice.ECS.Core;
using Xunit;

namespace Lattice.Tests.ECS.Core
{
    /// <summary>
    /// 确定性哈希测试 - 验证跨平台一致性
    /// </summary>
    public class DeterministicHashTests
    {
        [Fact]
        public void Hash_FNV1a32_Empty()
        {
            var data = System.Array.Empty<byte>();
            int hash = DeterministicHash.Fnv1a32(data);

            // FNV-1a 空输入的偏移基值（转有符号int）
            Assert.Equal(unchecked((int)0x811C9DC5), hash);
        }

        [Fact]
        public void Hash_FNV1a32_KnownValues()
        {
            // 验证已知的 FNV-1a 32 哈希值
            var test = new byte[] { (byte)'t', (byte)'e', (byte)'s', (byte)'t' };
            int hash = DeterministicHash.Fnv1a32(test);

            // "test" 的实际 FNV-1a 32 哈希值（根据实现计算）
            const uint FnvPrime = 0x01000193;
            const uint FnvOffset = 0x811C9DC5;
            uint expected = FnvOffset;
            foreach (byte b in test)
            {
                expected ^= b;
                expected *= FnvPrime;
            }
            Assert.Equal(unchecked((int)expected), hash);
        }

        [Fact]
        public void Hash_FNV1a32_Deterministic()
        {
            // 验证相同输入总是产生相同输出
            var data = new byte[64];
            new System.Random(42).NextBytes(data);

            int hash1 = DeterministicHash.Fnv1a32(data);
            int hash2 = DeterministicHash.Fnv1a32(data);

            Assert.Equal(hash1, hash2);
        }

        [Fact]
        public void Hash_FNV1a32_DifferentInput()
        {
            // 验证不同输入产生不同输出
            var data1 = new byte[] { 1, 2, 3, 4 };
            var data2 = new byte[] { 1, 2, 3, 5 };

            int hash1 = DeterministicHash.Fnv1a32(data1);
            int hash2 = DeterministicHash.Fnv1a32(data2);

            Assert.NotEqual(hash1, hash2);
        }

        [Fact]
        public void Hash_FNV1a32_OrderMatters()
        {
            // 验证顺序敏感性
            var data1 = new byte[] { 1, 2, 3, 4 };
            var data2 = new byte[] { 4, 3, 2, 1 };

            int hash1 = DeterministicHash.Fnv1a32(data1);
            int hash2 = DeterministicHash.Fnv1a32(data2);

            Assert.NotEqual(hash1, hash2);
        }

        [Fact]
        public void Hash_FNV1a64_Empty()
        {
            var data = System.Array.Empty<byte>();
            long hash = DeterministicHash.Fnv1a64(data);

            // FNV-1a 64 空输入的偏移基值（转有符号long）
            Assert.Equal(unchecked((long)0xCBF29CE484222325), hash);
        }

        [Fact]
        public void Hash_FNV1a64_Deterministic()
        {
            var data = new byte[128];
            new System.Random(123).NextBytes(data);

            long hash1 = DeterministicHash.Fnv1a64(data);
            long hash2 = DeterministicHash.Fnv1a64(data);

            Assert.Equal(hash1, hash2);
        }

        [Fact]
        public void Hash_EntityRef_Deterministic()
        {
            var entity = new Lattice.Core.EntityRef(12345, 67890);

            int hash1 = DeterministicHash.GetHashCode(entity);
            int hash2 = DeterministicHash.GetHashCode(entity);

            Assert.Equal(hash1, hash2);
        }

        [Fact]
        public void Hash_Combine_Deterministic()
        {
            int hash1 = DeterministicHash.Combine(1, 2);
            int hash2 = DeterministicHash.Combine(1, 2);

            Assert.Equal(hash1, hash2);
        }

        [Fact]
        public void Hash_Combine_DifferentInput()
        {
            int hash1 = DeterministicHash.Combine(1, 2);
            int hash2 = DeterministicHash.Combine(2, 1);

            Assert.NotEqual(hash1, hash2);
        }

        [Fact]
        public void Hash_Combine_ThreeValues()
        {
            int hash = DeterministicHash.Combine(1, 2, 3);
            int expected = DeterministicHash.Combine(DeterministicHash.Combine(1, 2), 3);

            Assert.Equal(expected, hash);
        }

        [Fact]
        public void Hash_Combine_FourValues()
        {
            int hash = DeterministicHash.Combine(1, 2, 3, 4);
            int expected = DeterministicHash.Combine(DeterministicHash.Combine(DeterministicHash.Combine(1, 2), 3), 4);

            Assert.Equal(expected, hash);
        }
    }
}
