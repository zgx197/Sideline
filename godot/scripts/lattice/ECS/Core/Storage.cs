// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

using System;
using System.Runtime.CompilerServices;
using Lattice.Core;

namespace Lattice.ECS.Core
{
    /// <summary>
    /// 存储标志
    /// </summary>
    [Flags]
    public enum StorageFlags : ushort
    {
        /// <summary>无特殊标志</summary>
        None = 0,

        /// <summary>单例组件（只允许一个实例）</summary>
        Singleton = 1 << 0,

        /// <summary>延迟删除（Remove 时标记，需要 Commit 才真正删除）</summary>
        DeferredRemoval = 1 << 1,
    }

    /// <summary>
    /// Block-based 高性能组件存储 - 超越 FrameSync 的内存效率与迭代性能
    /// 
    /// 核心设计：
    /// 1. Block 分层存储：每个 Block 包含固定数量组件，缓存友好
    /// 2. 稀疏数组存储全局索引(ushort)：Entity.Index → GlobalIndex
    /// 3. 版本号检测：迭代时修改检测，防止迭代中增删组件
    /// 4. 内存对齐：Block 内数据连续，支持 SIMD
    /// 5. 单例支持：可选单例组件模式
    /// 6. 延迟删除：支持预测回滚模型的延迟删除
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

        // 内存分配器
        private Allocator* _allocator;
        private bool _ownsAllocator;

        // 标志和特殊功能
        private StorageFlags _flags;
        private ushort _singletonSparse;  // 单例的全局索引（0 = 无单例）
        private int _pendingRemoval;      // 待删除计数（延迟删除模式）

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

        /// <summary>存储标志</summary>
        public StorageFlags Flags => _flags;

        /// <summary>是否为单例存储</summary>
        public bool IsSingleton => (_flags & StorageFlags.Singleton) != 0;

        /// <summary>是否启用延迟删除</summary>
        public bool IsDeferredRemoval => (_flags & StorageFlags.DeferredRemoval) != 0;

        /// <summary>待删除组件数量（延迟删除模式）</summary>
        public int PendingRemovalCount => _pendingRemoval;

        /// <summary>实际使用中的组件数量（减去待删除）</summary>
        public int UsedCount => Count - _pendingRemoval;

        /// <summary>单例实体引用（仅单例模式有效）</summary>
        public EntityRef SingletonEntity
        {
            get
            {
                if (!IsSingleton || _singletonSparse == 0)
                    return EntityRef.None;
                return GetEntityRefByGlobalIndex(_singletonSparse);
            }
        }

        #endregion

        #region 生命周期

        /// <summary>
        /// 初始化存储（使用内部Allocator）
        /// </summary>
        public void Initialize(int maxEntities, int blockCapacity = DefaultBlockCapacity, int componentTypeId = 0)
        {
            Initialize(maxEntities, StorageFlags.None, blockCapacity, componentTypeId);
        }

        /// <summary>
        /// 初始化存储（使用内部Allocator，带标志）
        /// </summary>
        public void Initialize(int maxEntities, StorageFlags flags, int blockCapacity = DefaultBlockCapacity, int componentTypeId = 0)
        {
            // 使用 Marshal 分配非托管内存来存储 Allocator
            var allocatorPtr = (Allocator*)System.Runtime.InteropServices.Marshal.AllocHGlobal(sizeof(Allocator)).ToPointer();
            *allocatorPtr = new Allocator();
            Initialize(maxEntities, allocatorPtr, flags, blockCapacity, componentTypeId);
            _ownsAllocator = true;
        }

        /// <summary>
        /// 初始化存储（使用外部Allocator）
        /// </summary>
        public void Initialize(int maxEntities, Allocator* allocator, int blockCapacity = DefaultBlockCapacity, int componentTypeId = 0)
        {
            Initialize(maxEntities, allocator, StorageFlags.None, blockCapacity, componentTypeId);
        }

        /// <summary>
        /// 初始化存储（使用外部Allocator，带标志）
        /// </summary>
        public void Initialize(int maxEntities, Allocator* allocator, StorageFlags flags, int blockCapacity = DefaultBlockCapacity, int componentTypeId = 0)
        {
            _sparseCapacity = maxEntities;
            _blockItemCapacity = System.Math.Max(blockCapacity, 16);
            _stride = sizeof(T);
            _componentTypeId = componentTypeId;
            _count = 1; // 索引0保留为无效值
            _version = 0;
            _allocator = allocator;
            _ownsAllocator = false;
            _flags = flags;
            _singletonSparse = 0;
            _pendingRemoval = 0;

            // 分配稀疏数组（初始化为0，即TOMBSTONE）
            _sparse = (ushort*)_allocator->AllocAndClear(sizeof(ushort) * maxEntities);

            // 分配 Block 列表
            _blockCapacity = InitialBlockListCapacity;
            _blockCount = 0;
            _blocks = (Block*)_allocator->Alloc(sizeof(Block) * _blockCapacity);
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
            if (_ownsAllocator && _allocator != null)
            {
                _allocator->Dispose();
                System.Runtime.InteropServices.Marshal.FreeHGlobal((IntPtr)_allocator);
            }

            _blocks = null;
            _sparse = null;
            _count = 0;
            _blockCount = 0;
            _allocator = null;
        }

        #endregion

        #region 预测回滚支持（快照/恢复）

        /// <summary>
        /// 计算快照大小（字节）
        /// </summary>
        public int GetSnapshotSize()
        {
            // 稀疏数组 + 组件数据 + 元数据
            return sizeof(ushort) * _sparseCapacity +  // 稀疏数组
                   sizeof(T) * _count +              // 组件数据（已使用部分）
                   sizeof(int) * 4;                   // _count, _version, _singletonSparse, _pendingRemoval
        }

        /// <summary>
        /// 创建快照（用于预测回滚）
        /// </summary>
        public void WriteSnapshot(byte* buffer, int bufferSize)
        {
            byte* ptr = buffer;

            // 写入元数据
            *(int*)ptr = _count; ptr += sizeof(int);
            *(int*)ptr = _version; ptr += sizeof(int);
            *(ushort*)ptr = _singletonSparse; ptr += sizeof(ushort);
            *(ushort*)ptr = (ushort)_pendingRemoval; ptr += sizeof(ushort);

            // 写入稀疏数组（只写入有效部分）
            int sparseBytes = sizeof(ushort) * _sparseCapacity;
            Buffer.MemoryCopy(_sparse, ptr, sparseBytes, sparseBytes);
            ptr += sparseBytes;

            // 写入组件数据（按 Block 写入）
            for (int i = 0; i < _blockCount; i++)
            {
                int blockItems = System.Math.Min(_blockItemCapacity, _count - i * _blockItemCapacity);
                if (blockItems <= 0) break;

                int dataBytes = sizeof(T) * blockItems;
                Buffer.MemoryCopy(_blocks[i].PackedData, ptr, dataBytes, dataBytes);
                ptr += dataBytes;
            }
        }

        /// <summary>
        /// 从快照恢复（用于回滚）
        /// </summary>
        public void ReadSnapshot(byte* buffer, int bufferSize)
        {
            byte* ptr = buffer;

            // 读取元数据
            int savedCount = *(int*)ptr; ptr += sizeof(int);
            int savedVersion = *(int*)ptr; ptr += sizeof(int);
            ushort savedSingletonSparse = *(ushort*)ptr; ptr += sizeof(ushort);
            ushort savedPendingRemoval = *(ushort*)ptr; ptr += sizeof(ushort);

            // 确保容量足够
            EnsureEntityCapacity(savedCount);

            // 恢复元数据
            _count = savedCount;
            _version = savedVersion;
            _singletonSparse = savedSingletonSparse;
            _pendingRemoval = savedPendingRemoval;

            // 恢复稀疏数组
            int sparseBytes = sizeof(ushort) * _sparseCapacity;
            Buffer.MemoryCopy(ptr, _sparse, sparseBytes, sparseBytes);
            ptr += sparseBytes;

            // 恢复组件数据
            for (int i = 0; i < _blockCount; i++)
            {
                int blockItems = System.Math.Min(_blockItemCapacity, _count - i * _blockItemCapacity);
                if (blockItems <= 0) break;

                int dataBytes = sizeof(T) * blockItems;
                Buffer.MemoryCopy(ptr, _blocks[i].PackedData, dataBytes, dataBytes);
                ptr += dataBytes;
            }
        }

        /// <summary>
        /// 确保有足够的实体容量
        /// </summary>
        private void EnsureEntityCapacity(int requiredCount)
        {
            int requiredBlocks = (requiredCount + _blockItemCapacity - 1) / _blockItemCapacity;

            // 扩展 Block 列表
            if (requiredBlocks > _blockCapacity)
            {
                int newCapacity = _blockCapacity;
                while (newCapacity < requiredBlocks)
                    newCapacity *= 2;

                var newBlocks = (Block*)_allocator->Alloc(sizeof(Block) * newCapacity);
                Buffer.MemoryCopy(_blocks, newBlocks, sizeof(Block) * newCapacity, sizeof(Block) * _blockCapacity);

                for (int i = _blockCapacity; i < newCapacity; i++)
                {
                    newBlocks[i].PackedHandles = null;
                    newBlocks[i].PackedData = null;
                }

                _blocks = newBlocks;
                _blockCapacity = newCapacity;
            }

            // 分配缺少的 Blocks
            while (_blockCount < requiredBlocks)
            {
                int blockIndex = _blockCount++;
                _blocks[blockIndex].PackedHandles = (EntityRef*)_allocator->Alloc(sizeof(EntityRef) * _blockItemCapacity);
                _blocks[blockIndex].PackedData = (T*)_allocator->Alloc(sizeof(T) * _blockItemCapacity);
            }
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

            // 单例检查
            if (IsSingleton)
            {
                if (_singletonSparse != 0)
                {
                    throw new InvalidOperationException(
                        $"Cannot add multiple instances of singleton component {typeof(T).Name}. " +
                        $"Existing entity: {GetEntityRefByGlobalIndex(_singletonSparse)}");
                }
            }

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

            // 更新单例索引
            if (IsSingleton)
            {
                _singletonSparse = (ushort)globalIndex;
            }

            _version++;
        }

        /// <summary>
        /// 删除组件 - O(1)
        /// 如果启用延迟删除，则标记为待删除，需要调用 CommitRemovals 才真正删除
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

            // 延迟删除模式：只标记，不立即删除
            if (IsDeferredRemoval)
            {
                _pendingRemoval++;

                // 清除单例索引
                if (IsSingleton && _singletonSparse == globalIndex)
                {
                    _singletonSparse = 0;
                }

                _version++;
                return;
            }

            // 立即删除模式
            RemoveImmediate(index, globalIndex);
        }

        /// <summary>
        /// 立即删除组件（内部方法）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RemoveImmediate(int entityIndex, ushort globalIndex)
        {
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

                // 更新单例索引（如果被移动的是单例）
                if (IsSingleton && _singletonSparse == lastGlobalIndex)
                {
                    _singletonSparse = globalIndex;
                }
            }

            _sparse[entityIndex] = TOMBSTONE;

            // 清除单例索引
            if (IsSingleton && _singletonSparse == globalIndex)
            {
                _singletonSparse = 0;
            }

            _version++;
        }

        /// <summary>
        /// 提交所有待删除（延迟删除模式）
        /// 注意：此实现需要 Frame 层维护待删除列表来配合
        /// </summary>
        public void CommitRemovals(Span<EntityRef> entitiesToRemove)
        {
            if (!IsDeferredRemoval || entitiesToRemove.IsEmpty)
                return;

            foreach (var entity in entitiesToRemove)
            {
                int index = entity.Index;
                if ((uint)index < (uint)_sparseCapacity)
                {
                    ushort globalIndex = _sparse[index];
                    if (globalIndex != TOMBSTONE)
                    {
                        RemoveImmediate(index, globalIndex);
                    }
                }
            }

            _pendingRemoval = 0;
        }

        /// <summary>
        /// 标记组件为待删除（延迟删除模式）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void MarkForRemoval(EntityRef entity)
        {
            if (!IsDeferredRemoval)
            {
                Remove(entity);
                return;
            }

            int index = entity.Index;
            ushort globalIndex = _sparse[index];

#if DEBUG
            if ((uint)index >= (uint)_sparseCapacity || globalIndex == TOMBSTONE)
                throw new InvalidOperationException($"Component not found for entity {entity}");
#endif

            _pendingRemoval++;

            // 清除单例索引
            if (IsSingleton && _singletonSparse == globalIndex)
            {
                _singletonSparse = 0;
            }

            _version++;
        }

        /// <summary>
        /// 获取单例组件指针（仅单例模式有效）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T* GetSingletonPointer()
        {
            if (!IsSingleton || _singletonSparse == 0)
                return null;

            int block = _singletonSparse / _blockItemCapacity;
            int offset = _singletonSparse % _blockItemCapacity;
            return &_blocks[block].PackedData[offset];
        }

        /// <summary>
        /// 获取单例组件引用（仅单例模式有效）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T GetSingleton()
        {
            if (!IsSingleton || _singletonSparse == 0)
                throw new InvalidOperationException($"Singleton component {typeof(T).Name} not found");

            int block = _singletonSparse / _blockItemCapacity;
            int offset = _singletonSparse % _blockItemCapacity;
            return ref _blocks[block].PackedData[offset];
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

        /// <summary>
        /// 通过全局索引获取实体引用（内部辅助）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private EntityRef GetEntityRefByGlobalIndex(int globalIndex)
        {
#if DEBUG
            if (globalIndex <= 0 || globalIndex >= _count)
                throw new ArgumentOutOfRangeException(nameof(globalIndex));
#endif
            int block = globalIndex / _blockItemCapacity;
            int offset = globalIndex % _blockItemCapacity;
            return _blocks[block].PackedHandles[offset];
        }

        private void EnsureBlockSpace()
        {
            int requiredBlock = (_count / _blockItemCapacity) + 1;

            // 扩展 Block 列表
            if (requiredBlock > _blockCapacity)
            {
                int newCapacity = _blockCapacity * 2;
                while (newCapacity < requiredBlock)
                    newCapacity *= 2;

                var newBlocks = (Block*)_allocator->Alloc(sizeof(Block) * newCapacity);

                // 复制旧数据
                Buffer.MemoryCopy(_blocks, newBlocks,
                    sizeof(Block) * newCapacity, sizeof(Block) * _blockCapacity);

                // 初始化新 Block
                for (int i = _blockCapacity; i < newCapacity; i++)
                {
                    newBlocks[i].PackedHandles = null;
                    newBlocks[i].PackedData = null;
                }

                _blocks = newBlocks;
                _blockCapacity = newCapacity;
            }

            // 分配新 Block
            while (_blockCount < requiredBlock)
            {
                int blockIndex = _blockCount++;
                _blocks[blockIndex].PackedHandles = (EntityRef*)_allocator->Alloc(sizeof(EntityRef) * _blockItemCapacity);
                _blocks[blockIndex].PackedData = (T*)_allocator->Alloc(sizeof(T) * _blockItemCapacity);

                // 初始化第一个元素（索引0保留为无效）
                if (blockIndex == 0)
                {
                    _blocks[0].PackedHandles[0] = EntityRef.None;
                    _blocks[0].PackedData[0] = default;
                }
            }
        }

        #endregion
    }
}
