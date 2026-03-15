// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Lattice.ECS.Core
{
    /// <summary>
    /// 256-bit 组件集合，用于中等规模的组件类型比较优化
    /// 对齐 FrameSync ComponentSet256 设计
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    internal struct ComponentSet256 : IEquatable<ComponentSet256>
    {
        public const int MaxComponents = 256;
        public const int BlockCount = 4;
        public const int Size = 32;

        /// <summary>
        /// 位集合数据（固定 4 个 ulong）
        /// </summary>
        [FieldOffset(0)]
        public unsafe fixed ulong Set[4];

        /// <summary>
        /// 检查是否为空
        /// </summary>
        public unsafe bool IsEmpty
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Set[0] == 0 && Set[1] == 0 && Set[2] == 0 && Set[3] == 0;
        }

        #region 核心操作

        /// <summary>
        /// 检查是否包含指定索引的组件
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe bool IsSet(int index)
        {
#if DEBUG
            if (index < 0 || index >= MaxComponents)
                throw new ArgumentOutOfRangeException(nameof(index), $"组件索引必须在 0-{MaxComponents - 1} 范围内");
#endif
            int block = index >> 6;
            int offset = index & 0x3F;
            return (Set[block] & (1UL << offset)) != 0;
        }

        /// <summary>
        /// 添加组件
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void Add(int index)
        {
#if DEBUG
            if (index < 0 || index >= MaxComponents)
                throw new ArgumentOutOfRangeException(nameof(index), $"组件索引必须在 0-{MaxComponents - 1} 范围内");
#endif
            int block = index >> 6;
            int offset = index & 0x3F;
            Set[block] |= (1UL << offset);
        }

        /// <summary>
        /// 移除组件
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void Remove(int index)
        {
#if DEBUG
            if (index < 0 || index >= MaxComponents)
                throw new ArgumentOutOfRangeException(nameof(index), $"组件索引必须在 0-{MaxComponents - 1} 范围内");
#endif
            int block = index >> 6;
            int offset = index & 0x3F;
            Set[block] &= ~(1UL << offset);
        }

        /// <summary>
        /// 泛型添加组件
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add<T>() where T : unmanaged
        {
            int id = ComponentTypeId<T>.Id;
            if (id < MaxComponents)
                Add(id);
            else
                throw new InvalidOperationException($"组件类型 {typeof(T).Name} 的 ID {id} 超出 ComponentSet256 范围");
        }

        /// <summary>
        /// 泛型移除组件
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Remove<T>() where T : unmanaged
        {
            int id = ComponentTypeId<T>.Id;
            if (id < MaxComponents)
                Remove(id);
        }

        /// <summary>
        /// 泛型检查组件
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsSet<T>() where T : unmanaged
        {
            int id = ComponentTypeId<T>.Id;
            return id < MaxComponents && IsSet(id);
        }

        #endregion

        #region 集合关系运算

        /// <summary>
        /// 检查当前集合是否为 other 的子集
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe bool IsSubsetOf(in ComponentSet256 other)
        {
            return (Set[0] & other.Set[0]) == Set[0] &&
                   (Set[1] & other.Set[1]) == Set[1] &&
                   (Set[2] & other.Set[2]) == Set[2] &&
                   (Set[3] & other.Set[3]) == Set[3];
        }

        /// <summary>
        /// 检查当前集合是否为 other 的超集
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe bool IsSupersetOf(in ComponentSet256 other)
        {
            return (Set[0] & other.Set[0]) == other.Set[0] &&
                   (Set[1] & other.Set[1]) == other.Set[1] &&
                   (Set[2] & other.Set[2]) == other.Set[2] &&
                   (Set[3] & other.Set[3]) == other.Set[3];
        }

        /// <summary>
        /// 检查是否与 other 有交集
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe bool Overlaps(in ComponentSet256 other)
        {
            return (Set[0] & other.Set[0]) != 0 ||
                   (Set[1] & other.Set[1]) != 0 ||
                   (Set[2] & other.Set[2]) != 0 ||
                   (Set[3] & other.Set[3]) != 0;
        }

        /// <summary>
        /// 与 ComponentSet64 的超集检查
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal unsafe bool IsSupersetOf(in ComponentSet64 other)
        {
            return (Set[0] & other.Set[0]) == other.Set[0];
        }

        /// <summary>
        /// 与 ComponentSet 的子集检查
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal unsafe bool IsSubsetOf(in ComponentSet other)
        {
            return (Set[0] & other.Set[0]) == Set[0] &&
                   (Set[1] & other.Set[1]) == Set[1] &&
                   (Set[2] & other.Set[2]) == Set[2] &&
                   (Set[3] & other.Set[3]) == Set[3];
        }

        /// <summary>
        /// 与 ComponentSet 的重叠检查
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal unsafe bool Overlaps(in ComponentSet other)
        {
            return (Set[0] & other.Set[0]) != 0 ||
                   (Set[1] & other.Set[1]) != 0 ||
                   (Set[2] & other.Set[2]) != 0 ||
                   (Set[3] & other.Set[3]) != 0;
        }

        #endregion

        #region 集合运算

        /// <summary>
        /// 与 ComponentSet64 的并集
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal unsafe void UnionWith(in ComponentSet64 other)
        {
            Set[0] |= other.Set[0];
        }

        /// <summary>
        /// 与 ComponentSet64 的交集
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal unsafe void IntersectWith(in ComponentSet64 other)
        {
            Set[0] &= other.Set[0];
            Set[1] = 0;
            Set[2] = 0;
            Set[3] = 0;
        }

        /// <summary>
        /// 与 ComponentSet64 的差集
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal unsafe void Remove(in ComponentSet64 other)
        {
            Set[0] &= ~other.Set[0];
        }

        /// <summary>
        /// 与 ComponentSet256 的并集
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void UnionWith(in ComponentSet256 other)
        {
            Set[0] |= other.Set[0];
            Set[1] |= other.Set[1];
            Set[2] |= other.Set[2];
            Set[3] |= other.Set[3];
        }

        /// <summary>
        /// 与 ComponentSet256 的交集
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void IntersectWith(in ComponentSet256 other)
        {
            Set[0] &= other.Set[0];
            Set[1] &= other.Set[1];
            Set[2] &= other.Set[2];
            Set[3] &= other.Set[3];
        }

        /// <summary>
        /// 与 ComponentSet256 的差集
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void Remove(in ComponentSet256 other)
        {
            Set[0] &= ~other.Set[0];
            Set[1] &= ~other.Set[1];
            Set[2] &= ~other.Set[2];
            Set[3] &= ~other.Set[3];
        }

        #endregion

        #region 等值比较

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe bool Equals(in ComponentSet256 other)
        {
            return Set[0] == other.Set[0] &&
                   Set[1] == other.Set[1] &&
                   Set[2] == other.Set[2] &&
                   Set[3] == other.Set[3];
        }

        bool IEquatable<ComponentSet256>.Equals(ComponentSet256 other) => Equals(other);

        public override bool Equals(object? obj) => obj is ComponentSet256 other && Equals(other);

        public unsafe override int GetHashCode()
        {
            int hash = Set[0].GetHashCode();
            hash = hash * 31 + Set[1].GetHashCode();
            hash = hash * 31 + Set[2].GetHashCode();
            hash = hash * 31 + Set[3].GetHashCode();
            return hash;
        }

        public static bool operator ==(ComponentSet256 left, ComponentSet256 right) => left.Equals(right);
        public static bool operator !=(ComponentSet256 left, ComponentSet256 right) => !left.Equals(right);

        #endregion

        /// <summary>
        /// 隐式转换为 ComponentSet（零开销，高位补零）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe implicit operator ComponentSet(ComponentSet256 set)
        {
            ComponentSet result = default;
            result.Set[0] = set.Set[0];
            result.Set[1] = set.Set[1];
            result.Set[2] = set.Set[2];
            result.Set[3] = set.Set[3];
            result.Set[4] = 0;
            result.Set[5] = 0;
            result.Set[6] = 0;
            result.Set[7] = 0;
            return result;
        }
    }
}
