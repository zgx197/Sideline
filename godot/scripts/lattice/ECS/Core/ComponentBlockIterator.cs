// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

using System;
using System.Runtime.CompilerServices;

namespace Lattice.ECS.Core
{
    /// <summary>
    /// 组件块迭代器接口，对齐 FrameSync ComponentBlockIterator 设计
    /// 用于高效遍历组件存储的内存块，支持批量处理
    /// </summary>
    /// <typeparam name="T">组件类型</typeparam>
    public unsafe interface IComponentBlockIterator<T> where T : unmanaged
    {
        /// <summary>
        /// 移动到下一个块
        /// </summary>
        /// <param name="data">块数据指针</param>
        /// <param name="entities">实体数组指针</param>
        /// <param name="count">块中元素数量</param>
        /// <returns>是否成功获取到有效块</returns>
        bool NextBlock(out byte* data, out Entity* entities, out int count);
    }

    /// <summary>
    /// 组件块视图，表示一个块中的连续数据
    /// </summary>
    public unsafe readonly ref struct ComponentBlockView<T> where T : unmanaged
    {
        /// <summary>组件数据指针</summary>
        public readonly T* Data;

        /// <summary>实体数组指针</summary>
        public readonly Entity* Entities;

        /// <summary>元素数量</summary>
        public readonly int Count;

        public ComponentBlockView(T* data, Entity* entities, int count)
        {
            Data = data;
            Entities = entities;
            Count = count;
        }

        /// <summary>
        /// 获取指定索引的组件引用
        /// </summary>
        public ref T this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if ((uint)index >= (uint)Count)
                    throw new ArgumentOutOfRangeException(nameof(index));
                return ref Data[index];
            }
        }

        /// <summary>
        /// 获取指定索引的实体
        /// </summary>
        public Entity GetEntity(int index)
        {
            if ((uint)index >= (uint)Count)
                throw new ArgumentOutOfRangeException(nameof(index));
            return Entities[index];
        }
    }


}
