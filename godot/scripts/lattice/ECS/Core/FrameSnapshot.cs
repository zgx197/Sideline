// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

using System;
using System.Buffers.Binary;
using System.IO;
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
        RawDense = 1
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
    /// 帧状态快照。
    /// </summary>
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

        public ulong Checksum => CalculateChecksum(ToByteArray());

        public byte[] ToByteArray()
        {
            using var stream = new MemoryStream();

            WriteInt32(stream, Tick);
            WriteInt64(stream, DeltaTime.RawValue);
            WriteInt32(stream, EntityCapacity);
            WriteInt32(stream, EntityCount);
            WriteInt32(stream, FreeListHead);

            WriteInt32(stream, EntityVersions.Length);
            for (int i = 0; i < EntityVersions.Length; i++)
            {
                WriteUInt16(stream, EntityVersions[i]);
            }

            WriteInt32(stream, EntityNextFree.Length);
            for (int i = 0; i < EntityNextFree.Length; i++)
            {
                WriteInt32(stream, EntityNextFree[i]);
            }

            WriteInt32(stream, EntityComponentMasks.Length);
            for (int i = 0; i < EntityComponentMasks.Length; i++)
            {
                WriteUInt64(stream, EntityComponentMasks[i]);
            }

            WriteInt32(stream, PendingRemovalEntities.Length);
            for (int i = 0; i < PendingRemovalEntities.Length; i++)
            {
                WriteUInt64(stream, PendingRemovalEntities[i].Raw);
            }

            WriteInt32(stream, PendingRemovalTypeIds.Length);
            for (int i = 0; i < PendingRemovalTypeIds.Length; i++)
            {
                WriteUInt16(stream, PendingRemovalTypeIds[i]);
            }

            WriteInt32(stream, ComponentStorages.Length);
            for (int i = 0; i < ComponentStorages.Length; i++)
            {
                ComponentStorageSnapshot storage = ComponentStorages[i];
                WriteInt32(stream, storage.TypeId);
                WriteByte(stream, (byte)storage.Kind);

                if (storage.Kind == ComponentSnapshotDataKind.RawDense)
                {
                    WriteInt32(stream, storage.DenseEntities.Length);
                    for (int j = 0; j < storage.DenseEntities.Length; j++)
                    {
                        WriteUInt64(stream, storage.DenseEntities[j].Raw);
                    }

                    WriteInt32(stream, storage.DenseData.Length);
                    if (storage.DenseData.Length > 0)
                    {
                        stream.Write(storage.DenseData, 0, storage.DenseData.Length);
                    }
                    continue;
                }

                WriteInt32(stream, storage.Entries.Length);

                for (int j = 0; j < storage.Entries.Length; j++)
                {
                    ComponentEntrySnapshot entry = storage.Entries[j];
                    WriteUInt64(stream, entry.Entity.Raw);
                    WriteInt32(stream, entry.Payload.Length);
                    if (entry.Payload.Length > 0)
                    {
                        stream.Write(entry.Payload, 0, entry.Payload.Length);
                    }
                }
            }

            return stream.ToArray();
        }

        public static ulong CalculateChecksum(byte[] data)
        {
            return unchecked((ulong)DeterministicHash.Fnv1a64(data));
        }

        private static void WriteUInt16(Stream stream, ushort value)
        {
            Span<byte> buffer = stackalloc byte[sizeof(ushort)];
            BinaryPrimitives.WriteUInt16LittleEndian(buffer, value);
            stream.Write(buffer);
        }

        private static void WriteByte(Stream stream, byte value)
        {
            stream.WriteByte(value);
        }

        private static void WriteInt32(Stream stream, int value)
        {
            Span<byte> buffer = stackalloc byte[sizeof(int)];
            BinaryPrimitives.WriteInt32LittleEndian(buffer, value);
            stream.Write(buffer);
        }

        private static void WriteInt64(Stream stream, long value)
        {
            Span<byte> buffer = stackalloc byte[sizeof(long)];
            BinaryPrimitives.WriteInt64LittleEndian(buffer, value);
            stream.Write(buffer);
        }

        private static void WriteUInt64(Stream stream, ulong value)
        {
            Span<byte> buffer = stackalloc byte[sizeof(ulong)];
            BinaryPrimitives.WriteUInt64LittleEndian(buffer, value);
            stream.Write(buffer);
        }
    }
}
