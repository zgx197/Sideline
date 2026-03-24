// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

using System;
using System.Collections.Generic;
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

        /// <summary>延迟删除队列中的实体。</summary>
        private EntityRef* _pendingRemovalEntities;

        /// <summary>延迟删除队列中的组件类型 ID。</summary>
        private ushort* _pendingRemovalTypeIds;

        /// <summary>延迟删除队列数量。</summary>
        private int _pendingRemovalCount;

        /// <summary>延迟删除队列容量。</summary>
        private int _pendingRemovalCapacity;

        /// <summary>内存分配器</summary>
        private Allocator* _allocator;

        /// <summary>显式注册的 Owning Group 索引。</summary>
        private Dictionary<ulong, object>? _owningGroups;

        /// <summary>Owning Group 生命周期维护列表。</summary>
        private List<IFrameOwningGroup>? _owningGroupBindings;

        #endregion

        #region 属性

        /// <summary>当前帧号</summary>
        public int Tick { get; set; }

        /// <summary>固定时间步长</summary>
        public FP DeltaTime { get; set; }

        /// <summary>实体数量</summary>
        public int EntityCount => _entityCount;

        /// <summary>实体容量。</summary>
        public int EntityCapacity => _entityCapacity;

        /// <summary>待提交的延迟删除数量。</summary>
        public int PendingDeferredRemovalCount => _pendingRemovalCount;

        #endregion

        #region 构造函数

        public Frame(int maxEntities = 65536)
        {
            InitializeState(maxEntities);
        }

        #endregion

        #region 快照与复制

        /// <summary>
        /// 创建帧快照。
        /// </summary>
        public FrameSnapshot CreateSnapshot(ComponentSerializationMode mode = ComponentSerializationMode.Default)
        {
            var entityVersions = new ushort[_entityCapacity];
            var entityNextFree = new int[_entityCapacity];
            var entityComponentMasks = new ulong[_entityCapacity * ComponentMaskBlockCount];

            for (int i = 0; i < _entityCapacity; i++)
            {
                entityVersions[i] = _entityVersions[i];
                entityNextFree[i] = _entityNextFree[i];
            }

            for (int i = 0; i < entityComponentMasks.Length; i++)
            {
                entityComponentMasks[i] = _entityComponentMasks[i];
            }

            var pendingEntities = new EntityRef[_pendingRemovalCount];
            var pendingTypeIds = new ushort[_pendingRemovalCount];
            for (int i = 0; i < _pendingRemovalCount; i++)
            {
                pendingEntities[i] = _pendingRemovalEntities[i];
                pendingTypeIds[i] = _pendingRemovalTypeIds[i];
            }

            var snapshots = new ComponentStorageSnapshot[ComponentRegistry.Count];
            int snapshotCount = 0;
            for (int typeId = 1; typeId <= ComponentRegistry.Count; typeId++)
            {
                if (!_storageInitialized[typeId] || _componentStorages[typeId] == null)
                {
                    continue;
                }

                ComponentStorageSnapshot? storageSnapshot = ComponentRegistry.GetSnapshotCapturer(typeId)(_componentStorages[typeId], mode);
                if (storageSnapshot == null)
                {
                    continue;
                }

                snapshots[snapshotCount++] = storageSnapshot;
            }

            Array.Resize(ref snapshots, snapshotCount);

            return new FrameSnapshot(
                Tick,
                DeltaTime,
                _entityCapacity,
                _entityCount,
                _freeListHead,
                entityVersions,
                entityNextFree,
                entityComponentMasks,
                pendingEntities,
                pendingTypeIds,
                snapshots);
        }

        #endregion

        /// <summary>
        /// 从快照恢复帧状态。
        /// </summary>
        public void RestoreFromSnapshot(FrameSnapshot snapshot, ComponentSerializationMode mode = ComponentSerializationMode.Default)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            bool preserveOwningGroups = _owningGroupBindings != null && _owningGroupBindings.Count > 0;

            ReleaseState(preserveOwningGroups);
            InitializeState(snapshot.EntityCapacity, preserveOwningGroups);

            if (preserveOwningGroups)
            {
                ReinitializeOwningGroups();
            }

            Tick = snapshot.Tick;
            DeltaTime = snapshot.DeltaTime;
            _entityCount = snapshot.EntityCount;
            _freeListHead = snapshot.FreeListHead;

            if (snapshot.EntityVersions.Length != _entityCapacity ||
                snapshot.EntityNextFree.Length != _entityCapacity ||
                snapshot.EntityComponentMasks.Length != _entityCapacity * ComponentMaskBlockCount)
            {
                throw new InvalidOperationException("Snapshot entity metadata size does not match frame capacity.");
            }

            for (int i = 0; i < _entityCapacity; i++)
            {
                _entityVersions[i] = snapshot.EntityVersions[i];
                _entityNextFree[i] = snapshot.EntityNextFree[i];
                _entities[i] = i < _entityCount ? new EntityRef(i, _entityVersions[i]) : EntityRef.None;
            }

            for (int i = 0; i < snapshot.EntityComponentMasks.Length; i++)
            {
                _entityComponentMasks[i] = snapshot.EntityComponentMasks[i];
            }

            for (int i = 0; i < snapshot.ComponentStorages.Length; i++)
            {
                ComponentStorageSnapshot storageSnapshot = snapshot.ComponentStorages[i];
                ComponentRegistry.GetSnapshotRestorer(storageSnapshot.TypeId)(this, storageSnapshot, mode);
            }

            EnsurePendingRemovalCapacity(snapshot.PendingRemovalEntities.Length);
            _pendingRemovalCount = snapshot.PendingRemovalEntities.Length;
            for (int i = 0; i < _pendingRemovalCount; i++)
            {
                _pendingRemovalEntities[i] = snapshot.PendingRemovalEntities[i];
                _pendingRemovalTypeIds[i] = snapshot.PendingRemovalTypeIds[i];

                int typeId = _pendingRemovalTypeIds[i];
                if (_storageInitialized[typeId] && _componentStorages[typeId] != null)
                {
                    ComponentRegistry.GetPendingRemovalMarker(typeId)(_componentStorages[typeId], _pendingRemovalEntities[i]);
                }
            }

            RebuildOwningGroups();
        }

        /// <summary>
        /// 克隆当前帧。
        /// </summary>
        public Frame Clone(ComponentSerializationMode mode = ComponentSerializationMode.Default)
        {
            var clone = new Frame(_entityCapacity);
            clone.RestoreFromSnapshot(CreateSnapshot(mode), mode);
            return clone;
        }

        /// <summary>
        /// 计算帧校验和。
        /// </summary>
        public ulong CalculateChecksum(ComponentSerializationMode mode = ComponentSerializationMode.Checkpoint)
        {
            return CreateSnapshot(mode).Checksum;
        }

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
        public void Add<T>(EntityRef entity, in T component) where T : unmanaged, IComponent
        {
            if (!IsValid(entity))
                throw new ArgumentException($"Invalid entity: {entity}");

            int typeId = ComponentTypeId<T>.Id;
            var storage = GetOrCreateStorage<T>(typeId);
            bool hasComponentMask = HasComponentMask(entity.Index, typeId);

            if (storage->Has(entity))
            {
                if (storage->IsDeferredRemoval && !hasComponentMask)
                {
                    int pendingQueueIndex = storage->GetPendingQueueIndex(entity);
                    storage->CancelPendingRemoval(entity);
                    RemovePendingRemovalAt(pendingQueueIndex);
                    storage->Get(entity) = component;
                    SetComponentMask(entity.Index, typeId);
                    InvokeOnAdded(entity, storage);
                    return;
                }

                throw new InvalidOperationException($"Component {typeof(T).Name} already exists on entity {entity}");
            }

            storage->Add(entity, component);
            SetComponentMask(entity.Index, typeId);
            InvokeOnAdded(entity, storage);
            NotifyOwningGroups(entity, typeId);
        }

        /// <summary>
        /// 设置组件值；若组件不存在则添加。
        /// </summary>
        public void Set<T>(EntityRef entity, in T component) where T : unmanaged, IComponent
        {
            if (!IsValid(entity))
                throw new ArgumentException($"Invalid entity: {entity}");

            int typeId = ComponentTypeId<T>.Id;
            var storage = GetOrCreateStorage<T>(typeId);

            if (HasComponentMask(entity.Index, typeId) && storage->Has(entity))
            {
                storage->Get(entity) = component;
                NotifyOwningGroups(entity, typeId);
                return;
            }

            if (storage->IsDeferredRemoval && storage->Has(entity))
            {
                int pendingQueueIndex = storage->GetPendingQueueIndex(entity);
                storage->CancelPendingRemoval(entity);
                RemovePendingRemovalAt(pendingQueueIndex);
                storage->Get(entity) = component;
                SetComponentMask(entity.Index, typeId);
                InvokeOnAdded(entity, storage);
                NotifyOwningGroups(entity, typeId);
                return;
            }

            Add(entity, component);
        }

        /// <summary>
        /// 移除组件 - O(1)
        /// </summary>
        public void Remove<T>(EntityRef entity) where T : unmanaged, IComponent
        {
            if (!IsValid(entity)) return;

            int typeId = ComponentTypeId<T>.Id;
            if (!HasComponentMask(entity.Index, typeId)) return;

            var storage = GetStorage<T>(typeId);
            if (storage == null || !storage->Has(entity)) return;

            ComponentChangedDelegate? onRemoved = ComponentTypeId<T>.Callbacks.OnRemoved;
            if (onRemoved != null)
            {
                T* removedComponent = storage->GetPointer(entity);
                if (removedComponent != null)
                {
                    onRemoved(entity, removedComponent, this);
                }
            }

            if (storage->IsDeferredRemoval)
            {
                storage->MarkForRemoval(entity);
                ClearComponentMaskBit(entity.Index, typeId);
                int pendingQueueIndex = EnqueuePendingRemoval(entity, typeId);
                storage->SetPendingQueueIndex(entity, pendingQueueIndex);
                NotifyOwningGroups(entity, typeId);
                return;
            }

            storage->Remove(entity);
            ClearComponentMaskBit(entity.Index, typeId);
            NotifyOwningGroups(entity, typeId);
        }

        /// <summary>
        /// 获取组件引用 - O(1)，2次内存访问
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T Get<T>(EntityRef entity) where T : unmanaged, IComponent
        {
            if (!IsValid(entity))
                throw new ArgumentException($"Invalid entity: {entity}");

            int typeId = ComponentTypeId<T>.Id;
            if (!HasComponentMask(entity.Index, typeId))
                throw new InvalidOperationException($"Component {typeof(T).Name} not found");

            var storage = GetStorage<T>(typeId);
            if (storage == null)
                throw new InvalidOperationException($"Component {typeof(T).Name} not found");
            return ref storage->Get(entity);
        }

        /// <summary>
        /// 获取组件指针 - O(1)，2次内存访问
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T* GetPointer<T>(EntityRef entity) where T : unmanaged, IComponent
        {
            if (!IsValid(entity))
                return null;

            int typeId = ComponentTypeId<T>.Id;
            if (!HasComponentMask(entity.Index, typeId))
                return null;

            var storage = GetStorage<T>(typeId);
            return storage != null ? storage->GetPointer(entity) : null;
        }

        /// <summary>
        /// 尝试获取组件 - O(1)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGet<T>(EntityRef entity, out T component) where T : unmanaged, IComponent
        {
            if (!IsValid(entity))
            {
                component = default;
                return false;
            }

            int typeId = ComponentTypeId<T>.Id;
            if (!HasComponentMask(entity.Index, typeId))
            {
                component = default;
                return false;
            }

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
        public bool Has<T>(EntityRef entity) where T : unmanaged, IComponent
        {
            if (!IsValid(entity)) return false;
            int typeId = ComponentTypeId<T>.Id;
            if (!HasComponentMask(entity.Index, typeId))
            {
                return false;
            }

            var storage = GetStorage<T>(typeId);
            return storage != null && storage->Has(entity);
        }

        /// <summary>
        /// 获取单组件过滤器。
        /// </summary>
#pragma warning disable CS0618
        public Filter<T> Filter<T>() where T : unmanaged, IComponent
        {
            return new Filter<T>(this);
        }

        /// <summary>
        /// 获取双组件过滤器。
        /// </summary>
        public Filter<T1, T2> Filter<T1, T2>()
            where T1 : unmanaged, IComponent
            where T2 : unmanaged, IComponent
        {
            return new Filter<T1, T2>(this);
        }

        /// <summary>
        /// 获取三组件过滤器。
        /// </summary>
        public Filter<T1, T2, T3> Filter<T1, T2, T3>()
            where T1 : unmanaged, IComponent
            where T2 : unmanaged, IComponent
            where T3 : unmanaged, IComponent
        {
            return new Filter<T1, T2, T3>(this);
        }
#pragma warning restore CS0618

        #endregion

        #region 查询支持

        /// <summary>
        /// 获取组件存储。
        /// 供 Query、兼容层 Filter 以及底层高性能遍历入口使用。
        /// </summary>
        internal Storage<T>* GetStorage<T>(int typeId) where T : unmanaged, IComponent
        {
            if (!_storageInitialized[typeId]) return null;
            return (Storage<T>*)_componentStorages[typeId];
        }

        internal Storage<T>* GetOrCreateStorageForSnapshotRestore<T>(int typeId) where T : unmanaged, IComponent
        {
            return GetOrCreateStorage<T>(typeId);
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
        /// 检查实体位图上是否仍然持有指定组件。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool HasComponentBit(int entityIndex, int typeId)
        {
            int block = typeId >> 6;
            int bit = typeId & 0x3F;
            return (_entityComponentMasks[entityIndex * ComponentMaskBlockCount + block] & (1UL << bit)) != 0;
        }

        /// <summary>
        /// 检查实体是否匹配位图组件过滤器。
        /// 该 API 主要用于动态条件匹配，不替代强类型 Query。
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

        /// <summary>
        /// 提交所有延迟删除。
        /// </summary>
        public void CommitDeferredRemovals()
        {
            if (_pendingRemovalCount == 0)
            {
                return;
            }

            for (int i = 0; i < _pendingRemovalCount; i++)
            {
                int typeId = _pendingRemovalTypeIds[i];
                if (!_storageInitialized[typeId] || _componentStorages[typeId] == null)
                {
                    continue;
                }

                ComponentRegistry.GetDeferredCommitter(typeId)(_componentStorages[typeId], _pendingRemovalEntities[i]);
            }

            _pendingRemovalCount = 0;
        }

        #endregion

        #region Unsafe API (高性能直接访问)

        /// <summary>
        /// 获取组件块迭代器 - 批量遍历，最大化缓存命中率
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ComponentBlockIterator<T> GetComponentBlockIterator<T>() where T : unmanaged, IComponent
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
        public ComponentBlockIterator<T> GetComponentBlockIterator<T>(int offset, int count) where T : unmanaged, IComponent
        {
            int typeId = ComponentTypeId<T>.Id;
            var storage = GetStorage<T>(typeId);
            if (storage == null)
                return default;
            return new ComponentBlockIterator<T>(storage, offset, count);
        }

        /// <summary>
        /// 获取单组件查询。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Query<T> Query<T>() where T : unmanaged, IComponent
        {
            return new Query<T>(this);
        }

        /// <summary>
        /// 获取双组件查询。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Query<T1, T2> Query<T1, T2>()
            where T1 : unmanaged, IComponent
            where T2 : unmanaged, IComponent
        {
            return new Query<T1, T2>(this);
        }

        /// <summary>
        /// 获取三组件查询。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Query<T1, T2, T3> Query<T1, T2, T3>()
            where T1 : unmanaged, IComponent
            where T2 : unmanaged, IComponent
            where T3 : unmanaged, IComponent
        {
            return new Query<T1, T2, T3>(this);
        }

        /// <summary>
        /// 注册并维护双组件 Owning Group。
        /// 仅建议用于极少数稳定热点系统。
        /// </summary>
        public OwningGroup<T1, T2> RegisterOwningGroup<T1, T2>()
            where T1 : unmanaged, IComponent
            where T2 : unmanaged, IComponent
        {
            int typeId1 = ComponentTypeId<T1>.Id;
            int typeId2 = ComponentTypeId<T2>.Id;
            ulong key = ComposeOwningGroupKey(2, typeId1, typeId2, 0);

            if (_owningGroups != null && _owningGroups.TryGetValue(key, out object? existing))
            {
                return (OwningGroup<T1, T2>)existing;
            }

            var group = new OwningGroup<T1, T2>(_allocator, _entityCapacity, typeId1, typeId2);
            group.Rebuild(this);
            _owningGroups ??= new Dictionary<ulong, object>();
            _owningGroupBindings ??= new List<IFrameOwningGroup>();
            _owningGroups[key] = group;
            _owningGroupBindings.Add(group);
            return group;
        }

        /// <summary>
        /// 注册并维护三组件 Owning Group。
        /// </summary>
        public OwningGroup<T1, T2, T3> RegisterOwningGroup<T1, T2, T3>()
            where T1 : unmanaged, IComponent
            where T2 : unmanaged, IComponent
            where T3 : unmanaged, IComponent
        {
            int typeId1 = ComponentTypeId<T1>.Id;
            int typeId2 = ComponentTypeId<T2>.Id;
            int typeId3 = ComponentTypeId<T3>.Id;
            ulong key = ComposeOwningGroupKey(3, typeId1, typeId2, typeId3);

            if (_owningGroups != null && _owningGroups.TryGetValue(key, out object? existing))
            {
                return (OwningGroup<T1, T2, T3>)existing;
            }

            var group = new OwningGroup<T1, T2, T3>(_allocator, _entityCapacity, typeId1, typeId2, typeId3);
            group.Rebuild(this);
            _owningGroups ??= new Dictionary<ulong, object>();
            _owningGroupBindings ??= new List<IFrameOwningGroup>();
            _owningGroups[key] = group;
            _owningGroupBindings.Add(group);
            return group;
        }

        /// <summary>
        /// 获取已注册的双组件 Owning Group。
        /// </summary>
        public OwningGroup<T1, T2> GetOwningGroup<T1, T2>()
            where T1 : unmanaged, IComponent
            where T2 : unmanaged, IComponent
        {
            if (TryGetOwningGroup<T1, T2>(out OwningGroup<T1, T2> group))
            {
                return group;
            }

            throw new InvalidOperationException(
                $"OwningGroup<{typeof(T1).Name}, {typeof(T2).Name}> 尚未注册，请先调用 RegisterOwningGroup。");
        }

        /// <summary>
        /// 获取已注册的三组件 Owning Group。
        /// </summary>
        public OwningGroup<T1, T2, T3> GetOwningGroup<T1, T2, T3>()
            where T1 : unmanaged, IComponent
            where T2 : unmanaged, IComponent
            where T3 : unmanaged, IComponent
        {
            if (TryGetOwningGroup<T1, T2, T3>(out OwningGroup<T1, T2, T3> group))
            {
                return group;
            }

            throw new InvalidOperationException(
                $"OwningGroup<{typeof(T1).Name}, {typeof(T2).Name}, {typeof(T3).Name}> 尚未注册，请先调用 RegisterOwningGroup。");
        }

        /// <summary>
        /// 尝试获取已注册的双组件 Owning Group。
        /// </summary>
        public bool TryGetOwningGroup<T1, T2>(out OwningGroup<T1, T2> group)
            where T1 : unmanaged, IComponent
            where T2 : unmanaged, IComponent
        {
            int typeId1 = ComponentTypeId<T1>.Id;
            int typeId2 = ComponentTypeId<T2>.Id;
            ulong key = ComposeOwningGroupKey(2, typeId1, typeId2, 0);

            if (_owningGroups != null && _owningGroups.TryGetValue(key, out object? existing))
            {
                group = (OwningGroup<T1, T2>)existing;
                return true;
            }

            group = null!;
            return false;
        }

        /// <summary>
        /// 尝试获取已注册的三组件 Owning Group。
        /// </summary>
        public bool TryGetOwningGroup<T1, T2, T3>(out OwningGroup<T1, T2, T3> group)
            where T1 : unmanaged, IComponent
            where T2 : unmanaged, IComponent
            where T3 : unmanaged, IComponent
        {
            int typeId1 = ComponentTypeId<T1>.Id;
            int typeId2 = ComponentTypeId<T2>.Id;
            int typeId3 = ComponentTypeId<T3>.Id;
            ulong key = ComposeOwningGroupKey(3, typeId1, typeId2, typeId3);

            if (_owningGroups != null && _owningGroups.TryGetValue(key, out object? existing))
            {
                group = (OwningGroup<T1, T2, T3>)existing;
                return true;
            }

            group = null!;
            return false;
        }

        /// <summary>
        /// 获取组件存储的原始指针（高级用法）
        /// </summary>
        internal Storage<T>* GetStoragePointer<T>() where T : unmanaged, IComponent
        {
            int typeId = ComponentTypeId<T>.Id;
            return GetStorage<T>(typeId);
        }

        /// <summary>
        /// 获取帧内部使用的分配器。
        /// </summary>
        internal Allocator* GetAllocatorPointer()
        {
            return _allocator;
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            ReleaseState();
        }

        #endregion

        #region 私有辅助

        private Storage<T>* GetOrCreateStorage<T>(int typeId) where T : unmanaged, IComponent
        {
            if (!_storageInitialized[typeId])
            {
                var storage = (Storage<T>*)_allocator->Alloc(sizeof(Storage<T>));
                storage->Initialize(_entityCapacity, _allocator, ComponentRegistry.GetStorageFlags(typeId), componentTypeId: typeId);
                _componentStorages[typeId] = storage;
                _storageInitialized[typeId] = true;
                return storage;
            }
            return (Storage<T>*)_componentStorages[typeId];
        }

        private void RemoveComponentInternal(EntityRef entity, int typeId)
        {
            if (!_storageInitialized[typeId] || _componentStorages[typeId] == null)
            {
                ClearComponentMaskBit(entity.Index, typeId);
                return;
            }

            ComponentRegistry.GetRemoveInvoker(typeId)(this, entity);
        }

        private int EnqueuePendingRemoval(EntityRef entity, int typeId)
        {
            EnsurePendingRemovalCapacity(_pendingRemovalCount + 1);
            _pendingRemovalEntities[_pendingRemovalCount] = entity;
            _pendingRemovalTypeIds[_pendingRemovalCount] = (ushort)typeId;
            return _pendingRemovalCount++;
        }

        private void RemovePendingRemovalAt(int queueIndex)
        {
            if ((uint)queueIndex >= (uint)_pendingRemovalCount)
            {
                return;
            }

            int lastIndex = _pendingRemovalCount - 1;
            if (queueIndex != lastIndex)
            {
                EntityRef swappedEntity = _pendingRemovalEntities[lastIndex];
                int swappedTypeId = _pendingRemovalTypeIds[lastIndex];

                _pendingRemovalEntities[queueIndex] = swappedEntity;
                _pendingRemovalTypeIds[queueIndex] = (ushort)swappedTypeId;

                if (_storageInitialized[swappedTypeId] && _componentStorages[swappedTypeId] != null)
                {
                    ComponentRegistry.GetPendingQueueIndexSetter(swappedTypeId)(
                        _componentStorages[swappedTypeId],
                        swappedEntity,
                        queueIndex);
                }
            }

            _pendingRemovalEntities[lastIndex] = EntityRef.None;
            _pendingRemovalTypeIds[lastIndex] = 0;
            _pendingRemovalCount--;
        }

        private void EnsurePendingRemovalCapacity(int required)
        {
            if (required <= _pendingRemovalCapacity)
            {
                return;
            }

            int newCapacity = _pendingRemovalCapacity * 2;
            while (newCapacity < required)
            {
                newCapacity *= 2;
            }

            var newEntities = (EntityRef*)_allocator->Alloc(sizeof(EntityRef) * newCapacity);
            var newTypeIds = (ushort*)_allocator->Alloc(sizeof(ushort) * newCapacity);

            Buffer.MemoryCopy(_pendingRemovalEntities, newEntities, sizeof(EntityRef) * newCapacity, sizeof(EntityRef) * _pendingRemovalCount);
            Buffer.MemoryCopy(_pendingRemovalTypeIds, newTypeIds, sizeof(ushort) * newCapacity, sizeof(ushort) * _pendingRemovalCount);

            _pendingRemovalEntities = newEntities;
            _pendingRemovalTypeIds = newTypeIds;
            _pendingRemovalCapacity = newCapacity;
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void InvokeOnAdded<T>(EntityRef entity, Storage<T>* storage) where T : unmanaged, IComponent
        {
            ComponentChangedDelegate? onAdded = ComponentTypeId<T>.Callbacks.OnAdded;
            if (onAdded == null)
            {
                return;
            }

            T* addedComponent = storage->GetPointer(entity);
            if (addedComponent != null)
            {
                onAdded(entity, addedComponent, this);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void NotifyOwningGroups(EntityRef entity, int typeId)
        {
            if (_owningGroupBindings == null || _owningGroupBindings.Count == 0)
            {
                return;
            }

            for (int i = 0; i < _owningGroupBindings.Count; i++)
            {
                IFrameOwningGroup binding = _owningGroupBindings[i];
                if (binding.DependsOn(typeId))
                {
                    binding.SyncEntity(this, entity, typeId);
                }
            }
        }

        private void RebuildOwningGroups()
        {
            if (_owningGroupBindings == null || _owningGroupBindings.Count == 0)
            {
                return;
            }

            for (int i = 0; i < _owningGroupBindings.Count; i++)
            {
                _owningGroupBindings[i].Rebuild(this);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong ComposeOwningGroupKey(int arity, int typeId1, int typeId2, int typeId3)
        {
            return ((ulong)(uint)arity << 48)
                | ((ulong)(uint)typeId1 << 32)
                | ((ulong)(uint)typeId2 << 16)
                | (uint)typeId3;
        }

        private void InitializeState(int maxEntities, bool preserveOwningGroups = false)
        {
            _entityCapacity = maxEntities;
            _entityCount = 0;
            _freeListHead = -1;
            Tick = 0;
            DeltaTime = FP.Zero;

            _allocator = Allocator.Create();

            _entities = (EntityRef*)_allocator->Alloc(sizeof(EntityRef) * maxEntities);
            _entityVersions = (ushort*)_allocator->Alloc(sizeof(ushort) * maxEntities);
            _entityNextFree = (int*)_allocator->Alloc(sizeof(int) * maxEntities);
            _entityComponentMasks = (ulong*)_allocator->Alloc(sizeof(ulong) * maxEntities * ComponentMaskBlockCount);

            for (int i = 0; i < maxEntities; i++)
            {
                _entityVersions[i] = 1;
                _entityNextFree[i] = -1;
            }

            _componentStorages = (void**)_allocator->Alloc(sizeof(void*) * MaxComponentTypes);
            _storageInitialized = (bool*)_allocator->Alloc(sizeof(bool) * MaxComponentTypes);

            for (int i = 0; i < MaxComponentTypes; i++)
            {
                _componentStorages[i] = null;
                _storageInitialized[i] = false;
            }

            _pendingRemovalCapacity = 64;
            _pendingRemovalCount = 0;
            _pendingRemovalEntities = (EntityRef*)_allocator->Alloc(sizeof(EntityRef) * _pendingRemovalCapacity);
            _pendingRemovalTypeIds = (ushort*)_allocator->Alloc(sizeof(ushort) * _pendingRemovalCapacity);

            if (!preserveOwningGroups || _owningGroups == null || _owningGroupBindings == null)
            {
                _owningGroups = new Dictionary<ulong, object>();
                _owningGroupBindings = new List<IFrameOwningGroup>();
            }
        }

        private void ReleaseState(bool preserveOwningGroups = false)
        {
            if (!preserveOwningGroups)
            {
                _owningGroups?.Clear();
                _owningGroupBindings?.Clear();
                _owningGroups = null;
                _owningGroupBindings = null;
            }

            if (_storageInitialized != null && _componentStorages != null)
            {
                for (int i = 0; i < MaxComponentTypes; i++)
                {
                    _componentStorages[i] = null;
                    _storageInitialized[i] = false;
                }
            }

            if (_allocator != null)
            {
                Allocator.Destroy(_allocator);
                _allocator = null;
            }

            _entities = null;
            _entityVersions = null;
            _entityNextFree = null;
            _entityComponentMasks = null;
            _componentStorages = null;
            _storageInitialized = null;
            _pendingRemovalEntities = null;
            _pendingRemovalTypeIds = null;
            _pendingRemovalCount = 0;
            _pendingRemovalCapacity = 0;
            _entityCapacity = 0;
            _entityCount = 0;
            _freeListHead = -1;
            Tick = 0;
            DeltaTime = FP.Zero;
        }

        private void ReinitializeOwningGroups()
        {
            if (_owningGroupBindings == null || _owningGroupBindings.Count == 0)
            {
                return;
            }

            for (int i = 0; i < _owningGroupBindings.Count; i++)
            {
                _owningGroupBindings[i].Reinitialize(_allocator, _entityCapacity);
            }
        }

        #endregion
    }
}
