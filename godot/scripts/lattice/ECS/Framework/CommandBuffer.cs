// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Lattice.Core;
using Lattice.ECS.Core;

namespace Lattice.ECS.Framework
{
    /// <summary>
    /// 命令类型
    /// </summary>
    public enum CommandType : byte
    {
        CreateEntity,
        DestroyEntity,
        AddComponent,
        RemoveComponent,
        SetComponent,
    }

    /// <summary>
    /// 命令头（变长命令的前4字节）
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct CommandHeader
    {
        public CommandType Type;
        public ushort ComponentTypeId; // ushort.MaxValue = 无组件
        public ushort PayloadSize;
    }

    /// <summary>
    /// 命令缓冲 - 预测回滚模型的核心组件
    /// 
    /// 设计原理：
    /// 1. 所有修改 ECS 状态的操作都通过命令缓冲
    /// 2. 命令在帧结束时批量执行（Playback）
    /// 3. 支持命令的序列化（用于网络同步）
    /// 4. 支持命令的撤销（用于回滚）
    /// </summary>
    public unsafe struct CommandBuffer
    {
        // 命令数据缓冲区
        private byte* _buffer;
        private int _capacity;
        private int _writePosition;

        // 实体创建追踪（用于映射临时索引）
        private EntityRef* _createdEntities;
        private int _createdCount;
        private int _createdCapacity;

        // 分配器
        private Allocator* _allocator;
        private bool _ownsAllocator;

        /// <summary>是否为空</summary>
        public bool IsEmpty => _writePosition == 0;

        /// <summary>命令数量</summary>
        public int CreatedEntityCount => _createdCount;

        /// <summary>
        /// 初始化命令缓冲
        /// </summary>
        public void Initialize(int capacity = 4096)
        {
            _allocator = Allocator.Create();
            Initialize(_allocator, capacity);
            _ownsAllocator = true;
        }

        /// <summary>
        /// 初始化命令缓冲
        /// </summary>
        public void Initialize(Frame frame, int capacity = 4096)
        {
            ArgumentNullException.ThrowIfNull(frame);
            Initialize(frame.GetAllocatorPointer(), capacity);
        }

        /// <summary>
        /// 初始化命令缓冲
        /// </summary>
        public void Initialize(Allocator* allocator, int capacity = 4096)
        {
            if (allocator == null)
            {
                throw new ArgumentNullException(nameof(allocator));
            }

            _allocator = allocator;
            _capacity = capacity;
            _writePosition = 0;
            _createdCount = 0;
            _createdCapacity = 64;
            _ownsAllocator = false;

            _buffer = (byte*)allocator->Alloc(capacity);
            _createdEntities = (EntityRef*)allocator->Alloc(sizeof(EntityRef) * _createdCapacity);
        }

        /// <summary>
        /// 释放命令缓冲
        /// </summary>
        public void Dispose()
        {
            _buffer = null;
            _createdEntities = null;
            _writePosition = 0;
            _createdCount = 0;

            if (_ownsAllocator && _allocator != null)
            {
                Allocator.Destroy(_allocator);
            }

            _allocator = null;
            _ownsAllocator = false;
        }

        /// <summary>
        /// 清空缓冲
        /// </summary>
        public void Clear()
        {
            _writePosition = 0;
            _createdCount = 0;
        }

        #region 命令写入

        /// <summary>
        /// 创建实体命令
        /// </summary>
        public EntityRef CreateEntity()
        {
            EnsureSpace(sizeof(CommandHeader));

            var header = new CommandHeader
            {
                Type = CommandType.CreateEntity,
                ComponentTypeId = ushort.MaxValue,
                PayloadSize = 0
            };

            WriteHeader(&header);

            // 记录临时实体引用（实际索引在 Playback 时确定）
            var tempRef = new EntityRef(-(_createdCount + 1), 0);  // 负索引表示临时

            // 扩展创建列表
            if (_createdCount >= _createdCapacity)
            {
                _createdCapacity *= 2;
                var newArray = (EntityRef*)_allocator->Alloc(sizeof(EntityRef) * _createdCapacity);
                Buffer.MemoryCopy(_createdEntities, newArray,
                    sizeof(EntityRef) * _createdCapacity, sizeof(EntityRef) * _createdCount);
                _createdEntities = newArray;
            }

            _createdEntities[_createdCount++] = tempRef;
            return tempRef;
        }

        /// <summary>
        /// 销毁实体命令
        /// </summary>
        public void DestroyEntity(EntityRef entity)
        {
            int payloadSize = sizeof(EntityRef);
            EnsureSpace(sizeof(CommandHeader) + payloadSize);

            var header = new CommandHeader
            {
                Type = CommandType.DestroyEntity,
                ComponentTypeId = ushort.MaxValue,
                PayloadSize = checked((ushort)payloadSize)
            };

            WriteHeader(&header);
            WriteData(&entity, payloadSize);
        }

        /// <summary>
        /// 添加组件命令（泛型版本，需要编译时知道类型）
        /// </summary>
        public void AddComponent<T>(EntityRef entity, T component) where T : unmanaged, IComponent
        {
            int componentSize = sizeof(T);
            int payloadSize = sizeof(EntityRef) + componentSize;
            EnsureSpace(sizeof(CommandHeader) + payloadSize);

            var header = new CommandHeader
            {
                Type = CommandType.AddComponent,
                ComponentTypeId = checked((ushort)ComponentTypeId<T>.Id),
                PayloadSize = checked((ushort)payloadSize)
            };

            WriteHeader(&header);
            WriteData(&entity, sizeof(EntityRef));
            WriteData(&component, componentSize);
        }

        /// <summary>
        /// 移除组件命令
        /// </summary>
        public void RemoveComponent<T>(EntityRef entity) where T : unmanaged, IComponent
        {
            int payloadSize = sizeof(EntityRef);
            EnsureSpace(sizeof(CommandHeader) + payloadSize);

            var header = new CommandHeader
            {
                Type = CommandType.RemoveComponent,
                ComponentTypeId = checked((ushort)ComponentTypeId<T>.Id),
                PayloadSize = checked((ushort)payloadSize)
            };

            WriteHeader(&header);
            WriteData(&entity, payloadSize);
        }

        /// <summary>
        /// 设置组件命令（替换现有组件）
        /// </summary>
        public void SetComponent<T>(EntityRef entity, T component) where T : unmanaged, IComponent
        {
            int componentSize = sizeof(T);
            int payloadSize = sizeof(EntityRef) + componentSize;
            EnsureSpace(sizeof(CommandHeader) + payloadSize);

            var header = new CommandHeader
            {
                Type = CommandType.SetComponent,
                ComponentTypeId = checked((ushort)ComponentTypeId<T>.Id),
                PayloadSize = checked((ushort)payloadSize)
            };

            WriteHeader(&header);
            WriteData(&entity, sizeof(EntityRef));
            WriteData(&component, componentSize);
        }

        #endregion

        #region 命令执行

        /// <summary>
        /// 执行所有命令（在帧开始时调用）
        /// </summary>
        public void Playback(Frame frame)
        {
            if (IsEmpty) return;

            byte* ptr = _buffer;
            byte* end = _buffer + _writePosition;
            int createdIndex = 0;

            while (ptr < end)
            {
                if (end - ptr < sizeof(CommandHeader))
                {
                    throw new InvalidOperationException("Command buffer ended before a full header could be read.");
                }

                var header = *(CommandHeader*)ptr;
                ptr += sizeof(CommandHeader);
                byte* payload = ptr;
                if (payload + header.PayloadSize > end)
                {
                    throw new InvalidOperationException($"Command payload exceeds buffer bounds. Type={header.Type}, PayloadSize={header.PayloadSize}");
                }

                ValidatePayloadSize(header);
                ptr += header.PayloadSize;

                switch (header.Type)
                {
                    case CommandType.CreateEntity:
                        {
                            var entity = frame.CreateEntity();
                            if (createdIndex < _createdCount)
                            {
                                _createdEntities[createdIndex++] = entity;
                            }
                            break;
                        }

                    case CommandType.DestroyEntity:
                        {
                            EntityRef entity = ResolveEntity(payload);
                            frame.DestroyEntity(entity);
                            break;
                        }

                    case CommandType.AddComponent:
                        {
                            EntityRef entity = ResolveEntity(payload);
                            PlaybackAddComponent(frame, entity, header.ComponentTypeId, payload + sizeof(EntityRef));
                            break;
                        }

                    case CommandType.RemoveComponent:
                        {
                            EntityRef entity = ResolveEntity(payload);
                            PlaybackRemoveComponent(frame, entity, header.ComponentTypeId);
                            break;
                        }

                    case CommandType.SetComponent:
                        {
                            EntityRef entity = ResolveEntity(payload);
                            PlaybackSetComponent(frame, entity, header.ComponentTypeId, payload + sizeof(EntityRef));
                            break;
                        }
                }
            }

            frame.CommitDeferredRemovals();
            Clear();
        }

        #endregion

        #region 序列化（网络同步）

        /// <summary>
        /// 序列化命令缓冲到字节数组（用于网络同步）
        /// </summary>
        public byte[] Serialize()
        {
            var result = new byte[_writePosition + sizeof(int)];
            fixed (byte* ptr = result)
            {
                *(int*)ptr = _writePosition;
                Buffer.MemoryCopy(_buffer, ptr + sizeof(int), _writePosition, _writePosition);
            }
            return result;
        }

        /// <summary>
        /// 从字节数组反序列化
        /// </summary>
        public void Deserialize(byte[] data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            if (data.Length < sizeof(int))
            {
                throw new InvalidOperationException("Serialized command buffer is too short.");
            }

            fixed (byte* ptr = data)
            {
                int size = *(int*)ptr;
                if (size < 0 || size > data.Length - sizeof(int))
                {
                    throw new InvalidOperationException($"Serialized command buffer size is invalid: {size}");
                }

                EnsureSpace(size);
                Buffer.MemoryCopy(ptr + sizeof(int), _buffer, size, size);
                _writePosition = size;
                RecountCreatedEntities();
            }
        }

        #endregion

        #region 内部辅助

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EnsureSpace(int size)
        {
            if (_writePosition + size > _capacity)
            {
                // 扩展缓冲区
                int newCapacity = _capacity * 2;
                while (newCapacity < _writePosition + size)
                    newCapacity *= 2;

                var newBuffer = (byte*)_allocator->Alloc(newCapacity);
                Buffer.MemoryCopy(_buffer, newBuffer, _capacity, _writePosition);
                _buffer = newBuffer;
                _capacity = newCapacity;
            }
        }

        private void EnsureCreatedCapacity(int requiredCapacity)
        {
            if (requiredCapacity <= _createdCapacity)
            {
                return;
            }

            int newCapacity = _createdCapacity;
            while (newCapacity < requiredCapacity)
            {
                newCapacity *= 2;
            }

            var newArray = (EntityRef*)_allocator->Alloc(sizeof(EntityRef) * newCapacity);
            if (_createdCount > 0)
            {
                Buffer.MemoryCopy(
                    _createdEntities,
                    newArray,
                    sizeof(EntityRef) * newCapacity,
                    sizeof(EntityRef) * _createdCount);
            }

            _createdEntities = newArray;
            _createdCapacity = newCapacity;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteHeader(CommandHeader* header)
        {
            *(CommandHeader*)(_buffer + _writePosition) = *header;
            _writePosition += sizeof(CommandHeader);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteData(void* data, int size)
        {
            Buffer.MemoryCopy(data, _buffer + _writePosition, size, size);
            _writePosition += size;
        }

        private void PlaybackAddComponent(Frame frame, EntityRef entity, ushort typeId, byte* ptr)
        {
            ComponentCommandRegistry.PlaybackAdd(frame, entity, typeId, ptr);
        }

        private void PlaybackRemoveComponent(Frame frame, EntityRef entity, ushort typeId)
        {
            ComponentCommandRegistry.PlaybackRemove(frame, entity, typeId);
        }

        private void PlaybackSetComponent(Frame frame, EntityRef entity, ushort typeId, byte* ptr)
        {
            ComponentCommandRegistry.PlaybackSet(frame, entity, typeId, ptr);
        }

        private EntityRef ResolveEntity(byte* payload)
        {
            EntityRef entity = *(EntityRef*)payload;
            if (entity.Index >= 0)
            {
                return entity;
            }

            int createdOrdinal = -entity.Index - 1;
            if ((uint)createdOrdinal >= (uint)_createdCount)
            {
                throw new InvalidOperationException($"未找到临时实体映射：{entity}。");
            }

            return _createdEntities[createdOrdinal];
        }

        private static void ValidatePayloadSize(in CommandHeader header)
        {
            int expectedSize = header.Type switch
            {
                CommandType.CreateEntity => 0,
                CommandType.DestroyEntity => sizeof(EntityRef),
                CommandType.RemoveComponent => sizeof(EntityRef),
                CommandType.AddComponent => sizeof(EntityRef) + ComponentCommandRegistry.GetComponentSize(header.ComponentTypeId),
                CommandType.SetComponent => sizeof(EntityRef) + ComponentCommandRegistry.GetComponentSize(header.ComponentTypeId),
                _ => throw new InvalidOperationException($"Unknown command type: {header.Type}")
            };

            if (header.PayloadSize != expectedSize)
            {
                throw new InvalidOperationException(
                    $"Command payload size mismatch. Type={header.Type}, ComponentTypeId={header.ComponentTypeId}, Expected={expectedSize}, Actual={header.PayloadSize}");
            }
        }

        private void RecountCreatedEntities()
        {
            _createdCount = 0;

            byte* ptr = _buffer;
            byte* end = _buffer + _writePosition;
            while (ptr < end)
            {
                CommandHeader header = *(CommandHeader*)ptr;
                ptr += sizeof(CommandHeader);

                if (header.Type == CommandType.CreateEntity)
                {
                    EnsureCreatedCapacity(_createdCount + 1);
                    _createdEntities[_createdCount++] = new EntityRef(-_createdCount, 0);
                }

                ptr += header.PayloadSize;
                if (ptr > end)
                {
                    throw new InvalidOperationException("Serialized command buffer is malformed.");
                }
            }
        }

        #endregion
    }
}
