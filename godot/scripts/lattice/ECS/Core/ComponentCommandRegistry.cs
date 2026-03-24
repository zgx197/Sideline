// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

using System;
using Lattice.Core;

namespace Lattice.ECS.Core
{
    internal unsafe delegate void ComponentCommandDataHandler(Frame frame, EntityRef entity, byte* data);

    internal unsafe delegate void ComponentCommandRemoveHandler(Frame frame, EntityRef entity);

    internal unsafe delegate int ComponentStorageSnapshotSizeHandler(void* storage);

    internal unsafe delegate void ComponentStorageSnapshotWriteHandler(void* storage, byte* buffer, int bufferSize);

    internal unsafe delegate void ComponentStorageSnapshotReadHandler(void* storage, byte* buffer, int bufferSize);

    internal unsafe delegate void* ComponentStorageCreateHandler(Allocator* allocator, int maxEntities, int typeId);

    /// <summary>
    /// 组件命令回放注册表。
    /// 为 CommandBuffer 提供按组件类型 ID 分发的 Add/Set/Remove 入口。
    /// </summary>
    internal static unsafe class ComponentCommandRegistry
    {
        private static readonly int[] ComponentSizes = new int[Frame.MaxComponentTypes];
        private static readonly ComponentCommandDataHandler?[] AddHandlers = new ComponentCommandDataHandler[Frame.MaxComponentTypes];
        private static readonly ComponentCommandDataHandler?[] SetHandlers = new ComponentCommandDataHandler[Frame.MaxComponentTypes];
        private static readonly ComponentCommandRemoveHandler?[] RemoveHandlers = new ComponentCommandRemoveHandler[Frame.MaxComponentTypes];
        private static readonly ComponentStorageSnapshotSizeHandler?[] StorageSnapshotSizeHandlers = new ComponentStorageSnapshotSizeHandler[Frame.MaxComponentTypes];
        private static readonly ComponentStorageSnapshotWriteHandler?[] StorageSnapshotWriteHandlers = new ComponentStorageSnapshotWriteHandler[Frame.MaxComponentTypes];
        private static readonly ComponentStorageSnapshotReadHandler?[] StorageSnapshotReadHandlers = new ComponentStorageSnapshotReadHandler[Frame.MaxComponentTypes];
        private static readonly ComponentStorageCreateHandler?[] StorageCreateHandlers = new ComponentStorageCreateHandler[Frame.MaxComponentTypes];

        public static void Register<T>(int typeId) where T : unmanaged
        {
            ComponentSizes[typeId] = sizeof(T);
            AddHandlers[typeId] = static (frame, entity, data) => frame.Add(entity, *(T*)data);
            SetHandlers[typeId] = static (frame, entity, data) =>
            {
                T value = *(T*)data;
                if (frame.Has<T>(entity))
                {
                    frame.Get<T>(entity) = value;
                    return;
                }

                frame.Add(entity, value);
            };
            RemoveHandlers[typeId] = static (frame, entity) =>
            {
                if (frame.Has<T>(entity))
                {
                    frame.Remove<T>(entity);
                }
            };
            StorageSnapshotSizeHandlers[typeId] = static storage => ((Storage<T>*)storage)->GetSnapshotSize();
            StorageSnapshotWriteHandlers[typeId] = static (storage, buffer, bufferSize) =>
                ((Storage<T>*)storage)->WriteSnapshot(buffer, bufferSize);
            StorageSnapshotReadHandlers[typeId] = static (storage, buffer, bufferSize) =>
                ((Storage<T>*)storage)->ReadSnapshot(buffer, bufferSize);
            StorageCreateHandlers[typeId] = static (allocator, maxEntities, registeredTypeId) =>
            {
                var storage = (Storage<T>*)allocator->Alloc(sizeof(Storage<T>));
                storage->Initialize(maxEntities, allocator, StorageFlags.None, componentTypeId: registeredTypeId);
                return storage;
            };
        }

        public static int GetComponentSize(int typeId)
        {
            ValidateTypeId(typeId);

            int size = ComponentSizes[typeId];
            if (size <= 0)
            {
                throw new InvalidOperationException($"组件类型 ID {typeId} 尚未注册命令回放信息。");
            }

            return size;
        }

        public static void PlaybackAdd(Frame frame, EntityRef entity, int typeId, byte* data)
        {
            ValidateTypeId(typeId);

            ComponentCommandDataHandler? handler = AddHandlers[typeId];
            if (handler == null)
            {
                throw new InvalidOperationException($"组件类型 ID {typeId} 未注册 Add 回放处理器。");
            }

            handler(frame, entity, data);
        }

        public static void PlaybackSet(Frame frame, EntityRef entity, int typeId, byte* data)
        {
            ValidateTypeId(typeId);

            ComponentCommandDataHandler? handler = SetHandlers[typeId];
            if (handler == null)
            {
                throw new InvalidOperationException($"组件类型 ID {typeId} 未注册 Set 回放处理器。");
            }

            handler(frame, entity, data);
        }

        public static void PlaybackRemove(Frame frame, EntityRef entity, int typeId)
        {
            ValidateTypeId(typeId);

            ComponentCommandRemoveHandler? handler = RemoveHandlers[typeId];
            if (handler == null)
            {
                throw new InvalidOperationException($"组件类型 ID {typeId} 未注册 Remove 回放处理器。");
            }

            handler(frame, entity);
        }

        public static int GetStorageSnapshotSize(int typeId, void* storage)
        {
            ValidateTypeId(typeId);

            ComponentStorageSnapshotSizeHandler? handler = StorageSnapshotSizeHandlers[typeId];
            if (handler == null)
            {
                throw new InvalidOperationException($"组件类型 ID {typeId} 未注册 Storage 快照大小处理器。");
            }

            return handler(storage);
        }

        public static void WriteStorageSnapshot(int typeId, void* storage, byte* buffer, int bufferSize)
        {
            ValidateTypeId(typeId);

            ComponentStorageSnapshotWriteHandler? handler = StorageSnapshotWriteHandlers[typeId];
            if (handler == null)
            {
                throw new InvalidOperationException($"组件类型 ID {typeId} 未注册 Storage 快照写入处理器。");
            }

            handler(storage, buffer, bufferSize);
        }

        public static void ReadStorageSnapshot(int typeId, void* storage, byte* buffer, int bufferSize)
        {
            ValidateTypeId(typeId);

            ComponentStorageSnapshotReadHandler? handler = StorageSnapshotReadHandlers[typeId];
            if (handler == null)
            {
                throw new InvalidOperationException($"组件类型 ID {typeId} 未注册 Storage 快照读取处理器。");
            }

            handler(storage, buffer, bufferSize);
        }

        public static void* CreateStorage(int typeId, Allocator* allocator, int maxEntities)
        {
            ValidateTypeId(typeId);

            ComponentStorageCreateHandler? handler = StorageCreateHandlers[typeId];
            if (handler == null)
            {
                throw new InvalidOperationException($"组件类型 ID {typeId} 未注册 Storage 创建处理器。");
            }

            return handler(allocator, maxEntities, typeId);
        }

        private static void ValidateTypeId(int typeId)
        {
            if ((uint)typeId >= Frame.MaxComponentTypes)
            {
                throw new ArgumentOutOfRangeException(nameof(typeId));
            }
        }
    }
}
