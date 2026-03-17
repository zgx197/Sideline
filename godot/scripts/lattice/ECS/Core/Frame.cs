// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

using System;
using System.Runtime.CompilerServices;
using Lattice.Core;
using Lattice.Math;

namespace Lattice.ECS.Core
{
    /// <summary>
    /// ECS 帧 - 高性能非托管实现（FrameSync 风格）
    /// 
    /// 设计原则：
    /// 1. 零 GC 压力 - 所有数据使用非托管内存
    /// 2. O(1) 组件访问 - 稀疏集实现
    /// 3. 缓存友好 - 密集数组存储
    /// 4. 确定性 - 完全可预测的行为
    /// </summary>
    public unsafe class Frame : IDisposable
    {
        #region 常量

        /// <summary>最大组件类型数</summary>
        public const int MaxComponentTypes = 512;

        /// <summary>组件位图块数（512位 = 8个ulong）</summary>
        private const int ComponentMaskBlockCount = 8;

        #endregion

        #region 实体管理

        /// <summary>实体数组（密集存储）</summary>
        private EntityRef* _entities;

        /// <summary>实体版本数组</summary>
        private ushort* _entityVersions;

        /// <summary>实体下一个空闲索引</summary>
        private int* _entityNextFree;

        /// <summary>实体组件位图（每个实体8个ulong）</summary>
        private ulong* _entityComponentMasks;

        /// <summary>实体数组容量</summary>
        private int _entityCapacity;

        /// <summary>实体数量</summary>
        private int _entityCount;

        /// <summary>空闲实体链表头</summary>
        private int _freeListHead;

        #endregion

        #region 组件存储

        /// <summary>组件存储数组（类型ID -> 存储指针）</summary>
        private void** _componentStorages;

        /// <summary>组件存储是否已初始化</summary>
        private bool* _storageInitialized;

        #endregion

        #region 属性

        /// <summary>当前帧号</summary>
        public int Tick { get; set; }

        /// <summary>固定时间步长</summary>
        public FP DeltaTime { get; set; }

        /// <summary>实体数量</summary>
        public int EntityCount => _entityCount;

        #endregion

        #region 构造函数

        public Frame(int maxEntities = 65536)
        {
            _entityCapacity = maxEntities;
            _entityCount = 0;
            _freeListHead = -1;

            // 分配实体数组
            _entities = (EntityRef*)Alloc(sizeof(EntityRef) * maxEntities);
            _entityVersions = (ushort*)Alloc(sizeof(ushort) * maxEntities);
            _entityNextFree = (int*)Alloc(sizeof(int) * maxEntities);
            _entityComponentMasks = (ulong*)Alloc(sizeof(ulong) * maxEntities * ComponentMaskBlockCount);

            // 初始化实体版本
            for (int i = 0; i < maxEntities; i++)
            {
                _entityVersions[i] = 1;
                _entityNextFree[i] = -1;
            }

            // 分配组件存储数组
            _componentStorages = (void**)Alloc(sizeof(void*) * MaxComponentTypes);
            _storageInitialized = (bool*)Alloc(sizeof(bool) * MaxComponentTypes);

            for (int i = 0; i < MaxComponentTypes; i++)
            {
                _componentStorages[i] = null;
                _storageInitialized[i] = false;
            }
        }

        #endregion

        #region 实体操作

        /// <summary>
        /// 创建实体 - O(1)
        /// </summary>
        public EntityRef CreateEntity()
        {
            int index;
            ushort version;

            if (_freeListHead >= 0)
            {
                // 复用空闲槽位
                index = _freeListHead;
                _freeListHead = _entityNextFree[index];
                version = _entityVersions[index];
            }
            else
            {
                // 分配新槽位
                if (_entityCount >= _entityCapacity)
                    throw new InvalidOperationException("Entity limit reached");

                index = _entityCount++;
                version = 1;
                _entityVersions[index] = version;
            }

            // 清空组件位图
            ClearComponentMask(index);

            var entity = new EntityRef(index, version);
            _entities[index] = entity;
            return entity;
        }

        /// <summary>
        /// 销毁实体 - O(C)，C为组件数
        /// </summary>
        public void DestroyEntity(EntityRef entity)
        {
            if (!IsValid(entity)) return;

            // 删除所有组件
            ulong* mask = GetComponentMaskPointer(entity.Index);
            for (int typeId = 0; typeId < MaxComponentTypes; typeId++)
            {
                int block = typeId >> 6;
                int bit = typeId & 0x3F;
                if ((mask[block] & (1UL << bit)) != 0)
                {
                    RemoveComponentInternal(entity, typeId);
                }
            }

            // 版本递增（使旧引用失效）
            _entityVersions[entity.Index]++;

            // 加入空闲链表
            _entityNextFree[entity.Index] = _freeListHead;
            _freeListHead = entity.Index;
        }

        /// <summary>
        /// 检查实体是否有效 - O(1)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsValid(EntityRef entity)
        {
            uint index = (uint)entity.Index;
            return index < (uint)_entityCount &&
                   _entityVersions[index] == entity.Version;
        }

        #endregion

        #region 组件操作

        /// <summary>
        /// 添加组件 - O(1) 均摊
        /// </summary>
        public void Add<T>(EntityRef entity, in T component) where T : unmanaged
        {
            if (!IsValid(entity))
                throw new ArgumentException($"Invalid entity: {entity}");

            int typeId = ComponentTypeId<T>.Id;
            var storage = GetOrCreateStorage<T>(typeId);

            storage->Add(entity, component);
            SetComponentMask(entity.Index, typeId);
        }

        /// <summary>
        /// 移除组件 - O(1)
        /// </summary>
        public void Remove<T>(EntityRef entity) where T : unmanaged
        {
            if (!IsValid(entity)) return;

            int typeId = ComponentTypeId<T>.Id;
            var storage = GetStorage<T>(typeId);
            if (storage == null) return;

            storage->Remove(entity);
            ClearComponentMaskBit(entity.Index, typeId);
        }

        /// <summary>
        /// 获取组件引用 - O(1)，2次内存访问
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T Get<T>(EntityRef entity) where T : unmanaged
        {
            int typeId = ComponentTypeId<T>.Id;
            var storage = GetStorage<T>(typeId);
            if (storage == null)
                throw new InvalidOperationException($"Component {typeof(T).Name} not found");
            return ref storage->Get(entity);
        }

        /// <summary>
        /// 获取组件指针 - O(1)，2次内存访问
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T* GetPointer<T>(EntityRef entity) where T : unmanaged
        {
            int typeId = ComponentTypeId<T>.Id;
            var storage = GetStorage<T>(typeId);
            return storage != null ? storage->GetPointer(entity) : null;
        }

        /// <summary>
        /// 尝试获取组件 - O(1)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGet<T>(EntityRef entity, out T component) where T : unmanaged
        {
            int typeId = ComponentTypeId<T>.Id;
            var storage = GetStorage<T>(typeId);
            if (storage != null)
                return storage->TryGet(entity, out component);
            component = default;
            return false;
        }

        /// <summary>
        /// 检查是否有组件 - O(1)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Has<T>(EntityRef entity) where T : unmanaged
        {
            if (!IsValid(entity)) return false;
            int typeId = ComponentTypeId<T>.Id;
            return HasComponentMask(entity.Index, typeId);
        }

        #endregion

        #region 查询支持

        /// <summary>
        /// 获取组件存储（用于 Filter）
        /// </summary>
        internal Storage<T>* GetStorage<T>(int typeId) where T : unmanaged
        {
            if (!_storageInitialized[typeId]) return null;
            return (Storage<T>*)_componentStorages[typeId];
        }

        /// <summary>
        /// 获取实体组件位图指针
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ulong* GetComponentMaskPointer(int entityIndex)
        {
            return _entityComponentMasks + (entityIndex * ComponentMaskBlockCount);
        }

        /// <summary>
        /// 检查实体是否匹配组件过滤器
        /// </summary>
        public bool MatchesFilter(EntityRef entity, in ComponentFilter filter)
        {
            if (!IsValid(entity)) return false;

            ulong* mask = GetComponentMaskPointer(entity.Index);

            // 检查必须包含的组件
            if (!filter.Required.IsEmpty)
            {
                for (int i = 0; i < ComponentMaskBlockCount; i++)
                {
                    ulong required = filter.Required.GetBlock(i);
                    if ((mask[i] & required) != required)
                        return false;
                }
            }

            // 检查必须排除的组件
            if (!filter.Excluded.IsEmpty)
            {
                for (int i = 0; i < ComponentMaskBlockCount; i++)
                {
                    ulong excluded = filter.Excluded.GetBlock(i);
                    if ((mask[i] & excluded) != 0)
                        return false;
                }
            }

            return true;
        }

        #endregion

        #region Unsafe API (高性能直接访问)

        /// <summary>
        /// 获取组件块迭代器 - 批量遍历，最大化缓存命中率
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ComponentBlockIterator<T> GetComponentBlockIterator<T>() where T : unmanaged
        {
            int typeId = ComponentTypeId<T>.Id;
            var storage = GetStorage<T>(typeId);
            if (storage == null)
                return default;
            return new ComponentBlockIterator<T>(storage);
        }

        /// <summary>
        /// 获取组件块迭代器（范围版）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ComponentBlockIterator<T> GetComponentBlockIterator<T>(int offset, int count) where T : unmanaged
        {
            int typeId = ComponentTypeId<T>.Id;
            var storage = GetStorage<T>(typeId);
            if (storage == null)
                return default;
            return new ComponentBlockIterator<T>(storage, offset, count);
        }

        /// <summary>
        /// 获取组件存储的原始指针（高级用法）
        /// </summary>
        internal Storage<T>* GetStoragePointer<T>() where T : unmanaged
        {
            int typeId = ComponentTypeId<T>.Id;
            return GetStorage<T>(typeId);
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            // 释放所有组件存储
            for (int i = 0; i < MaxComponentTypes; i++)
            {
                if (_storageInitialized[i] && _componentStorages[i] != null)
                {
                    // 这里需要知道类型来正确释放 Storage<T>
                    // 简化处理：依赖 GC 或添加类型注册表
                    _componentStorages[i] = null;
                    _storageInitialized[i] = false;
                }
            }

            Free(_entities);
            Free(_entityVersions);
            Free(_entityNextFree);
            Free(_entityComponentMasks);
            Free(_componentStorages);
            Free(_storageInitialized);
        }

        #endregion

        #region 私有辅助

        private static void* Alloc(int size)
        {
            return System.Runtime.InteropServices.Marshal.AllocHGlobal(size).ToPointer();
        }

        private static void Free(void* ptr)
        {
            if (ptr != null)
                System.Runtime.InteropServices.Marshal.FreeHGlobal((IntPtr)ptr);
        }

        private Storage<T>* GetOrCreateStorage<T>(int typeId) where T : unmanaged
        {
            if (!_storageInitialized[typeId])
            {
                var storage = (Storage<T>*)Alloc(sizeof(Storage<T>));
                storage->Initialize(_entityCapacity);
                _componentStorages[typeId] = storage;
                _storageInitialized[typeId] = true;
                return storage;
            }
            return (Storage<T>*)_componentStorages[typeId];
        }

        private void RemoveComponentInternal(EntityRef entity, int typeId)
        {
            // 简化实现：依赖外部管理存储生命周期
            ClearComponentMaskBit(entity.Index, typeId);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetComponentMask(int entityIndex, int typeId)
        {
            int block = typeId >> 6;
            int bit = typeId & 0x3F;
            _entityComponentMasks[entityIndex * ComponentMaskBlockCount + block] |= (1UL << bit);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ClearComponentMaskBit(int entityIndex, int typeId)
        {
            int block = typeId >> 6;
            int bit = typeId & 0x3F;
            _entityComponentMasks[entityIndex * ComponentMaskBlockCount + block] &= ~(1UL << bit);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ClearComponentMask(int entityIndex)
        {
            ulong* mask = GetComponentMaskPointer(entityIndex);
            for (int i = 0; i < ComponentMaskBlockCount; i++)
                mask[i] = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool HasComponentMask(int entityIndex, int typeId)
        {
            int block = typeId >> 6;
            int bit = typeId & 0x3F;
            return (_entityComponentMasks[entityIndex * ComponentMaskBlockCount + block] & (1UL << bit)) != 0;
        }

        #endregion
    }
}
