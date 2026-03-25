using System;
using System.Collections.Generic;
using Lattice.ECS.Core;
using Lattice.Math;

namespace Lattice.ECS.Framework
{
    /// <summary>
    /// 最小系统调度器。
    /// </summary>
    public sealed class SystemScheduler
    {
        private readonly List<ISystem> _systems = new();
        private bool _initialized;

        /// <summary>
        /// 当前已注册系统数量。
        /// </summary>
        public int Count => _systems.Count;

        /// <summary>
        /// 当前是否已完成初始化。
        /// </summary>
        public bool IsInitialized => _initialized;

        /// <summary>
        /// 注册系统。
        /// </summary>
        public void Add(ISystem system)
        {
            ArgumentNullException.ThrowIfNull(system);

            if (_systems.Contains(system))
            {
                throw new InvalidOperationException("System is already registered.");
            }

            _systems.Add(system);
        }

        /// <summary>
        /// 移除系统。
        /// </summary>
        public bool Remove(ISystem system)
        {
            ArgumentNullException.ThrowIfNull(system);
            return _systems.Remove(system);
        }

        /// <summary>
        /// 清空系统列表。
        /// 必须在未初始化状态下调用；若系统已初始化，请先执行 Shutdown(frame)。
        /// </summary>
        public void Clear()
        {
            if (_initialized)
            {
                throw new InvalidOperationException("Cannot clear systems while scheduler is initialized. Call Shutdown(frame) first.");
            }

            _systems.Clear();
        }

        /// <summary>
        /// 初始化所有系统。
        /// </summary>
        public void Initialize(Frame frame)
        {
            ArgumentNullException.ThrowIfNull(frame);

            if (_initialized)
            {
                return;
            }

            for (int i = 0; i < _systems.Count; i++)
            {
                _systems[i].OnInit(frame);
            }

            _initialized = true;
        }

        /// <summary>
        /// 按注册顺序更新所有系统。
        /// </summary>
        public void Update(Frame frame, FP deltaTime)
        {
            ArgumentNullException.ThrowIfNull(frame);

            if (!_initialized)
            {
                throw new InvalidOperationException("SystemScheduler must be initialized before update.");
            }

            for (int i = 0; i < _systems.Count; i++)
            {
                _systems[i].OnUpdate(frame, deltaTime);
            }
        }

        /// <summary>
        /// 关闭所有系统。
        /// </summary>
        public void Shutdown(Frame frame)
        {
            ArgumentNullException.ThrowIfNull(frame);

            if (!_initialized)
            {
                return;
            }

            for (int i = 0; i < _systems.Count; i++)
            {
                _systems[i].OnDestroy(frame);
            }

            _initialized = false;
        }
    }
}
