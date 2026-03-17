// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Lattice.ECS.Core
{
    /// <summary>
    /// 256位组件集合 - FrameSync 风格优化
    /// 
    /// 适用于只需要前 256 个组件类型的场景
    /// 比 ComponentSet（512位）节省一半内存
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public unsafe struct ComponentSet256 : IEquatable<ComponentSet256>
    {
        #region 常量

        public const int MaxComponents = 256;
        public const int BlockCount = 4;  // 256 / 64 = 4

        #endregion

        #region 字段

        [FieldOffset(0)]
        public fixed ulong Set[BlockCount];

        #endregion

        #region 构造函数

        public static ComponentSet256 Empty => default;

        #endregion

        #region 基本操作

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool IsSet(int index)
        {
#if DEBUG
            if ((uint)index >= MaxComponents)
                throw new ArgumentOutOfRangeException(nameof(index));
#endif
            int blockIndex = index >> 6;
            int bitOffset = index & 0x3F;
            return (Set[blockIndex] & (1UL << bitOffset)) != 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(int index)
        {
#if DEBUG
            if ((uint)index >= MaxComponents)
                throw new ArgumentOutOfRangeException(nameof(index));
#endif
            int blockIndex = index >> 6;
            int bitOffset = index & 0x3F;
            Set[blockIndex] |= (1UL << bitOffset);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Remove(int index)
        {
#if DEBUG
            if ((uint)index >= MaxComponents)
                throw new ArgumentOutOfRangeException(nameof(index));
#endif
            int blockIndex = index >> 6;
            int bitOffset = index & 0x3F;
            Set[blockIndex] &= ~(1UL << bitOffset);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool IsSet<T>() where T : unmanaged, IComponent
        {
            return (Set[ComponentTypeId<T>.BlockIndex] & ComponentTypeId<T>.BitMask) != 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add<T>() where T : unmanaged, IComponent
        {
            Set[ComponentTypeId<T>.BlockIndex] |= ComponentTypeId<T>.BitMask;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Remove<T>() where T : unmanaged, IComponent
        {
            Set[ComponentTypeId<T>.BlockIndex] &= ~ComponentTypeId<T>.BitMask;
        }

        public readonly bool IsEmpty
        {
            get
            {
                return Set[0] == 0 && Set[1] == 0 && Set[2] == 0 && Set[3] == 0;
            }
        }

        public readonly int Count
        {
            get
            {
                return System.Numerics.BitOperations.PopCount(Set[0])
                     + System.Numerics.BitOperations.PopCount(Set[1])
                     + System.Numerics.BitOperations.PopCount(Set[2])
                     + System.Numerics.BitOperations.PopCount(Set[3]);
            }
        }

        #endregion

        #region 集合运算

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool IsSupersetOf(in ComponentSet256 other)
        {
            return (Set[0] & other.Set[0]) == other.Set[0]
                && (Set[1] & other.Set[1]) == other.Set[1]
                && (Set[2] & other.Set[2]) == other.Set[2]
                && (Set[3] & other.Set[3]) == other.Set[3];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool IsSubsetOf(in ComponentSet256 other)
        {
            return other.IsSupersetOf(this);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool Overlaps(in ComponentSet256 other)
        {
            return (Set[0] & other.Set[0]) != 0
                || (Set[1] & other.Set[1]) != 0
                || (Set[2] & other.Set[2]) != 0
                || (Set[3] & other.Set[3]) != 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UnionWith(in ComponentSet256 other)
        {
            Set[0] |= other.Set[0];
            Set[1] |= other.Set[1];
            Set[2] |= other.Set[2];
            Set[3] |= other.Set[3];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void IntersectWith(in ComponentSet256 other)
        {
            Set[0] &= other.Set[0];
            Set[1] &= other.Set[1];
            Set[2] &= other.Set[2];
            Set[3] &= other.Set[3];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ExceptWith(in ComponentSet256 other)
        {
            Set[0] &= ~other.Set[0];
            Set[1] &= ~other.Set[1];
            Set[2] &= ~other.Set[2];
            Set[3] &= ~other.Set[3];
        }

        #endregion

        #region 与 ComponentSet 互操作

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool IsSupersetOf(in ComponentSet other)
        {
            return (Set[0] & other.Set[0]) == other.Set[0]
                && (Set[1] & other.Set[1]) == other.Set[1]
                && (Set[2] & other.Set[2]) == other.Set[2]
                && (Set[3] & other.Set[3]) == other.Set[3];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UnionWith(in ComponentSet other)
        {
            Set[0] |= other.Set[0];
            Set[1] |= other.Set[1];
            Set[2] |= other.Set[2];
            Set[3] |= other.Set[3];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void IntersectWith(in ComponentSet other)
        {
            Set[0] &= other.Set[0];
            Set[1] &= other.Set[1];
            Set[2] &= other.Set[2];
            Set[3] &= other.Set[3];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ExceptWith(in ComponentSet other)
        {
            Set[0] &= ~other.Set[0];
            Set[1] &= ~other.Set[1];
            Set[2] &= ~other.Set[2];
            Set[3] &= ~other.Set[3];
        }

        #endregion

        #region 相等性比较

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool Equals(ComponentSet256 other)
        {
            return Set[0] == other.Set[0]
                && Set[1] == other.Set[1]
                && Set[2] == other.Set[2]
                && Set[3] == other.Set[3];
        }

        public override readonly bool Equals(object? obj)
        {
            return obj is ComponentSet256 other && Equals(other);
        }

        public override readonly int GetHashCode()
        {
            int hash = (int)Set[0];
            hash = System.HashCode.Combine(hash, (int)(Set[0] >> 32));
            hash = System.HashCode.Combine(hash, (int)Set[1]);
            hash = System.HashCode.Combine(hash, (int)(Set[1] >> 32));
            return hash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(ComponentSet256 left, ComponentSet256 right)
        {
            return left.Equals(right);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(ComponentSet256 left, ComponentSet256 right)
        {
            return !left.Equals(right);
        }

        #endregion
    }
}
