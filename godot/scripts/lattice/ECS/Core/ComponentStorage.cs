using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Lattice.ECS.Core
{
    /// <summary>
    /// 组件数据块，对应 FrameSync 的 ComponentDataBlock
    /// 存储固定数量的组件和对应的实体引用
    /// </summary>
    internal unsafe struct ComponentDataBlock
    {
        /// <summary>组件数据原始内存指针</summary>
        public byte* Data;

        /// <summary>实体引用数组指针</summary>
        public Entity* Entities;

        /// <summary>块容量（默认 128）</summary>
        public int Capacity;
    }

    /// <summary>
    /// 组件存储缓冲区，完全对齐 FrameSync 的 ComponentDataBuffer 设计
    /// 使用不安全代码和原始内存管理实现极致性能
    /// </summary>
    /// <typeparam name="T">组件类型，必须是 unmanaged 结构体</typeparam>
    public unsafe class ComponentStorage<T> where T : unmanaged
    {
        // 常量配置
        public const int DEFAULT_BLOCK_CAPACITY = 128;
        public const int INITIAL_BLOCK_COUNT = 8;

        // 组件元数据
        private readonly int _stride;                    // sizeof(T)
        private readonly int _elementsPerBlock;          // 每块组件数量（原 _blockCapacity，避免命名冲突）

        // 计数器
        private int _count;                              // 总数量（含待删除）
        private int _usedCount;                          // 实际使用数量

        // 块管理
        private ComponentDataBlock* _blocks;             // 块数组指针
        private int _blockCount;                         // 当前块数量
        private int _blockArrayCapacity;                 // 块数组容量（可容纳的块描述符数量）

        // 空闲列表（槽位复用）
        private int* _freeList;                          // 空闲槽位索引数组
        private int _freeListCount;                      // 空闲槽位数量
        private int _freeListCapacity;                   // 空闲列表容量

        // 实体到索引的映射（类似 FrameSync 的 EntityInfo）
        // 使用稀疏数组，索引为 Entity.Index，值为存储索引（-1 表示无组件）
        private int* _entityToIndex;
        private int _entityMapCapacity;

        // 版本追踪（Lattice 原有功能保留）
        private int* _versions;

        /// <summary>当前存储的组件总数（含待删除）</summary>
        public int Count => _count;

        /// <summary>实际使用的组件数量</summary>
        public int UsedCount => _usedCount;

        /// <summary>当前分配的块数量</summary>
        public int BlockCount => _blockCount;

        public ComponentStorage(int blockCapacity = DEFAULT_BLOCK_CAPACITY, int initialEntityCapacity = 1024)
        {
            _stride = sizeof(T);
            _elementsPerBlock = global::System.Math.Max(1, blockCapacity);
            _count = 0;
            _usedCount = 0;
            _blockCount = 0;
            _blockArrayCapacity = INITIAL_BLOCK_COUNT;
            _freeListCount = 0;
            _freeListCapacity = global::System.Math.Max(16, _elementsPerBlock * 2);
            _entityMapCapacity = global::System.Math.Max(16, initialEntityCapacity);

            // 分配块数组
            _blocks = (ComponentDataBlock*)NativeMemory.AllocZeroed((nuint)(_blockArrayCapacity * sizeof(ComponentDataBlock)));

            // 分配空闲列表
            _freeList = (int*)NativeMemory.AllocZeroed((nuint)(_freeListCapacity * sizeof(int)));

            // 分配实体映射数组
            _entityToIndex = (int*)NativeMemory.AllocZeroed((nuint)(_entityMapCapacity * sizeof(int)));

            // 初始化为 -1（表示无组件）
            for (int i = 0; i < _entityMapCapacity; i++)
            {
                _entityToIndex[i] = -1;
            }

            // 分配版本数组
            _versions = (int*)NativeMemory.AllocZeroed((nuint)(_entityMapCapacity * sizeof(int)));
        }

        /// <summary>
        /// 确保实体映射数组容量足够
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EnsureEntityMapCapacity(int entityIndex)
        {
            if (entityIndex < _entityMapCapacity) return;

            int newCapacity = _entityMapCapacity * 2;
            while (newCapacity <= entityIndex)
            {
                newCapacity *= 2;
            }

            // 重新分配并复制
            int* newEntityToIndex = (int*)NativeMemory.AllocZeroed((nuint)(newCapacity * sizeof(int)));
            int* newVersions = (int*)NativeMemory.AllocZeroed((nuint)(newCapacity * sizeof(int)));

            // 复制旧数据
            Buffer.MemoryCopy(_entityToIndex, newEntityToIndex, _entityMapCapacity * sizeof(int), _entityMapCapacity * sizeof(int));
            Buffer.MemoryCopy(_versions, newVersions, _entityMapCapacity * sizeof(int), _entityMapCapacity * sizeof(int));

            // 初始化新区域为 -1
            for (int i = _entityMapCapacity; i < newCapacity; i++)
            {
                newEntityToIndex[i] = -1;
            }

            // 释放旧内存
            NativeMemory.Free(_entityToIndex);
            NativeMemory.Free(_versions);

            _entityToIndex = newEntityToIndex;
            _versions = newVersions;
            _entityMapCapacity = newCapacity;
        }

        /// <summary>
        /// 确保块数组容量足够
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EnsureBlockCapacity()
        {
            if (_blockCount < _blockArrayCapacity) return;

            int newCapacity = _blockArrayCapacity * 2;
            var newBlocks = (ComponentDataBlock*)NativeMemory.AllocZeroed((nuint)(newCapacity * sizeof(ComponentDataBlock)));

            // 复制旧块指针（注意：块数据本身不动，只复制块描述符）
            Buffer.MemoryCopy(_blocks, newBlocks, _blockCount * sizeof(ComponentDataBlock), _blockCount * sizeof(ComponentDataBlock));

            NativeMemory.Free(_blocks);
            _blocks = newBlocks;
            _blockArrayCapacity = newCapacity;
        }

        /// <summary>
        /// 分配新块
        /// </summary>
        private void AllocateBlock()
        {
            EnsureBlockCapacity();

            ref var block = ref _blocks[_blockCount];
            block.Capacity = _elementsPerBlock;

            // 分配组件数据内存（对齐到 8 字节）
            nuint dataSize = (nuint)(_elementsPerBlock * _stride);
            block.Data = (byte*)NativeMemory.AlignedAlloc(dataSize, 8);

            // 分配实体引用数组
            block.Entities = (Entity*)NativeMemory.AlignedAlloc((nuint)(_elementsPerBlock * sizeof(Entity)), 8);

            // 清零
            NativeMemory.Clear(block.Data, dataSize);
            NativeMemory.Clear(block.Entities, (nuint)(_elementsPerBlock * sizeof(Entity)));

            _blockCount++;
        }

        /// <summary>
        /// 添加组件到实体
        /// </summary>
        public int Add(Entity entity, in T component)
        {
            int entityIndex = entity.Index;
            EnsureEntityMapCapacity(entityIndex);

            // 检查是否已存在
            if (_entityToIndex[entityIndex] >= 0)
            {
                throw new InvalidOperationException($"Entity {entity} already has component {typeof(T).Name}");
            }

            int index;

            // 优先复用空闲槽位
            if (_freeListCount > 0)
            {
                index = _freeList[--_freeListCount];
                _versions[entityIndex] = entity.Version;
            }
            else
            {
                // 分配新槽位
                index = _count;

                // 检查是否需要新块
                int blockIndex = index / _elementsPerBlock;
                int elementIndex = index % _elementsPerBlock;

                // 按需分配块
                while (blockIndex >= _blockCount)
                {
                    EnsureBlockCapacity();
                    AllocateBlock();
                }

                _count++;
                _versions[entityIndex] = entity.Version;
            }

            // 写入数据
            SetComponent(index, entity, component);
            _entityToIndex[entityIndex] = index;
            _usedCount++;

            return index;
        }

        /// <summary>
        /// 获取组件索引（供内部使用）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetIndex(Entity entity)
        {
            int entityIndex = entity.Index;
            if (entityIndex >= _entityMapCapacity) return -1;

            int index = _entityToIndex[entityIndex];
            if (index < 0) return -1;

            // 验证版本
            if (_versions[entityIndex] != entity.Version) return -1;

            return index;
        }

        /// <summary>
        /// 检查实体是否有此组件
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Contains(Entity entity)
        {
            return GetIndex(entity) >= 0;
        }

        /// <summary>
        /// 获取组件指针（unsafe，最高性能）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T* GetPointer(Entity entity)
        {
            int index = GetIndex(entity);
            if (index < 0)
            {
                throw new InvalidOperationException($"Entity {entity} does not have component {typeof(T).Name}");
            }
            return GetPointerByIndex(index);
        }

        /// <summary>
        /// 尝试获取组件指针
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetPointer(Entity entity, out T* pointer)
        {
            int index = GetIndex(entity);
            if (index < 0)
            {
                pointer = null;
                return false;
            }
            pointer = GetPointerByIndex(index);
            return true;
        }

        /// <summary>
        /// 通过存储索引获取组件指针
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T* GetPointerByIndex(int index)
        {
            int blockIndex = index / _elementsPerBlock;
            int elementIndex = index % _elementsPerBlock;
            return (T*)(_blocks[blockIndex].Data + elementIndex * _stride);
        }

        /// <summary>
        /// 通过存储索引获取实体
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Entity GetEntityByIndex(int index)
        {
            int blockIndex = index / _elementsPerBlock;
            int elementIndex = index % _elementsPerBlock;
            return _blocks[blockIndex].Entities[elementIndex];
        }

        /// <summary>
        /// 获取组件值（复制）
        /// </summary>
        public T Get(Entity entity)
        {
            return *GetPointer(entity);
        }

        /// <summary>
        /// 设置组件值
        /// </summary>
        public void Set(Entity entity, in T component)
        {
            int index = GetIndex(entity);
            if (index < 0)
            {
                throw new InvalidOperationException($"Entity {entity} does not have component {typeof(T).Name}");
            }
            *GetPointerByIndex(index) = component;
        }

        /// <summary>
        /// 通过索引设置组件（内部使用）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetComponent(int index, Entity entity, in T component)
        {
            int blockIndex = index / _elementsPerBlock;
            int elementIndex = index % _elementsPerBlock;

            ref var block = ref _blocks[blockIndex];
            block.Entities[elementIndex] = entity;
            *(T*)(block.Data + elementIndex * _stride) = component;
        }

        /// <summary>
        /// 移除组件
        /// </summary>
        public bool Remove(Entity entity)
        {
            int entityIndex = entity.Index;
            if (entityIndex >= _entityMapCapacity) return false;

            int index = _entityToIndex[entityIndex];
            if (index < 0) return false;

            // 验证版本
            if (_versions[entityIndex] != entity.Version) return false;

            // 标记为已删除（增加版本）
            _versions[entityIndex] = -1;  // -1 表示已删除
            _entityToIndex[entityIndex] = -1;

            // 加入空闲列表
            EnsureFreeListCapacity();
            _freeList[_freeListCount++] = index;
            _usedCount--;

            return true;
        }

        /// <summary>
        /// 确保空闲列表容量
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EnsureFreeListCapacity()
        {
            if (_freeListCount < _freeListCapacity) return;

            int newCapacity = _freeListCapacity * 2;
            int* newFreeList = (int*)NativeMemory.AllocZeroed((nuint)(newCapacity * sizeof(int)));
            Buffer.MemoryCopy(_freeList, newFreeList, _freeListCount * sizeof(int), _freeListCount * sizeof(int));
            NativeMemory.Free(_freeList);
            _freeList = newFreeList;
            _freeListCapacity = newCapacity;
        }

        /// <summary>
        /// 获取组件版本（用于变更检测）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetVersion(Entity entity)
        {
            int entityIndex = entity.Index;
            if (entityIndex >= _entityMapCapacity) return -1;
            if (_entityToIndex[entityIndex] < 0) return -1;
            return _versions[entityIndex];
        }

        /// <summary>
        /// 释放所有资源
        /// </summary>
        public void Dispose()
        {
            // 释放所有块数据
            for (int i = 0; i < _blockCount; i++)
            {
                ref var block = ref _blocks[i];
                if (block.Data != null)
                {
                    NativeMemory.AlignedFree(block.Data);
                    block.Data = null;
                }
                if (block.Entities != null)
                {
                    NativeMemory.AlignedFree(block.Entities);
                    block.Entities = null;
                }
            }

            if (_blocks != null)
            {
                NativeMemory.Free(_blocks);
                _blocks = null;
            }

            if (_freeList != null)
            {
                NativeMemory.Free(_freeList);
                _freeList = null;
            }

            if (_entityToIndex != null)
            {
                NativeMemory.Free(_entityToIndex);
                _entityToIndex = null;
            }

            if (_versions != null)
            {
                NativeMemory.Free(_versions);
                _versions = null;
            }

            _blockCount = 0;
            _count = 0;
            _usedCount = 0;
            _freeListCount = 0;
        }

        /// <summary>
        /// 获取块数据（用于批量遍历）
        /// </summary>
        public bool GetBlockData(int blockIndex, out Entity* entities, out T* components, out int capacity)
        {
            if (blockIndex < 0 || blockIndex >= _blockCount)
            {
                entities = null;
                components = null;
                capacity = 0;
                return false;
            }

            ref var block = ref _blocks[blockIndex];
            entities = block.Entities;
            components = (T*)block.Data;
            capacity = _elementsPerBlock;
            return true;
        }

        /// <summary>
        /// 尝试获取块数据（对齐 FrameSync ComponentBlockIterator）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool TryGetBlock(int blockIndex, out byte* data, out Entity* entities, out int count)
        {
            if (blockIndex < 0 || blockIndex >= _blockCount)
            {
                data = null;
                entities = null;
                count = 0;
                return false;
            }

            ref var block = ref _blocks[blockIndex];
            data = block.Data;
            entities = block.Entities;

            // 计算块中的有效元素数量
            int startIndex = blockIndex * _elementsPerBlock;
            int endIndex = global::System.Math.Min(startIndex + _elementsPerBlock, _count);
            count = endIndex - startIndex;

            return count > 0;
        }
    }

    /// <summary>
    /// 组件存储异常
    /// </summary>
    public class ComponentStorageException : Exception
    {
        public ComponentStorageException(string message) : base(message) { }
        public ComponentStorageException(string message, Exception inner) : base(message, inner) { }
    }
}
