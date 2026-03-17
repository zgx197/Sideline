// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

using System;
using System.Runtime.CompilerServices;
using Lattice.Core;
using FPMath = Lattice.Math.FPMath;

namespace Lattice.ECS.Core
{
    /// <summary>
    /// 组件块迭代器 - FrameSync 风格
    /// 
    /// 提供对组件数据的批量指针访问，最大化缓存效率
    /// 注意：迭代过程中不应修改组件存储结构（添加/删除组件）
    /// </summary>
    public unsafe struct ComponentBlockIterator<T> where T : unmanaged, IComponent
    {
        /// <summary>
        /// 枚举器 - 支持 foreach
        /// </summary>
        public struct Enumerator
        {
            private ComponentBlockIterator _inner;
            private int _blockIndex;
            private int _blockCount;
            private EntityRef* _blockEntities;
            private T* _blockComponents;
            private int _typeId;

            public EntityComponentPair<T> Current
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get
                {
                    if ((uint)_blockIndex >= (uint)_blockCount)
                        throw new InvalidOperationException();
                    return new EntityComponentPair<T>
                    {
                        Entity = _blockEntities[_blockIndex],
                        Component = &_blockComponents[_blockIndex]
                    };
                }
            }

            internal Enumerator(ComponentBlockIterator inner, int typeId)
            {
                _inner = inner;
                _typeId = typeId;
                _blockIndex = -1;
                _blockCount = 0;
                _blockEntities = null;
                _blockComponents = null;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool MoveNext()
            {
                do
                {
                    _blockIndex++;
                    if (_blockIndex == _blockCount)
                    {
                        byte* rawComponents;
                        if (!_inner.NextBlock(out _blockEntities, out rawComponents, out _blockCount))
                        {
                            _blockComponents = null;
                            return false;
                        }
                        _blockComponents = (T*)rawComponents;
                        _blockIndex = 0;
                    }
                    // 检查实体是否仍然有此组件（版本号匹配）
                } while (_blockEntities[_blockIndex].Version == 0); // 跳过无效实体

                return true;
            }
        }

        internal ComponentBlockIterator _inner;
        private int _typeId;

        internal ComponentBlockIterator(ComponentDataBuffer* buffer)
        {
#if DEBUG
            if (sizeof(T) != buffer->Stride)
                throw new ArgumentException($"Component size mismatch: {sizeof(T)} vs {buffer->Stride}");
#endif
            _inner = new ComponentBlockIterator(buffer);
            _typeId = ComponentTypeId<T>.Id;
        }

        public Enumerator GetEnumerator()
        {
            return new Enumerator(_inner, _typeId);
        }

        /// <summary>
        /// 获取下一块数据（批量处理）
        /// </summary>
        public bool NextBlock(out EntityRef* entities, out T* components, out int count)
        {
            if (_inner.NextBlock(out entities, out var rawComponents, out count))
            {
                components = (T*)rawComponents;
                return true;
            }
            components = null;
            return false;
        }
    }

    /// <summary>
    /// 内部块迭代器（不依赖泛型）
    /// </summary>
    internal unsafe struct ComponentBlockIterator
    {
        private int _version;
        private int _remaining;
        private int _currentBlock;
        private int _currentBlockOffset;
        private ComponentDataBuffer* _buffer;

        internal ComponentBlockIterator(ComponentDataBuffer* buffer)
        {
            _buffer = buffer;
            _version = buffer->Version;
            _currentBlock = 0;
            _currentBlockOffset = 1; // 跳过索引0
            _remaining = buffer->Count - 1; // 减去无效索引0
        }

        /// <summary>
        /// 获取下一块
        /// </summary>
        internal bool NextBlock(out EntityRef* entities, out byte* components, out int count)
        {
            ValidateVersion();

            while (_currentBlock < _buffer->BlockListCount && _remaining > 0)
            {
                int blockCapacity = _buffer->BlockCapacity;
                count = blockCapacity - _currentBlockOffset;

                if (count > 0)
                {
                    count = System.Math.Min(_remaining, count);
                    entities = _buffer->Blocks[_currentBlock]->PackedHandles + _currentBlockOffset;
                    components = _buffer->Blocks[_currentBlock]->PackedData + _buffer->Stride * _currentBlockOffset;
                    _remaining -= count;
                    _currentBlockOffset += count;
                    return true;
                }

                _currentBlock++;
                _currentBlockOffset = 0;
            }

            entities = null;
            components = null;
            count = 0;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ValidateVersion()
        {
            if (_version != _buffer->Version)
                throw new InvalidOperationException("Cannot modify components while iterating");
        }
    }

    /// <summary>
    /// 实体-组件指针对
    /// </summary>
    public unsafe struct EntityComponentPair<T> where T : unmanaged
    {
        public EntityRef Entity;
        public T* Component;
    }
}
