using System;
using System.Collections.Generic;
using System.Reflection;
using Lattice.Core;
using Lattice.ECS.Core;
using Lattice.ECS.Framework;
using Lattice.Math;

namespace Lattice.ECS.Session
{
    /// <summary>
    /// ECS 会话管理器。
    /// 当前主干中的 Session 属于“纯 C#、单线程、固定顺序、可回滚”的最小模拟运行时，
    /// 既承载本地预测推进，也承载最小验证/回滚能力，但并不是完整网络会话产品层。
    /// </summary>
    public class Session : IDisposable
    {
        #region 配置

        /// <summary>最大预测帧数（超过需要回滚）</summary>
        public const int MaxPredictionFrames = 8;

        /// <summary>历史帧保留数量</summary>
        public const int HistorySize = 128;  // 约 2 秒 @ 60fps

        /// <summary>保留为活帧的历史数量。</summary>
        public const int LiveHistorySize = MaxPredictionFrames + 4;

        /// <summary>历史快照采样间隔。</summary>
        public const int HistorySnapshotInterval = 4;

        private const int RecycledFrameLimit = LiveHistorySize + 4;

        #endregion

        #region 字段

        /// <summary>运行时配置。</summary>
        public SessionRuntimeOptions RuntimeOptions { get; }

        /// <summary>固定时间步长</summary>
        public FP DeltaTime { get; }

        /// <summary>本地玩家 ID</summary>
        public int LocalPlayerId { get; protected set; }

        /// <summary>当前帧号（Verified 帧号）</summary>
        public int CurrentTick { get; protected set; }

        /// <summary>最新验证帧（服务器确认）</summary>
        public Frame? VerifiedFrame { get; protected set; }

        /// <summary>预测帧（本地模拟）</summary>
        public Frame? PredictedFrame { get; protected set; }

        /// <summary>上一帧（用于插值）</summary>
        public Frame? PreviousFrame { get; protected set; }

        /// <summary>按 Tick 索引的历史帧。</summary>
        protected readonly Dictionary<int, Frame> _historyByTick;

        /// <summary>历史帧的插入顺序。</summary>
        private readonly Queue<int> _historyOrder;

        /// <summary>采样保留的历史快照。</summary>
        private readonly Dictionary<int, PackedFrameSnapshot> _historySnapshotsByTick;

        /// <summary>按 Tick 升序维护的历史快照索引。</summary>
        private readonly List<int> _historySnapshotTicks;

        /// <summary>回收的帧实例，用于降低热路径分配成本。</summary>
        private readonly Stack<Frame> _recycledFrames;

        /// <summary>按需重建历史帧时复用的临时帧。</summary>
        private Frame? _historicalScratchFrame;

        /// <summary>小范围 materialize cache，避免同一片历史窗口反复从 snapshot 冷启动。</summary>
        private readonly Dictionary<int, Frame> _materializedHistoryByTick;

        /// <summary>materialize cache 的 LRU 顺序。</summary>
        private readonly List<int> _materializedHistoryOrder;

        private int _highestHistoryTick = -1;
        private const int MaterializedHistoryCacheSize = 4;
        private readonly bool _canReplayHistoricalFramesInPlace;

        /// <summary>是否正在回滚中</summary>
        public bool IsRollingBack { get; protected set; }

        /// <summary>是否已启动</summary>
        public bool IsRunning { get; protected set; }

        /// <summary>系统调度器</summary>
        protected readonly SystemScheduler _systemScheduler = new();

        /// <summary>输入缓冲区</summary>
        protected readonly Dictionary<int, InputBuffer> _inputBuffers = new();

        private bool _disposed;

        #endregion

        #region 事件

        /// <summary>帧更新事件</summary>
        public event Action<Frame, FP>? OnFrameUpdate;

        /// <summary>发生回滚事件</summary>
        public event Action<int, int>? OnRollback;  // fromTick, toTick

        /// <summary>帧验证事件</summary>
        public event Action<int, bool>? OnFrameVerified;  // tick, success

        #endregion

        #region 构造函数

        public Session(FP deltaTime, int localPlayerId = 0)
            : this(new SessionRuntimeOptions(deltaTime, localPlayerId))
        {
        }

        public Session(SessionRuntimeOptions runtimeOptions)
        {
            RuntimeOptions = runtimeOptions ?? throw new ArgumentNullException(nameof(runtimeOptions));
            DeltaTime = runtimeOptions.DeltaTime;
            LocalPlayerId = runtimeOptions.LocalPlayerId;
            _historyByTick = new Dictionary<int, Frame>(HistorySize);
            _historyOrder = new Queue<int>(HistorySize);
            _historySnapshotsByTick = new Dictionary<int, PackedFrameSnapshot>(HistorySize / HistorySnapshotInterval + 2);
            _historySnapshotTicks = new List<int>(HistorySize / HistorySnapshotInterval + 2);
            _recycledFrames = new Stack<Frame>(RecycledFrameLimit);
            _materializedHistoryByTick = new Dictionary<int, Frame>(MaterializedHistoryCacheSize);
            _materializedHistoryOrder = new List<int>(MaterializedHistoryCacheSize);
            _canReplayHistoricalFramesInPlace = CanReplayHistoricalFramesInPlace();

            CurrentTick = 0;
        }

        #endregion

        #region 生命周期

        /// <summary>
        /// 启动会话。
        /// 该方法是幂等的；若会话已在运行，则直接返回。
        /// </summary>
        public virtual void Start()
        {
            ThrowIfDisposed();

            if (IsRunning) return;

            ResetFrameState(disposeFrames: true);

            // 先在权威初始帧上做系统初始化，再克隆出预测帧，避免回滚基线缺失初始化状态
            SetVerifiedFrame(CreateFrame(0));
            _systemScheduler.Initialize(VerifiedFrame!);
            SetPredictedFrame(VerifiedFrame!.CloneState());
            SetPreviousFrame(null);
            CurrentTick = 0;

            AddHistoryFrame(VerifiedFrame!);

            IsRunning = true;
        }

        /// <summary>
        /// 停止会话。
        /// 该方法是幂等的；若会话未运行，则直接返回。
        /// </summary>
        public virtual void Stop()
        {
            ThrowIfDisposed();

            if (!IsRunning) return;

            // 销毁系统
            if (PredictedFrame != null)
            {
                _systemScheduler.Shutdown(PredictedFrame);
            }

            IsRunning = false;
        }

        /// <summary>
        /// 单步更新（FixedUpdate）。
        /// 这是当前最小本地推进入口：基于已知输入推进 `PredictedFrame`，并写入历史。
        /// 只能在会话运行期间调用。
        /// </summary>
        public virtual void Update()
        {
            ThrowIfDisposed();
            EnsureRunning(nameof(Update));

            if (PredictedFrame == null)
            {
                throw new InvalidOperationException("PredictedFrame is not available while session is running.");
            }

            // 保存上一帧
            SetPreviousFrame(PredictedFrame);

            // 创建新帧或复用
            SetPredictedFrame(AdvanceFrame(PredictedFrame!));
            CurrentTick = PredictedFrame.Tick;

            // 应用输入
            ApplyInputs(PredictedFrame!);

            // 执行系统更新
            UpdateSystems(PredictedFrame!, DeltaTime);

            // 触发事件
            OnFrameUpdate?.Invoke(PredictedFrame!, DeltaTime);

            // 保存到历史
            UpdateHistory(PredictedFrame!);
        }

        #endregion

        #region 系统管理

        /// <summary>
        /// 注册系统。
        /// 运行中的会话不允许动态修改系统集合，避免 Verified / Predicted / History 之间出现不一致。
        /// </summary>
        public void RegisterSystem(ISystem system)
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(system);

            if (IsRunning)
            {
                throw new InvalidOperationException("Cannot register systems while session is running. Stop the session first.");
            }

            _systemScheduler.Add(system);
        }

        /// <summary>
        /// 注销系统。
        /// 运行中的会话不允许动态修改系统集合，避免 Verified / Predicted / History 之间出现不一致。
        /// </summary>
        public void UnregisterSystem(ISystem system)
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(system);

            if (IsRunning)
            {
                throw new InvalidOperationException("Cannot unregister systems while session is running. Stop the session first.");
            }

            _systemScheduler.Remove(system);
        }

        /// <summary>
        /// 执行系统更新
        /// </summary>
        protected virtual void UpdateSystems(Frame frame, FP deltaTime)
        {
            _systemScheduler.Update(frame, deltaTime);
        }

        #endregion

        #region 输入管理

        /// <summary>
        /// 设置玩家输入
        /// </summary>
        public virtual void SetPlayerInput(int playerId, int tick, IInputCommand input)
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(input);

            if (!_inputBuffers.TryGetValue(playerId, out var buffer))
            {
                buffer = new InputBuffer(HistorySize);
                _inputBuffers[playerId] = buffer;
            }

            buffer.SetInput(tick, input);
        }

        /// <summary>
        /// 获取玩家输入
        /// </summary>
        public IInputCommand? GetPlayerInput(int playerId, int tick)
        {
            ThrowIfDisposed();

            if (!_inputBuffers.TryGetValue(playerId, out var buffer))
                return null;

            return buffer.GetInput(tick);
        }

        /// <summary>
        /// 应用输入到帧
        /// </summary>
        protected virtual void ApplyInputs(Frame frame)
        {
            // 子类实现：将输入应用到游戏逻辑
        }

        #endregion

        #region 帧同步（验证/回滚）

        /// <summary>
        /// 验证帧（服务器确认）。
        /// 该接口属于“预测/验证”语义：用于比较某个历史 Tick 的校验和，
        /// 不匹配时触发回滚与重模拟；它不是本地单机玩法推进所必需的入口。
        /// 只能在会话运行期间调用。
        /// </summary>
        public virtual void VerifyFrame(int tick, long expectedChecksum, byte[]? inputData = null)
        {
            ThrowIfDisposed();
            EnsureRunning(nameof(VerifyFrame));

            // 获取对应历史帧
            var frame = GetHistoricalFrame(tick);
            if (frame == null)
            {
                // 帧已不在历史中，无法验证
                OnFrameVerified?.Invoke(tick, false);
                return;
            }

            long actualChecksum = (long)frame.CalculateChecksum();
            bool success = actualChecksum == expectedChecksum;

            if (success)
            {
                // 验证通过
                SetVerifiedFrame(frame.CloneState());
                OnFrameVerified?.Invoke(tick, true);
            }
            else
            {
                // 校验和不匹配，需要回滚
                OnFrameVerified?.Invoke(tick, false);
                RollbackTo(tick, inputData);
            }
        }

        /// <summary>
        /// 回滚到指定帧。
        /// 该接口属于“预测修正”语义：它会把 `PredictedFrame` 回退到历史基线后再重模拟，
        /// 主要服务于校验失败后的状态修正，而不是普通本地 rewind 玩法。
        /// 只能在会话运行期间调用；该操作只回退帧状态，不重新初始化系统。
        /// </summary>
        public virtual void RollbackTo(int tick, byte[]? correctedInput = null)
        {
            ThrowIfDisposed();
            EnsureRunning(nameof(RollbackTo));

            int targetTick = CurrentTick;
            IsRollingBack = true;
            OnRollback?.Invoke(CurrentTick, tick);

            try
            {
                // 获取历史帧
                var baseFrame = GetHistoricalFrame(tick);
                if (baseFrame == null)
                {
                    throw new InvalidOperationException($"Frame {tick} not found in history");
                }

                // 克隆基础帧作为新的预测起点
                SetPredictedFrame(baseFrame.CloneState());
                SetPreviousFrame(null);
                CurrentTick = tick;

                // 如果有修正的输入，应用它
                if (correctedInput != null)
                {
                    ApplyCorrectedInput(PredictedFrame!, correctedInput);
                }

                UpdateHistory(PredictedFrame!);

                // 重新模拟到回滚前的目标时间
                if (targetTick > tick)
                {
                    Resimulate(tick, targetTick);
                }
            }
            finally
            {
                IsRollingBack = false;
            }
        }

        /// <summary>
        /// 重新模拟帧
        /// </summary>
        protected virtual void Resimulate(int fromTick, int toTick)
        {
            for (int tick = fromTick; tick < toTick; tick++)
            {
                SetPreviousFrame(PredictedFrame);
                SetPredictedFrame(AdvanceFrame(PredictedFrame!));
                CurrentTick = PredictedFrame!.Tick;

                // 应用该帧的输入（可能是预测输入）
                ApplyInputs(PredictedFrame!);

                // 执行系统
                UpdateSystems(PredictedFrame!, DeltaTime);

                // 更新历史（替换原有预测帧）
                UpdateHistory(PredictedFrame!);
            }
        }

        #endregion

        #region 历史帧管理

        /// <summary>
        /// 获取历史帧
        /// </summary>
        public Frame? GetHistoricalFrame(int tick)
        {
            ThrowIfDisposed();

            if (_historyByTick.TryGetValue(tick, out Frame? frame))
            {
                return frame;
            }

            if (_materializedHistoryByTick.TryGetValue(tick, out Frame? cachedFrame))
            {
                TouchMaterializedHistoryTick(tick);
                return cachedFrame;
            }

            if (_historicalScratchFrame != null && _historicalScratchFrame.Tick == tick)
            {
                return _historicalScratchFrame;
            }

            if (TryMaterializeHistoricalFrame(tick, out Frame? materialized))
            {
                return materialized;
            }

            return null;
        }

        /// <summary>
        /// 更新历史中的帧
        /// </summary>
        protected void UpdateHistory(Frame frame)
        {
            InvalidateHistoricalMaterializeCache();
            RemoveHistorySnapshot(frame.Tick);

            if (_historyByTick.TryGetValue(frame.Tick, out Frame? existing))
            {
                if (!ReferenceEquals(existing, frame))
                {
                    _historyByTick[frame.Tick] = frame;
                    DisposeFrameIfDetached(existing);
                }

                return;
            }

            // 不存在则添加
            AddHistoryFrame(frame);
        }

        #endregion

        #region 时光倒流（单机玩法）

        /// <summary>
        /// 回退指定帧数（单机时光倒流）。
        /// 该接口更偏“本地玩法语义”：直接恢复历史帧，不做额外重模拟，
        /// 与 `RollbackTo()` 的预测修正语义不同。
        /// 只能在会话运行期间调用。
        /// </summary>
        public void Rewind(int frameCount)
        {
            ThrowIfDisposed();
            EnsureRunning(nameof(Rewind));

            int targetTick = System.Math.Max(0, CurrentTick - frameCount);

            // 直接获取历史帧，不重新模拟（与网络回滚不同）
            var baseFrame = GetHistoricalFrame(targetTick);
            if (baseFrame != null)
            {
                SetPredictedFrame(baseFrame.CloneState());
                SetPreviousFrame(null);
                CurrentTick = targetTick;
            }
        }

        /// <summary>
        /// 创建检查点。
        /// 该接口更偏“本地工具/运行时管理”语义：保存当前 Verified / Predicted 帧状态，
        /// 便于显式恢复、热重载或外层玩法工具使用，不负责系统集合装配。
        /// 需要当前已经存在有效帧状态。
        /// </summary>
        public SessionCheckpoint CreateCheckpoint()
        {
            ThrowIfDisposed();
            EnsureFrameState(nameof(CreateCheckpoint));

            return new SessionCheckpoint
            {
                Tick = CurrentTick,
                VerifiedSnapshot = VerifiedFrame?.CapturePackedSnapshot(ComponentSerializationMode.Checkpoint),
                PredictedSnapshot = PredictedFrame?.CapturePackedSnapshot(ComponentSerializationMode.Checkpoint)
            };
        }

        /// <summary>
        /// 从检查点恢复。
        /// 该接口更偏“本地工具/运行时管理”语义：只恢复帧状态，
        /// 不重建系统集合，也不切换 Session 的运行模式。
        /// 该操作只恢复帧状态，不会重建系统集合，也不会重新初始化系统。
        /// </summary>
        public void RestoreFromCheckpoint(SessionCheckpoint checkpoint)
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(checkpoint);

            ResetFrameState(disposeFrames: true);

            CurrentTick = checkpoint.Tick;
            SetVerifiedFrame(checkpoint.VerifiedSnapshot != null
                ? RestoreFrame(checkpoint.VerifiedSnapshot, ComponentSerializationMode.Checkpoint)
                : null);
            SetPredictedFrame(checkpoint.PredictedSnapshot != null
                ? RestoreFrame(checkpoint.PredictedSnapshot, ComponentSerializationMode.Checkpoint)
                : null);
            SetPreviousFrame(null);

            if (VerifiedFrame != null) AddHistoryFrame(VerifiedFrame);
            if (PredictedFrame != null) UpdateHistory(PredictedFrame);
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 创建新帧
        /// </summary>
        protected virtual Frame CreateFrame(int tick)
        {
            var frame = new Frame();
            frame.Tick = tick;
            frame.DeltaTime = DeltaTime;
            return frame;
        }

        /// <summary>
        /// 推进到下一帧
        /// </summary>
        protected virtual Frame AdvanceFrame(Frame currentFrame)
        {
            Frame nextFrame = CopyFrameState(currentFrame);
            nextFrame.Tick = currentFrame.Tick + 1;
            nextFrame.DeltaTime = DeltaTime;
            return nextFrame;
        }

        /// <summary>
        /// 应用修正的输入
        /// </summary>
        protected virtual void ApplyCorrectedInput(Frame frame, byte[] inputData)
        {
            // 子类实现
        }

        private void AddHistoryFrame(Frame frame)
        {
            if (_historyByTick.ContainsKey(frame.Tick))
            {
                UpdateHistory(frame);
                return;
            }

            _historyByTick.Add(frame.Tick, frame);
            _historyOrder.Enqueue(frame.Tick);
            _highestHistoryTick = System.Math.Max(_highestHistoryTick, frame.Tick);

            TrimExpiredHistory();
            DemoteOldLiveHistory();
        }

        private void ResetFrameState(bool disposeFrames)
        {
            if (disposeFrames)
            {
                var frames = new HashSet<Frame>();

                if (VerifiedFrame != null) frames.Add(VerifiedFrame);
                if (PredictedFrame != null) frames.Add(PredictedFrame);
                if (PreviousFrame != null) frames.Add(PreviousFrame);
                if (_historicalScratchFrame != null) frames.Add(_historicalScratchFrame);
                foreach (Frame materializedFrame in _materializedHistoryByTick.Values)
                {
                    frames.Add(materializedFrame);
                }

                foreach (Frame historicalFrame in _historyByTick.Values)
                {
                    frames.Add(historicalFrame);
                }

                foreach (Frame recycledFrame in _recycledFrames)
                {
                    frames.Add(recycledFrame);
                }

                foreach (Frame frame in frames)
                {
                    frame.Dispose();
                }
            }

            VerifiedFrame = null;
            PredictedFrame = null;
            PreviousFrame = null;
            _historicalScratchFrame = null;
            CurrentTick = 0;
            IsRollingBack = false;
            _highestHistoryTick = -1;
            _historyByTick.Clear();
            _historyOrder.Clear();
            _historySnapshotsByTick.Clear();
            _historySnapshotTicks.Clear();
            _materializedHistoryByTick.Clear();
            _materializedHistoryOrder.Clear();
            _recycledFrames.Clear();
        }

        #endregion

        #region IDisposable

        public virtual void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            Stop();
            ResetFrameState(disposeFrames: true);
            _inputBuffers.Clear();
            _disposed = true;
        }

        #endregion

        private void EnsureRunning(string methodName)
        {
            if (!IsRunning)
            {
                throw new InvalidOperationException($"{methodName} requires the session to be running.");
            }
        }

        private void EnsureFrameState(string methodName)
        {
            if (VerifiedFrame == null || PredictedFrame == null)
            {
                throw new InvalidOperationException($"{methodName} requires the session to have initialized frame state.");
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().Name);
            }
        }

        private void SetVerifiedFrame(Frame? frame)
        {
            Frame? previous = VerifiedFrame;
            VerifiedFrame = frame;
            DisposeFrameIfDetached(previous);
        }

        private void SetPredictedFrame(Frame? frame)
        {
            Frame? previous = PredictedFrame;
            PredictedFrame = frame;
            DisposeFrameIfDetached(previous);
        }

        private void SetPreviousFrame(Frame? frame)
        {
            Frame? previous = PreviousFrame;
            PreviousFrame = frame;
            DisposeFrameIfDetached(previous);
        }

        private void DisposeFrameIfDetached(Frame? frame)
        {
            if (frame == null)
            {
                return;
            }

            if (ReferenceEquals(frame, VerifiedFrame) ||
                ReferenceEquals(frame, PredictedFrame) ||
                ReferenceEquals(frame, PreviousFrame) ||
                ReferenceEquals(frame, _historicalScratchFrame))
            {
                return;
            }

            if (_materializedHistoryByTick.TryGetValue(frame.Tick, out Frame? materializedFrame) &&
                ReferenceEquals(frame, materializedFrame))
            {
                return;
            }

            if (_historyByTick.TryGetValue(frame.Tick, out Frame? historicalFrame) &&
                ReferenceEquals(frame, historicalFrame))
            {
                return;
            }

            RecycleFrame(frame);
        }

        private bool TryMaterializeHistoricalFrame(int tick, out Frame? frame)
        {
            frame = null;

            if (tick < 0 || _highestHistoryTick < 0)
            {
                return false;
            }

            if (tick > _highestHistoryTick)
            {
                return false;
            }

            int minTick = _highestHistoryTick - HistorySize + 1;
            if (tick < minTick)
            {
                return false;
            }

            if (_historicalScratchFrame != null && _historicalScratchFrame.Tick == tick)
            {
                frame = _historicalScratchFrame;
                return true;
            }

            if (_materializedHistoryByTick.TryGetValue(tick, out Frame? cachedFrame))
            {
                TouchMaterializedHistoryTick(tick);
                SetHistoricalScratchFrame(cachedFrame);
                frame = cachedFrame;
                return true;
            }

            int baseTick = -1;
            Frame? baseFrame = null;
            foreach ((int historicalTick, Frame historicalFrame) in _historyByTick)
            {
                if (historicalTick <= tick && historicalTick > baseTick)
                {
                    baseTick = historicalTick;
                    baseFrame = historicalFrame;
                }
            }

            if (TryFindClosestMaterializedTick(tick, out int materializedTick) &&
                _materializedHistoryByTick.TryGetValue(materializedTick, out Frame? materializedBase) &&
                materializedTick > baseTick)
            {
                baseTick = materializedTick;
                baseFrame = materializedBase;
                TouchMaterializedHistoryTick(materializedTick);
            }

            if (_historicalScratchFrame != null &&
                _historicalScratchFrame.Tick <= tick &&
                _historicalScratchFrame.Tick > baseTick)
            {
                baseTick = _historicalScratchFrame.Tick;
                baseFrame = _historicalScratchFrame;
            }

            Frame workingFrame;
            if (baseFrame != null)
            {
                workingFrame = CopyFrameState(baseFrame);
            }
            else if (TryFindClosestSnapshotTick(tick, out int snapshotTick))
            {
                baseTick = snapshotTick;
                workingFrame = RestoreFrame(_historySnapshotsByTick[snapshotTick], ComponentSerializationMode.Prediction);
            }
            else
            {
                return false;
            }

            if (_canReplayHistoricalFramesInPlace)
            {
                ReplayHistoricalTicksInPlace(workingFrame, baseTick, tick);
            }
            else
            {
                for (int currentTick = baseTick; currentTick < tick; currentTick++)
                {
                    Frame nextFrame = AdvanceFrame(workingFrame);
                    RecycleFrame(workingFrame);
                    workingFrame = nextFrame;
                    ApplyInputs(workingFrame);
                    UpdateSystems(workingFrame, DeltaTime);
                }
            }

            StoreMaterializedHistoryFrame(workingFrame);
            SetHistoricalScratchFrame(workingFrame);
            frame = _historicalScratchFrame;
            return true;
        }

        private bool TryFindClosestSnapshotTick(int tick, out int snapshotTick)
        {
            snapshotTick = -1;
            if (_historySnapshotTicks.Count == 0)
            {
                return false;
            }

            int index = _historySnapshotTicks.BinarySearch(tick);
            if (index < 0)
            {
                index = ~index - 1;
            }

            if ((uint)index >= (uint)_historySnapshotTicks.Count)
            {
                return false;
            }

            snapshotTick = _historySnapshotTicks[index];
            return snapshotTick >= 0;
        }

        private void TrimExpiredHistory()
        {
            int minTick = _highestHistoryTick - HistorySize + 1;

            while (_historyOrder.Count > 0 && _historyOrder.Peek() < minTick)
            {
                int expiredTick = _historyOrder.Dequeue();
                if (_historyByTick.Remove(expiredTick, out Frame? expiredFrame))
                {
                    RemoveHistorySnapshot(expiredTick);
                    DisposeFrameIfDetached(expiredFrame);
                }
            }

            TrimExpiredSnapshots(minTick);
        }

        private void DemoteOldLiveHistory()
        {
            while (_historyByTick.Count > LiveHistorySize && _historyOrder.Count > 0)
            {
                int demotedTick = _historyOrder.Dequeue();
                if (!_historyByTick.Remove(demotedTick, out Frame? demotedFrame))
                {
                    continue;
                }

                CaptureHistorySnapshotIfNeeded(demotedTick, demotedFrame);
                DisposeFrameIfDetached(demotedFrame);
            }
        }

        private void CaptureHistorySnapshotIfNeeded(int tick, Frame frame)
        {
            if (tick != 0 && (tick % HistorySnapshotInterval) != 0)
            {
                return;
            }

            bool isNewSnapshot = !_historySnapshotsByTick.ContainsKey(tick);
            _historySnapshotsByTick[tick] = frame.CapturePackedSnapshot(ComponentSerializationMode.Prediction);
            if (isNewSnapshot)
            {
                InsertHistorySnapshotTick(tick);
            }
        }

        private void RemoveHistorySnapshot(int tick)
        {
            if (_historySnapshotsByTick.Remove(tick))
            {
                RemoveHistorySnapshotTick(tick);
            }
        }

        private void TrimExpiredSnapshots(int minTick)
        {
            if (_historySnapshotsByTick.Count == 0)
            {
                _historySnapshotTicks.Clear();
                return;
            }

            int firstWithinRangeIndex = _historySnapshotTicks.BinarySearch(minTick);
            if (firstWithinRangeIndex < 0)
            {
                firstWithinRangeIndex = ~firstWithinRangeIndex;
            }

            int anchorIndex = firstWithinRangeIndex - 1;
            for (int i = anchorIndex - 1; i >= 0; i--)
            {
                _historySnapshotsByTick.Remove(_historySnapshotTicks[i]);
                _historySnapshotTicks.RemoveAt(i);
            }
        }

        private Frame CopyFrameState(Frame source)
        {
            Frame frame = RentFrameBuffer(source.EntityCapacity);
            frame.CopyStateFrom(source);
            return frame;
        }

        private Frame RestoreFrame(PackedFrameSnapshot snapshot, ComponentSerializationMode mode = ComponentSerializationMode.Default)
        {
            Frame frame = RentFrameBuffer(snapshot.EntityCapacity);
            frame.RestoreFromPackedSnapshot(snapshot, mode);
            return frame;
        }

        private Frame RentFrameBuffer(int requiredEntityCapacity)
        {
            while (_recycledFrames.Count > 0)
            {
                Frame recycled = _recycledFrames.Pop();
                if (recycled.EntityCapacity == requiredEntityCapacity)
                {
                    return recycled;
                }

                recycled.Dispose();
            }

            return new Frame(requiredEntityCapacity);
        }

        private void RecycleFrame(Frame? frame)
        {
            if (frame == null)
            {
                return;
            }

            if (_recycledFrames.Count < RecycledFrameLimit)
            {
                _recycledFrames.Push(frame);
                return;
            }

            frame.Dispose();
        }

        private void SetHistoricalScratchFrame(Frame frame)
        {
            if (!ReferenceEquals(_historicalScratchFrame, frame))
            {
                DisposeFrameIfDetached(_historicalScratchFrame);
                _historicalScratchFrame = frame;
            }
        }

        private void ReplayHistoricalTicksInPlace(Frame frame, int baseTick, int targetTick)
        {
            for (int currentTick = baseTick; currentTick < targetTick; currentTick++)
            {
                frame.Tick = currentTick + 1;
                frame.DeltaTime = DeltaTime;
                ApplyInputs(frame);
                UpdateSystems(frame, DeltaTime);
            }
        }

        /// <summary>
        /// 清空按需重建历史帧使用的临时缓存。
        /// 仅供派生类在测试或基准场景下显式控制冷/热路径使用。
        /// </summary>
        protected void InvalidateHistoricalMaterializeCache()
        {
            Frame? scratchFrame = _historicalScratchFrame;
            _historicalScratchFrame = null;

            if (_materializedHistoryByTick.Count > 0)
            {
                for (int i = 0; i < _materializedHistoryOrder.Count; i++)
                {
                    int tick = _materializedHistoryOrder[i];
                    if (_materializedHistoryByTick.Remove(tick, out Frame? cachedFrame) &&
                        !ReferenceEquals(cachedFrame, scratchFrame))
                    {
                        DisposeFrameIfDetached(cachedFrame);
                    }
                }

                _materializedHistoryOrder.Clear();
            }

            DisposeFrameIfDetached(scratchFrame);
        }

        private bool TryFindClosestMaterializedTick(int tick, out int materializedTick)
        {
            materializedTick = -1;

            for (int i = 0; i < _materializedHistoryOrder.Count; i++)
            {
                int candidateTick = _materializedHistoryOrder[i];
                if (candidateTick <= tick && candidateTick > materializedTick)
                {
                    materializedTick = candidateTick;
                }
            }

            return materializedTick >= 0;
        }

        private void StoreMaterializedHistoryFrame(Frame frame)
        {
            int tick = frame.Tick;

            if (_materializedHistoryByTick.TryGetValue(tick, out Frame? existing))
            {
                if (!ReferenceEquals(existing, frame))
                {
                    _materializedHistoryByTick[tick] = frame;
                    DisposeFrameIfDetached(existing);
                }

                TouchMaterializedHistoryTick(tick);
                return;
            }

            _materializedHistoryByTick[tick] = frame;
            _materializedHistoryOrder.Add(tick);

            if (_materializedHistoryOrder.Count > MaterializedHistoryCacheSize)
            {
                int evictedTick = _materializedHistoryOrder[0];
                _materializedHistoryOrder.RemoveAt(0);
                if (_materializedHistoryByTick.Remove(evictedTick, out Frame? evictedFrame))
                {
                    DisposeFrameIfDetached(evictedFrame);
                }
            }
        }

        private void TouchMaterializedHistoryTick(int tick)
        {
            int index = _materializedHistoryOrder.IndexOf(tick);
            if (index < 0 || index == _materializedHistoryOrder.Count - 1)
            {
                return;
            }

            _materializedHistoryOrder.RemoveAt(index);
            _materializedHistoryOrder.Add(tick);
        }

        private void InsertHistorySnapshotTick(int tick)
        {
            int index = _historySnapshotTicks.BinarySearch(tick);
            if (index >= 0)
            {
                return;
            }

            _historySnapshotTicks.Insert(~index, tick);
        }

        private void RemoveHistorySnapshotTick(int tick)
        {
            int index = _historySnapshotTicks.BinarySearch(tick);
            if (index >= 0)
            {
                _historySnapshotTicks.RemoveAt(index);
            }
        }

        private bool CanReplayHistoricalFramesInPlace()
        {
            MethodInfo? advanceFrameMethod = GetType().GetMethod(
                nameof(AdvanceFrame),
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            return advanceFrameMethod?.DeclaringType == typeof(Session);
        }
    }

    /// <summary>
    /// 会话检查点
    /// </summary>
    public class SessionCheckpoint
    {
        public int Tick;
        public PackedFrameSnapshot? VerifiedSnapshot;
        public PackedFrameSnapshot? PredictedSnapshot;
    }

    /// <summary>
    /// 输入命令接口
    /// </summary>
    public interface IInputCommand
    {
        int PlayerId { get; }
        int Tick { get; }
        byte[] Serialize();
        void Deserialize(byte[] data);
    }

    /// <summary>
    /// 输入缓冲区
    /// </summary>
    public class InputBuffer
    {
        private readonly IInputCommand?[] _inputs;
        private readonly int[] _ticks;
        private readonly int _capacity;
        private int _latestTick = int.MinValue;

        public InputBuffer(int capacity)
        {
            _capacity = System.Math.Max(1, capacity);
            _inputs = new IInputCommand[_capacity];
            _ticks = new int[_capacity];

            for (int i = 0; i < _ticks.Length; i++)
            {
                _ticks[i] = int.MinValue;
            }
        }

        public void SetInput(int tick, IInputCommand input)
        {
            if (tick > _latestTick)
            {
                _latestTick = tick;
            }

            if (tick < GetMinTick())
            {
                return;
            }

            int index = ToIndex(tick);
            _inputs[index] = input;
            _ticks[index] = tick;
        }

        public IInputCommand? GetInput(int tick)
        {
            if (_latestTick == int.MinValue || tick < GetMinTick())
            {
                return null;
            }

            int index = ToIndex(tick);
            return _ticks[index] == tick ? _inputs[index] : null;
        }

        private int GetMinTick()
        {
            if (_latestTick == int.MinValue)
            {
                return int.MaxValue;
            }

            return _latestTick - _capacity + 1;
        }

        private int ToIndex(int tick)
        {
            int index = tick % _capacity;
            return index < 0 ? index + _capacity : index;
        }
    }
}
