// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Lattice.Core;

namespace Lattice.ECS.Core
{
    /// <summary>
    /// 组件数据缓冲区 - FrameSync 风格（集中式稀疏数组管理）
    /// 
    /// 核心设计：
    /// 1. 非托管内存（Block** 指针数组）
    /// 2. 每个缓冲区维护自己的稀疏数组（ushort* _sparse）
    /// 3. 密集存储（交换删除保持紧凑）
    /// 4. 版本控制（迭代安全）
    /// </summary>
    public unsafe struct ComponentDataBuffer
    {
        #region 常量

        public const int DefaultBlockCapacity = 512;
        public const ushort InvalidSparse = 0;

        #endregion

        #region Block 结构

        /// <summary>
        /// 内存块 - 密集存储实体引用和组件数据
        /// </summary>
        public struct Block
        {
            /// <summary>实体引用数组（非托管）</summary>
            public EntityRef* PackedHandles;

            /// <summary>组件数据数组（非托管）</summary>
            public byte* PackedData;

            /// <summary>当前已使用槽位数</summary>
            public int Count;

            /// <summary>构造代数（用于验证）</summary>
            public int Generation;
        }

        #endregion

        #region 字段

        /// <summary>组件类型 ID</summary>
        public int ComponentTypeId;

        /// <summary>组件大小（字节）</summary>
        public int Stride;

        /// <summary>每个 Block 容量</summary>
        public int BlockCapacity;

        /// <summary>Block 指针数组</summary>
        public Block** Blocks;

        /// <summary>当前 Block 数量</summary>
        public int BlockListCount;

        /// <summary>Block 数组容量</summary>
        public int BlockListCapacity;

        /// <summary>有效条目数（不含索引0）</summary>
        public int Count;

        /// <summary>版本号（修改时递增）</summary>
        public int Version;

        /// <summary>组件标志</summary>
        public ComponentFlags Flags;

        /// <summary>待移除数量</summary>
        public int PendingRemoval;

        /// <summary>稀疏数组：Entity.Index → ComponentIndex</summary>
        public ushort* Sparse;

        /// <summary>稀疏数组容量</summary>
        public int SparseCapacity;

        /// <summary>单例组件的稀疏索引</summary>
        public ushort SingletonSparse;

        /// <summary>每个 Block 数据偏移量</summary>
        public int BlockDataOffset;

        /// <summary>每个 Block 总字节大小</summary>
        public int BlockByteSize;

        #endregion

        #region 属性

        /// <summary>有效组件数（不含待移除）</summary>
        public int UsedCount => Count - PendingRemoval;

        /// <summary>是否为单例组件</summary>
        public bool IsSingleton => (Flags & ComponentFlags.Singleton) != 0;

        /// <summary>单例实体引用</summary>
        public EntityRef SingletonRef
        {
            get
            {
                if (!IsSingleton)
                    throw new InvalidOperationException($"Not a singleton component: {ComponentTypeId}");
                if (SingletonSparse == 0)
                    return EntityRef.None;
                return GetEntityRefByIndex(SingletonSparse);
            }
        }

        #endregion

        #region 分配与释放

        /// <summary>
        /// 创建并初始化缓冲区
        /// </summary>
        public static ComponentDataBuffer Create(
            int componentTypeId,
            int stride,
            int blockCapacity,
            int initialEntityCapacity)
        {
            var buffer = new ComponentDataBuffer
            {
                ComponentTypeId = componentTypeId,
                Stride = stride,
                BlockCapacity = blockCapacity,
                Count = 1, // 索引0保留为无效
                PendingRemoval = 0,
                Version = 1,
                SingletonSparse = 0,
                SparseCapacity = 0,
                Sparse = null,
                BlockListCapacity = 4,
                BlockListCount = 0
            };

            // 计算 Block 布局
            buffer.BlockDataOffset = RoundUpToAlignment(sizeof(EntityRef) * blockCapacity, 8);
            buffer.BlockByteSize = buffer.BlockDataOffset + stride * blockCapacity;

            // 分配 Block 指针数组
            buffer.Blocks = (Block**)Marshal.AllocHGlobal(sizeof(Block*) * buffer.BlockListCapacity);
            Unsafe.InitBlock(buffer.Blocks, 0, (uint)(sizeof(Block*) * buffer.BlockListCapacity));

            return buffer;
        }

        /// <summary>
        /// 释放所有非托管内存
        /// </summary>
        public void Free()
        {
            if (Blocks == null) return;

            for (int i = 0; i < BlockListCapacity; i++)
            {
                Block* block = Blocks[i];
                if (block != null && block->PackedHandles != null)
                {
                    Marshal.FreeHGlobal((IntPtr)block->PackedHandles);
                }
            }

            Marshal.FreeHGlobal((IntPtr)Blocks);
            Blocks = null;
        }

        #endregion

        #region 核心访问

        /// <summary>
        /// 通过索引获取实体引用（快速，无检查）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public EntityRef GetEntityRefByIndex(int index)
        {
#if DEBUG
            if (index <= 0 || index >= Count)
                throw new ArgumentOutOfRangeException(nameof(index));
#endif
            int block = index / BlockCapacity;
            int column = index % BlockCapacity;
            return Blocks[block]->PackedHandles[column];
        }

        /// <summary>
        /// 通过索引获取数据指针（快速，无检查）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void* GetDataPointerByIndex(int index)
        {
#if DEBUG
            if (index <= 0 || index >= Count)
                throw new ArgumentOutOfRangeException(nameof(index));
#endif
            int block = index / BlockCapacity;
            int column = index % BlockCapacity;
            return Blocks[block]->PackedData + Stride * column;
        }

        /// <summary>
        /// 获取组件数据指针（带存在性检查）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void* GetDataPointer(EntityRef entityRef)
        {
#if DEBUG
            if (entityRef.Index >= SparseCapacity)
                throw new ArgumentOutOfRangeException(nameof(entityRef));
#endif
            ushort sparse = Sparse[entityRef.Index];
            if (sparse == 0)
                throw new InvalidOperationException($"Entity {entityRef} does not have component {ComponentTypeId}");

#if DEBUG
            AssertHandleVersion(entityRef);
#endif
            return GetDataPointerByIndex(sparse);
        }

        /// <summary>
        /// 快速获取数据指针（调用方已验证存在性）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void* GetDataPointerFastUnsafe(EntityRef entityRef)
        {
#if DEBUG
            AssertHandleVersion(entityRef);
#endif
            ushort index = Sparse[entityRef.Index];
#if DEBUG
            if (index == 0 || index >= Count)
                throw new InvalidOperationException($"Invalid sparse index: {index}");
#endif
            int block = index / BlockCapacity;
            int column = index % BlockCapacity;
            return Blocks[block]->PackedData + Stride * column;
        }

        /// <summary>
        /// 检查实体是否拥有此组件
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Has(EntityRef entityRef)
        {
            if (entityRef.Index >= SparseCapacity) return false;
            return Sparse[entityRef.Index] != 0;
        }

        #endregion

        #region 添加与设置

        /// <summary>
        /// 设置组件数据（不存在则添加）
        /// </summary>
        public void* Set(EntityRef entityRef, void* data)
        {
            void* ptr = GetBufferEntry(entityRef);
            if (data == null)
                Unsafe.InitBlock(ptr, 0, (uint)Stride);
            else
                Unsafe.CopyBlock(ptr, data, (uint)Stride);
            return ptr;
        }

        /// <summary>
        /// 设置组件数据（泛型版本）
        /// </summary>
        public T* Set<T>(EntityRef entityRef, T value) where T : unmanaged
        {
            T* ptr = (T*)GetBufferEntry(entityRef);
            *ptr = value;
            return ptr;
        }

        /// <summary>
        /// 获取或创建缓冲区条目
        /// </summary>
        private void* GetBufferEntry(EntityRef entityRef)
        {
#if DEBUG
            if (entityRef.Index >= SparseCapacity)
                throw new ArgumentOutOfRangeException(nameof(entityRef));
#endif
            ushort sparse = Sparse[entityRef.Index];

            if (sparse == 0)
            {
                // 新条目
                if (Count >= BlockCapacity * BlockListCount)
                    UseNextBlock();

                if (IsSingleton)
                {
                    if (SingletonSparse != 0)
                        throw new InvalidOperationException($"Singleton component {ComponentTypeId} already exists");
                    SingletonSparse = (ushort)Count;
                }

                sparse = (ushort)Count++;
                int block = sparse / BlockCapacity;
                int column = sparse % BlockCapacity;

                Blocks[block]->PackedHandles[column] = entityRef;
                Sparse[entityRef.Index] = sparse;
                Version++;

                return Blocks[block]->PackedData + Stride * column;
            }
            else
            {
                // 更新现有条目
                if (IsSingleton && SingletonSparse != sparse)
                {
                    if (SingletonSparse != 0)
                        throw new InvalidOperationException($"Singleton component {ComponentTypeId} already exists");
                    SingletonSparse = sparse;
                }

#if DEBUG
                AssertHandleVersion(entityRef);
#endif
                int block = sparse / BlockCapacity;
                int column = sparse % BlockCapacity;
                return Blocks[block]->PackedData + Stride * column;
            }
        }

        #endregion

        #region 移除

        /// <summary>
        /// 移除组件（交换删除）
        /// </summary>
        public void Remove(EntityRef entityRef)
        {
#if DEBUG
            if (entityRef.Index >= SparseCapacity)
                throw new ArgumentOutOfRangeException(nameof(entityRef));
#endif
            ushort sparse = Sparse[entityRef.Index];
            if (sparse == 0)
                throw new InvalidOperationException($"Entity {entityRef} does not have component {ComponentTypeId}");

#if DEBUG
            AssertHandleVersion(entityRef);
            if (IsSingleton && SingletonSparse == sparse)
                throw new InvalidOperationException("Cannot remove singleton via Remove");
#endif

            PendingRemoval--;

            if (sparse < --Count)
            {
                // 与末尾交换
                int removedBlock = sparse / BlockCapacity;
                int removedColumn = sparse % BlockCapacity;
                int replacementBlock = Count / BlockCapacity;
                int replacementColumn = Count % BlockCapacity;

                EntityRef replacementHandle = Blocks[removedBlock]->PackedHandles[removedColumn] =
                    Blocks[replacementBlock]->PackedHandles[replacementColumn];

                byte* dest = Blocks[removedBlock]->PackedData + Stride * removedColumn;
                byte* src = Blocks[replacementBlock]->PackedData + Stride * replacementColumn;
                Unsafe.CopyBlock(dest, src, (uint)Stride);

                Sparse[replacementHandle.Index] = sparse;
                Blocks[replacementBlock]->PackedHandles[replacementColumn] = EntityRef.None;

                if (IsSingleton && SingletonSparse == Count)
                    SingletonSparse = sparse;
            }
            else
            {
                // 直接清除
                int block = sparse / BlockCapacity;
                int column = sparse % BlockCapacity;
                Blocks[block]->PackedHandles[column] = EntityRef.None;
            }

            Version++;
            Sparse[entityRef.Index] = 0;
        }

        /// <summary>
        /// 标记为待移除（延迟删除）
        /// </summary>
        public void RemovalPending(EntityRef entityRef)
        {
            PendingRemoval++;
            if (IsSingleton)
            {
                ushort sparse = Sparse[entityRef.Index];
                if (sparse != 0 && SingletonSparse == sparse)
                    SingletonSparse = 0;
            }
        }

        #endregion

        #region 内存管理

        /// <summary>
        /// 扩展稀疏数组容量
        /// </summary>
        public void ChangeEntityCapacity(int newCapacity)
        {
            if (newCapacity <= SparseCapacity) return;

            ushort* newSparse = (ushort*)Marshal.AllocHGlobal(sizeof(ushort) * newCapacity);
            if (Sparse != null)
            {
                Unsafe.CopyBlock(newSparse, Sparse, (uint)(sizeof(ushort) * SparseCapacity));
                Marshal.FreeHGlobal((IntPtr)Sparse);
            }
            Unsafe.InitBlock(newSparse + SparseCapacity, 0, (uint)(sizeof(ushort) * (newCapacity - SparseCapacity)));

            Sparse = newSparse;
            SparseCapacity = newCapacity;
        }

        /// <summary>
        /// 分配新 Block
        /// </summary>
        private void UseNextBlock()
        {
            if (BlockListCount == BlockListCapacity)
            {
                int newCapacity = BlockListCapacity * 2;
                Block** newBlocks = (Block**)Marshal.AllocHGlobal(sizeof(Block*) * newCapacity);
                Unsafe.InitBlock(newBlocks, 0, (uint)(sizeof(Block*) * newCapacity));

                for (int i = 0; i < BlockListCount; i++)
                    newBlocks[i] = Blocks[i];

                Marshal.FreeHGlobal((IntPtr)Blocks);
                Blocks = newBlocks;
                BlockListCapacity = newCapacity;
            }

            int index = BlockListCount++;
            if (Blocks[index] == null || Blocks[index]->PackedHandles == null)
            {
                Blocks[index] = AllocateBlock();
            }
            else
            {
                Unsafe.InitBlock(Blocks[index]->PackedHandles, 0, (uint)BlockByteSize);
            }
        }

        /// <summary>
        /// 分配 Block 内存
        /// </summary>
        private Block* AllocateBlock()
        {
            byte* data = (byte*)Marshal.AllocHGlobal(BlockByteSize);
            Unsafe.InitBlock(data, 0, (uint)BlockByteSize);

            var block = (Block*)Marshal.AllocHGlobal(sizeof(Block));
            *block = new Block
            {
                PackedHandles = (EntityRef*)data,
                PackedData = data + BlockDataOffset,
                Count = 0,
                Generation = Version
            };
            return block;
        }

        private static int RoundUpToAlignment(int value, int alignment)
        {
            return (value + alignment - 1) & ~(alignment - 1);
        }

        #endregion

        #region 调试

        [Conditional("DEBUG")]
        private void AssertHandleVersion(EntityRef entityRef)
        {
            ushort sparse = Sparse[entityRef.Index];
            int block = sparse / BlockCapacity;
            int column = sparse % BlockCapacity;
            if (Blocks[block]->PackedHandles[column].Raw != entityRef.Raw)
                throw new InvalidOperationException($"Entity version mismatch: {entityRef}");
        }

        #endregion
    }
}
