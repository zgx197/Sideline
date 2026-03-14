using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Lattice.Math;

namespace Lattice.Core
{
    /// <summary>
    /// 原生内存池 - 高性能内存管理
    /// 
    /// 特性：
    /// 1. 使用GC.AllocateArray(pinned: true)避免GC移动
    /// 2. 按页分配减少分配开销
    /// 3. 零内存清除（重用时不清零）
    /// </summary>
    public sealed unsafe class NativeMemoryPool : IDisposable
    {
        private readonly int _pageSize;
        private readonly int _elementSize;

        // 空闲链表
        private IntPtr* _freeList;
        private int _freeCount;
        private int _freeCapacity;

        // 已分配页
        private byte[][] _pages = null!;
        private int _pageCount;
        private int _pageCapacity;

        // GCHandles保持数组固定
        private GCHandle[] _handles = null!;

        // 当前页和偏移
        private byte[]? _currentPage;
        private int _currentOffset;
        private GCHandle _currentHandle;
        private byte* _currentPtr;

        // 统计
        private int _allocatedCount;
        private int _peakAllocatedCount;

        public NativeMemoryPool(int elementSize, int pageSize = 64 * 1024) // 默认64KB一页
        {
            if (elementSize <= 0) throw new ArgumentException("Element size must be positive", nameof(elementSize));
            if (pageSize <= 0) throw new ArgumentException("Page size must be positive", nameof(pageSize));

            _elementSize = elementSize;
            _pageSize = pageSize;

            _freeList = (IntPtr*)NativeMemory.Alloc((nuint)(1024 * sizeof(IntPtr)));
            _freeCapacity = 1024;

            // 初始化页数组
            _pages = new byte[4][];
            _handles = new GCHandle[4];
            _pageCapacity = 4;
        }

        /// <summary>
        /// 分配内存
        /// </summary>
        public byte* Alloc()
        {
            // 优先从空闲链表获取
            if (_freeCount > 0)
            {
                var ptr = (byte*)_freeList[--_freeCount];
                _allocatedCount++;
                return ptr;
            }

            // 检查当前页是否有空间
            if (_currentPage == null || _currentOffset + _elementSize > _pageSize)
            {
                AllocateNewPage();
            }

            var result = _currentPtr + _currentOffset;
            _currentOffset += _elementSize;

            _allocatedCount++;
            if (_allocatedCount > _peakAllocatedCount)
            {
                _peakAllocatedCount = _allocatedCount;
            }

            return result;
        }

        /// <summary>
        /// 释放内存（返回到池中）
        /// </summary>
        public void Free(byte* ptr)
        {
            if (ptr == null) return;

            // 扩容空闲链表
            if (_freeCount >= _freeCapacity)
            {
                int newCapacity = _freeCapacity * 2;
                var newList = (IntPtr*)NativeMemory.Alloc((nuint)(newCapacity * sizeof(IntPtr)));
                Buffer.MemoryCopy(_freeList, newList, newCapacity * sizeof(IntPtr), _freeCount * sizeof(IntPtr));
                NativeMemory.Free(_freeList);
                _freeList = newList;
                _freeCapacity = newCapacity;
            }

            _freeList[_freeCount++] = (IntPtr)ptr;
            _allocatedCount--;
        }

        /// <summary>
        /// 分配新页
        /// </summary>
        private void AllocateNewPage()
        {
            // 扩容
            if (_pageCount >= _pageCapacity)
            {
                int newCapacity = _pageCapacity * 2;
                Array.Resize(ref _pages, newCapacity);
                Array.Resize(ref _handles, newCapacity);
                _pageCapacity = newCapacity;
            }

            // 释放当前页的GCHandle
            if (_currentPage != null && _pageCount > 0)
            {
                _handles[_pageCount - 1] = _currentHandle;
            }

            // 分配新页（固定）
            _currentPage = GC.AllocateArray<byte>(_pageSize, pinned: true);
            _currentHandle = GCHandle.Alloc(_currentPage, GCHandleType.Pinned);
            _currentPtr = (byte*)_currentHandle.AddrOfPinnedObject();
            _currentOffset = 0;

            _pages[_pageCount++] = _currentPage;
        }

        /// <summary>
        /// 获取统计信息
        /// </summary>
        public MemoryPoolStats GetStats()
        {
            return new MemoryPoolStats
            {
                PageCount = _pageCount,
                TotalBytes = _pageCount * _pageSize,
                AllocatedCount = _allocatedCount,
                PeakAllocatedCount = _peakAllocatedCount,
                FreeCount = _freeCount,
                ElementSize = _elementSize
            };
        }

        public void Dispose()
        {
            // 释放GCHandles
            if (_handles != null)
            {
                for (int i = 0; i < _pageCount; i++)
                {
                    if (_handles[i].IsAllocated)
                    {
                        _handles[i].Free();
                    }
                }
            }

            // 释放当前页
            if (_currentHandle.IsAllocated)
            {
                _currentHandle.Free();
            }

            // 释放空闲链表
            if (_freeList != null)
            {
                NativeMemory.Free(_freeList);
                _freeList = null;
            }

            _pages = null!;
            _handles = null!;
            _currentPage = null;
            _currentPtr = null;
        }
    }

    /// <summary>
    /// 内存池统计
    /// </summary>
    public readonly struct MemoryPoolStats
    {
        public int PageCount { get; init; }
        public int TotalBytes { get; init; }
        public int AllocatedCount { get; init; }
        public int PeakAllocatedCount { get; init; }
        public int FreeCount { get; init; }
        public int ElementSize { get; init; }

        public FP UtilizationRate => TotalBytes > 0 ? (FP)AllocatedCount * (FP)ElementSize / (FP)TotalBytes : FP.Zero;

        public override string ToString()
        {
            return $"MemoryPool[Pages={PageCount}, Allocated={AllocatedCount}, Free={FreeCount}, " +
                   $"Util={UtilizationRate:P1}, Total={TotalBytes}B]";
        }
    }

    /// <summary>
    /// 数组池包装器 - 提供固定数组
    /// </summary>
    public static class PinnedArrayPool
    {
        /// <summary>
        /// 租用固定数组
        /// </summary>
        public static PinnedArray<T> Rent<T>(int minimumLength) where T : unmanaged
        {
            var array = GC.AllocateArray<T>(minimumLength, pinned: true);
            return new PinnedArray<T>(array);
        }

        /// <summary>
        /// 归还数组
        /// </summary>
        public static void Return<T>(PinnedArray<T> pinnedArray) where T : unmanaged
        {
            pinnedArray.Dispose();
        }
    }

    /// <summary>
    /// 固定数组包装
    /// </summary>
    public readonly unsafe struct PinnedArray<T> : IDisposable where T : unmanaged
    {
        private readonly T[] _array;
        private readonly GCHandle _handle;
        private readonly T* _ptr;

        public PinnedArray(T[] array)
        {
            _array = array;
            _handle = GCHandle.Alloc(array, GCHandleType.Pinned);
            _ptr = (T*)_handle.AddrOfPinnedObject();
        }

        public int Length => _array.Length;
        public T[] Array => _array;
        public T* Pointer => _ptr;

        public ref T this[int index] => ref _array[index];

        public void Dispose()
        {
            if (_handle.IsAllocated)
            {
                _handle.Free();
            }
        }
    }
}
