// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

using System;
using System.Runtime.CompilerServices;
using Lattice.Core;
using Lattice.Math;

namespace Lattice.ECS.Core
{
    /// <summary>
    /// 确定性哈希计算 - 跨平台一致
    /// 
    /// 禁用默认 GetHashCode（可能因平台/运行时不同）
    /// 使用 FNV-1a 等确定性算法
    /// </summary>
    public static class DeterministicHash
    {
        /// <summary>
        /// FNV-1a 32位哈希 - 完全确定性
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Fnv1a32(ReadOnlySpan<byte> data)
        {
            const uint FnvPrime = 0x01000193;
            const uint FnvOffset = 0x811C9DC5;

            uint hash = FnvOffset;
            foreach (byte b in data)
            {
                hash ^= b;
                hash *= FnvPrime;
            }
            return (int)hash;
        }

        /// <summary>
        /// FNV-1a 64位哈希
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long Fnv1a64(ReadOnlySpan<byte> data)
        {
            const ulong FnvPrime = 0x00000100000001B3;
            const ulong FnvOffset = 0xCBF29CE484222325;

            ulong hash = FnvOffset;
            foreach (byte b in data)
            {
                hash ^= b;
                hash *= FnvPrime;
            }
            return (long)hash;
        }

        /// <summary>
        /// 确定性实体引用哈希
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetHashCode(EntityRef entity)
        {
            // 直接使用 Raw 值的确定性组合
            uint a = (uint)entity.Version;
            uint b = (uint)entity.Index;
            return (int)(a + b * 59209);
        }

        /// <summary>
        /// 确定性组合哈希
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Combine(int h1, int h2)
        {
            // 确定性组合算法（来自 System.HashCode，但固定种子）
            uint rol5 = ((uint)h1 << 5) | ((uint)h1 >> 27);
            return ((int)rol5 + h1) ^ h2;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Combine(int h1, int h2, int h3)
        {
            int hash = Combine(h1, h2);
            return Combine(hash, h3);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Combine(int h1, int h2, int h3, int h4)
        {
            int hash = Combine(h1, h2);
            hash = Combine(hash, h3);
            return Combine(hash, h4);
        }

        /// <summary>
        /// ComponentSet 确定性哈希
        /// </summary>
        public static int GetHashCode(ComponentSetNet8 set)
        {
            var span = set.AsSpan();
            // 只使用部分位计算哈希，确保性能
            return Combine(
                (int)span[0],
                (int)(span[0] >> 32),
                (int)span[1],
                (int)(span[1] >> 32)
            );
        }
    }

    /// <summary>
    /// 确定性比较器
    /// </summary>
    public readonly struct DeterministicEntityComparer : IEqualityComparer<EntityRef>
    {
        public static readonly DeterministicEntityComparer Instance = new();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(EntityRef x, EntityRef y) => x.Raw == y.Raw;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetHashCode(EntityRef obj) => DeterministicHash.GetHashCode(obj);
    }

    /// <summary>
    /// 确定性序列化辅助
    /// 确保浮点数序列化跨平台一致
    /// </summary>
    public static class DeterministicSerialization
    {
        /// <summary>
        /// 将 FP 序列化为字节（确定性，平台无关）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteFP(Span<byte> buffer, FP value, ref int offset)
        {
            // FP 底层是 long（定点数），直接写入
            long raw = value.RawValue;
            buffer[offset++] = (byte)(raw);
            buffer[offset++] = (byte)(raw >> 8);
            buffer[offset++] = (byte)(raw >> 16);
            buffer[offset++] = (byte)(raw >> 24);
            buffer[offset++] = (byte)(raw >> 32);
            buffer[offset++] = (byte)(raw >> 40);
            buffer[offset++] = (byte)(raw >> 48);
            buffer[offset++] = (byte)(raw >> 56);
        }

        /// <summary>
        /// 从字节反序列化 FP
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP ReadFP(ReadOnlySpan<byte> buffer, ref int offset)
        {
            long raw = (long)buffer[offset++]
                | ((long)buffer[offset++] << 8)
                | ((long)buffer[offset++] << 16)
                | ((long)buffer[offset++] << 24)
                | ((long)buffer[offset++] << 32)
                | ((long)buffer[offset++] << 40)
                | ((long)buffer[offset++] << 48)
                | ((long)buffer[offset++] << 56);
            return new FP(raw);
        }

        /// <summary>
        /// 将 int 序列化为字节（小端序，确定性）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteInt32(Span<byte> buffer, int value, ref int offset)
        {
            buffer[offset++] = (byte)(value);
            buffer[offset++] = (byte)(value >> 8);
            buffer[offset++] = (byte)(value >> 16);
            buffer[offset++] = (byte)(value >> 24);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ReadInt32(ReadOnlySpan<byte> buffer, ref int offset)
        {
            return buffer[offset++]
                | (buffer[offset++] << 8)
                | (buffer[offset++] << 16)
                | (buffer[offset++] << 24);
        }

        /// <summary>
        /// 计算序列化数据的确定性校验和
        /// </summary>
        public static uint ComputeChecksum(ReadOnlySpan<byte> data)
        {
            // CRC32-C 确定性算法
            const uint Polynomial = 0x82F63B78;
            uint crc = 0xFFFFFFFF;

            foreach (byte b in data)
            {
                crc ^= b;
                for (int i = 0; i < 8; i++)
                {
                    crc = (crc >> 1) ^ ((crc & 1) * Polynomial);
                }
            }
            return ~crc;
        }
    }
}
