// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

using System;
using System.Runtime.CompilerServices;
using Lattice.Math;

namespace Lattice.ECS.Core
{
    /// <summary>
    /// 高性能非托管内存分配器
    /// 
    /// 特性：
    /// 1. 基于内存池的分配策略，减少系统调用
    /// 2. 对齐分配（默认8字节对齐）
    /// 3. 零初始化选项
    /// 4. 线程不安全（ECS单线程操作）
    /// </summary>
    public unsafe class Allocator : IDisposable
    {
        #region 常量

        /// <summary>默认对齐边界（8字节）</summary>
        public const int DefaultAlignment = 8;

        /// <summary>最小分配块大小（1KB）</summary>
        private const int MinBlockSize = 1024;

        /// <summary>最大分配块大小（1MB）</summary>
        private const int MaxBlockSize = 1024 * 1024;

        #endregion

        #region 内存块

        /// <summary>
        /// 内存块头部信息
        /// </summary>
        private struct BlockHeader
        {
            /// <summary>块大小（不包括头部）</summary>
            public int Size;

            /// <summary>已使用字节数</summary>
            public int Used;

            /// <summary>前一个块</summary>
            public BlockHeader* Prev;

            /// <summary>后一个块</summary>
            public BlockHeader* Next;

            /// <summary>数据起始地址</summary>
            public byte* Data => (byte*)((byte*)Unsafe.AsPointer(ref this) + sizeof(BlockHeader));
        }

        #endregion

        #region 字段

        private BlockHeader* _firstBlock;
        private BlockHeader* _currentBlock;
        private int _totalAllocated;
        private int _totalUsed;
        private bool _isDisposed;

        #endregion

        #region 属性

        /// <summary>总分配内存（字节）</summary>
        public int TotalAllocated => _totalAllocated;

        /// <summary>总使用内存（字节）</summary>
        public int TotalUsed => _totalUsed;

        /// <summary>内存使用效率（定点数 0-1）</summary>
        public FP Utilization => _totalAllocated > 0 ? FP.FromRaw((long)_totalUsed * FP.One.RawValue / _totalAllocated) : FP.Zero;

        #endregion

        #region 分配方法

        /// <summary>
        /// 分配内存（不对齐）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void* Alloc(int size)
        {
            if (size <= 0)
                return null;

            // 尝试从当前块分配
            if (_currentBlock != null)
            {
                int alignedSize = AlignSize(size, DefaultAlignment);
                if (_currentBlock->Used + alignedSize <= _currentBlock->Size)
                {
                    byte* ptr = _currentBlock->Data + _currentBlock->Used;
                    _currentBlock->Used += alignedSize;
                    _totalUsed += alignedSize;
                    return ptr;
                }
            }

            // 需要新块
            return AllocFromNewBlock(size);
        }

        /// <summary>
        /// 分配并清零内存
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void* AllocAndClear(int size)
        {
            void* ptr = Alloc(size);
            if (ptr != null)
            {
                ZeroMemory(ptr, size);
            }
            return ptr;
        }

        /// <summary>
        /// 分配对齐内存
        /// </summary>
        public void* AllocAligned(int size, int alignment)
        {
            if (size <= 0 || alignment <= 0)
                return null;

            // 分配足够大的空间以容纳对齐调整
            int allocSize = size + alignment + sizeof(void*);
            void* rawPtr = Alloc(allocSize);
            if (rawPtr == null)
                return null;

            // 计算对齐地址
            ulong rawAddr = (ulong)rawPtr;
            ulong alignedAddr = (rawAddr + (ulong)sizeof(void*) + (ulong)alignment - 1UL) & ~((ulong)alignment - 1UL);
            void* alignedPtr = (void*)alignedAddr;

            // 存储原始指针（用于释放）
            void** backPtr = (void**)((byte*)alignedPtr - sizeof(void*));
            *backPtr = rawPtr;

            return alignedPtr;
        }

        /// <summary>
        /// 重新分配内存
        /// </summary>
        public void* Realloc(void* ptr, int oldSize, int newSize)
        {
            if (ptr == null)
                return Alloc(newSize);

            if (newSize <= 0)
            {
                Free(ptr);
                return null;
            }

            // 分配新内存
            void* newPtr = Alloc(newSize);
            if (newPtr == null)
                return null;

            // 复制旧数据
            int copySize = System.Math.Min(oldSize, newSize);
            CopyMemory(newPtr, ptr, copySize);

            return newPtr;
        }

        /// <summary>
        /// 释放内存（当前实现为空，整块释放）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Free(void* ptr)
        {
            // 当前分配器策略：不单独释放，Dispose时统一释放
            // 这是ECS场景的典型优化（帧结束时统一清理）
        }

        /// <summary>
        /// 释放所有内存
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
                return;

            // 释放所有块
            BlockHeader* block = _firstBlock;
            while (block != null)
            {
                BlockHeader* next = block->Next;
                System.Runtime.InteropServices.Marshal.FreeHGlobal((IntPtr)block);
                block = next;
            }

            _firstBlock = null;
            _currentBlock = null;
            _totalAllocated = 0;
            _totalUsed = 0;
            _isDisposed = true;
        }

        #endregion

        #region 统计与调试

        /// <summary>
        /// 获取分配器统计信息
        /// </summary>
        public AllocatorStats GetStats()
        {
            int blockCount = 0;
            BlockHeader* block = _firstBlock;
            while (block != null)
            {
                blockCount++;
                block = block->Next;
            }

            return new AllocatorStats
            {
                TotalAllocated = _totalAllocated,
                TotalUsed = _totalUsed,
                BlockCount = blockCount,
                Utilization = Utilization
            };
        }

        #endregion

        #region 私有方法

        private void* AllocFromNewBlock(int size)
        {
            // 计算块大小（2倍增长策略）
            int blockSize = System.Math.Max(MinBlockSize, size * 2);
            blockSize = System.Math.Min(blockSize, MaxBlockSize);
            blockSize = System.Math.Max(blockSize, size + sizeof(BlockHeader));

            // 分配新块
            BlockHeader* newBlock = (BlockHeader*)System.Runtime.InteropServices.Marshal.AllocHGlobal(blockSize).ToPointer();
            if (newBlock == null)
                return null;

            // 初始化头部
            newBlock->Size = blockSize - sizeof(BlockHeader);
            newBlock->Used = 0;
            newBlock->Prev = null;
            newBlock->Next = null;

            // 链接到链表
            if (_firstBlock == null)
            {
                _firstBlock = newBlock;
            }
            else
            {
                newBlock->Prev = _currentBlock;
                if (_currentBlock != null)
                    _currentBlock->Next = newBlock;
            }
            _currentBlock = newBlock;

            _totalAllocated += newBlock->Size;

            // 从新区块分配
            int alignedSize = AlignSize(size, DefaultAlignment);
            byte* ptr = newBlock->Data;
            newBlock->Used = alignedSize;
            _totalUsed += alignedSize;

            return ptr;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int AlignSize(int size, int alignment)
        {
            return (size + alignment - 1) & ~(alignment - 1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ZeroMemory(void* ptr, int size)
        {
            byte* p = (byte*)ptr;
            for (int i = 0; i < size; i++)
                p[i] = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void CopyMemory(void* dest, void* src, int size)
        {
            byte* d = (byte*)dest;
            byte* s = (byte*)src;
            for (int i = 0; i < size; i++)
                d[i] = s[i];
        }

        #endregion
    }

    /// <summary>
    /// 分配器统计信息
    /// </summary>
    public struct AllocatorStats
    {
        /// <summary>总分配内存（字节）</summary>
        public int TotalAllocated;

        /// <summary>总使用内存（字节）</summary>
        public int TotalUsed;

        /// <summary>内存块数量</summary>
        public int BlockCount;

        /// <summary>内存使用效率（定点数 0-1）</summary>
        public FP Utilization;

        public override string ToString()
        {
            int utilPercent = (int)(Utilization * 100);
            return $"Allocator[Allocated: {TotalAllocated / 1024}KB, Used: {TotalUsed / 1024}KB, " +
                   $"Blocks: {BlockCount}, Util: {utilPercent}%]";
        }
    }
}
