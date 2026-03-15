// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

using System;
using System.Runtime.CompilerServices;

namespace Lattice.ECS.Core
{
    /// <summary>
    /// 组件存储扩展方法，提供批量操作 API
    /// </summary>
    public static class ComponentStorageExtensions
    {
        /// <summary>
        /// 遍历所有块（FrameSync 风格）
        /// </summary>
        public static unsafe void ForEachBlock<T>(this ComponentStorage<T> storage,
            ForEachBlockDelegate<T> action) where T : unmanaged
        {
            int blockCount = storage.BlockCount;
            for (int i = 0; i < blockCount; i++)
            {
                if (storage.GetBlockData(i, out var entities, out var components, out var capacity))
                {
                    int count = global::System.Math.Min(capacity, storage.UsedCount - i * capacity);
                    if (count > 0)
                    {
                        action(new ComponentBlockView<T>(components, entities, count));
                    }
                }
            }
        }

        /// <summary>
        /// 批量更新组件（无返回值）
        /// </summary>
        public static unsafe void ForEach<T>(this ComponentStorage<T> storage,
            ForEachComponentDelegate<T> action) where T : unmanaged
        {
            int count = storage.UsedCount;
            for (int i = 0; i < count; i++)
            {
                var entity = storage.GetEntityByIndex(i);
                T* component = storage.GetPointerByIndex(i);
                action(entity, component);
            }
        }

        /// <summary>
        /// 批量更新组件（带早期退出）
        /// </summary>
        public static unsafe bool ForEach<T>(this ComponentStorage<T> storage,
            ForEachComponentDelegateWithBreak<T> action) where T : unmanaged
        {
            int count = storage.UsedCount;
            for (int i = 0; i < count; i++)
            {
                var entity = storage.GetEntityByIndex(i);
                T* component = storage.GetPointerByIndex(i);
                if (!action(entity, component))
                    return false;  // 早期退出
            }
            return true;
        }

        /// <summary>
        /// SIMD 友好的批量操作（准备）
        /// 注意：实际 SIMD 实现需要 System.Numerics.Tensors 或硬件特定指令
        /// </summary>
        public static unsafe void BatchUpdate<T>(this ComponentStorage<T> storage,
            int batchSize,
            BatchUpdateBlockDelegate<T> action) where T : unmanaged
        {
            if (batchSize <= 0)
                throw new ArgumentException("Batch size must be positive", nameof(batchSize));

            storage.ForEachBlock((ComponentBlockView<T> block) =>
            {
                int offset = 0;
                while (offset < block.Count)
                {
                    int count = global::System.Math.Min(batchSize, block.Count - offset);
                    action(block.Entities + offset, block.Data + offset, count);
                    offset += count;
                }
            });
        }
    }

    /// <summary>
    /// 遍历块委托
    /// </summary>
    public unsafe delegate void ForEachBlockDelegate<T>(ComponentBlockView<T> block) where T : unmanaged;

    /// <summary>
    /// 遍历组件委托
    /// </summary>
    public unsafe delegate void ForEachComponentDelegate<T>(Entity entity, T* component) where T : unmanaged;

    /// <summary>
    /// 遍历组件委托（带早期退出）
    /// </summary>
    public unsafe delegate bool ForEachComponentDelegateWithBreak<T>(Entity entity, T* component) where T : unmanaged;

    /// <summary>
    /// 批量更新块委托
    /// </summary>
    public unsafe delegate void BatchUpdateBlockDelegate<T>(Entity* entities, T* components, int count) where T : unmanaged;
}
