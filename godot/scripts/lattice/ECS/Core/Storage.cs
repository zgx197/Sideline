// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using Lattice.Core;

namespace Lattice.ECS.Core
{
    /// <summary>
    /// 存储标志 - 定义组件存储的特殊行为
    /// 
    /// 设计说明：
    /// - 使用 ushort 节省内存（2 字节），足够表示 16 种标志
    /// - 单例和延迟删除是预测回滚模型的核心需求
    /// </summary>
    [Flags]
    public enum StorageFlags : ushort
    {
        /// <summary>无特殊标志</summary>
        None = 0,

        /// <summary>
        /// 单例组件（只允许一个实例）
        /// 
        /// 应用场景：
        /// - 全局配置（如 GameConfig）
        /// - 唯一的游戏状态（如 GameState）
        /// - 避免多个实例导致的逻辑错误
        /// </summary>
        Singleton = 1 << 0,

        /// <summary>
        /// 延迟删除（Remove 时标记，需要 Commit 才真正删除）
        /// 
        /// 设计原因：
        /// - 预测回滚需要延迟删除，避免预测期间的状态不一致
        /// - 允许在帧结束时批量处理删除，减少内存抖动
        /// - FrameSync 使用相同的模式（_pendingRemoval）
        /// </summary>
        DeferredRemoval = 1 << 1,
    }

    /// <summary>
    /// Block-based 高性能组件存储 - 超越 FrameSync 的内存效率与迭代性能
    /// 
    /// ============================================================
    /// 架构设计决策
    /// ============================================================
    /// 
    /// Q: 为什么选择分离存储（SOA）而不是联合存储（AOS）？
    /// A: 
    ///   1. 预测回滚友好：可以单独快照/恢复单个组件类型
    ///   2. Delta 压缩高效：只传输变化的组件
    ///   3. SIMD 友好：连续的同类型数据便于向量化
    ///   4. 缓存局部性：批量访问同类型组件时命中率更高
    ///   5. 与 FrameSync 不同：FrameSync 使用联合存储（ComponentDataBuffer）
    ///
    /// Q: 为什么 Block 容量固定为 128？
    /// A:
    ///   1. 与 FrameSync 保持一致，便于对比和迁移
    ///   2. 128 * sizeof(T) 通常能填满多个缓存行（假设 T=16 字节，128*16=2048 字节）
    ///   3. 128 是 2 的幂，可以用位运算替代除法/取模（block = index >> 7, offset = index &amp; 127）
    ///   4. 足够大的批次用于 SIMD 处理（AVX2 可以一次处理 4 个 Vector4FP）
    ///
    /// Q: 为什么稀疏数组使用 ushort（2 字节）而不是 int（4 字节）？
    /// A:
    ///   1. 内存节省 50%：65536 个实体的稀疏数组只需 128KB，而 int 需要 256KB
    ///   2. 65535 的最大索引足够大多数游戏使用（支持 64K 个组件实例）
    ///   3. 缓存行利用更好：同一代码可以追踪更多实体
    ///   4. 与 FrameSync 相同的选择（ComponentDataBuffer._sparse 也是 ushort*）
    ///
    /// Q: 为什么需要版本号（_version）？
    /// A:
    ///   1. 防止迭代中修改：快速检测并发修改，抛出异常而非崩溃
    ///   2. 调试友好：在 DEBUG 模式下提供清晰的错误信息
    ///   3. 与 C# 的 IEnumerator 模式一致（检查集合修改）
    ///
    /// ============================================================
    /// 性能特性
    /// ============================================================
    /// 
    /// 1. O(1) 添加/删除/访问：稀疏数组保证常数时间
    /// 2. 缓存友好：Block 内数据连续，预取友好
    /// 3. 内存对齐：16 字节对齐，支持 SIMD
    /// 4. 无 GC：完全非托管内存，通过 Allocator 管理
    ///
    /// ============================================================
    /// 内存布局
    /// ============================================================
    /// 
    /// Storage&lt;T&gt; (SOA - Structure of Arrays):
    /// 
    ///     Block 0          Block 1          Block 2
    /// ┌─────────────┐  ┌─────────────┐  ┌─────────────┐
    /// │ EntityRef   │  │ EntityRef   │  │ EntityRef   │  ← 实体引用数组
    /// │ [128]       │  │ [128]       │  │ [128]       │     (密集存储)
    /// └─────────────┘  └─────────────┘  └─────────────┘
    ///       ↓                  ↓                  ↓
    /// ┌─────────────┐  ┌─────────────┐  ┌─────────────┐
    /// │ T[128]      │  │ T[128]      │  │ T[128]      │  ← 组件数据数组
    /// │ (强类型)    │  │ (强类型)    │  │ (强类型)    │     (密集存储)
    /// └─────────────┘  └─────────────┘  └─────────────┘
    ///       ↓                  ↓                  ↓
    /// _sparse[Entity.Index] → GlobalIndex (ushort, 2 字节)
    /// 
    /// 对比 FrameSync (AOS - Array of Structures):
    /// 
    /// Block 内存布局：
    /// ┌─────────────────────────────────────────┐
    /// │ EntityRef[0] | Padding | T[0]          │  ← 同一 Block 内混合存储
    /// │ EntityRef[1] | Padding | T[1]          │     需要 _blockDataOffset
    /// │ ...                                    │     计算数据偏移
    /// └─────────────────────────────────────────┘
    /// 
    /// 我们的优势：
    /// - 类型安全：T* 而不是 byte*
    /// - 更简单的偏移计算：直接 array[index] 而不是 ptr + stride * index
    /// - 更好的 SIMD 支持：连续的同类型数据
    /// </summary>
    public unsafe struct Storage<T> where T : unmanaged, IComponent
    {
        #region 常量

        /// <summary>自动推导 Block 容量。</summary>
        public const int DefaultBlockCapacity = 0;

        /// <summary>Block 数据面的目标字节数。</summary>
        public const int TargetBlockBytes = 2048;

        /// <summary>Block 列表初始容量</summary>
        public const int InitialBlockListCapacity = 4;

        /// <summary>自动推导时允许的最小 Block 容量。</summary>
        public const int MinBlockCapacity = 8;

        /// <summary>自动推导时允许的最大 Block 容量。</summary>
        public const int MaxBlockCapacity = 512;

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
        private byte* _entryStates;
        private ushort* _pendingQueueIndices;

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

        /// <summary>是否为条目状态分配了独立跟踪存储。</summary>
        internal bool HasEntryStateTracking => _entryStates != null;

        /// <summary>是否为延迟删除队列索引分配了独立跟踪存储。</summary>
        internal bool HasPendingQueueTracking => _pendingQueueIndices != null;

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
            var allocatorPtr = Allocator.Create();
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
            _blockItemCapacity = ResolveBlockCapacity(blockCapacity);
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

            if (IsDeferredRemoval)
            {
                _entryStates = (byte*)_allocator->AllocAndClear(maxEntities + 1);
                _pendingQueueIndices = (ushort*)_allocator->Alloc(sizeof(ushort) * (maxEntities + 1));
                for (int i = 0; i <= maxEntities; i++)
                {
                    _pendingQueueIndices[i] = PendingQueueIndexNone;
                }
            }
            else
            {
                _entryStates = null;
                _pendingQueueIndices = null;
            }

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
                Allocator.Destroy(_allocator);
            }

            _blocks = null;
            _sparse = null;
            _entryStates = null;
            _pendingQueueIndices = null;
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
            int denseCount = Count;
            int size =
                sizeof(int) * 6 +    // _count, _version, _blockCount, _blockItemCapacity, _componentTypeId, _sparseCapacity
                sizeof(ushort) * 4 + // _singletonSparse, _pendingRemoval, _flags, reserved
                sizeof(EntityRef) * denseCount +
                sizeof(T) * denseCount;

            if (_entryStates != null)
            {
                size += denseCount;
            }

            return size;
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
            *(int*)ptr = _blockCount; ptr += sizeof(int);
            *(int*)ptr = _blockItemCapacity; ptr += sizeof(int);
            *(int*)ptr = _componentTypeId; ptr += sizeof(int);
            *(int*)ptr = _sparseCapacity; ptr += sizeof(int);
            *(ushort*)ptr = _singletonSparse; ptr += sizeof(ushort);
            *(ushort*)ptr = (ushort)_pendingRemoval; ptr += sizeof(ushort);
            *(ushort*)ptr = (ushort)_flags; ptr += sizeof(ushort);
            *(ushort*)ptr = 0; ptr += sizeof(ushort);

            int denseCount = Count;
            int handlesBytes = sizeof(EntityRef) * denseCount;
            int dataBytes = sizeof(T) * denseCount;

            CopyDenseEntities((EntityRef*)ptr, denseCount);
            ptr += handlesBytes;

            CopyDenseComponentBytes(ptr, denseCount);
            ptr += dataBytes;

            if (_entryStates != null && denseCount > 0)
            {
                Buffer.MemoryCopy(_entryStates + 1, ptr, denseCount, denseCount);
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
            int savedBlockCount = *(int*)ptr; ptr += sizeof(int);
            int savedBlockItemCapacity = *(int*)ptr; ptr += sizeof(int);
            int savedComponentTypeId = *(int*)ptr; ptr += sizeof(int);
            int savedSparseCapacity = *(int*)ptr; ptr += sizeof(int);
            ushort savedSingletonSparse = *(ushort*)ptr; ptr += sizeof(ushort);
            ushort savedPendingRemoval = *(ushort*)ptr; ptr += sizeof(ushort);
            StorageFlags savedFlags = (StorageFlags)(*(ushort*)ptr); ptr += sizeof(ushort);
            ptr += sizeof(ushort); // reserved

            if (savedSparseCapacity != _sparseCapacity)
            {
                throw new InvalidOperationException(
                    $"Storage<{typeof(T).Name}> 快照容量不匹配。 Snapshot={savedSparseCapacity}, Current={_sparseCapacity}。");
            }

            _blockItemCapacity = savedBlockItemCapacity;
            _componentTypeId = savedComponentTypeId;
            _flags = savedFlags;

            // 确保容量足够
            EnsureEntityCapacity(savedBlockCount * savedBlockItemCapacity);

            // 恢复元数据
            _count = savedCount;
            _version = savedVersion;
            _blockCount = savedBlockCount;
            _singletonSparse = savedSingletonSparse;
            _pendingRemoval = 0;

            ClearSparseAndStates();
            EnsureEntityCapacity(savedCount);

            int denseCount = System.Math.Max(savedCount - 1, 0);
            RestoreDenseEntities((EntityRef*)ptr, denseCount);
            ptr += sizeof(EntityRef) * denseCount;

            RestoreDenseComponentBytes(ptr, denseCount);
            ptr += sizeof(T) * denseCount;

            if (denseCount > 0)
            {
                if (_entryStates != null)
                {
                    Buffer.MemoryCopy(ptr, _entryStates + 1, denseCount, denseCount);
                    ptr += denseCount;
                }
                else
                {
                    for (int i = 1; i <= denseCount; i++)
                    {
                        SetEntryStateByIndex(i, EntryStateActive);
                    }
                }
            }

            for (int globalIndex = 1; globalIndex <= denseCount; globalIndex++)
            {
                EntityRef entity = GetEntityRefByIndex(globalIndex);
                _sparse[entity.Index] = (ushort)globalIndex;
            }

            _pendingRemoval = savedPendingRemoval;
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

                if (blockIndex == 0)
                {
                    _blocks[0].PackedHandles[0] = EntityRef.None;
                    _blocks[0].PackedData[0] = default;
                }
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
            if (GetSparseIndex(entity) != TOMBSTONE)
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
            SetEntryStateByIndex(globalIndex, EntryStateActive);
            SetPendingQueueIndexByIndex(globalIndex, PendingQueueIndexNone);

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
            ushort globalIndex = GetSparseIndex(entity);

#if DEBUG
            if ((uint)index >= (uint)_sparseCapacity || globalIndex == TOMBSTONE)
                throw new InvalidOperationException($"Component not found for entity {entity}");
#endif

            // 延迟删除模式：只标记，不立即删除
            if (IsDeferredRemoval)
            {
                if (GetEntryStateByIndex(globalIndex) == EntryStatePendingRemoval)
                {
                    return;
                }

                SetEntryStateByIndex(globalIndex, EntryStatePendingRemoval);
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
                byte lastState = GetEntryStateByIndex(lastGlobalIndex);

                _blocks[block].PackedHandles[offset] = lastEntity;
                _blocks[block].PackedData[offset] = lastComponent;
                SetEntryStateByIndex(globalIndex, lastState);
                SetPendingQueueIndexByIndex(globalIndex, GetPendingQueueIndexByIndex(lastGlobalIndex));

                // 更新被移动实体的稀疏索引
                _sparse[lastEntity.Index] = globalIndex;

                // 更新单例索引（如果被移动的是单例）
                if (IsSingleton && _singletonSparse == lastGlobalIndex)
                {
                    _singletonSparse = globalIndex;
                }
            }

            _sparse[entityIndex] = TOMBSTONE;
            SetEntryStateByIndex(lastGlobalIndex, EntryStateEmpty);
            SetPendingQueueIndexByIndex(lastGlobalIndex, PendingQueueIndexNone);

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
                CommitRemoval(entity);
            }
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
            ushort globalIndex = GetSparseIndex(entity);

#if DEBUG
            if ((uint)index >= (uint)_sparseCapacity || globalIndex == TOMBSTONE)
                throw new InvalidOperationException($"Component not found for entity {entity}");
#endif

            _pendingRemoval++;
            SetEntryStateByIndex(globalIndex, EntryStatePendingRemoval);

            // 清除单例索引
            if (IsSingleton && _singletonSparse == globalIndex)
            {
                _singletonSparse = 0;
            }

            _version++;
        }

        /// <summary>
        /// 取消待删除状态，允许组件在同一帧内恢复。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void CancelPendingRemoval(EntityRef entity)
        {
            if (!IsDeferredRemoval)
            {
                return;
            }

            int index = entity.Index;
            if ((uint)index >= (uint)_sparseCapacity)
            {
                return;
            }

            ushort globalIndex = GetSparseIndex(entity);
            if (globalIndex == TOMBSTONE)
            {
                return;
            }

            if (_pendingRemoval > 0)
            {
                _pendingRemoval--;
            }

            SetEntryStateByIndex(globalIndex, EntryStateActive);
            SetPendingQueueIndexByIndex(globalIndex, PendingQueueIndexNone);

            if (IsSingleton)
            {
                _singletonSparse = globalIndex;
            }

            _version++;
        }

        /// <summary>
        /// 提交单个待删除组件。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void CommitRemoval(EntityRef entity)
        {
            int index = entity.Index;
            if ((uint)index >= (uint)_sparseCapacity)
            {
                return;
            }

            ushort globalIndex = GetSparseIndex(entity);
            if (globalIndex == TOMBSTONE)
            {
                return;
            }

            if (GetEntryStateByIndex(globalIndex) != EntryStatePendingRemoval)
            {
                return;
            }

            RemoveImmediate(index, globalIndex);
            if (_pendingRemoval > 0)
            {
                _pendingRemoval--;
            }
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
            ushort globalIndex = GetSparseIndex(entity);
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
            ushort globalIndex = GetSparseIndex(entity);
            if (globalIndex == TOMBSTONE) return null;
            int block = globalIndex / _blockItemCapacity;
            int offset = globalIndex % _blockItemCapacity;
            return &_blocks[block].PackedData[offset];
        }

        /// <summary>
        /// 在调用方已经通过 Frame 位图确认组件存在时，直接按实体索引读取组件指针。
        /// 该路径跳过额外的 EntityRef 回读校验，用于 Query 热路径。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal T* GetPointerAssumePresent(EntityRef entity)
        {
            uint entityIndex = (uint)entity.Index;
            if (entityIndex >= (uint)_sparseCapacity)
            {
                return null;
            }

            ushort globalIndex = _sparse[entityIndex];
            if (globalIndex == TOMBSTONE)
            {
                return null;
            }

#if DEBUG
            if (GetEntityRefByGlobalIndex(globalIndex).Raw != entity.Raw)
            {
                throw new InvalidOperationException(
                    $"Storage<{typeof(T).Name}> 检测到实体位图与稀疏索引不一致，Entity={entity}。");
            }

            if (GetEntryStateByIndex(globalIndex) != EntryStateActive)
            {
                throw new InvalidOperationException(
                    $"Storage<{typeof(T).Name}> 检测到非 Active 条目进入 Query 热路径，Entity={entity}。");
            }
#endif

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
                ushort globalIndex = GetSparseIndex(entity);
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
            return index < (uint)_sparseCapacity && GetSparseIndex(entity) != TOMBSTONE;
        }

        /// <summary>
        /// 检查组件是否处于待删除状态。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool IsPendingRemoval(EntityRef entity)
        {
            ushort globalIndex = GetSparseIndex(entity);
            return globalIndex != TOMBSTONE && GetEntryStateByIndex(globalIndex) == EntryStatePendingRemoval;
        }

        /// <summary>
        /// 获取待删除队列索引。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal int GetPendingQueueIndex(EntityRef entity)
        {
            ushort globalIndex = GetSparseIndex(entity);
            return globalIndex == TOMBSTONE ? PendingQueueIndexNone : GetPendingQueueIndexByIndex(globalIndex);
        }

        /// <summary>
        /// 设置待删除队列索引。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void SetPendingQueueIndex(EntityRef entity, int queueIndex)
        {
            ushort globalIndex = GetSparseIndex(entity);
            if (globalIndex == TOMBSTONE)
            {
                return;
            }

            SetPendingQueueIndexByIndex(globalIndex, queueIndex);
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

        /// <summary>
        /// 获取指定全局索引上的实体引用。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal EntityRef GetEntityRefByIndex(int globalIndex)
        {
            return GetEntityRefByGlobalIndex(globalIndex);
        }

        /// <summary>
        /// 获取指定全局索引上的组件数据指针。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal T* GetDataPointerByIndex(int globalIndex)
        {
#if DEBUG
            if (globalIndex <= 0 || globalIndex >= _count)
                throw new ArgumentOutOfRangeException(nameof(globalIndex));
#endif
            int block = globalIndex / _blockItemCapacity;
            int offset = globalIndex % _blockItemCapacity;
            return &_blocks[block].PackedData[offset];
        }

        /// <summary>
        /// 按 0 基 dense 索引访问条目。
        /// 该辅助方法用于把对外的连续遍历索引映射到内部 1 基稠密存储布局。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void GetDenseEntryByLinearIndex(int denseIndex, out EntityRef entity, out T* component)
        {
#if DEBUG
            if ((uint)denseIndex >= (uint)Count)
                throw new ArgumentOutOfRangeException(nameof(denseIndex));
#endif
            int globalIndex = denseIndex + 1;
            int block = globalIndex / _blockItemCapacity;
            int offset = globalIndex % _blockItemCapacity;
            entity = _blocks[block].PackedHandles[offset];
            component = &_blocks[block].PackedData[offset];
        }

        /// <summary>
        /// 检查指定全局索引上的条目是否为 active。
        /// 没有启用状态跟踪时，非空 dense 条目都视为 active。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool IsDenseEntryActive(int globalIndex)
        {
            return GetEntryStateByIndex(globalIndex) == EntryStateActive;
        }

        /// <summary>
        /// 将密集实体数组复制到托管缓冲区。
        /// </summary>
        internal void CopyDenseEntities(EntityRef[] destination)
        {
            if (destination == null)
            {
                throw new ArgumentNullException(nameof(destination));
            }

            if (destination.Length < Count)
            {
                throw new ArgumentException("Destination buffer is too small.", nameof(destination));
            }

            fixed (EntityRef* dest = destination)
            {
                CopyDenseEntities(dest, Count);
            }
        }

        /// <summary>
        /// 将密集组件数据复制到字节缓冲区。
        /// </summary>
        internal void CopyDenseComponentBytes(byte[] destination)
        {
            if (destination == null)
            {
                throw new ArgumentNullException(nameof(destination));
            }

            int byteCount = Count * sizeof(T);
            if (destination.Length < byteCount)
            {
                throw new ArgumentException("Destination buffer is too small.", nameof(destination));
            }

            fixed (byte* dest = destination)
            {
                CopyDenseComponentBytes(dest, Count);
            }
        }

        /// <summary>
        /// 使用原始密集快照恢复组件存储。
        /// </summary>
        internal void RestoreDenseSnapshot(EntityRef[] entities, byte[] componentData)
        {
            if (entities == null)
            {
                throw new ArgumentNullException(nameof(entities));
            }

            if (componentData == null)
            {
                throw new ArgumentNullException(nameof(componentData));
            }

            int denseCount = entities.Length;
            if (componentData.Length != denseCount * sizeof(T))
            {
                throw new ArgumentException("Component snapshot data size does not match entity count.", nameof(componentData));
            }

            ClearSparseAndStates();
            _count = denseCount + 1;
            _pendingRemoval = 0;
            _singletonSparse = 0;

            EnsureEntityCapacity(_count);

            if (denseCount == 0)
            {
                _version++;
                return;
            }

            fixed (EntityRef* entityPtr = entities)
            fixed (byte* dataPtr = componentData)
            {
                RestoreDenseEntities(entityPtr, denseCount);
                RestoreDenseComponentBytes(dataPtr, denseCount);

                for (int i = 0; i < denseCount; i++)
                {
                    EntityRef entity = entityPtr[i];
                    int globalIndex = i + 1;
                    _sparse[entity.Index] = (ushort)globalIndex;
                    SetEntryStateByIndex(globalIndex, EntryStateActive);

                    if (IsSingleton)
                    {
                        _singletonSparse = (ushort)globalIndex;
                    }
                }
            }

            _version++;
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

        private static int ResolveBlockCapacity(int requestedCapacity)
        {
            if (requestedCapacity > 0)
            {
                return System.Math.Clamp(requestedCapacity, MinBlockCapacity, MaxBlockCapacity);
            }

            int ideal = TargetBlockBytes / System.Math.Max(sizeof(T), 1);
            ideal = System.Math.Clamp(ideal, MinBlockCapacity, MaxBlockCapacity);
            return RoundDownToPowerOfTwo(ideal);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ushort GetSparseIndex(EntityRef entity)
        {
            uint index = (uint)entity.Index;
            if (index >= (uint)_sparseCapacity)
            {
                return TOMBSTONE;
            }

            ushort sparse = _sparse[index];
            if (sparse == TOMBSTONE)
            {
                return TOMBSTONE;
            }

            int block = sparse / _blockItemCapacity;
            int offset = sparse % _blockItemCapacity;
            return _blocks[block].PackedHandles[offset].Raw == entity.Raw ? sparse : TOMBSTONE;
        }

        private void CopyDenseEntities(EntityRef* destination, int denseCount)
        {
            if (denseCount <= 0)
            {
                return;
            }

            int copied = 0;
            for (int blockIndex = 0; blockIndex < _blockCount && copied < denseCount; blockIndex++)
            {
                int blockCount = GetDenseBlockCopyCount(blockIndex, denseCount - copied);
                if (blockCount <= 0)
                {
                    continue;
                }

                int sourceOffset = blockIndex == 0 ? 1 : 0;
                Buffer.MemoryCopy(
                    _blocks[blockIndex].PackedHandles + sourceOffset,
                    destination + copied,
                    sizeof(EntityRef) * (denseCount - copied),
                    sizeof(EntityRef) * blockCount);
                copied += blockCount;
            }
        }

        private void CopyDenseComponentBytes(byte* destination, int denseCount)
        {
            if (denseCount <= 0)
            {
                return;
            }

            int copied = 0;
            for (int blockIndex = 0; blockIndex < _blockCount && copied < denseCount; blockIndex++)
            {
                int blockCount = GetDenseBlockCopyCount(blockIndex, denseCount - copied);
                if (blockCount <= 0)
                {
                    continue;
                }

                int sourceOffset = blockIndex == 0 ? 1 : 0;
                Buffer.MemoryCopy(
                    _blocks[blockIndex].PackedData + sourceOffset,
                    destination + (copied * sizeof(T)),
                    sizeof(T) * (denseCount - copied),
                    sizeof(T) * blockCount);
                copied += blockCount;
            }
        }

        private void RestoreDenseEntities(EntityRef* source, int denseCount)
        {
            int copied = 0;
            for (int blockIndex = 0; blockIndex < _blockCount && copied < denseCount; blockIndex++)
            {
                int blockCount = GetDenseBlockCopyCount(blockIndex, denseCount - copied);
                if (blockCount <= 0)
                {
                    continue;
                }

                int destinationOffset = blockIndex == 0 ? 1 : 0;
                Buffer.MemoryCopy(
                    source + copied,
                    _blocks[blockIndex].PackedHandles + destinationOffset,
                    sizeof(EntityRef) * blockCount,
                    sizeof(EntityRef) * blockCount);
                copied += blockCount;
            }
        }

        private void RestoreDenseComponentBytes(byte* source, int denseCount)
        {
            int copied = 0;
            for (int blockIndex = 0; blockIndex < _blockCount && copied < denseCount; blockIndex++)
            {
                int blockCount = GetDenseBlockCopyCount(blockIndex, denseCount - copied);
                if (blockCount <= 0)
                {
                    continue;
                }

                int destinationOffset = blockIndex == 0 ? 1 : 0;
                Buffer.MemoryCopy(
                    source + (copied * sizeof(T)),
                    _blocks[blockIndex].PackedData + destinationOffset,
                    sizeof(T) * blockCount,
                    sizeof(T) * blockCount);
                copied += blockCount;
            }
        }

        private int GetDenseBlockCopyCount(int blockIndex, int remainingCount)
        {
            if (remainingCount <= 0)
            {
                return 0;
            }

            int blockCapacity = blockIndex == 0 ? _blockItemCapacity - 1 : _blockItemCapacity;
            return System.Math.Min(blockCapacity, remainingCount);
        }

        private void ClearSparseAndStates()
        {
            Unsafe.InitBlockUnaligned(_sparse, 0, checked((uint)(sizeof(ushort) * _sparseCapacity)));
            if (_entryStates != null)
            {
                Unsafe.InitBlockUnaligned(_entryStates, 0, checked((uint)(_sparseCapacity + 1)));
            }

            if (_pendingQueueIndices != null)
            {
                for (int i = 0; i <= _sparseCapacity; i++)
                {
                    _pendingQueueIndices[i] = PendingQueueIndexNone;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private byte GetEntryStateByIndex(int globalIndex)
        {
            return _entryStates != null ? _entryStates[globalIndex] : EntryStateActive;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetEntryStateByIndex(int globalIndex, byte state)
        {
            if (_entryStates != null)
            {
                _entryStates[globalIndex] = state;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetPendingQueueIndexByIndex(int globalIndex)
        {
            return _pendingQueueIndices != null && _pendingQueueIndices[globalIndex] != PendingQueueIndexNone
                ? _pendingQueueIndices[globalIndex]
                : PendingQueueIndexNone;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetPendingQueueIndexByIndex(int globalIndex, int queueIndex)
        {
            if (_pendingQueueIndices != null)
            {
                _pendingQueueIndices[globalIndex] = queueIndex >= 0 ? checked((ushort)queueIndex) : PendingQueueIndexNone;
            }
        }

        private static int RoundDownToPowerOfTwo(int value)
        {
            if (value <= 1)
            {
                return 1;
            }

            return 1 << (BitOperations.Log2((uint)value));
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

        private const byte EntryStateEmpty = 0;
        private const byte EntryStateActive = 1;
        private const byte EntryStatePendingRemoval = 2;
        private const ushort PendingQueueIndexNone = ushort.MaxValue;

        #endregion
    }
}
