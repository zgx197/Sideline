// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

using System;
using Lattice.ECS.Core;
using Lattice.ECS.Framework;
using Lattice.Math;

namespace Lattice.ECS.Session
{
    /// <summary>
    /// 将输入写入帧状态的最小入口。
    /// </summary>
    public delegate void SimulationInputApplier<TInput>(Frame frame, TInput input) where TInput : unmanaged;

    /// <summary>
    /// 最小确定性模拟会话。
    /// </summary>
    public sealed class SimulationSession<TInput> : IDisposable where TInput : unmanaged
    {
        private readonly SimulationSessionOptions _options;
        private readonly SimulationInputApplier<TInput>? _applyInput;
        private readonly TickBuffer _predictedInputs;
        private readonly TickBuffer _verifiedInputs;
        private readonly SystemScheduler _scheduler = new();
        private bool _disposed;
        private Frame? _verifiedFrame;
        private Frame? _predictedFrame;

        public SimulationSession(
            SimulationSessionOptions options,
            SimulationInputApplier<TInput>? applyInput = null)
        {
            ArgumentNullException.ThrowIfNull(options);

            if (options.MaxEntities <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(options.MaxEntities));
            }

            if (options.InputCapacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(options.InputCapacity));
            }

            if (options.CommandBufferCapacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(options.CommandBufferCapacity));
            }

            _options = options;
            _applyInput = applyInput;
            _predictedInputs = new TickBuffer(options.InputCapacity);
            _verifiedInputs = new TickBuffer(options.InputCapacity);
            Commands = new SimulationCommandBufferHost();
        }

        /// <summary>当前系统调度器。</summary>
        public SystemScheduler Scheduler => _scheduler;

        /// <summary>当前 tick 命令缓冲宿主。</summary>
        public SimulationCommandBufferHost Commands { get; }

        /// <summary>当前权威帧。</summary>
        public Frame? VerifiedFrame => _verifiedFrame;

        /// <summary>当前预测帧。</summary>
        public Frame? PredictedFrame => _predictedFrame;

        /// <summary>当前权威 tick。</summary>
        public int VerifiedTick => _verifiedFrame?.Tick ?? _options.InitialTick;

        /// <summary>当前预测 tick。</summary>
        public int PredictedTick => _predictedFrame?.Tick ?? _options.InitialTick;

        /// <summary>会话是否已启动。</summary>
        public bool IsRunning { get; private set; }

        public void Add(ISystem system)
        {
            _scheduler.Add(system);
        }

        public void AddRange(ReadOnlySpan<ISystem> systems)
        {
            for (int i = 0; i < systems.Length; i++)
            {
                _scheduler.Add(systems[i]);
            }
        }

        public void Start(Action<Frame>? bootstrap = null)
        {
            ThrowIfDisposed();

            if (IsRunning)
            {
                throw new InvalidOperationException("SimulationSession 已启动。");
            }

            Frame verified = CreateFrame(_options.InitialTick);
            bootstrap?.Invoke(verified);

            _scheduler.Initialize(verified);

            _verifiedFrame = verified;
            _predictedFrame = verified.Clone();
            IsRunning = true;
        }

        public void SetPredictedInput(int tick, in TInput input)
        {
            ThrowIfDisposed();
            _predictedInputs.Set(tick, input);
        }

        public void SetVerifiedInput(int tick, in TInput input)
        {
            ThrowIfDisposed();
            _verifiedInputs.Set(tick, input);
        }

        public void AdvanceVerifiedTo(int targetTick)
        {
            ThrowIfDisposed();
            EnsureRunning();

            if (targetTick < VerifiedTick)
            {
                throw new InvalidOperationException("目标 verified tick 不能回退。");
            }

            while (_verifiedFrame!.Tick < targetTick)
            {
                int nextTick = _verifiedFrame.Tick + 1;
                TInput input = ResolveVerifiedInput(nextTick);
                Frame next = SimulateNext(_verifiedFrame, nextTick, input);
                ReplaceVerifiedFrame(next);
            }
        }

        public void AdvancePredictedTo(int targetTick)
        {
            ThrowIfDisposed();
            EnsureRunning();

            if (targetTick < VerifiedTick)
            {
                targetTick = VerifiedTick;
            }

            Frame current = _verifiedFrame!.Clone();
            while (current.Tick < targetTick)
            {
                int nextTick = current.Tick + 1;
                TInput input = ResolveBestInput(nextTick);
                Frame next = SimulateNext(current, nextTick, input);
                current.Dispose();
                current = next;
            }

            ReplacePredictedFrame(current);
        }

        public ulong CalculateVerifiedChecksum()
        {
            ThrowIfDisposed();
            EnsureRunning();
            return _verifiedFrame!.CalculateChecksum();
        }

        public ulong CalculatePredictedChecksum()
        {
            ThrowIfDisposed();
            EnsureRunning();
            return _predictedFrame!.CalculateChecksum();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                if (IsRunning && _verifiedFrame != null)
                {
                    _scheduler.Shutdown(_verifiedFrame);
                }
            }
            finally
            {
                DisposeCommandBuffer();

                Frame? verified = _verifiedFrame;
                Frame? predicted = _predictedFrame;

                _verifiedFrame = null;
                _predictedFrame = null;
                IsRunning = false;

                verified?.Dispose();
                if (!ReferenceEquals(predicted, verified))
                {
                    predicted?.Dispose();
                }

                _disposed = true;
            }
        }

        private Frame CreateFrame(int tick)
        {
            return new Frame(_options.MaxEntities)
            {
                Tick = tick,
                DeltaTime = _options.DeltaTime
            };
        }

        private Frame SimulateNext(Frame currentFrame, int nextTick, TInput input)
        {
            Frame next = currentFrame.Clone();
            next.Tick = nextTick;
            next.DeltaTime = _options.DeltaTime;

            PrepareCommandBuffer(next);
            try
            {
                _applyInput?.Invoke(next, input);
                _scheduler.Update(next, _options.DeltaTime);
                Commands.Buffer.Playback(next);
            }
            finally
            {
                Commands.Buffer.Dispose();
            }

            return next;
        }

        private void PrepareCommandBuffer(Frame frame)
        {
            DisposeCommandBuffer();
            Commands.Buffer.Initialize(frame, _options.CommandBufferCapacity);
        }

        private void DisposeCommandBuffer()
        {
            Commands.Buffer.Dispose();
            Commands.Buffer = default;
        }

        private TInput ResolveVerifiedInput(int tick)
        {
            if (_verifiedInputs.TryGet(tick, out TInput input))
            {
                return input;
            }

            throw new InvalidOperationException($"缺少 tick={tick} 的 verified 输入。");
        }

        private TInput ResolveBestInput(int tick)
        {
            if (_verifiedInputs.TryGet(tick, out TInput verified))
            {
                return verified;
            }

            if (_predictedInputs.TryGet(tick, out TInput predicted))
            {
                return predicted;
            }

            return default;
        }

        private void EnsureRunning()
        {
            if (!IsRunning || _verifiedFrame == null || _predictedFrame == null)
            {
                throw new InvalidOperationException("SimulationSession 尚未启动。");
            }
        }

        private void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
        }

        private void ReplaceVerifiedFrame(Frame next)
        {
            Frame? previous = _verifiedFrame;
            _verifiedFrame = next;
            previous?.Dispose();
        }

        private void ReplacePredictedFrame(Frame next)
        {
            Frame? previous = _predictedFrame;
            _predictedFrame = next;
            previous?.Dispose();
        }

        private sealed class TickBuffer
        {
            private readonly int[] _ticks;
            private readonly TInput[] _values;

            public TickBuffer(int capacity)
            {
                _ticks = new int[capacity];
                _values = new TInput[capacity];

                for (int i = 0; i < _ticks.Length; i++)
                {
                    _ticks[i] = int.MinValue;
                }
            }

            public void Set(int tick, TInput value)
            {
                int index = GetSlot(tick);
                _ticks[index] = tick;
                _values[index] = value;
            }

            public bool TryGet(int tick, out TInput value)
            {
                int index = GetSlot(tick);
                if (_ticks[index] == tick)
                {
                    value = _values[index];
                    return true;
                }

                value = default;
                return false;
            }

            private int GetSlot(int tick)
            {
                int index = tick % _ticks.Length;
                return index < 0 ? index + _ticks.Length : index;
            }
        }
    }
}
