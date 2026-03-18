// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

using System;
using System.Runtime.CompilerServices;
using Lattice.Core;

namespace Lattice.ECS.Core
{
    /// <summary>
    /// 组件块迭代器 - 批量遍历组件，最大化缓存命中率
    /// 
    /// 性能特性：
    /// 1. 按 Block 批量遍历，每 Block 内数据连续
    /// 2. 同时返回实体引用和组件指针，无需二次查找
    /// 3. 版本号检测，防止迭代中修改存储
    /// 4. 支持范围迭代（offset + count）
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
