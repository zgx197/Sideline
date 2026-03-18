// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Lattice.ECS.Framework
{
    /// <summary>
    /// 裁剪系统 - 高性能空间裁剪
    /// 
    /// 设计目标：
    /// 1. 多线程安全（原子操作）
    /// 2. 64 实体一组（一个 long），位操作高效
    /// 3. 与 Filter 集成，自动跳过裁剪实体
    /// </summary>
    public unsafe struct CullingSystem
    {
        // 裁剪状态位图（每 bit 代表一个实体）
        // 1 = 被裁剪（不可见），0 = 未裁剪（可见）
        private long* _culled;
        private int _capacity;  // 实体容量（64的倍数）

        /// <summary>是否已初始化</summary>
        public bool IsInitialized => _culled != null;

        /// <summary>
        /// 初始化裁剪系统
        /// </summary>
        public void Initialize(byte* buffer, int maxEntities)
        {
            // 容量对齐到 64
            _capacity = (maxEntities + 63) & ~63;
            _culled = (long*)buffer;

            // 初始化为全部未裁剪
            Clear();
        }

        /// <summary>
        /// 清空所有裁剪标记
        /// </summary>
        public void Clear()
        {
            int blockCount = _capacity / 64;
            for (int i = 0; i < blockCount; i++)
            {
                _culled[i] = 0;
            }
        }

        /// <summary>
        /// 标记实体为裁剪（线程安全）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void MarkCulled(int entityIndex)
        {
            int block = entityIndex >> 6;      // / 64
            int bit = entityIndex & 0x3F;      // % 64

            long mask = 1L << bit;
            long current;
            long newValue;

            // CAS 循环确保线程安全
            do
            {
                current = _culled[block];
                newValue = current | mask;
            }
            while (Interlocked.CompareExchange(ref _culled[block], newValue, current) != current);
        }

        /// <summary>
        /// 批量标记裁剪（非线程安全，用于单线程裁剪计算）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void MarkCulledBatch(int entityIndex)
        {
            int block = entityIndex >> 6;
            int bit = entityIndex & 0x3F;
            _culled[block] |= 1L << bit;
        }

        /// <summary>
        /// 检查实体是否被裁剪
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsCulled(int entityIndex)
        {
            int block = entityIndex >> 6;
            int bit = entityIndex & 0x3F;
            return (_culled[block] & (1L << bit)) != 0;
        }

        /// <summary>
        /// 取消裁剪标记
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UnmarkCulled(int entityIndex)
        {
            int block = entityIndex >> 6;
            int bit = entityIndex & 0x3F;

            long mask = ~(1L << bit);
            long current;
            long newValue;

            do
            {
                current = _culled[block];
                newValue = current & mask;
            }
            while (Interlocked.CompareExchange(ref _culled[block], newValue, current) != current);
        }

        /// <summary>
        /// 获取指定 Block 的裁剪位图（用于批量检查）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long GetCulledBlock(int blockIndex)
        {
            return _culled[blockIndex];
        }

        /// <summary>
        /// 计算所需缓冲区大小
        /// </summary>
        public static int CalculateBufferSize(int maxEntities)
        {
            int capacity = (maxEntities + 63) & ~63;
            return sizeof(long) * (capacity / 64);
        }
    }
}
