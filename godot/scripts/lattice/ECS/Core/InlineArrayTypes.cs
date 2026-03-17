// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Lattice.ECS.Core
{
    /// <summary>
    /// .NET 8 InlineArray 优化 - 8个ulong的栈上数组
    /// 用于 ComponentSet，无需 unsafe 代码即可获得 fixed array 性能
    /// </summary>
    [InlineArray(8)]
    [StructLayout(LayoutKind.Sequential, Size = 64)]
    public struct Ulong8
    {
        private ulong _element0;

        /// <summary>
        /// 获取 Span 视图（安全访问）
        /// </summary>
        public Span<ulong> AsSpan()
        {
            return MemoryMarshal.CreateSpan(ref _element0, 8);
        }

        /// <summary>
        /// 获取元素值
        /// </summary>
        public ulong GetValue(int index) => AsSpan()[index];

        /// <summary>
        /// 设置元素值
        /// </summary>
        public void SetValue(int index, ulong value) => AsSpan()[index] = value;

        /// <summary>
        /// 检查所有元素是否为零（快速空检查）
        /// </summary>
        public bool IsAllZero()
        {
            var span = AsSpan();
            return span[0] == 0 && span[1] == 0 && span[2] == 0 && span[3] == 0
                && span[4] == 0 && span[5] == 0 && span[6] == 0 && span[7] == 0;
        }

        /// <summary>
        /// 清零
        /// </summary>
        public void Clear() => AsSpan().Clear();
    }

    /// <summary>
    /// 4个ulong的栈上数组 - 用于 ComponentSet256
    /// </summary>
    [InlineArray(4)]
    [StructLayout(LayoutKind.Sequential, Size = 32)]
    public struct Ulong4
    {
        private ulong _element0;

        public Span<ulong> AsSpan() => MemoryMarshal.CreateSpan(ref _element0, 4);
    }

    /// <summary>
    /// 128个ushort的栈上数组 - 用于稀疏数组缓存
    /// </summary>
    [InlineArray(128)]
    public struct Ushort128
    {
        private ushort _element0;

        public Span<ushort> AsSpan() => MemoryMarshal.CreateSpan(ref _element0, 128);
    }
}
