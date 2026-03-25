// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

using System;
using System.Collections.Generic;
using Lattice.Core;
using Lattice.ECS.Serialization;

namespace Lattice.ECS.Core
{
    /// <summary>
    /// 组件类型元数据。
    /// </summary>
    public readonly struct ComponentTypeInfo
    {
        public ComponentTypeInfo(
            Type type,
            int id,
            int size,
            ComponentFlags flags,
            StorageFlags storageFlags,
            ComponentCallbacks callbacks)
        {
            Type = type;
            Id = id;
            Size = size;
            Flags = flags;
            StorageFlags = storageFlags;
            Callbacks = callbacks;
        }

        public Type Type { get; }

        public string Name => Type.Name;

        public int Id { get; }

        public int Size { get; }

        public ComponentFlags Flags { get; }

        public StorageFlags StorageFlags { get; }

        public ComponentCallbacks Callbacks { get; }
    }

    /// <summary>
    /// 组件类型注册中心，负责维护运行期组件元数据。
    /// </summary>
    public static unsafe class ComponentRegistry
    {
        private static readonly object SyncRoot = new();
        private static readonly Type?[] Types = new Type[Frame.MaxComponentTypes];
        private static readonly int[] Sizes = new int[Frame.MaxComponentTypes];
        private static readonly ComponentFlags[] Flags = new ComponentFlags[Frame.MaxComponentTypes];
        private static readonly StorageFlags[] StorageFlagsByType = new StorageFlags[Frame.MaxComponentTypes];
        private static readonly ComponentCallbacks[] Callbacks = new ComponentCallbacks[Frame.MaxComponentTypes];
        private static readonly ComponentFrameRemoveDelegate?[] RemoveInvokers = new ComponentFrameRemoveDelegate[Frame.MaxComponentTypes];
        private static readonly ComponentCommandApplyDelegate?[] AddAppliers = new ComponentCommandApplyDelegate[Frame.MaxComponentTypes];
        private static readonly ComponentCommandApplyDelegate?[] SetAppliers = new ComponentCommandApplyDelegate[Frame.MaxComponentTypes];
        private static readonly ComponentDeferredCommitDelegate?[] DeferredCommitters = new ComponentDeferredCommitDelegate[Frame.MaxComponentTypes];
        private static readonly ComponentSnapshotCaptureDelegate?[] SnapshotCapturers = new ComponentSnapshotCaptureDelegate[Frame.MaxComponentTypes];
        private static readonly ComponentSnapshotRestoreDelegate?[] SnapshotRestorers = new ComponentSnapshotRestoreDelegate[Frame.MaxComponentTypes];
        private static readonly ComponentPendingRemovalDelegate?[] PendingRemovalMarkers = new ComponentPendingRemovalDelegate[Frame.MaxComponentTypes];
        private static readonly ComponentPendingQueueIndexSetterDelegate?[] PendingQueueIndexSetters = new ComponentPendingQueueIndexSetterDelegate[Frame.MaxComponentTypes];
        private static readonly ComponentStorageCopyDelegate?[] StorageCopiers = new ComponentStorageCopyDelegate[Frame.MaxComponentTypes];
        private static readonly ComponentStorageResetDelegate?[] StorageResetters = new ComponentStorageResetDelegate[Frame.MaxComponentTypes];
        private static readonly ComponentPackedStatePresenceDelegate?[] PackedStatePresenceCheckers = new ComponentPackedStatePresenceDelegate[Frame.MaxComponentTypes];
        private static readonly ComponentPackedStateWriteDelegate?[] PackedStateWriters = new ComponentPackedStateWriteDelegate[Frame.MaxComponentTypes];
        private static readonly ComponentPackedStateRestoreDelegate?[] PackedStateRestorers = new ComponentPackedStateRestoreDelegate[Frame.MaxComponentTypes];
        private static readonly int[] FixedPackedPayloadSizes = CreateFixedPackedPayloadSizes();
        private static readonly Dictionary<Type, int> ReverseLookup = new();
        private static readonly Dictionary<string, int> ReverseNameLookup = new(StringComparer.Ordinal);

        private static int _nextId = 1;

        public static int Count => _nextId - 1;

        public static Builder CreateBuilder()
        {
            return new Builder();
        }

        public static bool IsRegistered<T>() where T : unmanaged, IComponent
        {
            return ComponentTypeId<T>.IsRegistered;
        }

        public static int Register<T>() where T : unmanaged, IComponent
        {
            return Register<T>(ComponentFlags.None, ComponentCallbacks.Empty, StorageFlags.None);
        }

        public static int Register<T>(ComponentFlags flags, ComponentCallbacks callbacks, StorageFlags storageFlags = StorageFlags.None)
            where T : unmanaged, IComponent
        {
            lock (SyncRoot)
            {
                StorageFlags resolvedStorageFlags = MapStorageFlags(flags, storageFlags);

                if (ReverseLookup.TryGetValue(typeof(T), out int existingId))
                {
                    ValidateCompatibleRegistration(existingId, flags, resolvedStorageFlags, callbacks);
                    return existingId;
                }

                if (_nextId >= Frame.MaxComponentTypes)
                {
                    throw new InvalidOperationException($"Reached max components declared: {Frame.MaxComponentTypes - 1}");
                }

                int id = _nextId++;

                Types[id] = typeof(T);
                Sizes[id] = sizeof(T);
                Flags[id] = flags;
                StorageFlagsByType[id] = resolvedStorageFlags;
                Callbacks[id] = callbacks;
                FixedPackedPayloadSizes[id] = callbacks.Serialize != null
                    ? ComponentRuntimeDispatch<T>.MeasurePackedPayloadSize(callbacks.Serialize)
                    : sizeof(T);
                RemoveInvokers[id] = ComponentRuntimeDispatch<T>.RemoveFromFrame;
                AddAppliers[id] = ComponentRuntimeDispatch<T>.AddToFrame;
                SetAppliers[id] = ComponentRuntimeDispatch<T>.SetInFrame;
                DeferredCommitters[id] = ComponentRuntimeDispatch<T>.CommitDeferred;
                SnapshotCapturers[id] = ComponentRuntimeDispatch<T>.CaptureSnapshot;
                SnapshotRestorers[id] = ComponentRuntimeDispatch<T>.RestoreSnapshot;
                PendingRemovalMarkers[id] = ComponentRuntimeDispatch<T>.MarkPendingRemoval;
                PendingQueueIndexSetters[id] = ComponentRuntimeDispatch<T>.SetPendingQueueIndex;
                StorageCopiers[id] = ComponentRuntimeDispatch<T>.CopyStorage;
                StorageResetters[id] = ComponentRuntimeDispatch<T>.ResetStorage;
                PackedStatePresenceCheckers[id] = ComponentRuntimeDispatch<T>.HasPackedState;
                PackedStateWriters[id] = ComponentRuntimeDispatch<T>.WritePackedState;
                PackedStateRestorers[id] = ComponentRuntimeDispatch<T>.RestorePackedState;

                ReverseLookup.Add(typeof(T), id);
                ReverseNameLookup[typeof(T).Name] = id;

                ComponentTypeId<T>.SetRegistration(id, sizeof(T), flags, resolvedStorageFlags, callbacks);
                return id;
            }
        }

        public static int GetComponentIndex(Type type)
        {
            if (ReverseLookup.TryGetValue(type, out int id))
            {
                return id;
            }

            throw new InvalidOperationException($"Type not registered: {type.FullName}");
        }

        public static int GetComponentIndex(string shortTypeName)
        {
            if (ReverseNameLookup.TryGetValue(shortTypeName, out int id))
            {
                return id;
            }

            throw new InvalidOperationException($"Type not registered: {shortTypeName}");
        }

        public static ComponentTypeInfo GetTypeInfo(int id)
        {
            EnsureValidTypeId(id);
            return new ComponentTypeInfo(Types[id]!, id, Sizes[id], Flags[id], StorageFlagsByType[id], Callbacks[id]);
        }

        public static Type GetComponentType(int id)
        {
            EnsureValidTypeId(id);
            return Types[id]!;
        }

        public static int GetComponentSize(int id)
        {
            EnsureValidTypeId(id);
            return Sizes[id];
        }

        public static ComponentFlags GetComponentFlags(int id)
        {
            EnsureValidTypeId(id);
            return Flags[id];
        }

        public static ComponentCallbacks GetComponentCallbacks(int id)
        {
            EnsureValidTypeId(id);
            return Callbacks[id];
        }

        public static bool CanSerializeComponent(int id, ComponentSerializationMode mode = ComponentSerializationMode.Default)
        {
            EnsureValidTypeId(id);
            ComponentFlags flags = Flags[id];

            if ((flags & ComponentFlags.DontSerialize) != 0)
            {
                return false;
            }

            if (mode == ComponentSerializationMode.Prediction &&
                (flags & ComponentFlags.ExcludeFromPrediction) != 0)
            {
                return false;
            }

            if (mode == ComponentSerializationMode.Checkpoint &&
                (flags & ComponentFlags.ExcludeFromCheckpoints) != 0)
            {
                return false;
            }

            return true;
        }

        public static unsafe bool SerializeComponent(
            int id,
            void* component,
            IFrameSerializer serializer,
            ComponentSerializationMode mode = ComponentSerializationMode.Default)
        {
            EnsureValidTypeId(id);

            if (!CanSerializeComponent(id, mode))
            {
                return false;
            }

            ComponentCallbacks callbacks = Callbacks[id];
            if (callbacks.Serialize != null)
            {
                callbacks.Serialize(component, serializer);
                return true;
            }

            serializer.Serialize(component, Sizes[id]);
            return true;
        }

        public static StorageFlags GetComponentStorageFlags(int id)
        {
            EnsureValidTypeId(id);
            return StorageFlagsByType[id];
        }

        internal static ComponentFrameRemoveDelegate GetRemoveInvoker(int id)
        {
            EnsureValidTypeId(id);
            return RemoveInvokers[id] ?? throw new InvalidOperationException($"Remove invoker missing for component type ID {id}");
        }

        internal static ComponentCommandApplyDelegate GetAddApplier(int id)
        {
            EnsureValidTypeId(id);
            return AddAppliers[id] ?? throw new InvalidOperationException($"Add applier missing for component type ID {id}");
        }

        internal static ComponentCommandApplyDelegate GetSetApplier(int id)
        {
            EnsureValidTypeId(id);
            return SetAppliers[id] ?? throw new InvalidOperationException($"Set applier missing for component type ID {id}");
        }

        internal static ComponentDeferredCommitDelegate GetDeferredCommitter(int id)
        {
            EnsureValidTypeId(id);
            return DeferredCommitters[id] ?? throw new InvalidOperationException($"Deferred committer missing for component type ID {id}");
        }

        internal static ComponentSnapshotCaptureDelegate GetSnapshotCapturer(int id)
        {
            EnsureValidTypeId(id);
            return SnapshotCapturers[id] ?? throw new InvalidOperationException($"Snapshot capturer missing for component type ID {id}");
        }

        internal static ComponentSnapshotRestoreDelegate GetSnapshotRestorer(int id)
        {
            EnsureValidTypeId(id);
            return SnapshotRestorers[id] ?? throw new InvalidOperationException($"Snapshot restorer missing for component type ID {id}");
        }

        internal static ComponentPendingQueueIndexSetterDelegate GetPendingQueueIndexSetter(int id)
        {
            EnsureValidTypeId(id);
            return PendingQueueIndexSetters[id] ?? throw new InvalidOperationException($"Pending queue index setter missing for component type ID {id}");
        }

        internal static ComponentPendingRemovalDelegate GetPendingRemovalMarker(int id)
        {
            EnsureValidTypeId(id);
            return PendingRemovalMarkers[id] ?? throw new InvalidOperationException($"Pending removal marker missing for component type ID {id}");
        }

        internal static ComponentStorageCopyDelegate GetStorageCopier(int id)
        {
            EnsureValidTypeId(id);
            return StorageCopiers[id] ?? throw new InvalidOperationException($"Storage copier missing for component type ID {id}");
        }

        internal static ComponentStorageResetDelegate GetStorageResetter(int id)
        {
            EnsureValidTypeId(id);
            return StorageResetters[id] ?? throw new InvalidOperationException($"Storage resetter missing for component type ID {id}");
        }

        internal static ComponentPackedStatePresenceDelegate GetPackedStatePresenceChecker(int id)
        {
            EnsureValidTypeId(id);
            return PackedStatePresenceCheckers[id] ?? throw new InvalidOperationException($"Packed state presence checker missing for component type ID {id}");
        }

        internal static ComponentPackedStateWriteDelegate GetPackedStateWriter(int id)
        {
            EnsureValidTypeId(id);
            return PackedStateWriters[id] ?? throw new InvalidOperationException($"Packed state writer missing for component type ID {id}");
        }

        internal static ComponentPackedStateRestoreDelegate GetPackedStateRestorer(int id)
        {
            EnsureValidTypeId(id);
            return PackedStateRestorers[id] ?? throw new InvalidOperationException($"Packed state restorer missing for component type ID {id}");
        }

        internal static int GetFixedPackedPayloadSize(int id)
        {
            EnsureValidTypeId(id);
            return FixedPackedPayloadSizes[id];
        }

        internal static StorageFlags GetStorageFlags(int id)
        {
            EnsureValidTypeId(id);
            return StorageFlagsByType[id];
        }

        internal static StorageFlags MapStorageFlags(ComponentFlags componentFlags, StorageFlags storageFlags = StorageFlags.None)
        {
            StorageFlags mappedFlags = storageFlags;

            if ((componentFlags & ComponentFlags.Singleton) != 0)
            {
                mappedFlags |= StorageFlags.Singleton;
            }

            return mappedFlags;
        }

        private static int[] CreateFixedPackedPayloadSizes()
        {
            int[] sizes = new int[Frame.MaxComponentTypes];
            Array.Fill(sizes, -1);
            return sizes;
        }

        private static void EnsureValidTypeId(int id)
        {
            if (id <= 0 || id >= _nextId || Types[id] == null)
            {
                throw new InvalidOperationException($"Component type ID not registered: {id}");
            }
        }

        private static void ValidateCompatibleRegistration(
            int id,
            ComponentFlags flags,
            StorageFlags storageFlags,
            ComponentCallbacks callbacks)
        {
            if (Flags[id] != flags)
            {
                throw new InvalidOperationException(
                    $"Component {Types[id]!.FullName} already registered with different flags. " +
                    $"Existing={Flags[id]}, Requested={flags}");
            }

            if (StorageFlagsByType[id] != storageFlags)
            {
                throw new InvalidOperationException(
                    $"Component {Types[id]!.FullName} already registered with different storage flags. " +
                    $"Existing={StorageFlagsByType[id]}, Requested={storageFlags}");
            }

            ComponentCallbacks existingCallbacks = Callbacks[id];
            if (existingCallbacks.Serialize != callbacks.Serialize ||
                existingCallbacks.OnAdded != callbacks.OnAdded ||
                existingCallbacks.OnRemoved != callbacks.OnRemoved)
            {
                throw new InvalidOperationException(
                    $"Component {Types[id]!.FullName} already registered with different callbacks.");
            }
        }

        /// <summary>
        /// 增量式注册 Builder，接口形态向 FrameSync 对齐。
        /// </summary>
        public readonly struct Builder
        {
            public Builder Add<T>(
                ComponentCallbacks callbacks,
                ComponentFlags flags = ComponentFlags.None,
                StorageFlags storageFlags = StorageFlags.None)
                where T : unmanaged, IComponent
            {
                Register<T>(flags, callbacks, storageFlags);
                return this;
            }

            public Builder Add<T>(
                ComponentSerializeDelegate? serialize = null,
                ComponentChangedDelegate? onAdded = null,
                ComponentChangedDelegate? onRemoved = null,
                ComponentFlags flags = ComponentFlags.None,
                StorageFlags storageFlags = StorageFlags.None)
                where T : unmanaged, IComponent
            {
                Register<T>(flags, new ComponentCallbacks(serialize, onAdded, onRemoved), storageFlags);
                return this;
            }

            public void Finish()
            {
            }
        }
    }

    /// <summary>
    /// 组件类型 ID 与元数据缓存（FrameSync 风格）。
    /// </summary>
    public static unsafe class ComponentTypeId<T> where T : unmanaged, IComponent
    {
        private static int _id;
        private static int _size;
        private static ComponentFlags _flags;
        private static StorageFlags _storageFlags;
        private static ComponentCallbacks _callbacks;

        public static int Id
        {
            get
            {
                if (_id == 0)
                {
                    ComponentRegistry.Register<T>();
                }

                return _id;
            }
        }

        public static int Size
        {
            get
            {
                if (_size == 0)
                {
                    _ = Id;
                }

                return _size;
            }
        }

        public static ComponentFlags Flags
        {
            get
            {
                if (_id == 0)
                {
                    _ = Id;
                }

                return _flags;
            }
        }

        public static StorageFlags StorageFlags
        {
            get
            {
                if (_id == 0)
                {
                    _ = Id;
                }

                return _storageFlags;
            }
        }

        public static ComponentCallbacks Callbacks
        {
            get
            {
                if (_id == 0)
                {
                    _ = Id;
                }

                return _callbacks;
            }
        }

        public static bool IsRegistered => _id != 0;

        internal static void SetRegistration(
            int id,
            int size,
            ComponentFlags flags,
            StorageFlags storageFlags,
            ComponentCallbacks callbacks)
        {
            if (_id != 0 && _id != id)
            {
                throw new InvalidOperationException($"Component type already registered with ID {_id}");
            }

            _id = id;
            _size = size;
            _flags = flags;
            _storageFlags = storageFlags;
            _callbacks = callbacks;
        }
    }

    internal static unsafe class ComponentRuntimeDispatch<T> where T : unmanaged, IComponent
    {
        public static int MeasurePackedPayloadSize(ComponentSerializeDelegate serialize)
        {
            if (serialize == null)
            {
                return sizeof(T);
            }

            T component = default;
            var sizingStream = new FrameStateBitStreamWriter(new FrameStateSizingWriter());
            var serializer = new FrameSerializer(sizingStream, writing: true);
            sizingStream.BeginPayload();
            serialize(&component, serializer);
            return sizingStream.EndPayload();
        }

        public static void RemoveFromFrame(Frame frame, EntityRef entity)
        {
            frame.Remove<T>(entity);
        }

        public static void AddToFrame(Frame frame, EntityRef entity, void* componentData)
        {
            frame.Add(entity, *(T*)componentData);
        }

        public static void SetInFrame(Frame frame, EntityRef entity, void* componentData)
        {
            frame.Set(entity, *(T*)componentData);
        }

        public static void CommitDeferred(void* storage, EntityRef entity)
        {
            var typedStorage = (Storage<T>*)storage;
            if (typedStorage == null)
            {
                return;
            }

            typedStorage->CommitRemoval(entity);
        }

        public static ComponentStorageSnapshot? CaptureSnapshot(void* storage, ComponentSerializationMode mode)
        {
            int typeId = ComponentTypeId<T>.Id;
            if (!ComponentRegistry.CanSerializeComponent(typeId, mode))
            {
                return null;
            }

            var typedStorage = (Storage<T>*)storage;
            if (typedStorage == null || typedStorage->Count == 0)
            {
                return null;
            }

            if (ComponentTypeId<T>.Callbacks.Serialize == null)
            {
                int rawCount = typedStorage->Count;
                var entities = new EntityRef[rawCount];
                var data = new byte[rawCount * sizeof(T)];

                typedStorage->CopyDenseEntities(entities);
                typedStorage->CopyDenseComponentBytes(data);

                return new ComponentStorageSnapshot(typeId, entities, data);
            }

            int count = typedStorage->Count;
            var entries = new ComponentEntrySnapshot[count];

            for (int index = 1; index <= count; index++)
            {
                EntityRef entity = typedStorage->GetEntityRefByIndex(index);
                T* componentData = typedStorage->GetDataPointerByIndex(index);
                var serializer = new FrameSerializer(new BitStream(), writing: true);
                ComponentRegistry.SerializeComponent(typeId, componentData, serializer, mode);
                entries[index - 1] = new ComponentEntrySnapshot(entity, serializer.Stream.ToArray());
            }

            return new ComponentStorageSnapshot(typeId, entries);
        }

        public static void RestoreSnapshot(Frame frame, ComponentStorageSnapshot snapshot, ComponentSerializationMode mode)
        {
            int typeId = ComponentTypeId<T>.Id;
            var storage = frame.GetOrCreateStorageForSnapshotRestore<T>(typeId);

            if (snapshot.Kind == ComponentSnapshotDataKind.RawDense)
            {
                storage->RestoreDenseSnapshot(snapshot.DenseEntities, snapshot.DenseData);
                return;
            }

            if (snapshot.Entries.Length == 0)
            {
                return;
            }

            for (int i = 0; i < snapshot.Entries.Length; i++)
            {
                ComponentEntrySnapshot entry = snapshot.Entries[i];
                T component = default;

                if (entry.Payload.Length > 0)
                {
                    var serializer = new FrameSerializer(new BitStream(entry.Payload), writing: false);
                    ComponentRegistry.SerializeComponent(typeId, &component, serializer, mode);
                }

                storage->Add(entry.Entity, component);
            }
        }

        public static void MarkPendingRemoval(void* storage, EntityRef entity)
        {
            var typedStorage = (Storage<T>*)storage;
            if (typedStorage == null)
            {
                return;
            }

            typedStorage->MarkForRemoval(entity);
        }

        public static void SetPendingQueueIndex(void* storage, EntityRef entity, int queueIndex)
        {
            var typedStorage = (Storage<T>*)storage;
            if (typedStorage == null)
            {
                return;
            }

            typedStorage->SetPendingQueueIndex(entity, queueIndex);
        }

        public static void CopyStorage(Frame destinationFrame, void* sourceStorage)
        {
            var destination = destinationFrame.GetOrCreateStorageForSnapshotRestore<T>(ComponentTypeId<T>.Id);
            var source = (Storage<T>*)sourceStorage;
            if (source == null)
            {
                return;
            }

            destination->CopyFrom(source);
        }

        public static void ResetStorage(void* storage)
        {
            var typedStorage = (Storage<T>*)storage;
            if (typedStorage == null)
            {
                return;
            }

            typedStorage->Reset();
        }

        public static bool HasPackedState(void* storage, ComponentSerializationMode mode)
        {
            int typeId = ComponentTypeId<T>.Id;
            if (!ComponentRegistry.CanSerializeComponent(typeId, mode))
            {
                return false;
            }

            var typedStorage = (Storage<T>*)storage;
            return typedStorage != null && typedStorage->Count > 0;
        }

        public static bool WritePackedState(void* storage, FrameStateWriter writer, ComponentSerializationMode mode)
        {
            var typedStorage = (Storage<T>*)storage;
            if (typedStorage == null || typedStorage->Count == 0)
            {
                return false;
            }

            int typeId = ComponentTypeId<T>.Id;

            writer.WriteInt32(typeId);

            if (ComponentTypeId<T>.Callbacks.Serialize == null)
            {
                writer.WriteByte((byte)ComponentSnapshotDataKind.RawDense);

                int denseCount = typedStorage->Count;
                writer.WriteInt32(denseCount);
                typedStorage->WriteDenseEntities(writer);

                int dataLength = denseCount * sizeof(T);
                writer.WriteInt32(dataLength);
                typedStorage->WriteDenseComponentBytes(writer);
                return true;
            }

            int entryCount = typedStorage->Count;
            int fixedPayloadBytes = ComponentRegistry.GetFixedPackedPayloadSize(typeId);
            if (fixedPayloadBytes >= 0)
            {
                writer.WriteByte((byte)ComponentSnapshotDataKind.FixedSizeEntryPayloads);
                writer.WriteInt32(entryCount);
                writer.WriteInt32(fixedPayloadBytes);

                var payloadStream = new FrameStateBitStreamWriter(writer);
                var serializer = new FrameSerializer(payloadStream, writing: true);

                for (int index = 1; index <= entryCount; index++)
                {
                    EntityRef entity = typedStorage->GetEntityRefByIndex(index);
                    T* componentData = typedStorage->GetDataPointerByIndex(index);

                    writer.WriteUInt64(entity.Raw);
                    payloadStream.BeginPayload();
                    ComponentRegistry.SerializeComponent(typeId, componentData, serializer, mode);
                    int actualPayloadBytes = payloadStream.EndPayload();
                    if (actualPayloadBytes != fixedPayloadBytes)
                    {
                        throw new InvalidOperationException(
                            $"Component {typeof(T).Name} produced variable-sized serialized payloads. " +
                            $"Expected={fixedPayloadBytes}, Actual={actualPayloadBytes}.");
                    }
                }

                return true;
            }

            writer.WriteByte((byte)ComponentSnapshotDataKind.EntryPayloads);
            writer.WriteInt32(entryCount);

            var fallbackPayloadStream = new BitStream();
            var fallbackSerializer = new FrameSerializer(fallbackPayloadStream, writing: true);

            for (int index = 1; index <= entryCount; index++)
            {
                EntityRef entity = typedStorage->GetEntityRefByIndex(index);
                T* componentData = typedStorage->GetDataPointerByIndex(index);

                fallbackPayloadStream.Reset();
                ComponentRegistry.SerializeComponent(typeId, componentData, fallbackSerializer, mode);

                writer.WriteUInt64(entity.Raw);
                ReadOnlySpan<byte> payload = fallbackPayloadStream.GetWrittenSpan();
                writer.WriteInt32(payload.Length);
                writer.WriteBytes(payload);
            }

            return true;
        }

        public static void RestorePackedState(Frame frame, FrameStateReader reader, ComponentSerializationMode mode)
        {
            int typeId = ComponentTypeId<T>.Id;
            byte kind = reader.ReadByte();
            var storage = frame.GetOrCreateStorageForSnapshotRestore<T>(typeId);

            if (kind == (byte)ComponentSnapshotDataKind.RawDense)
            {
                int denseCount = reader.ReadInt32();
                ReadOnlySpan<byte> entityBytes = reader.ReadBytes(denseCount * sizeof(EntityRef));
                int dataLength = reader.ReadInt32();
                ReadOnlySpan<byte> componentBytes = reader.ReadBytes(dataLength);
                storage->RestoreDenseSnapshot(entityBytes, componentBytes);
                return;
            }

            if (kind == (byte)ComponentSnapshotDataKind.FixedSizeEntryPayloads)
            {
                int fixedEntryCount = reader.ReadInt32();
                int payloadLength = reader.ReadInt32();
                var stream = new FrameStateBitStreamReader(reader);
                var serializer = new FrameSerializer(stream, writing: false);

                for (int i = 0; i < fixedEntryCount; i++)
                {
                    EntityRef entity = EntityRef.FromRaw(reader.ReadUInt64());
                    T component = default;

                    if (payloadLength > 0)
                    {
                        stream.BeginPayload(payloadLength);
                        ComponentRegistry.SerializeComponent(typeId, &component, serializer, mode);
                        stream.EndPayload();
                    }

                    storage->Add(entity, component);
                }

                return;
            }

            int entryCount = reader.ReadInt32();
            for (int i = 0; i < entryCount; i++)
            {
                EntityRef entity = EntityRef.FromRaw(reader.ReadUInt64());
                int payloadLength = reader.ReadInt32();
                T component = default;

                if (payloadLength > 0)
                {
                    var stream = reader.CreateBitStreamSlice(payloadLength);
                    var serializer = new FrameSerializer(stream, writing: false);
                    ComponentRegistry.SerializeComponent(typeId, &component, serializer, mode);
                }

                storage->Add(entity, component);
            }
        }
    }
}
