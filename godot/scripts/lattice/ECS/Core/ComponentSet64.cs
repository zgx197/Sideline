// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Lattice.ECS.Core
{
    /// <summary>
    /// 64位组件集合 - FrameSync 风格优化
    /// 
    /// 适用于只需要前 64 个组件类型的场景（最常见的用例）
    /// 比 ComponentSet（512位）更节省内存，操作更快
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 8)]
    public struct ComponentSet64 : IEquatable<ComponentSet64>
    {
        #region 常量

        public const int MaxComponents = 64;

        #endregion

        #region 字段

        [FieldOffset(0)]
        public ulong Set;

        #endregion

        #region 构造函数

        public static ComponentSet64 Empty => default;

        #endregion

        #region 基本操作

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool IsSet(int index)
        {
#if DEBUG
            if ((uint)index >= MaxComponents)
                throw new ArgumentOutOfRangeException(nameof(index));
#endif
            return (Set & (1UL << index)) != 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetBit(int index)
        {
#if DEBUG
            if ((uint)index >= MaxComponents)
                throw new ArgumentOutOfRangeException(nameof(index));
#endif
            Set |= (1UL << index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ClearBit(int index)
        {
#if DEBUG
            if ((uint)index >= MaxComponents)
                throw new ArgumentOutOfRangeException(nameof(index));
#endif
            Set &= ~(1UL << index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool IsSet<T>() where T : unmanaged, IComponent
        {
            return (Set & ComponentTypeId<T>.BitMask) != 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add<T>() where T : unmanaged, IComponent
        {
            Set |= ComponentTypeId<T>.BitMask;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Remove<T>() where T : unmanaged, IComponent
        {
            Set &= ~ComponentTypeId<T>.BitMask;
        }

        public readonly bool IsEmpty => Set == 0;

        public readonly int Count
        {
            get => System.Numerics.BitOperations.PopCount(Set);
        }

        #endregion

        #region 集合运算

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool IsSupersetOf(in ComponentSet64 other)
        {
            return (Set & other.Set) == other.Set;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool IsSubsetOf(in ComponentSet64 other)
        {
            return other.IsSupersetOf(this);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool Overlaps(in ComponentSet64 other)
        {
            return (Set & other.Set) != 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UnionWith(in ComponentSet64 other)
        {
            Set |= other.Set;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void IntersectWith(in ComponentSet64 other)
        {
            Set &= other.Set;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ExceptWith(in ComponentSet64 other)
        {
            Set &= ~other.Set;
        }

        #endregion

        #region 与 ComponentSet 互操作

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool IsSupersetOf(in ComponentSet other)
        {
            // 只检查低 64 位
            unsafe
            {
                return (Set & other.Set[0]) == other.Set[0];
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UnionWith(in ComponentSet other)
        {
            unsafe
            {
                Set |= other.Set[0];
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void IntersectWith(in ComponentSet other)
        {
            unsafe
            {
                Set &= other.Set[0];
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ExceptWith(in ComponentSet other)
        {
            unsafe
            {
                Set &= ~other.Set[0];
            }
        }

        #endregion

        #region 相等性比较

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool Equals(ComponentSet64 other)
        {
            return Set == other.Set;
        }

        public override readonly bool Equals(object? obj)
        {
            return obj is ComponentSet64 other && Equals(other);
        }

        public override readonly int GetHashCode()
        {
            return Set.GetHashCode();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(ComponentSet64 left, ComponentSet64 right)
        {
            return left.Equals(right);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(ComponentSet64 left, ComponentSet64 right)
        {
            return !left.Equals(right);
        }

        #endregion

        #region 转换

        public static implicit operator ComponentSet64(ComponentSet set)
        {
            unsafe
            {
                return new ComponentSet64 { Set = set.Set[0] };
            }
        }

        #endregion
    }
}
