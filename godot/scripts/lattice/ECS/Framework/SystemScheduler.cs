using System;
using System.Collections.Generic;
using Lattice.ECS.Core;
using Lattice.Math;

namespace Lattice.ECS.Framework
{
    /// <summary>
    /// 最小系统调度器。
    /// 当前正式支持的是单线程、平面、按 phase 再按注册顺序执行的系统集合；
    /// 不提供 enable / disable、依赖排序或 `SystemGroup` 层级模型。
    /// </summary>
    public sealed class SystemScheduler
    {
        private static readonly SystemSchedulerBoundary FlatPhasedOrderedBoundary = new(
            SystemSchedulerKind.FlatPhasedOrdered,
            SystemSchedulerCapability.OrderedExecution |
            SystemSchedulerCapability.ExplicitLifecycle |
            SystemSchedulerCapability.StaticRegistrationBeforeInitialize |
            SystemSchedulerCapability.PhasedExecution |
            SystemSchedulerCapability.AuthoringContractValidation,
            UnsupportedSystemSchedulerCapability.RuntimeMutation |
            UnsupportedSystemSchedulerCapability.EnableDisable |
            UnsupportedSystemSchedulerCapability.DependencyOrdering |
            UnsupportedSystemSchedulerCapability.HierarchicalGrouping |
            UnsupportedSystemSchedulerCapability.ThreadedExecution);

        private readonly List<SystemRegistration> _registrations = new();
        private readonly List<SystemRegistration> _executionPlan = new();
        private bool _initialized;
        private int _nextRegistrationOrder;

        /// <summary>
        /// 当前已注册系统数量。
        /// </summary>
        public int Count => _registrations.Count;

        /// <summary>
        /// 当前是否已完成初始化。
        /// </summary>
        public bool IsInitialized => _initialized;

        /// <summary>
        /// 当前系统层正式支持与不支持的调度边界。
        /// </summary>
        public SystemSchedulerBoundary Boundary => FlatPhasedOrderedBoundary;

        /// <summary>
        /// 注册系统。
        /// 初始化后不允许再动态增删系统；若需要调整系统集合，请先 `Shutdown(frame)`。
        /// </summary>
        public void Add(ISystem system)
        {
            ArgumentNullException.ThrowIfNull(system);

            if (_initialized)
            {
                throw new InvalidOperationException("Cannot add systems while scheduler is initialized. Call Shutdown(frame) first.");
            }

            for (int i = 0; i < _registrations.Count; i++)
            {
                if (ReferenceEquals(_registrations[i].System, system))
                {
                    throw new InvalidOperationException("System is already registered.");
                }
            }

            ValidateContract(system);
            _registrations.Add(new SystemRegistration(system, _nextRegistrationOrder++));
        }

        /// <summary>
        /// 移除系统。
        /// 初始化后不允许再动态增删系统；若需要调整系统集合，请先 `Shutdown(frame)`。
        /// </summary>
        public bool Remove(ISystem system)
        {
            ArgumentNullException.ThrowIfNull(system);

            if (_initialized)
            {
                throw new InvalidOperationException("Cannot remove systems while scheduler is initialized. Call Shutdown(frame) first.");
            }

            for (int i = 0; i < _registrations.Count; i++)
            {
                if (ReferenceEquals(_registrations[i].System, system))
                {
                    _registrations.RemoveAt(i);
                    return true;
                }
            }

            return false;
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

            _registrations.Clear();
            _executionPlan.Clear();
        }

        /// <summary>
        /// 初始化所有系统。
        /// 初始化后，当前系统集合会被冻结，直到显式执行 `Shutdown(frame)`。
        /// </summary>
        public void Initialize(Frame frame)
        {
            ArgumentNullException.ThrowIfNull(frame);

            if (_initialized)
            {
                return;
            }

            RebuildExecutionPlan();

            for (int i = 0; i < _executionPlan.Count; i++)
            {
                _executionPlan[i].System.OnInit(frame);
            }

            _initialized = true;
        }

        /// <summary>
        /// 按 phase 再按注册顺序更新所有系统。
        /// </summary>
        public void Update(Frame frame, FP deltaTime)
        {
            ArgumentNullException.ThrowIfNull(frame);

            if (!_initialized)
            {
                throw new InvalidOperationException("SystemScheduler must be initialized before update.");
            }

            for (int i = 0; i < _executionPlan.Count; i++)
            {
                ISystem system = _executionPlan[i].System;
                frame.BeginSystemAuthoringScope(system);
                try
                {
                    system.OnUpdate(frame, deltaTime);
                }
                finally
                {
                    frame.EndSystemAuthoringScope();
                }
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

            for (int i = 0; i < _executionPlan.Count; i++)
            {
                _executionPlan[i].System.OnDestroy(frame);
            }

            _initialized = false;
        }

        private void RebuildExecutionPlan()
        {
            _executionPlan.Clear();

            if (_registrations.Count == 0)
            {
                return;
            }

            _executionPlan.AddRange(_registrations);
            _executionPlan.Sort(static (left, right) =>
            {
                int phaseComparison = left.Phase.CompareTo(right.Phase);
                if (phaseComparison != 0)
                {
                    return phaseComparison;
                }

                return left.RegistrationOrder.CompareTo(right.RegistrationOrder);
            });
        }

        private static void ValidateContract(ISystem system)
        {
            system.Contract.ValidateForPhase(system.Phase, system.GetType());
        }

        private readonly struct SystemRegistration
        {
            public SystemRegistration(ISystem system, int registrationOrder)
            {
                System = system;
                RegistrationOrder = registrationOrder;
            }

            public ISystem System { get; }

            public SystemPhase Phase => System.Phase;

            public int RegistrationOrder { get; }
        }
    }
}
