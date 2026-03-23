// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

using System.Runtime.CompilerServices;

namespace Lattice.ECS.Core
{
    /// <summary>
    /// 组件过滤器 - 描述查询条件
    /// 
    /// FrameSync 风格：
    /// - 必须包含的组件（Required）
    /// - 必须排除的组件（Excluded）
    /// </summary>
    public struct ComponentFilter
    {
        /// <summary>必须包含的组件位图</summary>
        public ComponentMask Required;

        /// <summary>必须排除的组件位图</summary>
        public ComponentMask Excluded;

        /// <summary>创建空过滤器</summary>
        public static ComponentFilter Empty => new ComponentFilter
        {
            Required = ComponentMask.Empty,
            Excluded = ComponentMask.Empty
        };

        /// <summary>添加必须包含的组件</summary>
        public ComponentFilter With<T>() where T : unmanaged, IComponent
        {
            Required.Add(ComponentTypeId<T>.Id);
            return this;
        }

        /// <summary>添加必须排除的组件</summary>
        public ComponentFilter Without<T>() where T : unmanaged, IComponent
        {
            Excluded.Add(ComponentTypeId<T>.Id);
            return this;
        }
    }

    /// <summary>
    /// 组件位图 - 512位组件集合（8 x ulong）
    /// </summary>
    public unsafe struct ComponentMask
    {
        private fixed ulong _bits[8];

        /// <summary>最大组件类型数</summary>
        public const int MaxComponents = 512;

        /// <summary>位图块数</summary>
        public const int BlockCount = 8;

        /// <summary>空位图</summary>
        public static ComponentMask Empty => new ComponentMask();

        /// <summary>是否为空</summary>
        public bool IsEmpty
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                for (int i = 0; i < BlockCount; i++)
                    if (_bits[i] != 0) return false;
                return true;
            }
        }

        /// <summary>获取指定块的位</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ulong GetBlock(int index)
        {
            return _bits[index];
        }

        /// <summary>添加组件类型</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(int typeId)
        {
            int block = typeId >> 6;
            int bit = typeId & 0x3F;
            _bits[block] |= (1UL << bit);
        }

        /// <summary>移除组件类型</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Remove(int typeId)
        {
            int block = typeId >> 6;
            int bit = typeId & 0x3F;
            _bits[block] &= ~(1UL << bit);
        }

        /// <summary>检查是否包含组件类型</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Contains(int typeId)
        {
            int block = typeId >> 6;
            int bit = typeId & 0x3F;
            return (_bits[block] & (1UL << bit)) != 0;
        }

        /// <summary>检查当前集合是否包含另一个集合的所有组件</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsSupersetOf(in ComponentMask other)
        {
            for (int i = 0; i < BlockCount; i++)
                if ((_bits[i] & other._bits[i]) != other._bits[i])
                    return false;
            return true;
        }

        /// <summary>并集</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UnionWith(in ComponentMask other)
        {
            for (int i = 0; i < BlockCount; i++)
                _bits[i] |= other._bits[i];
        }

        /// <summary>交集</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void IntersectWith(in ComponentMask other)
        {
            for (int i = 0; i < BlockCount; i++)
                _bits[i] &= other._bits[i];
        }
    }
}
