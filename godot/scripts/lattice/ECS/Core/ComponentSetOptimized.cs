// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace Lattice.ECS.Core
{
    /// <summary>
    /// .NET 8 优化的 ComponentSet
    /// 
    /// 关键优化：
    /// 1. InlineArray - 无需 unsafe 的栈上存储
    /// 2. Vector512 - SIMD 批量运算（如果硬件支持）
    /// 3. ref readonly - 避免大结构体复制
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 64)]
    public struct ComponentSetNet8 : IEquatable<ComponentSetNet8>
    {
        #region 字段

        // .NET 8 InlineArray - 安全且高性能的栈上数组
        private Ulong8 _data;

        #endregion

        #region 常量

        public const int MaxComponents = 512;
        public const int BlockCount = 8;

        #endregion

        #region 核心操作（.NET 8 优化）

        /// <summary>
        /// 检查组件是否存在 - 内联优化
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsSet(int index)
        {
#if DEBUG
            if ((uint)index >= MaxComponents)
                throw new ArgumentOutOfRangeException(nameof(index));
#endif
            int block = index >> 6;
            int bit = index & 0x3F;
            return (_data.GetValue(block) & (1UL << bit)) != 0;
        }

        /// <summary>
        /// 添加组件 - 内联优化
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(int index)
        {
#if DEBUG
            if ((uint)index >= MaxComponents)
                throw new ArgumentOutOfRangeException(nameof(index));
#endif
            int block = index >> 6;
            int bit = index & 0x3F;
            var span = _data.AsSpan();
            span[block] |= (1UL << bit);
        }

        /// <summary>
        /// SIMD 超集检查（512位一次性运算）
        /// </summary>
        public bool IsSupersetOf(in ComponentSetNet8 other)
        {
            // .NET 8 Vector512 支持
            if (Vector512.IsHardwareAccelerated)
            {
                var a = Vector512.LoadUnsafe(
                    ref MemoryMarshal.GetReference(_data.AsSpan()));
                var b = Vector512.LoadUnsafe(
                    ref MemoryMarshal.GetReference(other._data.AsSpan()));

                // 512位并行: (a & b) == b
                var and = Vector512.BitwiseAnd(a, b);
                return and == b;
            }

            // 回退：Vector256 (256位 x 2)
            if (Vector256.IsHardwareAccelerated)
            {
                var spanA = _data.AsSpan();
                var spanB = other._data.AsSpan();

                var a1 = Vector256.LoadUnsafe(ref spanA[0]);
                var a2 = Vector256.LoadUnsafe(ref spanA[4]);
                var b1 = Vector256.LoadUnsafe(ref spanB[0]);
                var b2 = Vector256.LoadUnsafe(ref spanB[4]);

                var and1 = Vector256.BitwiseAnd(a1, b1);
                var and2 = Vector256.BitwiseAnd(a2, b2);

                return and1 == b1 && and2 == b2;
            }

            // 标量回退
            var span = _data.AsSpan();
            var otherSpan = other._data.AsSpan();
            for (int i = 0; i < BlockCount; i++)
            {
                if ((span[i] & otherSpan[i]) != otherSpan[i])
                    return false;
            }
            return true;
        }

        /// <summary>
        /// SIMD 交集检查
        /// </summary>
        public bool Overlaps(in ComponentSetNet8 other)
        {
            if (Vector512.IsHardwareAccelerated)
            {
                var a = Vector512.LoadUnsafe(
                    ref MemoryMarshal.GetReference(_data.AsSpan()));
                var b = Vector512.LoadUnsafe(
                    ref MemoryMarshal.GetReference(other._data.AsSpan()));
                return Vector512.BitwiseAnd(a, b) != Vector512<ulong>.Zero;
            }

            // 标量回退
            var span = _data.AsSpan();
            var otherSpan = other._data.AsSpan();
            for (int i = 0; i < BlockCount; i++)
            {
                if ((span[i] & otherSpan[i]) != 0)
                    return true;
            }
            return false;
        }

        #endregion

        #region 集合运算

        /// <summary>
        /// 并集 - SIMD 优化
        /// </summary>
        public void UnionWith(in ComponentSetNet8 other)
        {
            if (Vector512.IsHardwareAccelerated)
            {
                var a = Vector512.LoadUnsafe(
                    ref MemoryMarshal.GetReference(_data.AsSpan()));
                var b = Vector512.LoadUnsafe(
                    ref MemoryMarshal.GetReference(other._data.AsSpan()));
                Vector512.BitwiseOr(a, b).StoreUnsafe(
                    ref MemoryMarshal.GetReference(_data.AsSpan()));
                return;
            }

            var span = _data.AsSpan();
            var otherSpan = other._data.AsSpan();
            for (int i = 0; i < BlockCount; i++)
                span[i] |= otherSpan[i];
        }

        /// <summary>
        /// 交集 - SIMD 优化
        /// </summary>
        public void IntersectWith(in ComponentSetNet8 other)
        {
            if (Vector512.IsHardwareAccelerated)
            {
                var a = Vector512.LoadUnsafe(
                    ref MemoryMarshal.GetReference(_data.AsSpan()));
                var b = Vector512.LoadUnsafe(
                    ref MemoryMarshal.GetReference(other._data.AsSpan()));
                Vector512.BitwiseAnd(a, b).StoreUnsafe(
                    ref MemoryMarshal.GetReference(_data.AsSpan()));
                return;
            }

            var span = _data.AsSpan();
            var otherSpan = other._data.AsSpan();
            for (int i = 0; i < BlockCount; i++)
                span[i] &= otherSpan[i];
        }

        #endregion

        #region 工具方法

        /// <summary>
        /// 获取 Span 视图（用于序列化）
        /// </summary>
        public Span<ulong> AsSpan() => _data.AsSpan();

        /// <summary>
        /// 是否为空
        /// </summary>
        public bool IsEmpty => _data.IsAllZero();

        /// <summary>
        /// 清空
        /// </summary>
        public void Clear() => _data.Clear();

        #endregion

        #region 相等性

        public bool Equals(ComponentSetNet8 other)
        {
            if (Vector512.IsHardwareAccelerated)
            {
                var a = Vector512.LoadUnsafe(
                    ref MemoryMarshal.GetReference(_data.AsSpan()));
                var b = Vector512.LoadUnsafe(
                    ref MemoryMarshal.GetReference(other._data.AsSpan()));
                return a == b;
            }

            var span = _data.AsSpan();
            var otherSpan = other._data.AsSpan();
            for (int i = 0; i < BlockCount; i++)
            {
                if (span[i] != otherSpan[i])
                    return false;
            }
            return true;
        }

        public override bool Equals(object? obj) => obj is ComponentSetNet8 other && Equals(other);

        public override int GetHashCode()
        {
            var span = _data.AsSpan();
            return HashCode.Combine(span[0], span[1], span[2]);
        }

        #endregion
    }
}
