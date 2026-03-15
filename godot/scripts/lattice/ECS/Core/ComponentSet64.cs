// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Lattice.ECS.Core
{
    /// <summary>
    /// 64-bit 组件集合，用于快速比较和单一组件查询优化
    /// 对齐 FrameSync ComponentSet64 设计
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 8)]
    internal struct ComponentSet64 : IEquatable<ComponentSet64>
    {
        public const int MaxComponents = 64;
        public const int BlockCount = 1;
        public const int Size = 8;

        /// <summary>
        /// 位集合数据（固定 1 个 ulong）
        /// </summary>
        [FieldOffset(0)]
        public unsafe fixed ulong Set[1];

        /// <summary>
        /// 检查是否为空
        /// </summary>
        public unsafe bool IsEmpty
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Set[0] == 0;
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
            return (Set[0] & (1UL << index)) != 0;
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
            Set[0] |= (1UL << index);
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
            Set[0] &= ~(1UL << index);
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
                throw new InvalidOperationException($"组件类型 {typeof(T).Name} 的 ID {id} 超出 ComponentSet64 范围");
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
        public unsafe bool IsSubsetOf(in ComponentSet64 other)
        {
            return (Set[0] & other.Set[0]) == Set[0];
        }

        /// <summary>
        /// 检查当前集合是否为 other 的超集
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe bool IsSupersetOf(in ComponentSet64 other)
        {
            return (Set[0] & other.Set[0]) == other.Set[0];
        }

        /// <summary>
        /// 检查是否与 other 有交集
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe bool Overlaps(in ComponentSet64 other)
        {
            return (Set[0] & other.Set[0]) != 0;
        }

        /// <summary>
        /// 与 ComponentSet256 的子集检查
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal unsafe bool IsSubsetOf(in ComponentSet256 other)
        {
            return (Set[0] & other.Set[0]) == Set[0];
        }

        /// <summary>
        /// 与 ComponentSet256 的重叠检查
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal unsafe bool Overlaps(in ComponentSet256 other)
        {
            return (Set[0] & other.Set[0]) != 0;
        }

        /// <summary>
        /// 与 ComponentSet 的子集检查
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal unsafe bool IsSubsetOf(in ComponentSet other)
        {
            return (Set[0] & other.Set[0]) == Set[0];
        }

        /// <summary>
        /// 与 ComponentSet 的重叠检查
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal unsafe bool Overlaps(in ComponentSet other)
        {
            return (Set[0] & other.Set[0]) != 0;
        }

        #endregion

        #region 集合运算

        /// <summary>
        /// 并集（修改当前集合）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void UnionWith(in ComponentSet64 other)
        {
            Set[0] |= other.Set[0];
        }

        /// <summary>
        /// 交集（修改当前集合）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void IntersectWith(in ComponentSet64 other)
        {
            Set[0] &= other.Set[0];
        }

        /// <summary>
        /// 差集（修改当前集合）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void Remove(in ComponentSet64 other)
        {
            Set[0] &= ~other.Set[0];
        }

        #endregion

        #region 等值比较

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe bool Equals(in ComponentSet64 other)
        {
            return Set[0] == other.Set[0];
        }

        bool IEquatable<ComponentSet64>.Equals(ComponentSet64 other) => Equals(other);

        public override bool Equals(object? obj) => obj is ComponentSet64 other && Equals(other);

        public unsafe override int GetHashCode()
        {
            return Set[0].GetHashCode();
        }

        public static bool operator ==(ComponentSet64 left, ComponentSet64 right) => left.Equals(right);
        public static bool operator !=(ComponentSet64 left, ComponentSet64 right) => !left.Equals(right);

        #endregion

        /// <summary>
        /// 从 ComponentSet 隐式转换（取前 64 位）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe implicit operator ComponentSet64(ComponentSet set)
        {
            ComponentSet64 result = default;
            result.Set[0] = set.Set[0];
            return result;
        }
    }
}
