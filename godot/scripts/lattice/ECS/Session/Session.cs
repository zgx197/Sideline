using System;
using System.Collections.Generic;
using Lattice.Core;
using Lattice.ECS.Core;
using Lattice.Math;

namespace Lattice.ECS.Session
{
    /// <summary>
    /// ECS 会话管理器 - FrameSync 风格分层架构
    /// 
    /// 职责：
    /// 1. 多帧管理（Verified / Predicted / History）
    /// 2. 网络同步（验证帧、处理回滚）
    /// 3. 输入管理（收集、预测、应用）
    /// 4. 协调 World 的更新
    /// </summary>
    public abstract class Session : IDisposable
    {
        #region 配置

        /// <summary>最大预测帧数（超过需要回滚）</summary>
        public const int MaxPredictionFrames = 8;

        /// <summary>历史帧保留数量</summary>
        public const int HistorySize = 128;  // 约 2 秒 @ 60fps

        #endregion

        #region 字段

        /// <summary>组件类型注册表</summary>
        protected readonly ComponentTypeRegistry _typeRegistry;

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

        /// <summary>历史帧环形缓冲区</summary>
        protected readonly RingBuffer<Frame> _history;

        /// <summary>是否正在回滚中</summary>
        public bool IsRollingBack { get; protected set; }

        /// <summary>是否已启动</summary>
        public bool IsRunning { get; protected set; }

        /// <summary>系统列表</summary>
        protected readonly List<ISystem> _systems = new();

        /// <summary>输入缓冲区</summary>
        protected readonly Dictionary<int, InputBuffer> _inputBuffers = new();

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

        protected Session(FP deltaTime, ComponentTypeRegistry typeRegistry, int localPlayerId = 0)
        {
            DeltaTime = deltaTime;
            _typeRegistry = typeRegistry;
            LocalPlayerId = localPlayerId;
            _history = new RingBuffer<Frame>(HistorySize);

            CurrentTick = 0;
        }

        #endregion

        #region 生命周期

        /// <summary>
        /// 启动会话
        /// </summary>
        public virtual void Start()
        {
            if (IsRunning) return;

            // 创建初始帧
            VerifiedFrame = CreateFrame(0);
            PredictedFrame = CreateFrame(0);
            PreviousFrame = null;

            _history.Clear();
            _history.PushBack(VerifiedFrame);

            IsRunning = true;

            // 初始化系统
            foreach (var system in _systems)
            {
                system.OnInit(PredictedFrame);
            }
        }

        /// <summary>
        /// 停止会话
        /// </summary>
        public virtual void Stop()
        {
            if (!IsRunning) return;

            // 销毁系统
            if (PredictedFrame != null)
            {
                foreach (var system in _systems)
                {
                    system.OnDestroy(PredictedFrame);
                }
            }

            IsRunning = false;
        }

        /// <summary>
        /// 单步更新（FixedUpdate）
        /// </summary>
        public virtual void Update()
        {
            if (!IsRunning || PredictedFrame == null) return;

            // 保存上一帧
            PreviousFrame = PredictedFrame;

            // 创建新帧或复用
            PredictedFrame = AdvanceFrame(PredictedFrame);
            CurrentTick = PredictedFrame.Tick;

            // 应用输入
            ApplyInputs(PredictedFrame);

            // 执行系统更新
            UpdateSystems(PredictedFrame, DeltaTime);

            // 触发事件
            OnFrameUpdate?.Invoke(PredictedFrame, DeltaTime);

            // 保存到历史
            _history.PushBack(PredictedFrame);
        }

        #endregion

        #region 系统管理

        /// <summary>
        /// 注册系统
        /// </summary>
        public void RegisterSystem(ISystem system)
        {
            _systems.Add(system);
            if (IsRunning && PredictedFrame != null)
            {
                system.OnInit(PredictedFrame);
            }
        }

        /// <summary>
        /// 注销系统
        /// </summary>
        public void UnregisterSystem(ISystem system)
        {
            if (_systems.Remove(system) && IsRunning && PredictedFrame != null)
            {
                system.OnDestroy(PredictedFrame);
            }
        }

        /// <summary>
        /// 执行系统更新
        /// </summary>
        protected virtual void UpdateSystems(Frame frame, FP deltaTime)
        {
            foreach (var system in _systems)
            {
                system.OnUpdate(frame, deltaTime);
            }
        }

        #endregion

        #region 输入管理

        /// <summary>
        /// 设置玩家输入
        /// </summary>
        public virtual void SetPlayerInput(int playerId, int tick, IInputCommand input)
        {
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
        /// 验证帧（服务器确认）
        /// </summary>
        public virtual void VerifyFrame(int tick, long expectedChecksum, byte[]? inputData = null)
        {
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
                frame.IsVerified = true;
                VerifiedFrame = frame;
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
        /// 回滚到指定帧
        /// </summary>
        public virtual void RollbackTo(int tick, byte[]? correctedInput = null)
        {
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
                PredictedFrame = baseFrame.Clone();
                CurrentTick = tick;

                // 如果有修正的输入，应用它
                if (correctedInput != null)
                {
                    ApplyCorrectedInput(PredictedFrame, correctedInput);
                }

                // 重新模拟到当前时间
                Resimulate(tick, GetTargetTick());
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
                // 创建下一帧
                PredictedFrame = AdvanceFrame(PredictedFrame!);
                CurrentTick = PredictedFrame.Tick;

                // 应用该帧的输入（可能是预测输入）
                ApplyInputs(PredictedFrame);

                // 执行系统
                UpdateSystems(PredictedFrame, DeltaTime);

                // 更新历史（替换原有预测帧）
                UpdateHistory(PredictedFrame);
            }
        }

        /// <summary>
        /// 获取目标帧号（用于预测）
        /// </summary>
        protected virtual int GetTargetTick()
        {
            // 默认继续推进一帧
            return CurrentTick + 1;
        }

        #endregion

        #region 历史帧管理

        /// <summary>
        /// 获取历史帧
        /// </summary>
        public Frame? GetHistoricalFrame(int tick)
        {
            foreach (var frame in _history)
            {
                if (frame.Tick == tick)
                    return frame;
            }
            return null;
        }

        /// <summary>
        /// 更新历史中的帧
        /// </summary>
        protected void UpdateHistory(Frame frame)
        {
            // 查找并替换
            for (int i = 0; i < _history.Count; i++)
            {
                var existing = _history.GetAt(i);
                if (existing?.Tick == frame.Tick)
                {
                    _history.SetAt(i, frame);
                    return;
                }
            }

            // 不存在则添加
            _history.PushBack(frame);
        }

        #endregion

        #region 时光倒流（单机玩法）

        /// <summary>
        /// 回退指定帧数（单机时光倒流）
        /// </summary>
        public void Rewind(int frameCount)
        {
            int targetTick = System.Math.Max(0, CurrentTick - frameCount);

            // 直接获取历史帧，不重新模拟（与网络回滚不同）
            var baseFrame = GetHistoricalFrame(targetTick);
            if (baseFrame != null)
            {
                PredictedFrame = baseFrame.Clone();
                CurrentTick = targetTick;
            }
        }

        /// <summary>
        /// 创建检查点
        /// </summary>
        public SessionCheckpoint CreateCheckpoint()
        {
            return new SessionCheckpoint
            {
                Tick = CurrentTick,
                VerifiedFrame = VerifiedFrame?.Clone(),
                PredictedFrame = PredictedFrame?.Clone()
            };
        }

        /// <summary>
        /// 从检查点恢复
        /// </summary>
        public void RestoreFromCheckpoint(SessionCheckpoint checkpoint)
        {
            CurrentTick = checkpoint.Tick;
            VerifiedFrame = checkpoint.VerifiedFrame;
            PredictedFrame = checkpoint.PredictedFrame;

            _history.Clear();
            if (VerifiedFrame != null) _history.PushBack(VerifiedFrame);
            if (PredictedFrame != null) _history.PushBack(PredictedFrame);
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 创建新帧
        /// </summary>
        protected virtual Frame CreateFrame(int tick)
        {
            return new Frame(tick, DeltaTime, _typeRegistry);
        }

        /// <summary>
        /// 推进到下一帧
        /// </summary>
        protected virtual Frame AdvanceFrame(Frame currentFrame)
        {
            // 创建新帧，复制必要状态
            var nextFrame = CreateFrame(currentFrame.Tick + 1);

            // 可以在这里复制需要跨帧保持的状态
            // 例如：持久化组件、全局数据等

            return nextFrame;
        }

        /// <summary>
        /// 应用修正的输入
        /// </summary>
        protected virtual void ApplyCorrectedInput(Frame frame, byte[] inputData)
        {
            // 子类实现
        }

        #endregion

        #region IDisposable

        public virtual void Dispose()
        {
            Stop();

            foreach (var frame in _history)
            {
                frame.Dispose();
            }
            _history.Clear();
        }

        #endregion
    }

    /// <summary>
    /// 会话检查点
    /// </summary>
    public class SessionCheckpoint
    {
        public int Tick;
        public Frame? VerifiedFrame;
        public Frame? PredictedFrame;
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
        private readonly RingBuffer<IInputCommand?> _inputs;
        private int _startTick;

        public InputBuffer(int capacity)
        {
            _inputs = new RingBuffer<IInputCommand?>(capacity);
            _startTick = 0;
        }

        public void SetInput(int tick, IInputCommand input)
        {
            // 简化实现：直接存储
            while (_inputs.Count <= tick - _startTick)
            {
                _inputs.PushBack(null);
            }

            // 需要更复杂的索引管理
        }

        public IInputCommand? GetInput(int tick)
        {
            int index = tick - _startTick;
            if (index < 0 || index >= _inputs.Count)
                return null;

            // 需要实现 GetAt
            return null;
        }
    }
}
