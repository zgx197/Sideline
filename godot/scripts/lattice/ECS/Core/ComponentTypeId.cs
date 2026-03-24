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
                RemoveInvokers[id] = ComponentRuntimeDispatch<T>.RemoveFromFrame;
                AddAppliers[id] = ComponentRuntimeDispatch<T>.AddToFrame;
                SetAppliers[id] = ComponentRuntimeDispatch<T>.SetInFrame;
                DeferredCommitters[id] = ComponentRuntimeDispatch<T>.CommitDeferred;
                SnapshotCapturers[id] = ComponentRuntimeDispatch<T>.CaptureSnapshot;
                SnapshotRestorers[id] = ComponentRuntimeDispatch<T>.RestoreSnapshot;
                PendingRemovalMarkers[id] = ComponentRuntimeDispatch<T>.MarkPendingRemoval;
                PendingQueueIndexSetters[id] = ComponentRuntimeDispatch<T>.SetPendingQueueIndex;

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
            int typeId = _nextId++;
            ComponentTypeId<T>.Register(typeId);
            ComponentCommandRegistry.Register<T>(typeId);
        }

        /// <summary>确保组件类型已注册。</summary>
        public static void EnsureRegistered<T>() where T : unmanaged
        {
            if (ComponentTypeId<T>.IsRegistered)
            {
                return;
            }

            Register<T>();
        }
    }
}
