// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Lattice.Core;
using Lattice.ECS.Framework;
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

        /// <summary>结构性修改命令缓冲。</summary>
        private CommandBuffer _structuralCommandBuffer;

        /// <summary>结构性修改命令缓冲是否已初始化。</summary>
        private bool _structuralCommandBufferInitialized;

        /// <summary>当前是否正在延迟结构性修改。</summary>
        private bool _deferStructuralChanges;

        /// <summary>当前是否正在回放结构性修改。</summary>
        private bool _isReplayingStructuralChanges;

        /// <summary>当前是否处于系统作者契约守卫作用域。</summary>
        private bool _hasActiveSystemAuthoringContract;

        /// <summary>当前执行系统的作者契约。</summary>
        private SystemAuthoringContract _activeSystemAuthoringContract;

        /// <summary>当前执行系统名称，仅用于守卫报错。</summary>
        private string? _activeSystemName;

        #endregion

        #region 属性

        /// <summary>当前帧号</summary>
        public int Tick { get; set; }

        /// <summary>固定时间步长</summary>
        public FP DeltaTime
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get;
            set;
        }

        /// <summary>实体数量</summary>
        public int EntityCount => _entityCount;

        /// <summary>实体容量。</summary>
        internal int EntityCapacity => _entityCapacity;

        /// <summary>待提交的延迟删除数量。</summary>
        public int PendingDeferredRemovalCount => _pendingRemovalCount;

        /// <summary>
        /// 当前是否存在尚未提交的结构性修改。
        /// 主要用于运行时 Tick 管线与测试验证，不属于快照状态的一部分。
        /// </summary>
        public bool HasPendingStructuralChanges => _structuralCommandBufferInitialized && !_structuralCommandBuffer.IsEmpty;

        #endregion

        #region 构造函数

        public Frame(int maxEntities = 65536)
        {
            InitializeState(maxEntities);
        }

        #endregion

        #region 快照与复制

        /// <summary>
        /// 创建帧状态对象图快照。
        /// 该接口主要保留给兼容、调试和显式恢复测试，不用于 Session 热路径。
        /// </summary>
        [Obsolete("请优先改用 CapturePackedSnapshot()。CreateSnapshot() 仅保留给兼容、调试和显式恢复测试。", false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public FrameSnapshot CreateSnapshot(ComponentSerializationMode mode = ComponentSerializationMode.Default)
        {
            int metadataCount = _entityCount;
            var entityVersions = new ushort[metadataCount];
            var entityNextFree = new int[metadataCount];
            var entityComponentMasks = new ulong[metadataCount * ComponentMaskBlockCount];

            if (metadataCount > 0)
            {
                new ReadOnlySpan<ushort>(_entityVersions, metadataCount).CopyTo(entityVersions);
                new ReadOnlySpan<int>(_entityNextFree, metadataCount).CopyTo(entityNextFree);
                new ReadOnlySpan<ulong>(_entityComponentMasks, entityComponentMasks.Length).CopyTo(entityComponentMasks);
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

        /// <summary>
        /// 创建紧凑字节帧快照。
        /// 主要用于 Session 的 checkpoint 与采样历史。
        /// 当前会同时记录 packed snapshot 格式版本和本次实际写出的组件 schema 摘要。
        /// </summary>
        public PackedFrameSnapshot CapturePackedSnapshot(ComponentSerializationMode mode = ComponentSerializationMode.Default)
        {
            var writer = new FrameStateBufferWriter();
            ComponentSchemaManifest schemaManifest = WritePackedState(writer, mode);
            return writer.ToSnapshot(Tick, _entityCapacity, schemaManifest);
        }

        #endregion

        /// <summary>
        /// 从对象图快照恢复帧状态。
        /// 该接口主要保留给兼容 API 与测试使用。
        /// </summary>
        [Obsolete("请优先改用 RestoreFromPackedSnapshot()。RestoreFromSnapshot() 仅保留给兼容 API 与测试使用。", false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public void RestoreFromSnapshot(FrameSnapshot snapshot, ComponentSerializationMode mode = ComponentSerializationMode.Default)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            PrepareForSnapshotRestore(snapshot.EntityCapacity, reuseExistingState: false);

            Tick = snapshot.Tick;
            DeltaTime = snapshot.DeltaTime;
            _entityCount = snapshot.EntityCount;
            _freeListHead = snapshot.FreeListHead;
            int metadataCount = snapshot.EntityVersions.Length;

            if (metadataCount < snapshot.EntityCount ||
                snapshot.EntityNextFree.Length != metadataCount ||
                snapshot.EntityComponentMasks.Length != metadataCount * ComponentMaskBlockCount)
            {
                throw new InvalidOperationException("Snapshot entity metadata size does not match frame capacity.");
            }

            for (int i = 0; i < metadataCount; i++)
            {
                _entityVersions[i] = snapshot.EntityVersions[i];
                _entityNextFree[i] = snapshot.EntityNextFree[i];
                _entities[i] = i < _entityCount ? new EntityRef(i, _entityVersions[i]) : EntityRef.None;
            }

            for (int i = metadataCount; i < _entityCapacity; i++)
            {
                _entityVersions[i] = 1;
                _entityNextFree[i] = -1;
                _entities[i] = EntityRef.None;
            }

            for (int i = 0; i < snapshot.EntityComponentMasks.Length; i++)
            {
                _entityComponentMasks[i] = snapshot.EntityComponentMasks[i];
            }

            for (int i = snapshot.EntityComponentMasks.Length; i < _entityCapacity * ComponentMaskBlockCount; i++)
            {
                _entityComponentMasks[i] = 0;
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
            }

            FinalizeSnapshotRestore();
        }

        /// <summary>
        /// 从紧凑字节帧快照恢复状态。
        /// </summary>
        public void RestoreFromPackedSnapshot(PackedFrameSnapshot snapshot, ComponentSerializationMode mode = ComponentSerializationMode.Default)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (snapshot.FormatVersion != PackedFrameSnapshot.CurrentFormatVersion)
            {
                throw new InvalidOperationException(
                    $"Packed snapshot format version {snapshot.FormatVersion} is not compatible with current runtime format version {PackedFrameSnapshot.CurrentFormatVersion}.");
            }

            if (snapshot.SchemaManifest.IsSpecified && snapshot.SchemaManifest.SerializationMode != mode)
            {
                throw new InvalidOperationException(
                    $"Packed snapshot was captured with serialization mode {snapshot.SchemaManifest.SerializationMode}, but restore requested mode {mode}.");
            }

            var reader = new FrameStateReader(snapshot.Data, snapshot.Length);

            int tick = reader.ReadInt32();
            long deltaTimeRaw = reader.ReadInt64();
            int entityCapacity = reader.ReadInt32();
            int entityCount = reader.ReadInt32();
            int freeListHead = reader.ReadInt32();

            PrepareForSnapshotRestore(entityCapacity, reuseExistingState: true);

            Tick = tick;
            DeltaTime = FP.FromRaw(deltaTimeRaw);
            _entityCount = entityCount;
            _freeListHead = freeListHead;

            int metadataCount = reader.ReadInt32();
            if (metadataCount < entityCount)
            {
                throw new InvalidOperationException("Packed snapshot entity metadata size does not match frame capacity.");
            }

            for (int i = 0; i < metadataCount; i++)
            {
                _entityVersions[i] = reader.ReadUInt16();
            }

            for (int i = metadataCount; i < _entityCapacity; i++)
            {
                _entityVersions[i] = 1;
            }

            int nextFreeCount = reader.ReadInt32();
            if (nextFreeCount != metadataCount)
            {
                throw new InvalidOperationException("Packed snapshot next-free metadata size does not match entity metadata.");
            }

            for (int i = 0; i < metadataCount; i++)
            {
                _entityNextFree[i] = reader.ReadInt32();
                _entities[i] = i < _entityCount ? new EntityRef(i, _entityVersions[i]) : EntityRef.None;
            }

            for (int i = metadataCount; i < _entityCapacity; i++)
            {
                _entityNextFree[i] = -1;
                _entities[i] = EntityRef.None;
            }

            int maskCount = reader.ReadInt32();
            if (maskCount != metadataCount * ComponentMaskBlockCount)
            {
                throw new InvalidOperationException("Packed snapshot component mask size does not match entity metadata.");
            }

            ReadOnlySpan<byte> maskBytes = reader.ReadBytes(maskCount * sizeof(ulong));
            fixed (byte* maskPtr = maskBytes)
            {
                Buffer.MemoryCopy(maskPtr, _entityComponentMasks, sizeof(ulong) * _entityCapacity * ComponentMaskBlockCount, maskBytes.Length);
            }

            for (int i = maskCount; i < _entityCapacity * ComponentMaskBlockCount; i++)
            {
                _entityComponentMasks[i] = 0;
            }

            int pendingEntityCount = reader.ReadInt32();
            EnsurePendingRemovalCapacity(pendingEntityCount);
            _pendingRemovalCount = pendingEntityCount;
            for (int i = 0; i < pendingEntityCount; i++)
            {
                _pendingRemovalEntities[i] = EntityRef.FromRaw(reader.ReadUInt64());
            }

            int pendingTypeCount = reader.ReadInt32();
            if (pendingTypeCount != pendingEntityCount)
            {
                throw new InvalidOperationException("Packed snapshot pending removal metadata size does not match.");
            }

            for (int i = 0; i < pendingTypeCount; i++)
            {
                _pendingRemovalTypeIds[i] = reader.ReadUInt16();
            }

            int storageCount = reader.ReadInt32();
            ResetInitializedStorages();
            Span<int> serializedTypeIds = stackalloc int[MaxComponentTypes];

            for (int i = 0; i < storageCount; i++)
            {
                int typeId = reader.ReadInt32();
                serializedTypeIds[i] = typeId;
                ComponentRegistry.GetPackedStateRestorer(typeId)(this, reader, mode);
                _storageInitialized[typeId] = true;
            }

            if (snapshot.SchemaManifest.IsSpecified)
            {
                ComponentSchemaManifest currentSchema = ComponentRegistry.CreateSchemaManifest(serializedTypeIds[..storageCount], mode);
                if (!currentSchema.Equals(snapshot.SchemaManifest))
                {
                    throw new InvalidOperationException(
                        $"Packed snapshot component schema fingerprint does not match the current registry for mode {mode}. " +
                        $"Snapshot={snapshot.SchemaManifest.Fingerprint}, Current={currentSchema.Fingerprint}.");
                }
            }

            FinalizeSnapshotRestore();
        }

        /// <summary>
        /// 克隆当前帧。
        /// </summary>
        public Frame Clone(ComponentSerializationMode mode = ComponentSerializationMode.Default)
        {
            var clone = new Frame(_entityCapacity);
            clone.RestoreFromPackedSnapshot(CapturePackedSnapshot(mode), mode);
            return clone;
        }

        /// <summary>
        /// 克隆当前帧的完整运行时状态。
        /// 该路径保留所有组件状态，供预测推进、回滚和历史补帧复用。
        /// </summary>
        public Frame CloneState()
        {
            var clone = new Frame(_entityCapacity);
            clone.CopyStateFrom(this);
            return clone;
        }

        /// <summary>
        /// 从另一个帧复制状态到当前帧。
        /// </summary>
        public void CopyFrom(Frame source, ComponentSerializationMode mode = ComponentSerializationMode.Default)
        {
            ArgumentNullException.ThrowIfNull(source);
            RestoreFromPackedSnapshot(source.CapturePackedSnapshot(mode), mode);
        }

        /// <summary>
        /// 从另一个帧直接复制完整运行时状态，避免热路径走托管快照分配。
        /// </summary>
        public void CopyStateFrom(Frame source)
        {
            ArgumentNullException.ThrowIfNull(source);

            if (ReferenceEquals(this, source))
            {
                return;
            }

            bool preserveOwningGroups = _owningGroupBindings != null && _owningGroupBindings.Count > 0;
            EnsureCompatibleStateForCopy(source._entityCapacity, preserveOwningGroups);
            ResetStructuralChangeTracking();

            Tick = source.Tick;
            DeltaTime = source.DeltaTime;
            _entityCount = source._entityCount;
            _freeListHead = source._freeListHead;

            int entityBytes = sizeof(EntityRef) * source._entityCapacity;
            int versionBytes = sizeof(ushort) * source._entityCapacity;
            int nextFreeBytes = sizeof(int) * source._entityCapacity;
            int maskBytes = sizeof(ulong) * source._entityCapacity * ComponentMaskBlockCount;

            Buffer.MemoryCopy(source._entities, _entities, entityBytes, entityBytes);
            Buffer.MemoryCopy(source._entityVersions, _entityVersions, versionBytes, versionBytes);
            Buffer.MemoryCopy(source._entityNextFree, _entityNextFree, nextFreeBytes, nextFreeBytes);
            Buffer.MemoryCopy(source._entityComponentMasks, _entityComponentMasks, maskBytes, maskBytes);

            EnsurePendingRemovalCapacity(source._pendingRemovalCount);
            _pendingRemovalCount = source._pendingRemovalCount;
            if (_pendingRemovalCount > 0)
            {
                int pendingEntityBytes = sizeof(EntityRef) * _pendingRemovalCount;
                int pendingTypeBytes = sizeof(ushort) * _pendingRemovalCount;
                Buffer.MemoryCopy(source._pendingRemovalEntities, _pendingRemovalEntities, pendingEntityBytes, pendingEntityBytes);
                Buffer.MemoryCopy(source._pendingRemovalTypeIds, _pendingRemovalTypeIds, pendingTypeBytes, pendingTypeBytes);
            }

            for (int typeId = 1; typeId <= ComponentRegistry.Count; typeId++)
            {
                if (source._storageInitialized[typeId] && source._componentStorages[typeId] != null)
                {
                    ComponentRegistry.GetStorageCopier(typeId)(this, source._componentStorages[typeId]);
                    _storageInitialized[typeId] = true;
                    continue;
                }

                if (_storageInitialized[typeId] && _componentStorages[typeId] != null)
                {
                    ComponentRegistry.GetStorageResetter(typeId)(_componentStorages[typeId]);
                }
            }

            RebuildOwningGroups();
        }

        /// <summary>
        /// 计算帧校验和。
        /// </summary>
        public ulong CalculateChecksum(ComponentSerializationMode mode = ComponentSerializationMode.Checkpoint)
        {
            var writer = new FrameStateChecksumWriter();
            WritePackedState(writer, mode);
            return writer.Checksum;
        }

        #region 实体操作

        /// <summary>
        /// 创建实体 - O(1)。
        /// 若当前帧正处于运行时的结构性修改延迟阶段，则返回临时实体引用，并在后续提交阶段真正物化。
        /// </summary>
        public EntityRef CreateEntity()
        {
            EnsureStructuralChangesAllowed(nameof(CreateEntity));

            if (ShouldDeferStructuralChanges())
            {
                return QueueCreateEntity();
            }

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
            EnsureStructuralChangesAllowed(nameof(DestroyEntity));

            if (ShouldDeferStructuralChanges())
            {
                QueueDestroyEntity(entity);
                return;
            }

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
        /// 添加组件 - O(1) 均摊。
        /// 若当前帧正处于运行时的结构性修改延迟阶段，则该操作会先排入结构提交缓冲，而不是立即影响查询拓扑。
        /// </summary>
        public void Add<T>(EntityRef entity, in T component) where T : unmanaged, IComponent
        {
            EnsureStructuralChangesAllowed(nameof(Add));

            if (ShouldDeferStructuralChanges())
            {
                QueueAddComponent(entity, component);
                return;
            }

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
        /// 当运行时开启结构性修改延迟时，对已存在组件的字段写入仍会立即生效；
        /// 但对缺失组件或临时实体的写入会延迟到结构提交阶段统一生效。
        /// </summary>
        public void Set<T>(EntityRef entity, in T component) where T : unmanaged, IComponent
        {
            if (ShouldDeferSet<T>(entity))
            {
                EnsureStructuralChangesAllowed(nameof(Set));
                QueueSetComponent(entity, component);
                return;
            }

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
        /// 移除组件 - O(1)。
        /// 若当前帧正处于运行时的结构性修改延迟阶段，则移除会延迟到结构提交阶段统一生效。
        /// </summary>
        public void Remove<T>(EntityRef entity) where T : unmanaged, IComponent
        {
            EnsureStructuralChangesAllowed(nameof(Remove));

            if (ShouldDeferStructuralChanges())
            {
                QueueRemoveComponent<T>(entity);
                return;
            }

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
        /// 检查当前帧是否存在指定的全局 singleton 组件。
        /// 该 API 是玩法层正式推荐的全局状态入口，不再要求外部自己约定“特殊实体”。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasGlobal<T>() where T : unmanaged, IComponent
        {
            EnsureGlobalReadAllowed(nameof(HasGlobal));

            int typeId = ComponentTypeId<T>.Id;
            EnsureSingletonComponentType<T>(typeId);
            var storage = GetStorage<T>(typeId);
            return storage != null && storage->SingletonEntity != EntityRef.None;
        }

        /// <summary>
        /// 获取全局 singleton 组件引用。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T GetGlobal<T>() where T : unmanaged, IComponent
        {
            EnsureGlobalWriteAllowed(nameof(GetGlobal));

            int typeId = ComponentTypeId<T>.Id;
            EnsureSingletonComponentType<T>(typeId);

            var storage = GetStorage<T>(typeId);
            if (storage == null || storage->SingletonEntity == EntityRef.None)
            {
                throw new InvalidOperationException($"Global component {typeof(T).Name} not found.");
            }

            return ref storage->GetSingleton();
        }

        /// <summary>
        /// 尝试获取全局 singleton 组件值。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetGlobal<T>(out T component) where T : unmanaged, IComponent
        {
            EnsureGlobalReadAllowed(nameof(TryGetGlobal));

            int typeId = ComponentTypeId<T>.Id;
            EnsureSingletonComponentType<T>(typeId);

            var storage = GetStorage<T>(typeId);
            if (storage != null && storage->SingletonEntity != EntityRef.None)
            {
                component = storage->GetSingleton();
                return true;
            }

            component = default;
            return false;
        }

        /// <summary>
        /// 获取全局 singleton 组件所在实体。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public EntityRef GetGlobalEntity<T>() where T : unmanaged, IComponent
        {
            EnsureGlobalWriteAllowed(nameof(GetGlobalEntity));

            int typeId = ComponentTypeId<T>.Id;
            EnsureSingletonComponentType<T>(typeId);

            var storage = GetStorage<T>(typeId);
            if (storage == null || storage->SingletonEntity == EntityRef.None)
            {
                throw new InvalidOperationException($"Global component {typeof(T).Name} not found.");
            }

            return storage->SingletonEntity;
        }

        /// <summary>
        /// 尝试获取全局 singleton 组件所在实体。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetGlobalEntity<T>(out EntityRef entity) where T : unmanaged, IComponent
        {
            EnsureGlobalWriteAllowed(nameof(TryGetGlobalEntity));

            int typeId = ComponentTypeId<T>.Id;
            EnsureSingletonComponentType<T>(typeId);

            var storage = GetStorage<T>(typeId);
            if (storage != null && storage->SingletonEntity != EntityRef.None)
            {
                entity = storage->SingletonEntity;
                return true;
            }

            entity = EntityRef.None;
            return false;
        }

        /// <summary>
        /// 设置全局 singleton 组件。
        /// 若当前尚未存在对应全局状态，则会自动创建承载它的实体。
        /// </summary>
        public EntityRef SetGlobal<T>(in T component) where T : unmanaged, IComponent
        {
            EnsureGlobalWriteAllowed(nameof(SetGlobal));

            int typeId = ComponentTypeId<T>.Id;
            EnsureSingletonComponentType<T>(typeId);

            var storage = GetStorage<T>(typeId);
            if (storage != null && storage->SingletonEntity != EntityRef.None)
            {
                EntityRef entity = storage->SingletonEntity;
                Set(entity, component);
                return entity;
            }

            EntityRef created = CreateEntity();
            Add(created, component);
            return created;
        }

        /// <summary>
        /// 移除全局 singleton 组件。
        /// 若该组件是实体上唯一的组件，则会一并销毁其承载实体，避免留下空壳全局实体。
        /// </summary>
        public bool RemoveGlobal<T>() where T : unmanaged, IComponent
        {
            EnsureGlobalWriteAllowed(nameof(RemoveGlobal));

            int typeId = ComponentTypeId<T>.Id;
            EnsureSingletonComponentType<T>(typeId);

            var storage = GetStorage<T>(typeId);
            if (storage == null || storage->SingletonEntity == EntityRef.None)
            {
                return false;
            }

            EntityRef entity = storage->SingletonEntity;
            if (EntityHasOnlyComponent(entity, typeId))
            {
                DestroyEntity(entity);
                return true;
            }

            Remove<T>(entity);
            return true;
        }

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
            if (ShouldDeferStructuralChanges())
            {
                throw new InvalidOperationException(
                    "Cannot commit deferred removals while structural changes are being deferred. " +
                    "Wait for the runtime structural commit stage to complete.");
            }

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

        /// <summary>
        /// 进入“结构性修改延迟提交”模式。
        /// 在该模式下，实体创建/销毁与组件增删会先进入命令缓冲，直到显式提交时才生效。
        /// </summary>
        internal void BeginDeferredStructuralChanges()
        {
            if (_deferStructuralChanges)
            {
                throw new InvalidOperationException("Deferred structural changes have already been started for this frame.");
            }

            EnsureStructuralCommandBuffer();
            _structuralCommandBuffer.Clear();
            _deferStructuralChanges = true;
            _isReplayingStructuralChanges = false;
        }

        /// <summary>
        /// 提交当前 Tick 累积的结构性修改。
        /// </summary>
        internal void CommitStructuralChanges()
        {
            if (!_deferStructuralChanges && !_isReplayingStructuralChanges)
            {
                CommitDeferredRemovals();
                return;
            }

            _deferStructuralChanges = false;

            if (!_structuralCommandBufferInitialized || _structuralCommandBuffer.IsEmpty)
            {
                CommitDeferredRemovals();
                return;
            }

            _isReplayingStructuralChanges = true;
            try
            {
                _structuralCommandBuffer.Playback(this);
            }
            finally
            {
                _isReplayingStructuralChanges = false;
            }
        }

        /// <summary>
        /// 中止当前 Tick 尚未提交的结构性修改。
        /// 仅供运行时在 Tick 失败或异常路径下回收临时状态。
        /// </summary>
        internal void AbortStructuralChanges()
        {
            if (_structuralCommandBufferInitialized)
            {
                _structuralCommandBuffer.Clear();
            }

            _deferStructuralChanges = false;
            _isReplayingStructuralChanges = false;
        }

        /// <summary>
        /// 进入系统作者契约守卫作用域。
        /// 仅供 `SystemScheduler` 在系统 `OnUpdate(...)` 执行前后使用。
        /// </summary>
        internal void BeginSystemAuthoringScope(ISystem system)
        {
            ArgumentNullException.ThrowIfNull(system);

            if (_hasActiveSystemAuthoringContract)
            {
                throw new InvalidOperationException("A system authoring scope is already active for this frame.");
            }

            _activeSystemAuthoringContract = system.Contract;
            _activeSystemName = system.GetType().Name;
            _hasActiveSystemAuthoringContract = true;
        }

        /// <summary>
        /// 退出系统作者契约守卫作用域。
        /// </summary>
        internal void EndSystemAuthoringScope()
        {
            ResetSystemAuthoringGuard();
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
        /// 获取四组件查询。
        /// 这是当前正式公开的强类型 Query 上限；
        /// 更高维组合应优先考虑拆分系统职责，或结合 `MatchesFilter(...)` 做显式条件匹配。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Query<T1, T2, T3, T4> Query<T1, T2, T3, T4>()
            where T1 : unmanaged, IComponent
            where T2 : unmanaged, IComponent
            where T3 : unmanaged, IComponent
            where T4 : unmanaged, IComponent
        {
            return new Query<T1, T2, T3, T4>(this);
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void EnsureSingletonComponentType<T>(int typeId) where T : unmanaged, IComponent
        {
            if ((ComponentRegistry.GetStorageFlags(typeId) & StorageFlags.Singleton) == 0)
            {
                throw new InvalidOperationException(
                    $"Component {typeof(T).Name} is not registered as a singleton/global component.");
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool EntityHasOnlyComponent(EntityRef entity, int typeId)
        {
            if (!IsValid(entity))
            {
                return false;
            }

            ulong* mask = GetComponentMaskPointer(entity.Index);
            int targetBlock = typeId >> 6;
            ulong targetBit = 1UL << (typeId & 0x3F);

            for (int block = 0; block < ComponentMaskBlockCount; block++)
            {
                ulong value = mask[block];
                if (block == targetBlock)
                {
                    if (value != targetBit)
                    {
                        return false;
                    }

                    continue;
                }

                if (value != 0)
                {
                    return false;
                }
            }

            return true;
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

        private ComponentSchemaManifest WritePackedState(FrameStateWriter writer, ComponentSerializationMode mode)
        {
            writer.WriteInt32(Tick);
            writer.WriteInt64(DeltaTime.RawValue);
            writer.WriteInt32(_entityCapacity);
            writer.WriteInt32(_entityCount);
            writer.WriteInt32(_freeListHead);

            int metadataCount = _entityCount;
            writer.WriteInt32(metadataCount);
            for (int i = 0; i < metadataCount; i++)
            {
                writer.WriteUInt16(_entityVersions[i]);
            }

            writer.WriteInt32(metadataCount);
            for (int i = 0; i < metadataCount; i++)
            {
                writer.WriteInt32(_entityNextFree[i]);
            }

            int maskCount = metadataCount * ComponentMaskBlockCount;
            writer.WriteInt32(maskCount);
            if (maskCount > 0)
            {
                writer.WriteBytes(_entityComponentMasks, maskCount * sizeof(ulong));
            }

            writer.WriteInt32(_pendingRemovalCount);
            for (int i = 0; i < _pendingRemovalCount; i++)
            {
                writer.WriteUInt64(_pendingRemovalEntities[i].Raw);
            }

            writer.WriteInt32(_pendingRemovalCount);
            for (int i = 0; i < _pendingRemovalCount; i++)
            {
                writer.WriteUInt16(_pendingRemovalTypeIds[i]);
            }

            Span<int> serializableTypeIds = stackalloc int[MaxComponentTypes];
            int storageCount = 0;
            for (int typeId = 1; typeId <= ComponentRegistry.Count; typeId++)
            {
                if (!_storageInitialized[typeId] || _componentStorages[typeId] == null)
                {
                    continue;
                }

                if (!ComponentRegistry.CanSerializeComponent(typeId, mode))
                {
                    continue;
                }

                if (!ComponentRegistry.GetPackedStatePresenceChecker(typeId)(_componentStorages[typeId], mode))
                {
                    continue;
                }

                serializableTypeIds[storageCount++] = typeId;
            }

            ComponentSchemaManifest schemaManifest = ComponentRegistry.CreateSchemaManifest(serializableTypeIds[..storageCount], mode);

            writer.WriteInt32(storageCount);
            for (int i = 0; i < storageCount; i++)
            {
                int typeId = serializableTypeIds[i];
                ComponentRegistry.GetPackedStateWriter(typeId)(_componentStorages[typeId], writer, mode);
            }

            return schemaManifest;
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

            _structuralCommandBuffer = default;
            _structuralCommandBufferInitialized = false;
            _deferStructuralChanges = false;
            _isReplayingStructuralChanges = false;
            ResetSystemAuthoringGuard();
        }

        private void PrepareForSnapshotRestore(int entityCapacity, bool reuseExistingState)
        {
            bool preserveOwningGroups = _owningGroupBindings != null && _owningGroupBindings.Count > 0;
            if (reuseExistingState)
            {
                EnsureCompatibleStateForCopy(entityCapacity, preserveOwningGroups);
                return;
            }

            ReleaseState(preserveOwningGroups);
            InitializeState(entityCapacity, preserveOwningGroups);

            if (preserveOwningGroups)
            {
                ReinitializeOwningGroups();
            }
        }

        private void ResetInitializedStorages()
        {
            for (int typeId = 1; typeId <= ComponentRegistry.Count; typeId++)
            {
                if (_storageInitialized[typeId] && _componentStorages[typeId] != null)
                {
                    ComponentRegistry.GetStorageResetter(typeId)(_componentStorages[typeId]);
                }
            }
        }

        private void FinalizeSnapshotRestore()
        {
            ResetStructuralChangeTracking();

            for (int i = 0; i < _pendingRemovalCount; i++)
            {
                int typeId = _pendingRemovalTypeIds[i];
                if (_storageInitialized[typeId] && _componentStorages[typeId] != null)
                {
                    ComponentRegistry.GetPendingRemovalMarker(typeId)(_componentStorages[typeId], _pendingRemovalEntities[i]);
                }
            }

            RebuildOwningGroups();
        }

        private void EnsureCompatibleStateForCopy(int sourceEntityCapacity, bool preserveOwningGroups)
        {
            if (_allocator != null && _entityCapacity == sourceEntityCapacity)
            {
                return;
            }

            ReleaseState(preserveOwningGroups);
            InitializeState(sourceEntityCapacity, preserveOwningGroups);

            if (preserveOwningGroups)
            {
                ReinitializeOwningGroups();
            }
        }

        private void ReleaseState(bool preserveOwningGroups = false)
        {
            ReleaseStructuralCommandBuffer();

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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ShouldDeferStructuralChanges()
        {
            return _deferStructuralChanges && !_isReplayingStructuralChanges;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ShouldDeferSet<T>(EntityRef entity) where T : unmanaged, IComponent
        {
            if (!ShouldDeferStructuralChanges())
            {
                return false;
            }

            if (entity.Index < 0)
            {
                return true;
            }

            if (!IsValid(entity))
            {
                return false;
            }

            return !Has<T>(entity);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private EntityRef QueueCreateEntity()
        {
            EnsureStructuralCommandBuffer();
            return _structuralCommandBuffer.CreateEntity();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void QueueDestroyEntity(EntityRef entity)
        {
            if (entity.Index >= 0 && !IsValid(entity))
            {
                return;
            }

            EnsureStructuralCommandBuffer();
            _structuralCommandBuffer.DestroyEntity(entity);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void QueueAddComponent<T>(EntityRef entity, in T component) where T : unmanaged, IComponent
        {
            if (entity.Index >= 0 && !IsValid(entity))
            {
                throw new ArgumentException($"Invalid entity: {entity}");
            }

            EnsureStructuralCommandBuffer();
            _structuralCommandBuffer.AddComponent(entity, component);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void QueueSetComponent<T>(EntityRef entity, in T component) where T : unmanaged, IComponent
        {
            if (entity.Index >= 0 && !IsValid(entity))
            {
                throw new ArgumentException($"Invalid entity: {entity}");
            }

            EnsureStructuralCommandBuffer();
            _structuralCommandBuffer.SetComponent(entity, component);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void QueueRemoveComponent<T>(EntityRef entity) where T : unmanaged, IComponent
        {
            if (entity.Index >= 0 && !IsValid(entity))
            {
                return;
            }

            EnsureStructuralCommandBuffer();
            _structuralCommandBuffer.RemoveComponent<T>(entity);
        }

        private void EnsureStructuralCommandBuffer()
        {
            if (_structuralCommandBufferInitialized)
            {
                return;
            }

            _structuralCommandBuffer.Initialize(capacity: 1024);
            _structuralCommandBufferInitialized = true;
        }

        private void ResetStructuralChangeTracking()
        {
            if (_structuralCommandBufferInitialized)
            {
                _structuralCommandBuffer.Clear();
            }

            _deferStructuralChanges = false;
            _isReplayingStructuralChanges = false;
            ResetSystemAuthoringGuard();
        }

        private void ReleaseStructuralCommandBuffer()
        {
            if (_structuralCommandBufferInitialized)
            {
                _structuralCommandBuffer.Dispose();
                _structuralCommandBufferInitialized = false;
            }

            _structuralCommandBuffer = default;
            _deferStructuralChanges = false;
            _isReplayingStructuralChanges = false;
            ResetSystemAuthoringGuard();
        }

        private void EnsureGlobalReadAllowed(string apiName)
        {
            if (!_hasActiveSystemAuthoringContract || _activeSystemAuthoringContract.AllowsGlobalReads)
            {
                return;
            }

            throw new InvalidOperationException(
                $"System {_activeSystemName} cannot call {apiName} because its contract does not allow global state access.");
        }

        private void EnsureGlobalWriteAllowed(string apiName)
        {
            if (!_hasActiveSystemAuthoringContract || _activeSystemAuthoringContract.AllowsGlobalWrites)
            {
                return;
            }

            throw new InvalidOperationException(
                $"System {_activeSystemName} cannot call {apiName} because its contract does not allow writable global state access.");
        }

        private void EnsureStructuralChangesAllowed(string apiName)
        {
            if (!_hasActiveSystemAuthoringContract || _activeSystemAuthoringContract.AllowsStructuralChanges)
            {
                return;
            }

            throw new InvalidOperationException(
                $"System {_activeSystemName} cannot call {apiName} because its contract does not allow structural changes.");
        }

        private void ResetSystemAuthoringGuard()
        {
            _hasActiveSystemAuthoringContract = false;
            _activeSystemAuthoringContract = SystemAuthoringContract.Default;
            _activeSystemName = null;
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
