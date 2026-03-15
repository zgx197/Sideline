// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

using System.Runtime.CompilerServices;

namespace Lattice.ECS.Serialization
{
    /// <summary>
    /// 哈希码工具类（对齐 FrameSync）
    /// </summary>
    public static class HashCodeUtils
    {
        /// <summary>
        /// 合并两个哈希码（FrameSync 算法：((a &lt;&lt; 5) + a) ^ b）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int CombineHashCodes(int a, int b)
        {
            return ((a << 5) + a) ^ b;
        }

        /// <summary>
        /// 合并三个哈希码
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int CombineHashCodes(int a, int b, int c)
        {
            int t = ((a << 5) + a) ^ b;
            return ((t << 5) + t) ^ c;
        }

        /// <summary>
        /// 计算 long 的哈希码
        /// </summary>
        public static int Int64HashCode(long value)
        {
            return (int)value ^ (int)(value >> 32);
        }

        /// <summary>
        /// 计算 ulong 的哈希码（FrameSync 算法：(int)value ^ (int)(value >> 32)）
        /// </summary>
        public static int UInt64HashCode(ulong value)
        {
            return (int)value ^ (int)(value >> 32);
        }
    }
}
