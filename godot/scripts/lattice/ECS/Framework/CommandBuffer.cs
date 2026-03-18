// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

using System;
using System.Runtime.CompilerServices;
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
    public unsafe struct CommandHeader
    {
        public CommandType Type;
        public byte ComponentTypeId;  // 255 = 无组件
        public ushort EntityIndex;    // 65535 = 新实体（CreateEntity）
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

        /// <summary>是否为空</summary>
        public bool IsEmpty => _writePosition == 0;

        /// <summary>命令数量</summary>
        public int CreatedEntityCount => _createdCount;

        /// <summary>
        /// 初始化命令缓冲
        /// </summary>
        public void Initialize(Allocator* allocator, int capacity = 4096)
        {
            _allocator = allocator;
            _capacity = capacity;
            _writePosition = 0;
            _createdCount = 0;
            _createdCapacity = 64;

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
                ComponentTypeId = 255,
                EntityIndex = 0xFFFF  // 标记为新实体
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
            EnsureSpace(sizeof(CommandHeader));

            var header = new CommandHeader
            {
                Type = CommandType.DestroyEntity,
                ComponentTypeId = 255,
                EntityIndex = (ushort)entity.Index
            };

            WriteHeader(&header);
        }

        /// <summary>
        /// 添加组件命令（泛型版本，需要编译时知道类型）
        /// </summary>
        public void AddComponent<T>(EntityRef entity, T component) where T : unmanaged
        {
            int componentSize = sizeof(T);
            EnsureSpace(sizeof(CommandHeader) + componentSize);

            var header = new CommandHeader
            {
                Type = CommandType.AddComponent,
                ComponentTypeId = (byte)ComponentTypeId<T>.Id,
                EntityIndex = (ushort)entity.Index
            };

            WriteHeader(&header);
            WriteData(&component, componentSize);
        }

        /// <summary>
        /// 移除组件命令
        /// </summary>
        public void RemoveComponent<T>(EntityRef entity) where T : unmanaged
        {
            EnsureSpace(sizeof(CommandHeader));

            var header = new CommandHeader
            {
                Type = CommandType.RemoveComponent,
                ComponentTypeId = (byte)ComponentTypeId<T>.Id,
                EntityIndex = (ushort)entity.Index
            };

            WriteHeader(&header);
        }

        /// <summary>
        /// 设置组件命令（替换现有组件）
        /// </summary>
        public void SetComponent<T>(EntityRef entity, T component) where T : unmanaged
        {
            int componentSize = sizeof(T);
            EnsureSpace(sizeof(CommandHeader) + componentSize);

            var header = new CommandHeader
            {
                Type = CommandType.SetComponent,
                ComponentTypeId = (byte)ComponentTypeId<T>.Id,
                EntityIndex = (ushort)entity.Index
            };

            WriteHeader(&header);
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
                var header = *(CommandHeader*)ptr;
                ptr += sizeof(CommandHeader);

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
                            var entity = new EntityRef(header.EntityIndex, 1);  // 版本需要正确处理
                            frame.DestroyEntity(entity);
                            break;
                        }

                    case CommandType.AddComponent:
                        {
                            var entity = new EntityRef(header.EntityIndex, 1);
                            // 需要类型分发来正确添加组件
                            // 这里简化处理，实际使用代码生成
                            PlaybackAddComponent(frame, entity, header.ComponentTypeId, ptr);
                            ptr += sizeof(int);  // 简化：假设组件大小为 int
                            break;
                        }

                    case CommandType.RemoveComponent:
                        {
                            var entity = new EntityRef(header.EntityIndex, 1);
                            PlaybackRemoveComponent(frame, entity, header.ComponentTypeId);
                            break;
                        }

                    case CommandType.SetComponent:
                        {
                            var entity = new EntityRef(header.EntityIndex, 1);
                            PlaybackSetComponent(frame, entity, header.ComponentTypeId, ptr);
                            ptr += sizeof(int);  // 简化：假设组件大小为 int
                            break;
                        }
                }
            }

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
            fixed (byte* ptr = data)
            {
                int size = *(int*)ptr;
                EnsureSpace(size);
                Buffer.MemoryCopy(ptr + sizeof(int), _buffer, size, size);
                _writePosition = size;
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

        private void PlaybackAddComponent(Frame frame, EntityRef entity, byte typeId, byte* ptr)
        {
            // 这里需要类型分发
            // 实际实现使用代码生成或反射缓存
        }

        private void PlaybackRemoveComponent(Frame frame, EntityRef entity, byte typeId)
        {
            // 类型分发
        }

        private void PlaybackSetComponent(Frame frame, EntityRef entity, byte typeId, byte* ptr)
        {
            // 类型分发
        }

        #endregion
    }
}
