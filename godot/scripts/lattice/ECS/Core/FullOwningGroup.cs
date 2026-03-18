// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

using System;
using System.Runtime.CompilerServices;
using Lattice.Core;

namespace Lattice.ECS.Core
{
    /// <summary>
    /// Full-Owning Group - 终极性能的多组件存储
    /// 
    /// 核心思想：
    /// 1. 将经常一起访问的组件存储在同一块内存中（AOS 布局）
    /// 2. 遍历组件时缓存命中率达到理论最大值
    /// 3. 无需稀疏数组查找，直接索引访问
    /// 
    /// 与传统 Storage 的区别：
    /// - Storage<T1>, Storage<T2>: 分离存储，需要 EntityRef → Index 转换
    /// - FullOwningGroup<T1, T2>: 联合存储，直接 Index → (T1, T2) 访问
    /// 
    /// 适用场景：
    /// - Transform + RigidBody（物理更新）
    /// - Position + Velocity + Health（移动系统）
    /// - 任何每帧一起更新的组件组合
    /// </summary>
    public unsafe struct FullOwningGroup<T1, T2>
        where T1 : unmanaged
        where T2 : unmanaged
    {
        #region 常量

        public const int BlockCapacity = 128;  // 与 Storage 一致，方便互操作

        #endregion

        #region Block 结构

        /// <summary>
        /// AOS 布局的数据块
        /// 内存布局：[T1_0, T2_0, T1_1, T2_1, ...] 或 [T1_0..T1_127, T2_0..T2_127]
        /// 选择后者（SOA 组内）以获得更好的 SIMD 支持
        /// </summary>
        public struct Block
        {
            /// <summary>T1 组件数组</summary>
            public T1* Data1;
            /// <summary>T2 组件数组</summary>
            public T2* Data2;
            /// <summary>实体引用数组</summary>
            public EntityRef* Entities;
            /// <summary>有效项数（0-128）</summary>
            public int Count;
        }

        #endregion

        #region 字段

        private Block* _blocks;
        private int _blockCount;
        private int _blockCapacity;
        private int _count;
        private int _version;

        private Allocator* _allocator;

        #endregion

        #region 属性

        public int Count => _count;
        public int BlockCount => _blockCount;
        public int Version => _version;

        #endregion

        #region 生命周期

        public void Initialize(Allocator* allocator)
        {
            _allocator = allocator;
            _blockCapacity = 4;
            _blockCount = 0;
            _count = 0;
            _version = 0;

            _blocks = (Block*)allocator->Alloc(sizeof(Block) * _blockCapacity);
            for (int i = 0; i < _blockCapacity; i++)
            {
                _blocks[i].Data1 = null;
                _blocks[i].Data2 = null;
                _blocks[i].Entities = null;
                _blocks[i].Count = 0;
            }
        }

        public void Dispose()
        {
            _blocks = null;
            _blockCount = 0;
            _count = 0;
        }

        #endregion

        #region 添加/删除

        /// <summary>
        /// 添加实体和组件对 - O(1) 均摊
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(EntityRef entity, in T1 c1, in T2 c2)
        {
            // 确保有空间
            if (_blockCount == 0 || _blocks[_blockCount - 1].Count >= BlockCapacity)
            {
                EnsureBlockSpace();
            }

            Block* block = &_blocks[_blockCount - 1];
            int index = block->Count++;

            block->Entities[index] = entity;
            block->Data1[index] = c1;
            block->Data2[index] = c2;

            _count++;
            _version++;
        }

        /// <summary>
        /// 通过实体查找并删除 - O(N)，不推荐频繁使用
        /// </summary>
        public void Remove(EntityRef entity)
        {
            // 找到实体
            for (int b = 0; b < _blockCount; b++)
            {
                Block* block = &_blocks[b];
                for (int i = 0; i < block->Count; i++)
                {
                    if (block->Entities[i] == entity)
                    {
                        RemoveAt(b, i);
                        return;
                    }
                }
            }
        }

        /// <summary>
        /// 通过索引删除（高性能版本）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveAt(int blockIndex, int indexInBlock)
        {
            Block* block = &_blocks[blockIndex];
            int lastIndex = block->Count - 1;

            // 与最后一个交换
            if (indexInBlock != lastIndex)
            {
                block->Entities[indexInBlock] = block->Entities[lastIndex];
                block->Data1[indexInBlock] = block->Data1[lastIndex];
                block->Data2[indexInBlock] = block->Data2[lastIndex];
            }

            block->Count--;
            _count--;
            _version++;
        }

        #endregion

        #region 迭代器（终极性能）

        /// <summary>
        /// Block 迭代器 - 批量访问
        /// </summary>
        public bool NextBlock(int* blockIndex, out EntityRef* entities, out T1* data1, out T2* data2, out int count)
        {
            if (*blockIndex >= _blockCount)
            {
                entities = null;
                data1 = null;
                data2 = null;
                count = 0;
                return false;
            }

            Block* block = &_blocks[*blockIndex];
            entities = block->Entities;
            data1 = block->Data1;
            data2 = block->Data2;
            count = block->Count;

            (*blockIndex)++;
            return true;
        }

        /// <summary>
        /// 逐个迭代（保持接口一致）
        /// </summary>
        public bool Next(int* globalIndex, out EntityRef entity, out T1* c1, out T2* c2)
        {
            if (*globalIndex >= _count)
            {
                entity = EntityRef.None;
                c1 = null;
                c2 = null;
                return false;
            }

            int blockIdx = *globalIndex / BlockCapacity;
            int itemIdx = *globalIndex % BlockCapacity;

            Block* block = &_blocks[blockIdx];
            entity = block->Entities[itemIdx];
            c1 = &block->Data1[itemIdx];
            c2 = &block->Data2[itemIdx];

            (*globalIndex)++;
            return true;
        }

        #endregion

        #region 内部辅助

        private void EnsureBlockSpace()
        {
            if (_blockCount >= _blockCapacity)
            {
                int newCapacity = _blockCapacity * 2;
                var newBlocks = (Block*)_allocator->Alloc(sizeof(Block) * newCapacity);

                Buffer.MemoryCopy(_blocks, newBlocks,
                    sizeof(Block) * newCapacity, sizeof(Block) * _blockCapacity);

                // 初始化新 block
                for (int i = _blockCapacity; i < newCapacity; i++)
                {
                    newBlocks[i].Data1 = null;
                    newBlocks[i].Data2 = null;
                    newBlocks[i].Entities = null;
                    newBlocks[i].Count = 0;
                }

                _blocks = newBlocks;
                _blockCapacity = newCapacity;
            }

            // 分配新 block
            Block* newBlock = &_blocks[_blockCount++];
            newBlock->Data1 = (T1*)_allocator->Alloc(sizeof(T1) * BlockCapacity);
            newBlock->Data2 = (T2*)_allocator->Alloc(sizeof(T2) * BlockCapacity);
            newBlock->Entities = (EntityRef*)_allocator->Alloc(sizeof(EntityRef) * BlockCapacity);
            newBlock->Count = 0;
        }

        #endregion
    }

    /// <summary>
    /// 三组件 Full-Owning Group
    /// </summary>
    public unsafe struct FullOwningGroup<T1, T2, T3>
        where T1 : unmanaged
        where T2 : unmanaged
        where T3 : unmanaged
    {
        public struct Block
        {
            public T1* Data1;
            public T2* Data2;
            public T3* Data3;
            public EntityRef* Entities;
            public int Count;
        }

        private Block* _blocks;
        private int _blockCount;
        private int _blockCapacity;
        private int _count;

        private Allocator* _allocator;

        public int Count => _count;

        public void Initialize(Allocator* allocator)
        {
            _allocator = allocator;
            _blockCapacity = 4;
            _blockCount = 0;
            _count = 0;

            _blocks = (Block*)allocator->Alloc(sizeof(Block) * _blockCapacity);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(EntityRef entity, in T1 c1, in T2 c2, in T3 c3)
        {
            if (_blockCount == 0 || _blocks[_blockCount - 1].Count >= FullOwningGroup<T1, T2>.BlockCapacity)
            {
                EnsureBlockSpace();
            }

            Block* block = &_blocks[_blockCount - 1];
            int index = block->Count++;

            block->Entities[index] = entity;
            block->Data1[index] = c1;
            block->Data2[index] = c2;
            block->Data3[index] = c3;

            _count++;
        }

        private void EnsureBlockSpace()
        {
            if (_blockCount >= _blockCapacity)
            {
                int newCapacity = _blockCapacity * 2;
                var newBlocks = (Block*)_allocator->Alloc(sizeof(Block) * newCapacity);
                Buffer.MemoryCopy(_blocks, newBlocks,
                    sizeof(Block) * newCapacity, sizeof(Block) * _blockCapacity);
                _blocks = newBlocks;
                _blockCapacity = newCapacity;
            }

            Block* newBlock = &_blocks[_blockCount++];
            newBlock->Data1 = (T1*)_allocator->Alloc(sizeof(T1) * FullOwningGroup<T1, T2>.BlockCapacity);
            newBlock->Data2 = (T2*)_allocator->Alloc(sizeof(T2) * FullOwningGroup<T1, T2>.BlockCapacity);
            newBlock->Data3 = (T3*)_allocator->Alloc(sizeof(T3) * FullOwningGroup<T1, T2>.BlockCapacity);
            newBlock->Entities = (EntityRef*)_allocator->Alloc(sizeof(EntityRef) * FullOwningGroup<T1, T2>.BlockCapacity);
            newBlock->Count = 0;
        }
    }
}
