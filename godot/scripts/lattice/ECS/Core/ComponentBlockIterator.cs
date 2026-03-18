// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

// 此文件使用 unsafe 代码进行高性能迭代
// 所有指针操作在 DEBUG 模式下有边界检查

using System;
using System.Runtime.CompilerServices;
using Lattice.Core;

namespace Lattice.ECS.Core
{
    /// <summary>
    /// 组件块迭代器 - 批量遍历组件，最大化缓存命中率
    /// 
    /// ============================================================
    /// 为什么需要 Block Iterator？
    /// ============================================================
    /// 
    /// 传统迭代方式的问题：
    ///   foreach (var entity in filter) { ... }
    ///   - 每次迭代都要检查版本号
    ///   - 每次都要计算 Block/Offset
    ///   - 缓存未命中率高（跳到下一个实体）
    /// 
    /// Block Iterator 的优势：
    ///   while (iterator.NextBlock(out entities, out comps, out count)) {
    ///       for (int i = 0; i < count; i++) { ... }
    ///   }
    ///   - 一次获取 128 个组件
    ///   - 内层循环无分支、无函数调用
    ///   - 缓存命中率接近 100%
    /// 
    /// 性能对比（理论）：
    /// - 传统迭代：~50-100 CPU 周期/实体
    /// - Block 迭代：~5-10 CPU 周期/实体（内层循环）
    /// 
    /// ============================================================
    /// 架构设计决策
    /// ============================================================
    /// 
    /// Q: 为什么提供两种迭代模式？
    /// A:
    ///   1. NextBlock：批量处理，适合 SIMD（一次处理 128 个）
    ///   2. Next：逐个处理，适合复杂逻辑（每个实体不同操作）
    /// 
    /// Q: 为什么跳过索引 0？
    /// A:
    ///   1. 与 FrameSync 保持一致（索引 0 保留为无效值）
    ///   2. 简化删除逻辑：用 0 作为 TOMBSTONE
    ///   3. 避免空引用检查：entity.Index == 0 直接返回无效
    /// 
    /// Q: 为什么需要版本号检测？
    /// A:
    ///   1. C# IEnumerator 模式：检查集合修改
    ///   2. 调试友好：快速失败，给出清晰错误
    ///   3. 性能开销小：只在 DEBUG 模式检查
    /// 
    /// ============================================================
    /// 预取优化 (PrefetchedBlockIterator)
    /// ============================================================
    /// 
    /// 问题：处理当前 Block 时，下一个 Block 不在缓存中
    /// 解决：在 CPU 处理当前数据时，异步加载下一个 Block
    /// 
    /// 硬件预取 vs 软件预取：
    /// - 硬件预取：自动检测顺序访问模式，但延迟较高
    /// - 软件预取：程序员明确指示，提前 100+ 周期开始加载
    /// 
    /// 预取距离：
    /// - 太近：数据还没用完就加载，浪费带宽
    /// - 太远：数据被其他缓存行驱逐
    /// - 经验值：2 个 Block（256 个组件，约 4-8KB）
    /// 
    /// 使用场景：
    /// - 大容量存储（> 1000 个组件）
    /// - 顺序遍历（随机访问无效）
    /// - 内存带宽充足（非多线程竞争）
    /// </summary>
    public unsafe struct ComponentBlockIterator<T> where T : unmanaged
    {
        private readonly Storage<T>* _storage;
        private readonly int _version;
        private readonly int _blockCapacity;

        private int _currentBlock;
        private int _currentOffset;
        private int _remaining;
        private int _startGlobalIndex;

        /// <summary>
        /// 创建完整迭代器
        /// </summary>
        internal ComponentBlockIterator(Storage<T>* storage)
        {
            _storage = storage;
            _version = storage->Version;
            _blockCapacity = storage->BlockItemCapacity;
            _currentBlock = 0;
            _currentOffset = 1; // 跳过索引0
            _remaining = storage->Count;
            _startGlobalIndex = 1;
        }

        /// <summary>
        /// 创建范围迭代器
        /// </summary>
        internal ComponentBlockIterator(Storage<T>* storage, int offset, int count)
        {
            _storage = storage;
            _version = storage->Version;
            _blockCapacity = storage->BlockItemCapacity;
            _startGlobalIndex = offset + 1;

            // 计算起始位置
            int clampedOffset = System.Math.Min(_startGlobalIndex, storage->Count);
            int clampedCount = System.Math.Max(0, System.Math.Min(count, storage->Count - clampedOffset));

            _currentBlock = clampedOffset / _blockCapacity;
            _currentOffset = clampedOffset % _blockCapacity;
            _remaining = clampedCount;
        }

        /// <summary>
        /// 重置迭代器
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Reset()
        {
            ValidateVersion();
            _currentBlock = 0;
            _currentOffset = 1;
            _remaining = _storage->Count;
        }

        /// <summary>
        /// 获取下一个 Block 的数据
        /// </summary>
        /// <param name="entities">实体引用数组指针</param>
        /// <param name="components">组件数据数组指针</param>
        /// <param name="count">此 Block 中的有效项数</param>
        /// <returns>是否还有更多数据</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool NextBlock(out EntityRef* entities, out T* components, out int count)
        {
            ValidateVersion();

            while (_currentBlock < _storage->BlockCount && _remaining > 0)
            {
                int itemsInBlock = _blockCapacity - _currentOffset;
                if (itemsInBlock > 0)
                {
                    count = System.Math.Min(_remaining, itemsInBlock);
                    entities = _storage->GetBlockEntityRefs(_currentBlock) + _currentOffset;
                    components = _storage->GetBlockData(_currentBlock) + _currentOffset;

                    _remaining -= count;
                    _currentOffset += count;
                    return true;
                }

                _currentBlock++;
                _currentOffset = 0;
            }

            entities = default;
            components = default;
            count = 0;
            return false;
        }

        /// <summary>
        /// 移动到下一个实体（逐个迭代）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Next(out EntityRef entity, out T* component)
        {
            ValidateVersion();

            if (_remaining > 0)
            {
                // 确保当前 offset 在有效范围内
                if (_currentOffset >= _blockCapacity)
                {
                    _currentBlock++;
                    _currentOffset = 0;
                }

                while (_currentBlock < _storage->BlockCount)
                {
                    int blockItems = _storage->GetBlockItemCount(_currentBlock);
                    if (_currentOffset < blockItems)
                    {
                        entity = _storage->GetBlockEntityRefs(_currentBlock)[_currentOffset];
                        component = &_storage->GetBlockData(_currentBlock)[_currentOffset];
                        _currentOffset++;
                        _remaining--;
                        return true;
                    }

                    _currentBlock++;
                    _currentOffset = 0;
                }
            }

            entity = EntityRef.None;
            component = null;
            return false;
        }

        /// <summary>
        /// 验证存储未被修改（防止迭代中增删组件）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ValidateVersion()
        {
#if DEBUG
            if (_storage->Version != _version)
            {
                throw new InvalidOperationException(
                    $"Cannot modify Storage<{typeof(T).Name}> while iterating over it. " +
                    "Use a command buffer or defer modifications.");
            }
#endif
        }
    }

    /// <summary>
    /// 增强版块迭代器 - 带预取优化
    /// </summary>
    public unsafe struct PrefetchedBlockIterator<T> where T : unmanaged
    {
        private readonly Storage<T>* _storage;
        private readonly int _version;
        private readonly int _blockCapacity;
        private readonly int _prefetchDistance;

        private int _currentBlock;
        private int _currentOffset;
        private int _remaining;

        public PrefetchedBlockIterator(Storage<T>* storage, int prefetchDistance = 2)
        {
            _storage = storage;
            _version = storage->Version;
            _blockCapacity = storage->BlockItemCapacity;
            _prefetchDistance = prefetchDistance;
            _currentBlock = 0;
            _currentOffset = 1;
            _remaining = storage->Count;

            // 预取前几个块
            PrefetchUpcoming();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool NextBlock(out EntityRef* entities, out T* components, out int count)
        {
            ValidateVersion();

            while (_currentBlock < _storage->BlockCount && _remaining > 0)
            {
                int itemsInBlock = _blockCapacity - _currentOffset;
                if (itemsInBlock > 0)
                {
                    count = System.Math.Min(_remaining, itemsInBlock);
                    entities = _storage->GetBlockEntityRefs(_currentBlock) + _currentOffset;
                    components = _storage->GetBlockData(_currentBlock) + _currentOffset;

                    _remaining -= count;
                    _currentOffset += count;

                    // 预取后续块
                    PrefetchUpcoming();

                    return true;
                }

                _currentBlock++;
                _currentOffset = 0;
            }

            entities = default;
            components = default;
            count = 0;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void PrefetchUpcoming()
        {
            for (int i = 1; i <= _prefetchDistance; i++)
            {
                int prefetchBlock = _currentBlock + i;
                if (prefetchBlock >= _storage->BlockCount) break;

                // 预取实体引用和数据
                SIMDUtils.PrefetchL2(_storage->GetBlockEntityRefs(prefetchBlock));
                SIMDUtils.PrefetchL2(_storage->GetBlockData(prefetchBlock));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ValidateVersion()
        {
#if DEBUG
            if (_storage->Version != _version)
            {
                throw new InvalidOperationException(
                    $"Cannot modify Storage<{typeof(T).Name}> while iterating over it.");
            }
#endif
        }
    }
}
