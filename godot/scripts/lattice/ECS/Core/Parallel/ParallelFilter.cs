// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

using System;
using System.Runtime.CompilerServices;
using System.Threading;
using Lattice.Core;

namespace Lattice.ECS.Core.Parallel
{
    /// <summary>
    /// 并行查询兼容层。
    /// 为既有并行遍历调用方保留外形，内部按致密索引顺序只读扫描组件。
    /// 
    /// 使用方式：
    /// 1. 将迭代范围分割给多个线程
    /// 2. 每个线程访问不同的 Block，无竞争
    /// 3. 主线程等待所有工作完成
    /// 
    /// 注意：只支持只读操作！写入需要额外同步。
    /// </summary>
    public unsafe class ParallelFilter<T> where T : unmanaged, IComponent
    {
        private readonly Storage<T>* _storage;
        private readonly JobSystem* _jobSystem;

        public ParallelFilter(Storage<T>* storage, JobSystem* jobSystem)
        {
            _storage = storage;
            _jobSystem = jobSystem;
        }

        /// <summary>
        /// 并行遍历所有组件（只读）
        /// </summary>
        public void ForEach(ParallelForDelegate<T> action)
        {
            if (_storage == null || _storage->Count == 0 || action == null) return;

            int totalItems = _storage->Count;

            // 创建上下文
            var context = new ForEachContext<T>
            {
                Storage = _storage,
                Action = action
            };

            // 调度并行任务
            JobDelegate jobDelegate = (void* userData, int startIndex, int endIndex) =>
            {
                var ctx = (ForEachContext<T>*)userData;
                var storage = ctx->Storage;

                for (int i = startIndex; i < endIndex; i++)
                {
                    storage->GetDenseEntryByLinearIndex(i, out EntityRef entity, out T* component);
                    ctx->Action(entity, component);
                }
            };

            var config = JobConfig.Create(jobDelegate, &context, totalItems, 64);
            var handle = _jobSystem->ScheduleParallel(config);
            _jobSystem->WaitForComplete(handle);
        }

        private unsafe struct ForEachContext<T1> where T1 : unmanaged, IComponent
        {
            public Storage<T1>* Storage;
            public ParallelForDelegate<T1> Action;
        }
    }

    /// <summary>
    /// 并行 For 委托
    /// </summary>
    public unsafe delegate void ParallelForDelegate<T>(EntityRef entity, T* component) where T : unmanaged, IComponent;

    /// <summary>
    /// 线程安全的只读 Frame 访问视图。
    /// 新代码可直接在该视图上使用 Query API 构建只读查询。
    /// </summary>
    public unsafe readonly struct FrameReadOnly
    {
        private readonly Frame _frame;

        public FrameReadOnly(Frame frame)
        {
            _frame = frame ?? throw new ArgumentNullException(nameof(frame));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsValid(EntityRef entity)
        {
            return _frame.IsValid(entity);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Has<T>(EntityRef entity) where T : unmanaged, IComponent
        {
            return _frame.Has<T>(entity);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Get<T>(EntityRef entity) where T : unmanaged, IComponent
        {
            return _frame.Get<T>(entity);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T* GetPointer<T>(EntityRef entity) where T : unmanaged, IComponent
        {
            return _frame.GetPointer<T>(entity);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGet<T>(EntityRef entity, out T component) where T : unmanaged, IComponent
        {
            return _frame.TryGet<T>(entity, out component);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Query<T> Query<T>() where T : unmanaged, IComponent
        {
            return _frame.Query<T>();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Query<T1, T2> Query<T1, T2>()
            where T1 : unmanaged, IComponent
            where T2 : unmanaged, IComponent
        {
            return _frame.Query<T1, T2>();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Query<T1, T2, T3> Query<T1, T2, T3>()
            where T1 : unmanaged, IComponent
            where T2 : unmanaged, IComponent
            where T3 : unmanaged, IComponent
        {
            return _frame.Query<T1, T2, T3>();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ComponentBlockIterator<T> GetComponentBlockIterator<T>() where T : unmanaged, IComponent
        {
            return _frame.GetComponentBlockIterator<T>();
        }
    }

    /// <summary>
    /// 原子计数器 - 多线程统计
    /// </summary>
    public unsafe struct AtomicCounter
    {
        private long _value;

        public long Value => Interlocked.Read(ref _value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long Increment()
        {
            return Interlocked.Increment(ref _value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long Add(long amount)
        {
            return Interlocked.Add(ref _value, amount);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Reset()
        {
            Interlocked.Exchange(ref _value, 0);
        }
    }

    /// <summary>
    /// 原子标志数组 - 多线程标记（如裁剪系统）
    /// </summary>
    public unsafe struct AtomicFlagArray
    {
        private long* _bits;
        private int _capacity;

        public void Initialize(byte* buffer, int capacity)
        {
            _capacity = (capacity + 63) & ~63;  // 对齐到 64
            _bits = (long*)buffer;
            Clear();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Set(int index)
        {
            int block = index >> 6;
            int bit = index & 0x3F;
            long mask = 1L << bit;
            Interlocked.Or(ref _bits[block], mask);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear(int index)
        {
            int block = index >> 6;
            int bit = index & 0x3F;
            long mask = ~(1L << bit);
            Interlocked.And(ref _bits[block], mask);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsSet(int index)
        {
            int block = index >> 6;
            int bit = index & 0x3F;
            return (_bits[block] & (1L << bit)) != 0;
        }

        public void Clear()
        {
            int blockCount = _capacity >> 6;
            for (int i = 0; i < blockCount; i++)
                _bits[i] = 0;
        }

        public static int CalculateBufferSize(int capacity)
        {
            return sizeof(long) * ((capacity + 63) & ~63) / 64;
        }
    }
}
