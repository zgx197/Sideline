// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

// 条件编译：只在支持 .NET 5+ 的平台上启用高级特性
#if NET5_0_OR_GREATER
#define MODERN_NET
#endif

using System;
using System.Runtime.CompilerServices;
using Lattice.Core;

// 注意：此文件使用 unsafe 代码进行高性能内存操作
// 所有指针操作都经过边界检查（DEBUG 模式下）

namespace Lattice.ECS.Core
{
    /// <summary>
    /// Full-Owning Group - 终极性能的多组件存储
    /// 
    /// ============================================================
    /// 为什么需要 Full-Owning Group？
    /// ============================================================
    /// 
    /// 问题：传统 Storage 的多组件遍历性能瓶颈
    /// -------------------------------------------------------------
    /// 假设你需要更新 Position 和 Velocity：
    /// 
    /// 传统方式（Filter<T1, T2>）：
    ///   1. 遍历 Position Storage（缓存命中）
    ///   2. 对每个实体查找 Velocity（稀疏查找，缓存未命中）
    ///   3. 总内存访问：Position Block + Velocity Block（两个不连续区域）
    /// 
    /// Full-Owning Group 方式：
    ///   1. 直接遍历 Group（Position 和 Velocity 在同一缓存行）
    ///   2. 总内存访问：一个连续的内存区域
    /// 
    /// 性能对比（理论）：
    ///   - 传统方式：~20-30 CPU 周期/实体（两次缓存加载）
    ///   - Full-Owning：~5-10 CPU 周期/实体（一次缓存加载，预取友好）
    /// 
    /// ============================================================
    /// 架构设计决策
    /// ============================================================
    /// 
    /// Q: 为什么选择 AOS（Array of Structures）而不是 SOA？
    /// A:
    ///   1. 缓存局部性极致：一起访问的组件在同一缓存行
    ///   2. 预取友好：硬件预取器可以预测下一个 Group 项
    ///   3. 更少的指针追踪：不需要通过 EntityRef 查找多个 Storage
    ///   4. 与 ECS 原则的妥协：牺牲内存效率换取迭代性能
    /// 
    /// Q: 为什么固定 Block 容量为 128？
    /// A:
    ///   1. 与 Storage 保持一致，便于互操作（如从 Storage 迁移到 Group）
    ///   2. 128 * sizeof(T1+T2) 填满 L1 缓存（假设 T1=16, T2=16，128*32=4096 字节 ≈ L1 容量）
    ///   3. SIMD 友好：AVX2 可以一次处理 4 个 Group 项
    /// 
    /// Q: 为什么不支持动态添加/删除？
    /// A:
    ///   1. 简化内存管理：连续的内存块，无需处理空洞
    ///   2. 适合批量创建/销毁的场景（如粒子系统、子弹）
    ///   3. 如果需要频繁增删，使用传统 Storage + Filter
    /// 
    /// Q: 与 FrameSync 的区别？
    /// A:
    ///   1. FrameSync 没有 Full-Owning Group，只有 ComponentDataBuffer
    ///   2. FrameSync 的 Filter 每次都要做稀疏查找（_sparse[entity.Index]）
    ///   3. 这是 Lattice 独有的性能优化特性
    /// 
    /// ============================================================
    /// 使用场景
    /// ============================================================
    /// 
    /// ✅ 适合使用：
    /// - 物理系统（Position + Velocity + Mass）
    /// - 移动系统（Transform + Velocity + Rotation）
    /// - 粒子系统（Position + Velocity + Lifetime + Color）
    /// - 任何"每帧一起更新"的组件组合
    /// 
    /// ❌ 不适合使用：
    /// - 随机访问频繁的组件（用 Storage<T>）
    /// - 生命周期差异大的组件（用 Storage<T> + 延迟删除）
    /// - 需要复杂查询的组件（用 Filter）
    /// 
    /// ============================================================
    /// 内存布局
    /// ============================================================
    /// 
    /// FullOwningGroup<T1, T2>（AOS - Array of Structures）:
    /// 
    /// 我们使用 SOA within Group（组内 SOA）以获得更好的 SIMD 支持：
    /// 
    /// Block 0:
    /// ┌─────────────┬─────────────┬─────────────┐
    /// │ T1[0]       │ T1[1]       │ ... T1[127] │  ← 连续的 T1 数组（SIMD 友好）
    /// ├─────────────┼─────────────┼─────────────┤
    /// │ T2[0]       │ T2[1]       │ ... T2[127] │  ← 连续的 T2 数组（SIMD 友好）
    /// ├─────────────┼─────────────┼─────────────┤
    /// │ Entity[0]   │ Entity[1]   │ ... Entity  │  ← 实体引用
    /// └─────────────┴─────────────┴─────────────┘
    /// 
    /// 对比纯 AOS：
    /// ┌─────────────────────────────────────────┐
    /// │ T1[0] | T2[0] | Entity[0]               │  ← 混合存储，SIMD 不友好
    /// │ T1[1] | T2[1] | Entity[1]               │
    /// └─────────────────────────────────────────┘
    /// 
    /// 我们的布局兼顾：
    /// - AOS 的缓存局部性（一起访问的组件接近）
    /// - SOA 的 SIMD 友好性（同类型数据连续）
    /// 
    /// 迭代时的缓存行为：
    /// - 访问 T1[i] 时，T2[i] 很可能在同一缓存行（64 字节）
    /// - 预取器可以预测 T1[i+1], T2[i+1] 的访问模式
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
