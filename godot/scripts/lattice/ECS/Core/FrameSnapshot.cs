// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

using System;
using System.ComponentModel;
using Lattice.Core;
using Lattice.Math;

namespace Lattice.ECS.Core
{
    /// <summary>
    /// 组件条目快照。
    /// </summary>
    public readonly struct ComponentEntrySnapshot
    {
        public ComponentEntrySnapshot(EntityRef entity, byte[] payload)
        {
            Entity = entity;
            Payload = payload ?? Array.Empty<byte>();
        }

        public EntityRef Entity { get; }

        public byte[] Payload { get; }
    }

    /// <summary>
    /// 组件存储快照数据类型。
    /// </summary>
    public enum ComponentSnapshotDataKind : byte
    {
        EntryPayloads = 0,
        RawDense = 1,
        FixedSizeEntryPayloads = 2
    }

    /// <summary>
    /// 单个组件类型的存储快照。
    /// </summary>
    public sealed class ComponentStorageSnapshot
    {
        public ComponentStorageSnapshot(int typeId, ComponentEntrySnapshot[] entries)
        {
            TypeId = typeId;
            Kind = ComponentSnapshotDataKind.EntryPayloads;
            Entries = entries ?? Array.Empty<ComponentEntrySnapshot>();
            DenseEntities = Array.Empty<EntityRef>();
            DenseData = Array.Empty<byte>();
        }

        public ComponentStorageSnapshot(int typeId, EntityRef[] denseEntities, byte[] denseData)
        {
            TypeId = typeId;
            Kind = ComponentSnapshotDataKind.RawDense;
            Entries = Array.Empty<ComponentEntrySnapshot>();
            DenseEntities = denseEntities ?? Array.Empty<EntityRef>();
            DenseData = denseData ?? Array.Empty<byte>();
        }

        public int TypeId { get; }

        public ComponentSnapshotDataKind Kind { get; }

        public ComponentEntrySnapshot[] Entries { get; }

        public EntityRef[] DenseEntities { get; }

        public byte[] DenseData { get; }
    }

    /// <summary>
    /// 帧状态对象图快照。
    /// 主要用于兼容 API、调试断言和显式恢复测试，不作为 Session 热路径 checkpoint 格式。
    /// </summary>
    [Obsolete("请优先改用 PackedFrameSnapshot 及 Frame.CapturePackedSnapshot()/RestoreFromPackedSnapshot()。FrameSnapshot 仅保留为兼容、调试和显式恢复测试。", false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class FrameSnapshot
    {
        public FrameSnapshot(
            int tick,
            FP deltaTime,
            int entityCapacity,
            int entityCount,
            int freeListHead,
            ushort[] entityVersions,
            int[] entityNextFree,
            ulong[] entityComponentMasks,
            EntityRef[] pendingRemovalEntities,
            ushort[] pendingRemovalTypeIds,
            ComponentStorageSnapshot[] componentStorages)
        {
            Tick = tick;
            DeltaTime = deltaTime;
            EntityCapacity = entityCapacity;
            EntityCount = entityCount;
            FreeListHead = freeListHead;
            EntityVersions = entityVersions ?? Array.Empty<ushort>();
            EntityNextFree = entityNextFree ?? Array.Empty<int>();
            EntityComponentMasks = entityComponentMasks ?? Array.Empty<ulong>();
            PendingRemovalEntities = pendingRemovalEntities ?? Array.Empty<EntityRef>();
            PendingRemovalTypeIds = pendingRemovalTypeIds ?? Array.Empty<ushort>();
            ComponentStorages = componentStorages ?? Array.Empty<ComponentStorageSnapshot>();
        }

        public int Tick { get; }

        public FP DeltaTime { get; }

        public int EntityCapacity { get; }

        public int EntityCount { get; }

        public int FreeListHead { get; }

        public ushort[] EntityVersions { get; }

        public int[] EntityNextFree { get; }

        public ulong[] EntityComponentMasks { get; }

        public EntityRef[] PendingRemovalEntities { get; }

        public ushort[] PendingRemovalTypeIds { get; }

        public ComponentStorageSnapshot[] ComponentStorages { get; }

        /// <summary>
        /// 基于当前对象图布局计算的校验和。
        /// </summary>
        public ulong Checksum
        {
            get
            {
                var writer = new FrameStateChecksumWriter();
                WriteTo(writer);
                return writer.Checksum;
            }
        }

        /// <summary>
        /// 将对象图快照编码为稳定字节布局。
        /// </summary>
        public byte[] ToByteArray()
        {
            var writer = new FrameStateBufferWriter();
            WriteTo(writer);
            return writer.ToArray();
        }

        /// <summary>
        /// 对已有快照字节布局计算校验和。
        /// </summary>
        public static ulong CalculateChecksum(byte[] data)
        {
            ArgumentNullException.ThrowIfNull(data);

            var writer = new FrameStateChecksumWriter();
            writer.WriteBytes(data);
            return writer.Checksum;
        }

        private void WriteTo(FrameStateWriter writer)
        {
            writer.WriteInt32(Tick);
            writer.WriteInt64(DeltaTime.RawValue);
            writer.WriteInt32(EntityCapacity);
            writer.WriteInt32(EntityCount);
            writer.WriteInt32(FreeListHead);

            writer.WriteInt32(EntityVersions.Length);
            for (int i = 0; i < EntityVersions.Length; i++)
            {
                writer.WriteUInt16(EntityVersions[i]);
            }

            writer.WriteInt32(EntityNextFree.Length);
            for (int i = 0; i < EntityNextFree.Length; i++)
            {
                writer.WriteInt32(EntityNextFree[i]);
            }

            writer.WriteInt32(EntityComponentMasks.Length);
            for (int i = 0; i < EntityComponentMasks.Length; i++)
            {
                writer.WriteUInt64(EntityComponentMasks[i]);
            }

            writer.WriteInt32(PendingRemovalEntities.Length);
            for (int i = 0; i < PendingRemovalEntities.Length; i++)
            {
                writer.WriteUInt64(PendingRemovalEntities[i].Raw);
            }

            writer.WriteInt32(PendingRemovalTypeIds.Length);
            for (int i = 0; i < PendingRemovalTypeIds.Length; i++)
            {
                writer.WriteUInt16(PendingRemovalTypeIds[i]);
            }

            writer.WriteInt32(ComponentStorages.Length);
            for (int i = 0; i < ComponentStorages.Length; i++)
            {
                ComponentStorageSnapshot storage = ComponentStorages[i];
                writer.WriteInt32(storage.TypeId);
                writer.WriteByte((byte)storage.Kind);

                if (storage.Kind == ComponentSnapshotDataKind.RawDense)
                {
                    writer.WriteInt32(storage.DenseEntities.Length);
                    for (int j = 0; j < storage.DenseEntities.Length; j++)
                    {
                        writer.WriteUInt64(storage.DenseEntities[j].Raw);
                    }

                    writer.WriteInt32(storage.DenseData.Length);
                    writer.WriteBytes(storage.DenseData);
                    continue;
                }

                writer.WriteInt32(storage.Entries.Length);

                for (int j = 0; j < storage.Entries.Length; j++)
                {
                    ComponentEntrySnapshot entry = storage.Entries[j];
                    writer.WriteUInt64(entry.Entity.Raw);
                    writer.WriteInt32(entry.Payload.Length);
                    writer.WriteBytes(entry.Payload);
                }
            }
        }
    }
}
