using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Lattice.ECS.Core;
using Lattice.ECS.Framework;
using Lattice.Math;

namespace Lattice.ECS.Session
{
    /// <summary>
    /// 共享 Session 运行时内核。
    /// 该类型承载固定帧推进、系统驱动、输入缓冲、历史帧、检查点与回滚相关的通用机制，
    /// 具体运行模式通过派生类决定，不直接假定自己就是唯一的正式 Session 形态。
    /// </summary>
    public abstract class SessionRuntime : IDisposable
    {
        private static readonly SessionRuntimeDataBoundary DefaultDataBoundary = new(
            SessionInputStorageKind.PlayerTickFixedWindow,
            SessionHistoryStorageKind.BoundedLiveFramesWithSampledSnapshots,
            SessionCheckpointStorageKind.PackedSnapshot,
            SessionRuntimeDataCapability.PlayerTickInputLookup |
            SessionRuntimeDataCapability.BoundedInputRetention |
            SessionRuntimeDataCapability.TickAddressableHistory |
            SessionRuntimeDataCapability.SampledHistoryMaterialization |
            SessionRuntimeDataCapability.ExplicitCheckpointRestore |
            SessionRuntimeDataCapability.PackedCheckpointStorage,
            UnsupportedSessionRuntimeDataCapability.ConfigurableInputRetention |
            UnsupportedSessionRuntimeDataCapability.ConfigurableHistoryRetention |
            UnsupportedSessionRuntimeDataCapability.ConfigurableSnapshotSampling |
            UnsupportedSessionRuntimeDataCapability.PluggableHistoryStore |
            UnsupportedSessionRuntimeDataCapability.AlternativeCheckpointFormats |
            UnsupportedSessionRuntimeDataCapability.UnboundedPerTickRetention);

        private static readonly SessionTickPipelineBoundary DefaultTickPipeline = new(
            SessionTickPipelineKind.PhasedStructuralCommit,
            SessionTickPipelineCapability.ExplicitStageExposure |
            SessionTickPipelineCapability.DeferredStructuralChanges |
            SessionTickPipelineCapability.StructuralCommitStage |
            SessionTickPipelineCapability.CleanupAndHistoryStages |
            SessionTickPipelineCapability.ImmediateComponentMutationDuringSimulation,
            UnsupportedSessionTickPipelineCapability.ImmediateStructuralVisibilityDuringSimulation |
            UnsupportedSessionTickPipelineCapability.RuntimeReorderedStages |
            UnsupportedSessionTickPipelineCapability.PerSystemStructuralCommit);

        private static readonly SessionRuntimeInputBoundary DefaultInputBoundary = new(
            SessionInputContractKind.TickScopedPlayerInputSet,
            SessionMissingInputPolicy.OmitMissingPlayers,
            SessionInputWritePolicy.LatestWriteWins,
            SessionInputOrder.PlayerIdAscending,
            SessionRuntimeInputCapability.TickScopedInputAggregation |
            SessionRuntimeInputCapability.SparsePlayerInput |
            SessionRuntimeInputCapability.StablePlayerOrdering |
            SessionRuntimeInputCapability.LatestWriteWins |
            SessionRuntimeInputCapability.TransportDecoupledInputModel,
            UnsupportedSessionRuntimeInputCapability.ImplicitPreviousInputCarryForward |
            UnsupportedSessionRuntimeInputCapability.BuiltInDefaultInputSynthesis |
            UnsupportedSessionRuntimeInputCapability.ConfigurableInputAggregation |
            UnsupportedSessionRuntimeInputCapability.ConfigurableMissingInputPolicy |
            UnsupportedSessionRuntimeInputCapability.BuiltInTransportSerialization);

        #region 字段

        /// <summary>运行时配置。</summary>
        public SessionRuntimeOptions RuntimeOptions { get; }

        /// <summary>
        /// 当前运行时上下文。
        /// 用于承载运行期共享对象与稳定公开的运行元信息。
        /// </summary>
        public SessionRuntimeContext Context { get; }

        /// <summary>
        /// 当前 Session 的正式运行边界描述。
        /// 用于明确当前主干承诺的能力面，而不是把 Session 误读为完整联机会话产品层。
        /// </summary>
        public abstract SessionRuntimeBoundary RuntimeBoundary { get; }

        /// <summary>
        /// 当前运行时的输入 / 历史 / checkpoint 数据策略边界。
        /// 用于明确哪些是稳定公开契约，哪些 sizing 仍属于内部实现。
        /// </summary>
        public virtual SessionRuntimeDataBoundary DataBoundary => DefaultDataBoundary;

        /// <summary>
        /// 当前运行时的 Tick 管线边界。
        /// 用于明确 Tick 阶段与结构性修改的正式语义，而不是继续依赖系统注册顺序和调用约定。
        /// </summary>
        public virtual SessionTickPipelineBoundary TickPipeline => DefaultTickPipeline;

        /// <summary>
        /// 当前运行时的正式输入契约边界。
        /// 用于明确输入按什么模型聚合、缺失输入如何处理、重复写入如何收敛，以及输入模型是否与传输序列化解耦。
        /// </summary>
        public virtual SessionRuntimeInputBoundary InputBoundary => DefaultInputBoundary;

        /// <summary>
        /// 当前运行时类型。
        /// </summary>
        public SessionRuntimeKind RuntimeKind => RuntimeBoundary.RuntimeKind;

        /// <summary>固定时间步长</summary>
        public FP DeltaTime
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get;
        }

        /// <summary>本地玩家 ID</summary>
        public int LocalPlayerId { get; protected set; }

        /// <summary>当前帧号（Verified 帧号）</summary>
        public int CurrentTick { get; protected set; }

        /// <summary>
        /// 当前 Tick 正在执行的阶段。
        /// 正常空闲态为 `Idle`；运行中的单步推进、回滚重模拟与历史 materialize 都会复用同一套阶段语义。
        /// </summary>
        public SessionTickStage CurrentTickStage { get; private set; }

        /// <summary>最新验证帧（服务器确认）</summary>
        public Frame? VerifiedFrame { get; protected set; }

        /// <summary>预测帧（本地模拟）</summary>
        public Frame? PredictedFrame { get; protected set; }

        /// <summary>上一帧（用于插值）</summary>
        public Frame? PreviousFrame { get; protected set; }

        /// <summary>SessionRuntime 内部历史帧与帧池管理。</summary>
        private readonly SessionFrameHistory _frameHistory;

        private readonly bool _canReplayHistoricalFramesInPlace;

        /// <summary>是否正在回滚中</summary>
        public bool IsRollingBack { get; protected set; }

        /// <summary>是否已启动</summary>
        public bool IsRunning { get; protected set; }

        /// <summary>系统调度器</summary>
        protected readonly SystemScheduler _systemScheduler = new();

        /// <summary>SessionRuntime 内部输入保留与聚合管理。</summary>
        private readonly SessionInputStore _inputStore;

        /// <summary>SessionRuntime 内部 Tick 管线执行器。</summary>
        private readonly SessionTickProcessor _tickProcessor = new();

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

        protected SessionRuntime(FP deltaTime, int localPlayerId = 0)
            : this(new SessionRuntimeOptions(deltaTime, localPlayerId))
        {
        }

        protected SessionRuntime(SessionRuntimeOptions runtimeOptions)
        {
            RuntimeOptions = runtimeOptions ?? throw new ArgumentNullException(nameof(runtimeOptions));
            DeltaTime = runtimeOptions.DeltaTime;
            LocalPlayerId = runtimeOptions.LocalPlayerId;
            Context = new SessionRuntimeContext(this, GetType().Name);
            _frameHistory = new SessionFrameHistory();
            _inputStore = new SessionInputStore(SessionRuntimeDataDefaults.HistorySize);
            _canReplayHistoricalFramesInPlace = CanReplayHistoricalFramesInPlace();

            CurrentTick = 0;
            CurrentTickStage = SessionTickStage.Idle;
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
            CurrentTickStage = SessionTickStage.Idle;

            UpdateHistory(VerifiedFrame!);

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
            CurrentTickStage = SessionTickStage.Idle;
        }

        /// <summary>
        /// 单步更新（FixedUpdate）。
        /// 这是当前最小本地推进入口：基于已知输入推进 `PredictedFrame`，并写入历史。
        /// 只能在会话运行期间调用。
        /// </summary>
        public virtual void Update()
        {
            ThrowIfDisposed();
            EnsureCapability(SessionRuntimeCapability.LocalPredictionStep, nameof(Update));
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

            ExecuteTickPipeline(PredictedFrame!, raiseFrameUpdateEvent: true, captureHistory: true);
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

        /// <summary>
        /// Tick 末尾清理阶段。
        /// 当前默认只保留显式阶段语义，不额外引入新的运行逻辑。
        /// </summary>
        protected virtual void CleanupFrame(Frame frame)
        {
        }

        /// <summary>
        /// Tick 历史写入阶段。
        /// 默认把当前帧写入运行时历史；保留显式 hook 仅用于测试与后续边界扩展。
        /// </summary>
        protected virtual void CaptureHistory(Frame frame)
        {
            UpdateHistory(frame);
        }

        #endregion

        #region 输入管理

        /// <summary>
        /// 设置玩家输入。
        /// 稳定公开契约是按 `(playerId, tick)` 读写输入；
        /// 具体窗口大小、ring buffer sizing 与淘汰细节仍属于内部策略，可通过 `DataBoundary` 了解当前承诺的能力边界。
        /// </summary>
        public virtual void SetPlayerInput(int playerId, int tick, IPlayerInput input)
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(input);
            ValidateInputIdentity(playerId, tick, input);

            _inputStore.SetPlayerInput(playerId, tick, input);
        }

        /// <summary>
        /// 获取玩家输入。
        /// 若请求 tick 已超出当前内部保留窗口，则返回 `null`；窗口大小本身不属于稳定公开 API。
        /// </summary>
        public IPlayerInput? GetPlayerInput(int playerId, int tick)
        {
            ThrowIfDisposed();
            return _inputStore.GetPlayerInput(playerId, tick);
        }

        /// <summary>
        /// 为指定 tick 聚合输入集合。
        /// 当前正式语义为：
        /// - 只聚合实际写入的玩家输入
        /// - 缺失玩家不会自动生成默认输入
        /// - 遍历顺序按玩家 ID 升序稳定排列
        /// </summary>
        protected virtual SessionInputSet CollectInputSet(int tick)
        {
            return _inputStore.CollectInputSet(tick);
        }

        /// <summary>
        /// 将聚合后的输入集合应用到当前帧。
        /// 这是当前正式推荐的输入扩展点，新玩法 Session 默认应优先重写该方法，而不是直接操作底层输入缓冲。
        /// </summary>
        protected virtual void ApplyInputSet(Frame frame, in SessionInputSet inputSet)
        {
        }

        /// <summary>
        /// 应用输入到帧。
        /// 默认实现会先构建正式 `SessionInputSet`，再调用 `ApplyInputSet(frame, inputSet)`。
        /// 保留该 hook 仅用于兼容现有派生类；新代码应优先重写 `ApplyInputSet(...)`。
        /// </summary>
        protected virtual void ApplyInputs(Frame frame)
        {
            SessionInputSet inputSet = CollectInputSet(frame.Tick);
            ApplyInputSet(frame, inputSet);
        }

        private void ExecuteTickPipeline(Frame frame, bool raiseFrameUpdateEvent, bool captureHistory)
        {
            _tickProcessor.Execute(
                frame,
                DeltaTime,
                EnterTickStage,
                ApplyInputs,
                UpdateSystems,
                CleanupFrame,
                CaptureHistory,
                OnFrameUpdate,
                raiseFrameUpdateEvent,
                captureHistory);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private void EnterTickStage(SessionTickStage stage)
        {
            CurrentTickStage = stage;
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
            EnsureCapability(SessionRuntimeCapability.PredictionVerification, nameof(VerifyFrame));
            EnsureRunning(nameof(VerifyFrame));
            ArgumentOutOfRangeException.ThrowIfNegative(tick);

            if (tick > CurrentTick)
            {
                throw new ArgumentOutOfRangeException(nameof(tick), "VerifyFrame only accepts historical or current ticks.");
            }

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
            EnsureCapability(SessionRuntimeCapability.PredictionVerification, nameof(RollbackTo));
            EnsureRunning(nameof(RollbackTo));
            ArgumentOutOfRangeException.ThrowIfNegative(tick);

            if (tick > CurrentTick)
            {
                throw new ArgumentOutOfRangeException(nameof(tick), "RollbackTo only accepts historical or current ticks.");
            }

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
                ExecuteTickPipeline(PredictedFrame!, raiseFrameUpdateEvent: false, captureHistory: true);
            }
        }

        #endregion

        #region 历史帧管理

        /// <summary>
        /// 获取历史帧。
        /// 返回结果可能来自 live history，也可能来自 sampled snapshot 的按需 materialize；
        /// 具体保留数量与采样间隔属于内部策略，不作为稳定公开 API 承诺。
        /// </summary>
        public Frame? GetHistoricalFrame(int tick)
        {
            ThrowIfDisposed();

            if (_frameHistory.TryGetLiveFrame(tick, out Frame? frame))
            {
                return frame;
            }

            if (_frameHistory.TryGetMaterializedFrame(tick, out Frame? cachedFrame))
            {
                return cachedFrame;
            }

            if (_frameHistory.TryGetScratchFrame(tick, out Frame? scratchFrame))
            {
                return scratchFrame;
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
            _frameHistory.UpdateHistory(frame, DisposeFrameIfDetached);
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
            EnsureCapability(SessionRuntimeCapability.LocalRewind, nameof(Rewind));
            EnsureRunning(nameof(Rewind));
            ArgumentOutOfRangeException.ThrowIfNegative(frameCount);

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
        /// 当前公开契约是“可保存并恢复运行态帧状态，并携带正式 checkpoint 协议 metadata”；
        /// 底层 packed payload 细节仍通过显式版本号治理，而不是默认承诺永远隐式兼容。
        /// 需要当前已经存在有效帧状态。
        /// </summary>
        public SessionCheckpoint CreateCheckpoint()
        {
            ThrowIfDisposed();
            EnsureCapability(SessionRuntimeCapability.CheckpointRestore, nameof(CreateCheckpoint));
            EnsureFrameState(nameof(CreateCheckpoint));
            return SessionCheckpointFactory.Capture(CurrentTick, VerifiedFrame, PredictedFrame, InputBoundary, DataBoundary);
        }

        /// <summary>
        /// 从检查点恢复。
        /// 该接口更偏“本地工具/运行时管理”语义：只恢复帧状态，
        /// 不重建系统集合，也不切换 Session 的运行模式。
        /// 该操作只恢复帧状态，不会重建系统集合，也不会重新初始化系统。
        /// 当前会先校验 checkpoint 协议、输入契约和组件 schema，再执行底层 snapshot restore。
        /// </summary>
        public void RestoreFromCheckpoint(SessionCheckpoint checkpoint)
        {
            ThrowIfDisposed();
            EnsureCapability(SessionRuntimeCapability.CheckpointRestore, nameof(RestoreFromCheckpoint));
            ArgumentNullException.ThrowIfNull(checkpoint);

            ResetFrameState(disposeFrames: true);

            CurrentTick = checkpoint.Tick;
            SessionCheckpointFrames restoredFrames = SessionCheckpointFactory.RestoreFrames(checkpoint, InputBoundary, DataBoundary, RestoreFrame);
            SetVerifiedFrame(restoredFrames.VerifiedFrame);
            SetPredictedFrame(restoredFrames.PredictedFrame);
            SetPreviousFrame(null);

            if (VerifiedFrame != null) UpdateHistory(VerifiedFrame);
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

        private static void ValidateInputIdentity(int playerId, int tick, IPlayerInput input)
        {
            if (input.PlayerId != playerId)
            {
                throw new ArgumentException("Input PlayerId must match the playerId argument.", nameof(input));
            }

            if (input.Tick != tick)
            {
                throw new ArgumentException("Input Tick must match the tick argument.", nameof(input));
            }
        }

        private void ResetFrameState(bool disposeFrames)
        {
            if (disposeFrames)
            {
                var frames = new HashSet<Frame>();

                if (VerifiedFrame != null) frames.Add(VerifiedFrame);
                if (PredictedFrame != null) frames.Add(PredictedFrame);
                if (PreviousFrame != null) frames.Add(PreviousFrame);
                _frameHistory.CopyOwnedFramesTo(frames);

                foreach (Frame frame in frames)
                {
                    frame.Dispose();
                }
            }

            VerifiedFrame = null;
            PredictedFrame = null;
            PreviousFrame = null;
            CurrentTick = 0;
            IsRollingBack = false;
            _frameHistory.Clear();
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
            _inputStore.Clear();
            Context.Dispose();
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

        private void EnsureCapability(SessionRuntimeCapability capability, string methodName)
        {
            if (!RuntimeBoundary.Supports(capability))
            {
                throw new NotSupportedException(
                    $"{GetType().Name} does not support {capability} required by {methodName}.");
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().Name);
            }
        }

        internal void BindContextRunnerName(string runnerName)
        {
            Context.BindRunnerName(runnerName);
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
                ReferenceEquals(frame, PreviousFrame))
            {
                return;
            }

            if (_frameHistory.Owns(frame))
            {
                return;
            }

            _frameHistory.RecycleFrame(frame);
        }

        private bool TryMaterializeHistoricalFrame(int tick, out Frame? frame)
        {
            frame = null;

            if (!_frameHistory.IsTickWithinRetentionWindow(tick))
            {
                return false;
            }

            if (_frameHistory.TryGetScratchFrame(tick, out Frame? scratchFrame))
            {
                frame = scratchFrame;
                return true;
            }

            if (_frameHistory.TryGetMaterializedFrame(tick, out Frame? cachedFrame))
            {
                _frameHistory.SetScratchFrame(cachedFrame!, DisposeFrameIfDetached);
                frame = cachedFrame!;
                return true;
            }

            int baseTick = -1;
            Frame? baseFrame = null;
            _frameHistory.TryFindClosestLiveFrame(tick, out baseTick, out baseFrame);

            if (_frameHistory.TryFindClosestMaterializedFrame(tick, out int materializedTick, out Frame? materializedBase) &&
                materializedTick > baseTick)
            {
                baseTick = materializedTick;
                baseFrame = materializedBase;
            }

            if (_frameHistory.TryGetScratchAsBaseFrame(tick, out int scratchTick, out Frame? scratchBase) &&
                scratchTick > baseTick)
            {
                baseTick = scratchTick;
                baseFrame = scratchBase;
            }

            Frame workingFrame;
            if (baseFrame != null)
            {
                workingFrame = CopyFrameState(baseFrame);
            }
            else if (_frameHistory.TryFindClosestSnapshotTick(tick, out int snapshotTick))
            {
                baseTick = snapshotTick;
                workingFrame = RestoreFrame(_frameHistory.GetSnapshot(snapshotTick), ComponentSerializationMode.Prediction);
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
                    _frameHistory.RecycleFrame(workingFrame);
                    workingFrame = nextFrame;
                    ExecuteTickPipeline(workingFrame, raiseFrameUpdateEvent: false, captureHistory: false);
                }
            }

            _frameHistory.StoreMaterializedFrame(workingFrame, DisposeFrameIfDetached);
            _frameHistory.SetScratchFrame(workingFrame, DisposeFrameIfDetached);
            frame = workingFrame;
            return true;
        }

        private Frame CopyFrameState(Frame source)
        {
            Frame frame = _frameHistory.RentFrameBuffer(source.EntityCapacity);
            frame.CopyStateFrom(source);
            return frame;
        }

        private Frame RestoreFrame(PackedFrameSnapshot snapshot, ComponentSerializationMode mode = ComponentSerializationMode.Default)
        {
            Frame frame = _frameHistory.RentFrameBuffer(snapshot.EntityCapacity);
            frame.RestoreFromPackedSnapshot(snapshot, mode);
            return frame;
        }

        private void ReplayHistoricalTicksInPlace(Frame frame, int baseTick, int targetTick)
        {
            for (int currentTick = baseTick; currentTick < targetTick; currentTick++)
            {
                frame.Tick = currentTick + 1;
                frame.DeltaTime = DeltaTime;
                ExecuteTickPipeline(frame, raiseFrameUpdateEvent: false, captureHistory: false);
            }
        }

        /// <summary>
        /// 清空按需重建历史帧使用的临时缓存。
        /// 仅供派生类在测试或基准场景下显式控制冷/热路径使用。
        /// </summary>
        protected void InvalidateHistoricalMaterializeCache()
        {
            _frameHistory.InvalidateMaterializedCache(DisposeFrameIfDetached);
        }

        private bool CanReplayHistoricalFramesInPlace()
        {
            MethodInfo? advanceFrameMethod = GetType().GetMethod(
                nameof(AdvanceFrame),
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            return advanceFrameMethod?.DeclaringType == typeof(SessionRuntime);
        }
    }

    /// <summary>
    /// 会话检查点
    /// </summary>
    public class SessionCheckpoint
    {
        public int Tick;
        public SessionCheckpointProtocol Protocol;
        public PackedFrameSnapshot? VerifiedSnapshot;
        public PackedFrameSnapshot? PredictedSnapshot;
    }
}
