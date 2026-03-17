// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

using System;
using System.Runtime.CompilerServices;
using Lattice.Core;

namespace Lattice.ECS.Core
{
    /// <summary>
    /// 确定性哈希计算 - 跨平台一致
    /// </summary>
    public static class DeterministicHash
    {
        /// <summary>FNV-1a 32位哈希</summary>
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

        /// <summary>FNV-1a 64位哈希</summary>
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

        /// <summary>实体引用哈希</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetHashCode(EntityRef entity)
        {
            return entity.Index * 59209 + entity.Version;
        }

        /// <summary>组合哈希</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Combine(int h1, int h2)
        {
            uint rol5 = ((uint)h1 << 5) | ((uint)h1 >> 27);
            return ((int)rol5 + h1) ^ h2;
        }
    }
}
