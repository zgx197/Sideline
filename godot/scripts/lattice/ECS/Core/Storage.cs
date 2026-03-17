// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

using System;
using System.Runtime.CompilerServices;
using Lattice.Core;

namespace Lattice.ECS.Core
{
    /// <summary>
    /// Block-based 高性能组件存储 - 超越 FrameSync 的内存效率与迭代性能
    /// 
    /// 核心设计：
    /// 1. Block 分层存储：每个 Block 包含固定数量组件，缓存友好
    /// 2. 稀疏数组存储全局索引(ushort)：Entity.Index → GlobalIndex
    /// 3. 版本号检测：迭代时修改检测，防止迭代中增删组件
    /// 4. 内存对齐：Block 内数据连续，支持 SIMD
    /// </summary>
    public unsafe struct Storage<T> where T : unmanaged
    {
        #region 常量

        /// <summary>默认 Block 容量（与 FrameSync 一致）</summary>
        public const int DefaultBlockCapacity = 128;

        /// <summary>Block 列表初始容量</summary>
        public const int InitialBlockListCapacity = 4;

        /// <summary>稀疏数组 Tombstone 标记</summary>
        public const ushort TOMBSTONE = 0;

        #endregion

        #region Block 结构

        /// <summary>
        /// 数据块 - 包含固定数量的实体引用和组件数据
        /// </summary>
        public struct Block
        {
            /// <summary>实体引用数组（紧密排列）</summary>
            public EntityRef* PackedHandles;

            /// <summary>组件数据数组（紧密排列）</summary>
            public T* PackedData;

            /// <summary>Block 是否已分配</summary>
            public bool IsAllocated => PackedData != null;
        }

        #endregion

        #region 字段

        // Block 管理
        private Block* _blocks;
        private int _blockCount;
        private int _blockCapacity;
        private int _blockItemCapacity;

        // 稀疏数组：Entity.Index → GlobalIndex (1-based, 0 = empty)
        private ushort* _sparse;
        private int _sparseCapacity;

        // 状态
        private int _count;
        private int _stride;
        private int _version;
        private int _componentTypeId;

        #endregion

        #region 属性

        /// <summary>当前组件数量（不包括索引0）</summary>
        public int Count => _count - 1;

        /// <summary>是否为空</summary>
        public bool IsEmpty => _count <= 1;

        /// <summary>当前版本号（用于迭代检测）</summary>
        public int Version => _version;

        /// <summary>Block 数量</summary>
        public int BlockCount => _blockCount;

        /// <summary>每个 Block 的容量</summary>
        public int BlockItemCapacity => _blockItemCapacity;

        /// <summary>组件类型ID</summary>
        public int ComponentTypeId => _componentTypeId;

        #endregion

        #region 生命周期

        /// <summary>
        /// 初始化存储
        /// </summary>
        /// <param name="maxEntities">最大实体数</param>
        /// <param name="blockCapacity">每个 Block 的组件容量</param>
        /// <param name="componentTypeId">组件类型ID</param>
        public void Initialize(int maxEntities, int blockCapacity = DefaultBlockCapacity, int componentTypeId = 0)
        {
            _sparseCapacity = maxEntities;
            _blockItemCapacity = System.Math.Max(blockCapacity, 16);
            _stride = sizeof(T);
            _componentTypeId = componentTypeId;
            _count = 1; // 索引0保留为无效值
            _version = 0;

            // 分配稀疏数组（初始化为0，即TOMBSTONE）
            _sparse = (ushort*)Alloc(sizeof(ushort) * maxEntities);
            for (int i = 0; i < maxEntities; i++)
                _sparse[i] = TOMBSTONE;

            // 分配 Block 列表
            _blockCapacity = InitialBlockListCapacity;
            _blockCount = 0;
            _blocks = (Block*)Alloc(sizeof(Block) * _blockCapacity);
            for (int i = 0; i < _blockCapacity; i++)
            {
                _blocks[i].PackedHandles = null;
                _blocks[i].PackedData = null;
            }
        }

        /// <summary>
        /// 释放所有内存
        /// </summary>
        public void Dispose()
        {
            if (_blocks != null)
            {
                // 释放所有 Block
                for (int i = 0; i < _blockCapacity; i++)
                {
                    if (_blocks[i].PackedHandles != null)
                        Free(_blocks[i].PackedHandles);
                    if (_blocks[i].PackedData != null)
                        Free(_blocks[i].PackedData);
                }
                Free(_blocks);
                _blocks = null;
            }

            if (_sparse != null)
            {
                Free(_sparse);
                _sparse = null;
            }

            _count = 0;
            _blockCount = 0;
        }

        #endregion

        #region 核心操作

        /// <summary>
        /// 添加组件 - O(1) 均摊
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(EntityRef entity, in T component)
        {
            int index = entity.Index;

#if DEBUG
            if ((uint)index >= (uint)_sparseCapacity)
                throw new ArgumentOutOfRangeException(nameof(entity), "Entity index out of range");
            if (_sparse[index] != TOMBSTONE)
                throw new InvalidOperationException($"Component already exists for entity {entity}");
#endif

            // 确保有空间
            if (_count >= (_blockCount * _blockItemCapacity))
                EnsureBlockSpace();

            int globalIndex = _count++;
            int block = globalIndex / _blockItemCapacity;
            int offset = globalIndex % _blockItemCapacity;

            // 写入数据
            Block* b = &_blocks[block];
            b->PackedHandles[offset] = entity;
            b->PackedData[offset] = component;

            // 更新稀疏数组
            _sparse[index] = (ushort)globalIndex;
            _version++;
        }

        /// <summary>
        /// 删除组件 - O(1)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Remove(EntityRef entity)
        {
            int index = entity.Index;
            ushort globalIndex = _sparse[index];

#if DEBUG
            if ((uint)index >= (uint)_sparseCapacity || globalIndex == TOMBSTONE)
                throw new InvalidOperationException($"Component not found for entity {entity}");
#endif

            int lastGlobalIndex = --_count;

            // 如果不是最后一个，与末尾交换
            if (globalIndex != lastGlobalIndex)
            {
                int lastBlock = lastGlobalIndex / _blockItemCapacity;
                int lastOffset = lastGlobalIndex % _blockItemCapacity;

                int block = globalIndex / _blockItemCapacity;
                int offset = globalIndex % _blockItemCapacity;

                EntityRef lastEntity = _blocks[lastBlock].PackedHandles[lastOffset];
                T lastComponent = _blocks[lastBlock].PackedData[lastOffset];

                _blocks[block].PackedHandles[offset] = lastEntity;
                _blocks[block].PackedData[offset] = lastComponent;

                // 更新被移动实体的稀疏索引
                _sparse[lastEntity.Index] = globalIndex;
            }

            _sparse[index] = TOMBSTONE;
            _version++;
        }

        /// <summary>
        /// 获取组件引用 - O(1)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T Get(EntityRef entity)
        {
            ushort globalIndex = _sparse[entity.Index];
#if DEBUG
            if ((uint)entity.Index >= (uint)_sparseCapacity || globalIndex == TOMBSTONE)
                throw new InvalidOperationException($"Component not found for entity {entity}");
#endif
            int block = globalIndex / _blockItemCapacity;
            int offset = globalIndex % _blockItemCapacity;
            return ref _blocks[block].PackedData[offset];
        }

        /// <summary>
        /// 获取组件指针 - O(1)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T* GetPointer(EntityRef entity)
        {
            ushort globalIndex = _sparse[entity.Index];
            if (globalIndex == TOMBSTONE) return null;
            int block = globalIndex / _blockItemCapacity;
            int offset = globalIndex % _blockItemCapacity;
            return &_blocks[block].PackedData[offset];
        }

        /// <summary>
        /// 尝试获取组件 - O(1)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGet(EntityRef entity, out T component)
        {
            int index = entity.Index;
            if ((uint)index < (uint)_sparseCapacity)
            {
                ushort globalIndex = _sparse[index];
                if (globalIndex != TOMBSTONE)
                {
                    int block = globalIndex / _blockItemCapacity;
                    int offset = globalIndex % _blockItemCapacity;
                    component = _blocks[block].PackedData[offset];
                    return true;
                }
            }
            component = default;
            return false;
        }

        /// <summary>
        /// 检查是否存在 - O(1)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Has(EntityRef entity)
        {
            uint index = (uint)entity.Index;
            return index < (uint)_sparseCapacity && _sparse[index] != TOMBSTONE;
        }

        #endregion

        #region Block 访问 API

        /// <summary>
        /// 获取指定 Block 的数据指针
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T* GetBlockData(int blockIndex)
        {
#if DEBUG
            if ((uint)blockIndex >= (uint)_blockCount)
                throw new ArgumentOutOfRangeException(nameof(blockIndex));
#endif
            return _blocks[blockIndex].PackedData;
        }

        /// <summary>
        /// 获取指定 Block 的实体引用指针
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public EntityRef* GetBlockEntityRefs(int blockIndex)
        {
#if DEBUG
            if ((uint)blockIndex >= (uint)_blockCount)
                throw new ArgumentOutOfRangeException(nameof(blockIndex));
#endif
            return _blocks[blockIndex].PackedHandles;
        }

        /// <summary>
        /// 获取 Block 中有效项的数量
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetBlockItemCount(int blockIndex)
        {
            if (blockIndex < 0 || blockIndex >= _blockCount) return 0;

            int startIndex = blockIndex * _blockItemCapacity;
            int endIndex = System.Math.Min(startIndex + _blockItemCapacity, _count);
            return System.Math.Max(0, endIndex - startIndex);
        }

        #endregion

        #region 内部辅助

        private void EnsureBlockSpace()
        {
            int requiredBlock = (_count / _blockItemCapacity) + 1;

            // 扩展 Block 列表
            if (requiredBlock > _blockCapacity)
            {
                int newCapacity = _blockCapacity * 2;
                while (newCapacity < requiredBlock)
                    newCapacity *= 2;

                var newBlocks = (Block*)Alloc(sizeof(Block) * newCapacity);

                // 复制旧数据
                Buffer.MemoryCopy(_blocks, newBlocks,
                    sizeof(Block) * newCapacity, sizeof(Block) * _blockCapacity);

                // 初始化新 Block
                for (int i = _blockCapacity; i < newCapacity; i++)
                {
                    newBlocks[i].PackedHandles = null;
                    newBlocks[i].PackedData = null;
                }

                Free(_blocks);
                _blocks = newBlocks;
                _blockCapacity = newCapacity;
            }

            // 分配新 Block
            while (_blockCount < requiredBlock)
            {
                int blockIndex = _blockCount++;
                _blocks[blockIndex].PackedHandles = (EntityRef*)Alloc(sizeof(EntityRef) * _blockItemCapacity);
                _blocks[blockIndex].PackedData = (T*)Alloc(sizeof(T) * _blockItemCapacity);

                // 初始化第一个元素（索引0保留为无效）
                if (blockIndex == 0)
                {
                    _blocks[0].PackedHandles[0] = EntityRef.None;
                    _blocks[0].PackedData[0] = default;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void* Alloc(int size)
        {
            return System.Runtime.InteropServices.Marshal.AllocHGlobal(size).ToPointer();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Free(void* ptr)
        {
            if (ptr != null)
                System.Runtime.InteropServices.Marshal.FreeHGlobal((IntPtr)ptr);
        }

        #endregion
    }
}
