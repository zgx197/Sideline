// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

using System;
using System.Runtime.CompilerServices;
using System.Threading;
using Lattice.Math;

namespace Lattice.ECS.Core.Parallel
{
    /// <summary>
    /// 任务委托 - 多线程执行的回调
    /// </summary>
    public unsafe delegate void JobDelegate(void* userData, int startIndex, int endIndex);

    /// <summary>
    /// 任务句柄 - 用于依赖管理
    /// </summary>
    public readonly struct JobHandle
    {
        public readonly int Id;
        public readonly bool IsValid;

        public static readonly JobHandle Invalid = new(-1);

        public JobHandle(int id)
        {
            Id = id;
            IsValid = id >= 0;
        }
    }

    /// <summary>
    /// 任务配置
    /// </summary>
    public unsafe struct JobConfig
    {
        /// <summary>任务委托</summary>
        public JobDelegate Delegate;
        /// <summary>用户数据指针</summary>
        public void* UserData;
        /// <summary>元素总数（用于自动分片）</summary>
        public int ElementCount;
        /// <summary>最小分片大小</summary>
        public int MinBatchSize;
        /// <summary>依赖的任务</summary>
        public JobHandle Dependency;

        public static JobConfig Create(JobDelegate del, void* userData, int elementCount, int minBatchSize = 64)
        {
            return new JobConfig
            {
                Delegate = del,
                UserData = userData,
                ElementCount = elementCount,
                MinBatchSize = minBatchSize,
                Dependency = JobHandle.Invalid
            };
        }
    }

    /// <summary>
    /// 简单任务系统 - 类似 FrameSync 的 TaskContext
    /// </summary>
    public unsafe class JobSystem : IDisposable
    {
        public readonly int WorkerCount;
        private const int MaxJobs = 1024;

        private readonly Thread[] _workers;
        private readonly JobQueue _queue;
        private volatile bool _isRunning;
        private int _jobIdCounter;
        private JobCompletionState* _completionStates;

        public JobSystem(int workerCount = -1)
        {
            WorkerCount = workerCount > 0 ? workerCount : Environment.ProcessorCount;
            _workers = new Thread[WorkerCount];
            _queue = new JobQueue(MaxJobs);
            _isRunning = true;

            _completionStates = (JobCompletionState*)System.Runtime.InteropServices.Marshal.AllocHGlobal(
                sizeof(JobCompletionState) * MaxJobs).ToPointer();

            for (int i = 0; i < MaxJobs; i++)
                _completionStates[i] = new JobCompletionState();

            for (int i = 0; i < WorkerCount; i++)
            {
                _workers[i] = new Thread(WorkerLoop)
                {
                    Name = $"LatticeJobWorker-{i}",
                    IsBackground = true
                };
                _workers[i].Start(i);
            }
        }

        public JobHandle ScheduleParallel(JobConfig config)
        {
            int jobId = Interlocked.Increment(ref _jobIdCounter) - 1;
            if (jobId >= MaxJobs)
                throw new InvalidOperationException("Job limit exceeded");

            int batchSize = CalculateBatchSize(config.ElementCount, config.MinBatchSize);
            int batchCount = (config.ElementCount + batchSize - 1) / batchSize;

            var handle = new JobHandle(jobId);
            ref var state = ref _completionStates[jobId];
            state.Reset(batchCount);

            for (int i = 0; i < batchCount; i++)
            {
                int start = i * batchSize;
                int end = System.Math.Min(start + batchSize, config.ElementCount);

                var batch = new JobBatch
                {
                    JobId = jobId,
                    Delegate = config.Delegate,
                    UserData = config.UserData,
                    StartIndex = start,
                    EndIndex = end
                };

                _queue.TryEnqueue(batch);
            }

            return handle;
        }

        public void WaitForComplete(JobHandle handle)
        {
            if (!handle.IsValid) return;

            ref var state = ref _completionStates[handle.Id];

            int spinCount = 0;
            while (!state.IsComplete && spinCount < 1000)
            {
                Thread.SpinWait(10);
                spinCount++;
            }

            if (!state.IsComplete)
                state.WaitEvent.WaitOne();
        }

        private void WorkerLoop(object state)
        {
            while (_isRunning)
            {
                if (_queue.TryDequeue(out var batch))
                {
                    batch.Delegate(batch.UserData, batch.StartIndex, batch.EndIndex);

                    ref var completion = ref _completionStates[batch.JobId];
                    if (completion.IncrementCompleted())
                        completion.WaitEvent.Set();
                }
                else
                {
                    Thread.Sleep(1);
                }
            }
        }

        private int CalculateBatchSize(int elementCount, int minBatchSize)
        {
            int targetBatches = WorkerCount * 4;
            return System.Math.Max(minBatchSize, (elementCount + targetBatches - 1) / targetBatches);
        }

        public void Dispose()
        {
            _isRunning = false;
            foreach (var worker in _workers)
                worker?.Join(1000);

            System.Runtime.InteropServices.Marshal.FreeHGlobal((IntPtr)_completionStates);
        }

        #region 内部结构

        private struct JobBatch
        {
            public int JobId;
            public JobDelegate Delegate;
            public void* UserData;
            public int StartIndex;
            public int EndIndex;
        }

        private struct JobCompletionState
        {
            private volatile int _completedCount;
            private volatile int _totalCount;
            public readonly ManualResetEvent WaitEvent;

            public bool IsComplete => _completedCount >= _totalCount && _totalCount > 0;

            public JobCompletionState()
            {
                _completedCount = 0;
                _totalCount = 0;
                WaitEvent = new ManualResetEvent(false);
            }

            public void Reset(int totalCount)
            {
                _completedCount = 0;
                _totalCount = totalCount;
                WaitEvent.Reset();
            }

            public bool IncrementCompleted()
            {
                return Interlocked.Increment(ref _completedCount) >= _totalCount;
            }
        }

        private unsafe class JobQueue
        {
            private readonly JobBatch* _buffer;
            private readonly int _capacity;
            private volatile int _head;
            private volatile int _tail;

            public JobQueue(int capacity)
            {
                _capacity = capacity;
                _buffer = (JobBatch*)System.Runtime.InteropServices.Marshal.AllocHGlobal(
                    sizeof(JobBatch) * capacity).ToPointer();
                _head = 0;
                _tail = 0;
            }

            public bool TryEnqueue(in JobBatch batch)
            {
                int currentTail = _tail;
                int nextTail = (currentTail + 1) % _capacity;
                if (nextTail == _head) return false;

                _buffer[currentTail] = batch;
                _tail = nextTail;
                return true;
            }

            public bool TryDequeue(out JobBatch batch)
            {
                int currentHead = _head;
                if (currentHead == _tail)
                {
                    batch = default;
                    return false;
                }

                batch = _buffer[currentHead];
                _head = (currentHead + 1) % _capacity;
                return true;
            }
        }

        #endregion
    }
}
