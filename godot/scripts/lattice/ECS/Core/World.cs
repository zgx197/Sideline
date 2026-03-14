using System;
using System.Collections.Generic;
using Lattice.Core;
using Lattice.Math;

namespace Lattice.ECS.Core
{
    /// <summary>
    /// ECS 世界 - 管理多帧状态（Verified / Predicted / Previous）
    /// 
    /// FrameSync 风格设计：
    /// - VerifiedFrame: 服务器确认帧（权威状态）
    /// - PredictedFrame: 本地预测帧（可能回滚）
    /// - PreviousFrame: 上一帧（插值用）
    /// - History: 历史帧缓冲区（用于回滚）
    /// </summary>
    public sealed class World : IDisposable
    {
        #region 配置

        /// <summary>最大预测帧数（超过需要回滚）</summary>
        public const int MaxPredictionFrames = 8;

        /// <summary>历史帧保留数量（用于回滚）</summary>
        public const int HistorySize = 128;  // 约 2 秒 @ 60fps

        #endregion

        #region 字段

        /// <summary>组件类型注册表</summary>
        private readonly ComponentTypeRegistry _typeRegistry;

        /// <summary>当前游戏时间（累计）</summary>
        public FP TotalTime { get; private set; }

        /// <summary>固定时间步长</summary>
        public FP DeltaTime { get; }

        /// <summary>当前帧号（Verified 帧号）</summary>
        public int CurrentTick { get; private set; }

        /// <summary>最新验证帧（服务器确认）</summary>
        public Frame? VerifiedFrame { get; private set; }

        /// <summary>预测帧（本地模拟）</summary>
        public Frame? PredictedFrame { get; private set; }

        /// <summary>上一帧（用于插值）</summary>
        public Frame? PreviousFrame { get; private set; }

        /// <summary>历史帧环形缓冲区</summary>
        private readonly RingBuffer<Frame> _history;

        /// <summary>是否正在回滚中</summary>
        public bool IsRollingBack { get; private set; }

        /// <summary>系统列表</summary>
        private readonly List<ISystem> _systems = new();

        #endregion

        #region 构造函数

        public World(FP deltaTime, ComponentTypeRegistry typeRegistry)
        {
            DeltaTime = deltaTime;
            _typeRegistry = typeRegistry;
            _history = new RingBuffer<Frame>(HistorySize);
            
            CurrentTick = 0;
            TotalTime = FP.Zero;
        }

        #endregion

        #region 初始化

        /// <summary>
        /// 初始化世界（创建第一帧）
        /// </summary>
        public void Initialize()
        {
            VerifiedFrame = CreateFrame(0);
            PredictedFrame = CreateFrame(0);
            PreviousFrame = null;
            
            _history.Clear();
            _history.PushBack(VerifiedFrame);
        }

        #endregion

        #region 实体操作（操作预测帧）

        /// <summary>
        /// 创建实体（在预测帧中）
        /// </summary>
        public Entity CreateEntity()
        {
            if (PredictedFrame == null)
                throw new InvalidOperationException("World not initialized");
            
            return PredictedFrame.CreateEntity();
        }

        /// <summary>
        /// 销毁实体（在预测帧中）
        /// </summary>
        public bool DestroyEntity(Entity entity)
        {
            if (PredictedFrame == null)
                return false;
            
            return PredictedFrame.DestroyEntity(entity);
        }

        #endregion

        #region 组件操作（操作预测帧）

        /// <summary>
        /// 添加组件
        /// </summary>
        public void AddComponent<T>(Entity entity, in T component) where T : struct
        {
            PredictedFrame?.AddComponent(entity, component);
        }

        /// <summary>
        /// 移除组件
        /// </summary>
        public bool RemoveComponent<T>(Entity entity) where T : struct
        {
            return PredictedFrame != null && PredictedFrame.RemoveComponent<T>(entity);
        }

        /// <summary>
        /// 获取组件引用
        /// </summary>
        public ref T GetComponent<T>(Entity entity) where T : struct
        {
            if (PredictedFrame == null)
                throw new InvalidOperationException("World not initialized");
            
            return ref PredictedFrame.GetComponent<T>(entity);
        }

        /// <summary>
        /// 尝试获取组件
        /// </summary>
        public bool TryGetComponent<T>(Entity entity, out T component) where T : struct
        {
            if (PredictedFrame == null)
            {
                component = default;
                return false;
            }
            
            return PredictedFrame.TryGetComponent(entity, out component);
        }

        /// <summary>
        /// 检查是否有组件
        /// </summary>
        public bool HasComponent<T>(Entity entity) where T : struct
        {
            return PredictedFrame != null && PredictedFrame.HasComponent<T>(entity);
        }

        #endregion

        #region 查询

        /// <summary>
        /// 创建单类型查询
        /// </summary>
        public Query<T> Query<T>() where T : struct
        {
            if (PredictedFrame == null)
                throw new InvalidOperationException("World not initialized");
            
            return new Query<T>(PredictedFrame);
        }

        /// <summary>
        /// 创建双类型查询
        /// </summary>
        public Query<T1, T2> Query<T1, T2>() where T1 : struct where T2 : struct
        {
            if (PredictedFrame == null)
                throw new InvalidOperationException("World not initialized");
            
            return new Query<T1, T2>(PredictedFrame);
        }

        /// <summary>
        /// 创建三类型查询
        /// </summary>
        public Query<T1, T2, T3> Query<T1, T2, T3>() 
            where T1 : struct where T2 : struct where T3 : struct
        {
            if (PredictedFrame == null)
                throw new InvalidOperationException("World not initialized");
            
            return new Query<T1, T2, T3>(PredictedFrame);
        }

        #endregion

        #region 系统管理

        /// <summary>
        /// 注册系统
        /// </summary>
        public void RegisterSystem(ISystem system)
        {
            _systems.Add(system);
            system.OnInit(this);
        }

        /// <summary>
        /// 注销系统
        /// </summary>
        public void UnregisterSystem(ISystem system)
        {
            if (_systems.Remove(system))
            {
                system.OnDestroy(this);
            }
        }

        #endregion

        #region 游戏循环

        /// <summary>
        /// 单步更新（FixedUpdate）
        /// </summary>
        public void Tick()
        {
            if (PredictedFrame == null)
                throw new InvalidOperationException("World not initialized");

            // 保存上一帧
            PreviousFrame = PredictedFrame;

            // 更新预测帧
            PredictedFrame.Tick++;
            CurrentTick = PredictedFrame.Tick;
            TotalTime += DeltaTime;

            // 执行系统更新
            foreach (var system in _systems)
            {
                system.OnUpdate(this, DeltaTime);
            }

            // 保存到历史
            _history.PushBack(PredictedFrame);
        }

        #endregion

        #region 帧同步（预测/回滚）

        /// <summary>
        /// 验证帧（服务器确认）
        /// </summary>
        public void VerifyFrame(int tick, long expectedChecksum)
        {
            // 获取对应历史帧
            var frame = GetHistoricalFrame(tick);
            if (frame == null)
            {
                // 帧已不在历史中，无法验证
                return;
            }

            long actualChecksum = frame.CalculateChecksum();
            
            if (actualChecksum != expectedChecksum)
            {
                // 校验和不匹配，需要回滚
                RollbackTo(tick);
            }
            else
            {
                // 验证通过
                frame.IsVerified = true;
                VerifiedFrame = frame;
            }
        }

        /// <summary>
        /// 回滚到指定帧
        /// </summary>
        public void RollbackTo(int tick)
        {
            IsRollingBack = true;
            
            try
            {
                // 获取历史帧
                var baseFrame = GetHistoricalFrame(tick);
                if (baseFrame == null)
                {
                    throw new InvalidOperationException($"Frame {tick} not found in history");
                }

                // 回滚到该帧
                CurrentTick = tick;
                PredictedFrame = baseFrame;
                
                // 重新模拟到当前时间
                // 实际实现需要重新执行所有系统
            }
            finally
            {
                IsRollingBack = false;
            }
        }

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

        #endregion

        #region 时光倒流（单机玩法）

        /// <summary>
        /// 回退指定帧数（单机时光倒流）
        /// </summary>
        public void Rewind(int frameCount)
        {
            int targetTick = System.Math.Max(0, CurrentTick - frameCount);
            RollbackTo(targetTick);
        }

        /// <summary>
        /// 创建当前状态的检查点（用于 SL）
        /// </summary>
        public WorldSnapshot CreateCheckpoint()
        {
            return new WorldSnapshot
            {
                Tick = CurrentTick,
                TotalTime = TotalTime,
                // 序列化所有帧数据
            };
        }

        /// <summary>
        /// 从检查点恢复
        /// </summary>
        public void RestoreFromCheckpoint(WorldSnapshot snapshot)
        {
            // 反序列化恢复状态
            CurrentTick = snapshot.Tick;
            TotalTime = snapshot.TotalTime;
        }

        #endregion

        #region 辅助方法

        private Frame CreateFrame(int tick)
        {
            return new Frame(tick, DeltaTime, _typeRegistry);
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            foreach (var frame in _history)
            {
                frame.Dispose();
            }
            _history.Clear();
            
            foreach (var system in _systems)
            {
                system.OnDestroy(this);
            }
            _systems.Clear();
        }

        #endregion
    }

    /// <summary>
    /// 系统接口
    /// </summary>
    public interface ISystem
    {
        void OnInit(World world);
        void OnUpdate(World world, FP deltaTime);
        void OnDestroy(World world);
    }

    /// <summary>
    /// 世界快照（用于存档/读档）
    /// </summary>
    public struct WorldSnapshot
    {
        public int Tick;
        public FP TotalTime;
        public byte[] FrameData;
    }

    /// <summary>
    /// 环形缓冲区
    /// </summary>
    public sealed class RingBuffer<T>
    {
        private readonly T[] _buffer;
        private int _head;
        private int _count;

        public RingBuffer(int capacity)
        {
            _buffer = new T[capacity];
            _head = 0;
            _count = 0;
        }

        public int Count => _count;
        public int Capacity => _buffer.Length;

        public void PushBack(T item)
        {
            _buffer[_head] = item;
            _head = (_head + 1) % _buffer.Length;
            if (_count < _buffer.Length)
                _count++;
        }

        public T? PopFront()
        {
            if (_count == 0) return default;
            
            int tail = (_head - _count + _buffer.Length) % _buffer.Length;
            var item = _buffer[tail];
            _count--;
            return item;
        }

        public void Clear()
        {
            _head = 0;
            _count = 0;
        }

        public IEnumerable<T> GetAll()
        {
            for (int i = 0; i < _count; i++)
            {
                int index = (_head - _count + i + _buffer.Length) % _buffer.Length;
                yield return _buffer[index]!;
            }
        }

        public IEnumerator<T> GetEnumerator()
        {
            return GetAll().GetEnumerator();
        }
    }
}
