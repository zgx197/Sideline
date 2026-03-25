// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

using Lattice.Core;
using Lattice.ECS.Serialization;

namespace Lattice.ECS.Core
{
    /// <summary>
    /// 组件序列化委托，对齐 FrameSync 的组件序列化回调设计。
    /// </summary>
    public unsafe delegate void ComponentSerializeDelegate(void* component, IFrameSerializer serializer);

    /// <summary>
    /// 组件生命周期回调，在组件添加或移除时触发。
    /// </summary>
    public unsafe delegate void ComponentChangedDelegate(EntityRef entity, void* component, Frame frame);

    /// <summary>
    /// 组件回调集合。
    /// </summary>
    public readonly struct ComponentCallbacks
    {
        public static readonly ComponentCallbacks Empty = new(null, null, null);

        public ComponentCallbacks(
            ComponentSerializeDelegate? serialize,
            ComponentChangedDelegate? onAdded,
            ComponentChangedDelegate? onRemoved)
        {
            Serialize = serialize;
            OnAdded = onAdded;
            OnRemoved = onRemoved;
        }

        /// <summary>组件序列化回调。</summary>
        public ComponentSerializeDelegate? Serialize { get; }

        /// <summary>组件添加回调。</summary>
        public ComponentChangedDelegate? OnAdded { get; }

        /// <summary>组件移除回调。</summary>
        public ComponentChangedDelegate? OnRemoved { get; }
    }

    internal delegate void ComponentFrameRemoveDelegate(Frame frame, EntityRef entity);

    internal unsafe delegate void ComponentCommandApplyDelegate(Frame frame, EntityRef entity, void* componentData);

    internal unsafe delegate void ComponentDeferredCommitDelegate(void* storage, EntityRef entity);

    internal unsafe delegate ComponentStorageSnapshot? ComponentSnapshotCaptureDelegate(void* storage, ComponentSerializationMode mode);

    internal delegate void ComponentSnapshotRestoreDelegate(Frame frame, ComponentStorageSnapshot snapshot, ComponentSerializationMode mode);

    internal unsafe delegate void ComponentPendingRemovalDelegate(void* storage, EntityRef entity);

    internal unsafe delegate void ComponentPendingQueueIndexSetterDelegate(void* storage, EntityRef entity, int queueIndex);

    internal unsafe delegate void ComponentStorageCopyDelegate(Frame destinationFrame, void* sourceStorage);

    internal unsafe delegate void ComponentStorageResetDelegate(void* storage);

    internal unsafe delegate bool ComponentPackedStatePresenceDelegate(void* storage, ComponentSerializationMode mode);

    internal unsafe delegate bool ComponentPackedStateWriteDelegate(void* storage, FrameStateWriter writer, ComponentSerializationMode mode);

    internal delegate void ComponentPackedStateRestoreDelegate(Frame frame, FrameStateReader reader, ComponentSerializationMode mode);
}
