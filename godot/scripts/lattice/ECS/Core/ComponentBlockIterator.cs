// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

// 本文件使用 unsafe 指针和块式遍历来实现高性能 ECS 组件迭代。
// 所有版本校验仅在 DEBUG 模式下启用，用于在开发期尽早发现边迭代边修改的问题。

using System;
using System.Runtime.CompilerServices;
using Lattice.Core;

namespace Lattice.ECS.Core
{
    /// <summary>
    /// 组件块迭代器。
    /// 通过按块批量返回实体引用与组件指针，尽量提升缓存命中率并降低循环中的分支与函数调用开销。
    /// </summary>
    public unsafe struct ComponentBlockIterator<T> where T : unmanaged, IComponent
    {
        private readonly Storage<T>* _storage;
        private readonly int _version;
        private readonly int _blockCapacity;

        private int _currentBlock;
        private int _currentOffset;
        private int _remaining;
        private int _startGlobalIndex;

        /// <summary>
        /// 创建一个覆盖整个存储区的迭代器实例。
        /// </summary>
        internal ComponentBlockIterator(Storage<T>* storage)
        {
            _storage = storage;
            _version = storage->Version;
            _blockCapacity = storage->BlockItemCapacity;
            _currentBlock = 0;
            _currentOffset = 1; // 跳过索引 0，保持与实体无效值约定一致。
            _remaining = storage->Count;
            _startGlobalIndex = 1;
        }

        /// <summary>
        /// 创建一个带偏移量与数量限制的范围迭代器。
        /// 该构造用于分页式扫描或仅遍历某一段连续组件数据。
        /// </summary>
        internal ComponentBlockIterator(Storage<T>* storage, int offset, int count)
        {
            _storage = storage;
            _version = storage->Version;
            _blockCapacity = storage->BlockItemCapacity;
            _startGlobalIndex = offset + 1;

            // 把输入范围收敛到当前存储真实有效范围内，避免越界。
            int clampedOffset = System.Math.Min(_startGlobalIndex, storage->Count);
            int clampedCount = System.Math.Max(0, System.Math.Min(count, storage->Count - clampedOffset));

            _currentBlock = clampedOffset / _blockCapacity;
            _currentOffset = clampedOffset % _blockCapacity;
            _remaining = clampedCount;
        }

        /// <summary>
        /// 重置迭代器到起始位置，重新遍历当前存储中的所有有效数据。
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
        /// 获取下一个连续数据块。
        /// 调用方可直接对返回的指针区间做顺序遍历或 SIMD 批处理。
        /// </summary>
        /// <param name="entities">当前块内实体引用数组的起始指针。</param>
        /// <param name="components">当前块内组件数组的起始指针。</param>
        /// <param name="count">当前块中可用元素数量。</param>
        /// <returns>若成功取得下一块数据则返回 <c>true</c>。</returns>
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
        /// 逐个返回下一个实体与组件指针。
        /// 适合单体逻辑较复杂、不适合整块批处理的调用场景。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Next(out EntityRef entity, out T* component)
        {
            ValidateVersion();

            if (_remaining > 0)
            {
                // 当前块已消耗完时，切到下一个块的起始位置。
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
        /// 在 DEBUG 模式下校验存储版本，防止遍历过程中对容器做结构性修改。
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
    /// 仅遍历 active 条目的组件迭代器。
    /// 相比通用块迭代器，它会跳过延迟删除中的条目，适合 Query 等查询热路径。
    /// </summary>
    public unsafe struct ActiveComponentIterator<T> where T : unmanaged, IComponent
    {
        private readonly Storage<T>* _storage;
        private readonly int _version;
        private readonly int _blockCapacity;
        private int _currentBlock;
        private int _currentOffset;
        private int _remainingActive;

        internal ActiveComponentIterator(Storage<T>* storage)
        {
            _storage = storage;
            _version = storage->Version;
            _blockCapacity = storage->BlockItemCapacity;
            _currentBlock = 0;
            _currentOffset = 1;
            _remainingActive = storage->UsedCount;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Next(out EntityRef entity, out T* component)
        {
            ValidateVersion();

            while (_remainingActive > 0 && _currentBlock < _storage->BlockCount)
            {
                if (_currentOffset >= _blockCapacity)
                {
                    _currentBlock++;
                    _currentOffset = 0;
                    continue;
                }

                int blockItems = _storage->GetBlockItemCount(_currentBlock);
                while (_currentOffset < blockItems)
                {
                    int globalIndex = (_currentBlock * _blockCapacity) + _currentOffset;
                    EntityRef currentEntity = _storage->GetBlockEntityRefs(_currentBlock)[_currentOffset];
                    T* currentComponent = &_storage->GetBlockData(_currentBlock)[_currentOffset];
                    _currentOffset++;

                    if (!_storage->IsDenseEntryActive(globalIndex))
                    {
                        continue;
                    }

                    _remainingActive--;
                    entity = currentEntity;
                    component = currentComponent;
                    return true;
                }

                _currentBlock++;
                _currentOffset = 0;
            }

            entity = EntityRef.None;
            component = null;
            return false;
        }

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
    /// 带预取优化的块迭代器。
    /// 在遍历当前块时尝试提前把后续块拉入缓存，以降低大批量顺序遍历的等待成本。
    /// </summary>
    public unsafe struct PrefetchedBlockIterator<T> where T : unmanaged, IComponent
    {
        private readonly Storage<T>* _storage;
        private readonly int _version;
        private readonly int _blockCapacity;
        private readonly int _prefetchDistance;

        private int _currentBlock;
        private int _currentOffset;
        private int _remaining;

        /// <summary>
        /// 创建带预取能力的块迭代器。
        /// <paramref name="prefetchDistance" /> 表示提前预取多少个后续块。
        /// </summary>
        public PrefetchedBlockIterator(Storage<T>* storage, int prefetchDistance = 2)
        {
            _storage = storage;
            _version = storage->Version;
            _blockCapacity = storage->BlockItemCapacity;
            _prefetchDistance = prefetchDistance;
            _currentBlock = 0;
            _currentOffset = 1;
            _remaining = storage->Count;

            // 在首次开始遍历前先预取一小段后续块。
            PrefetchUpcoming();
        }

        /// <summary>
        /// 获取下一块数据，并在成功返回后继续预取更靠后的块。
        /// </summary>
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

                    // 当前块返回后，立刻尝试预取后续块。
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

        /// <summary>
        /// 预取后续若干块的实体引用与组件数据。
        /// 该优化仅在顺序遍历、大容量存储场景下才更容易带来收益。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void PrefetchUpcoming()
        {
            for (int i = 1; i <= _prefetchDistance; i++)
            {
                int prefetchBlock = _currentBlock + i;
                if (prefetchBlock >= _storage->BlockCount)
                {
                    break;
                }

                SIMDUtils.PrefetchL2(_storage->GetBlockEntityRefs(prefetchBlock));
                SIMDUtils.PrefetchL2(_storage->GetBlockData(prefetchBlock));
            }
        }

        /// <summary>
        /// 在 DEBUG 模式下校验预取迭代器绑定的存储版本。
        /// </summary>
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
