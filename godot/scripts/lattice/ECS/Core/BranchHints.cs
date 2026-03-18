// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

using System.Runtime.CompilerServices;

namespace Lattice.ECS.Core
{
    /// <summary>
    /// 分支预测提示 - 帮助 CPU 预测分支走向
    /// 
    /// 使用场景：
    /// - Likely：条件在大多数情况下为真（如：组件存在的检查）
    /// - Unlikely：条件很少发生（如：错误检查、边界情况）
    /// </summary>
    public static class BranchHints
    {
        /// <summary>
        /// 标记条件在大多数情况下为真
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Likely(bool condition)
        {
            return condition;
        }

        /// <summary>
        /// 标记条件很少发生
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Unlikely(bool condition)
        {
            return condition;
        }
    }

    /// <summary>
    /// 缓存行大小常量
    /// </summary>
    public static class CacheLine
    {
        public const int Size = 64;
        public const int Size128 = 128;  // 对于高端处理器（如 Apple Silicon）

        /// <summary>
        /// 对齐到缓存行大小
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static nint Align(nint value)
        {
            return (value + Size - 1) & ~(Size - 1);
        }

        /// <summary>
        /// 计算缓存行数量
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Count(int bytes)
        {
            return (bytes + Size - 1) / Size;
        }
    }
}
