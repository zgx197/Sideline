// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

using System.Runtime.CompilerServices;

namespace Lattice.ECS.Core
{
    /// <summary>
    /// 哈希码工具类，对齐 FrameSync HashCodeUtils
    /// </summary>
    public static class HashCodeUtils
    {
        /// <summary>
        /// 计算 ulong 的哈希码
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int UInt64HashCode(ulong value)
        {
            // 使用与 FrameSync 相同的算法
            return value.GetHashCode();
        }

        /// <summary>
        /// 组合两个哈希码
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int CombineHashCodes(int h1, int h2)
        {
            // 使用与 .NET 相同的算法
            unchecked
            {
                return ((h1 << 5) + h1) ^ h2;
            }
        }
    }
}
