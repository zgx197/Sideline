// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Lattice.Math;

namespace Lattice.ECS.Core
{
    /// <summary>
    /// 高性能非托管 Arena 分配器。
    ///
    /// 设计目标：
    /// 1. 完全驻留于非托管内存，可安全通过 <c>Allocator*</c> 传递；
    /// 2. 采用线性追加分配，避免 ECS 热路径中的碎片化与单次释放开销；
    /// 3. 为组件块、位图、命令缓冲等场景提供稳定的对齐分配能力。
    /// </summary>
    public unsafe struct Allocator
    {
        #region 常量

        /// <summary>默认对齐边界（16 字节，兼顾 SIMD 与常见平台 ABI）。</summary>
        public const int DefaultAlignment = 16;

        /// <summary>最小分配块大小（4KB）。</summary>
        private const int MinBlockSize = 4 * 1024;

        /// <summary>最大常规分配块大小（1MB）。超出时按需扩展。</summary>
        private const int MaxBlockSize = 1024 * 1024;

        #endregion

        #region 内存块

        private struct BlockHeader
        {
            public nuint Size;
            public nuint Used;
            public BlockHeader* Prev;
            public BlockHeader* Next;
        }

        #endregion

        #region 字段

        private BlockHeader* _firstBlock;
        private BlockHeader* _currentBlock;
        private nuint _totalAllocated;
        private nuint _totalUsed;
        private bool _isDisposed;

        #endregion

        #region 属性

        /// <summary>总分配内存（字节）。</summary>
        public int TotalAllocated => checked((int)_totalAllocated);

        /// <summary>总使用内存（字节）。</summary>
        public int TotalUsed => checked((int)_totalUsed);

        /// <summary>内存使用效率（定点数 0-1）。</summary>
        public FP Utilization => _totalAllocated > 0
            ? FP.FromRaw((long)_totalUsed * FP.One.RawValue / (long)_totalAllocated)
            : FP.Zero;

        #endregion

        #region 生命周期

        /// <summary>
        /// 创建一个驻留在非托管内存中的分配器实例。
        /// </summary>
        public static Allocator* Create()
        {
            return (Allocator*)NativeMemory.AllocZeroed((nuint)sizeof(Allocator));
        }

        /// <summary>
        /// 销毁非托管分配器实例及其持有的所有块。
        /// </summary>
        public static void Destroy(Allocator* allocator)
        {
            if (allocator == null)
            {
                return;
            }

            allocator->Dispose();
            NativeMemory.Free(allocator);
        }

        /// <summary>
        /// 释放所有 Arena 块。
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            BlockHeader* block = _firstBlock;
            while (block != null)
            {
                BlockHeader* next = block->Next;
                NativeMemory.Free(block);
                block = next;
            }

            _firstBlock = null;
            _currentBlock = null;
            _totalAllocated = 0;
            _totalUsed = 0;
            _isDisposed = true;
        }

        #endregion

        #region 分配方法

        /// <summary>
        /// 分配默认对齐的非托管内存。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void* Alloc(int size)
        {
            return AllocAligned(size, DefaultAlignment);
        }

        /// <summary>
        /// 分配并清零非托管内存。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void* AllocAndClear(int size)
        {
            void* ptr = Alloc(size);
            if (ptr != null)
            {
                Unsafe.InitBlockUnaligned(ptr, 0, checked((uint)size));
            }

            return ptr;
        }

        /// <summary>
        /// 分配指定对齐的非托管内存。
        /// </summary>
        public void* AllocAligned(int size, int alignment)
        {
            if (size <= 0)
            {
                return null;
            }

            int normalizedAlignment = NormalizeAlignment(alignment);

            if (_currentBlock != null &&
                TryAllocFromBlock(_currentBlock, size, normalizedAlignment, out void* currentPtr))
            {
                return currentPtr;
            }

            BlockHeader* newBlock = AllocateBlock(size, normalizedAlignment);
            _currentBlock = newBlock;

            if (_firstBlock == null)
            {
                _firstBlock = newBlock;
            }

            return TryAllocFromBlock(newBlock, size, normalizedAlignment, out void* allocated)
                ? allocated
                : null;
        }

        /// <summary>
        /// 重新分配内存。Arena 不支持原地扩容，因此会申请新区域并复制旧内容。
        /// </summary>
        public void* Realloc(void* ptr, int oldSize, int newSize)
        {
            if (ptr == null)
            {
                return Alloc(newSize);
            }

            if (newSize <= 0)
            {
                Free(ptr);
                return null;
            }

            void* newPtr = Alloc(newSize);
            if (newPtr == null)
            {
                return null;
            }

            int copySize = System.Math.Min(oldSize, newSize);
            Buffer.MemoryCopy(ptr, newPtr, newSize, copySize);
            return newPtr;
        }

        /// <summary>
        /// 单个块不支持提前释放，统一由 <see cref="Dispose()" /> 回收。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Free(void* ptr)
        {
        }

        #endregion

        #region 统计与调试

        /// <summary>
        /// 获取当前分配器统计信息。
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
                TotalAllocated = TotalAllocated,
                TotalUsed = TotalUsed,
                BlockCount = blockCount,
                Utilization = Utilization
            };
        }

        #endregion

        #region 私有辅助

        private static int NormalizeAlignment(int alignment)
        {
            int normalized = alignment <= 0 ? DefaultAlignment : alignment;
            normalized = System.Math.Max(normalized, sizeof(nuint));

            if ((normalized & (normalized - 1)) != 0)
            {
                int value = 1;
                while (value < normalized)
                {
                    value <<= 1;
                }

                normalized = value;
            }

            return normalized;
        }

        private static byte* GetBlockData(BlockHeader* block)
        {
            return (byte*)block + sizeof(BlockHeader);
        }

        private bool TryAllocFromBlock(BlockHeader* block, int size, int alignment, out void* ptr)
        {
            byte* raw = GetBlockData(block) + block->Used;
            nuint aligned = AlignUp((nuint)raw, (nuint)alignment);
            nuint padding = aligned - (nuint)raw;
            nuint totalSize = padding + (nuint)size;

            if (block->Used + totalSize > block->Size)
            {
                ptr = null;
                return false;
            }

            block->Used += totalSize;
            _totalUsed += totalSize;
            ptr = (void*)aligned;
            return true;
        }

        private BlockHeader* AllocateBlock(int requestedSize, int alignment)
        {
            int minRequired = checked(requestedSize + alignment);
            int blockSize = System.Math.Max(MinBlockSize, minRequired);

            if (_currentBlock != null)
            {
                int doubled = checked((int)System.Math.Min((long)_currentBlock->Size * 2L, MaxBlockSize));
                blockSize = System.Math.Max(blockSize, doubled);
            }

            if (blockSize > MaxBlockSize && minRequired <= MaxBlockSize)
            {
                blockSize = MaxBlockSize;
            }

            nuint allocationSize = (nuint)(sizeof(BlockHeader) + blockSize);
            BlockHeader* newBlock = (BlockHeader*)NativeMemory.AllocZeroed(allocationSize);

            newBlock->Size = (nuint)blockSize;
            newBlock->Used = 0;
            newBlock->Prev = _currentBlock;
            newBlock->Next = null;

            if (_currentBlock != null)
            {
                _currentBlock->Next = newBlock;
            }

            _totalAllocated += (nuint)blockSize;
            return newBlock;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static nuint AlignUp(nuint value, nuint alignment)
        {
            return (value + alignment - 1) & ~(alignment - 1);
        }

        #endregion
    }

    /// <summary>
    /// 分配器统计信息。
    /// </summary>
    public struct AllocatorStats
    {
        /// <summary>总分配内存（字节）。</summary>
        public int TotalAllocated;

        /// <summary>总使用内存（字节）。</summary>
        public int TotalUsed;

        /// <summary>内存块数量。</summary>
        public int BlockCount;

        /// <summary>内存使用效率（定点数 0-1）。</summary>
        public FP Utilization;

        public override string ToString()
        {
            int utilPercent = (int)(Utilization * 100);
            return $"Allocator[Allocated: {TotalAllocated / 1024}KB, Used: {TotalUsed / 1024}KB, " +
                   $"Blocks: {BlockCount}, Util: {utilPercent}%]";
        }
    }
}
