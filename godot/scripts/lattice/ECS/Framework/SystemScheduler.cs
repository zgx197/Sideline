// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

using System;
using System.Collections.Generic;
using Lattice.ECS.Core;
using Lattice.Math;

namespace Lattice.ECS.Framework
{
    /// <summary>
    /// 调度器运行状态。
    /// </summary>
    public enum SystemSchedulerState
    {
        Uninitialized,
        Initializing,
        Ready,
        Updating,
        Disposing
    }

    /// <summary>
    /// 单线程 ECS 系统调度器。
    /// </summary>
    public sealed class SystemScheduler
    {
        private sealed class SystemNode
        {
            public required ISystem System { get; init; }

            public required SystemNode? Parent { get; init; }

            public required bool IsGroup { get; init; }

            public required int Depth { get; init; }

            public required int RegistrationIndex { get; init; }

            public bool EnabledSelf { get; set; }

            public bool Initialized { get; set; }

            public bool Active { get; set; }

            public List<SystemNode>? Children { get; set; }
        }

        private readonly List<SystemNode> _orderedNodes = new();
        private readonly List<SystemNode> _rootNodes = new();
        private readonly Dictionary<ISystem, SystemNode> _nodesByInstance = new(ReferenceEqualityComparer.Instance);
        private SystemSchedulerState _state;
        private Frame? _currentFrame;
        private int _nextRegistrationIndex;

        /// <summary>当前是否已初始化。</summary>
        public bool IsInitialized => _state == SystemSchedulerState.Ready;

        /// <summary>当前调度器状态。</summary>
        public SystemSchedulerState State => _state;

        /// <summary>当前正在执行生命周期或更新回调的系统。</summary>
        public SystemNodeInfo? CurrentSystem { get; private set; }

        /// <summary>调度 trace 钩子，可用于 profiler 或调试观察。</summary>
        public Action<SystemSchedulerTraceEvent>? Trace { get; set; }

        /// <summary>已注册系统数量（包含组）。</summary>
        public int Count => _orderedNodes.Count;

        /// <summary>
        /// 添加系统。
        /// </summary>
        public void Add(ISystem system)
        {
            ArgumentNullException.ThrowIfNull(system);
            EnsureMutationAllowed("Add");

            RegisterNode(system, parent: null, depth: 0);

            if (_state == SystemSchedulerState.Ready && _currentFrame != null)
            {
                InitializePendingNodes(_currentFrame);
            }
        }

        /// <summary>
        /// 批量添加系统。
        /// </summary>
        public void AddRange(IEnumerable<ISystem> systems)
        {
            ArgumentNullException.ThrowIfNull(systems);

            foreach (ISystem system in systems)
            {
                Add(system);
            }
        }

        /// <summary>
        /// 初始化已注册系统。
        /// </summary>
        public void Initialize(Frame frame)
        {
            ArgumentNullException.ThrowIfNull(frame);
            if (_state != SystemSchedulerState.Uninitialized)
            {
                throw new InvalidOperationException("SystemScheduler 只能在未初始化状态下调用 Initialize。");
            }

            _currentFrame = frame;
            _state = SystemSchedulerState.Initializing;

            try
            {
                InitializePendingNodes(frame);
                _state = SystemSchedulerState.Ready;
            }
            catch
            {
                _state = SystemSchedulerState.Uninitialized;
                throw;
            }
        }

        /// <summary>
        /// 执行一次系统更新。
        /// </summary>
        public void Update(Frame frame, FP deltaTime)
        {
            ArgumentNullException.ThrowIfNull(frame);

            if (_state != SystemSchedulerState.Ready)
            {
                throw new InvalidOperationException("SystemScheduler 必须处于 Ready 状态才能调用 Update。");
            }

            _currentFrame = frame;
            _state = SystemSchedulerState.Updating;

            try
            {
                for (int i = 0; i < _orderedNodes.Count; i++)
                {
                    SystemNode node = _orderedNodes[i];
                    if (node.IsGroup || !node.Active)
                    {
                        continue;
                    }

                    InvokeUpdate(node, frame, deltaTime);
                }
            }
            finally
            {
                _state = SystemSchedulerState.Ready;
            }
        }

        /// <summary>
        /// 销毁所有已初始化系统。
        /// </summary>
        public void Dispose(Frame frame)
        {
            ArgumentNullException.ThrowIfNull(frame);
            if (_state != SystemSchedulerState.Ready)
            {
                throw new InvalidOperationException("SystemScheduler 必须处于 Ready 状态才能调用 Dispose。");
            }

            _state = SystemSchedulerState.Disposing;

            try
            {
                for (int i = _orderedNodes.Count - 1; i >= 0; i--)
                {
                    SystemNode node = _orderedNodes[i];
                    if (!node.Initialized)
                    {
                        continue;
                    }

                    if (node.Active)
                    {
                        InvokeOnDisabled(node, frame);
                    }

                    InvokeOnDispose(node, frame);
                }
            }
            finally
            {
                _state = SystemSchedulerState.Uninitialized;
                _currentFrame = null;
                CurrentSystem = null;
            }
        }

        /// <summary>
        /// 启用指定系统实例。
        /// </summary>
        public void Enable(ISystem system)
        {
            SetEnabled(system, enabled: true);
        }

        /// <summary>
        /// 禁用指定系统实例。
        /// </summary>
        public void Disable(ISystem system)
        {
            SetEnabled(system, enabled: false);
        }

        /// <summary>
        /// 判断指定系统实例当前是否在层级内启用。
        /// </summary>
        public bool IsEnabled(ISystem system)
        {
            return IsEnabledInHierarchy(GetRequiredNode(system));
        }

        /// <summary>
        /// 导出当前系统树的稳定快照（注册顺序）。
        /// </summary>
        public SystemNodeInfo[] GetNodes()
        {
            var result = new SystemNodeInfo[_orderedNodes.Count];
            for (int i = 0; i < _orderedNodes.Count; i++)
            {
                result[i] = CreateNodeInfo(_orderedNodes[i]);
            }

            return result;
        }

        /// <summary>
        /// 导出当前系统执行序列快照（仅非组系统）。
        /// </summary>
        public SystemNodeInfo[] GetExecutionOrder()
        {
            var result = new List<SystemNodeInfo>(_orderedNodes.Count);
            for (int i = 0; i < _orderedNodes.Count; i++)
            {
                SystemNode node = _orderedNodes[i];
                if (!node.IsGroup)
                {
                    result.Add(CreateNodeInfo(node));
                }
            }

            return result.ToArray();
        }

        /// <summary>
        /// 按实例查询系统节点。
        /// </summary>
        public bool TryGetNode(ISystem system, out SystemNodeInfo info)
        {
            ArgumentNullException.ThrowIfNull(system);

            if (_nodesByInstance.TryGetValue(system, out SystemNode? node))
            {
                info = CreateNodeInfo(node);
                return true;
            }

            info = default;
            return false;
        }

        /// <summary>
        /// 按名称查询系统节点。
        /// </summary>
        public SystemNodeInfo[] GetNodes(string name)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);

            var result = new List<SystemNodeInfo>();
            for (int i = 0; i < _orderedNodes.Count; i++)
            {
                SystemNode node = _orderedNodes[i];
                if (string.Equals(node.System.Name, name, StringComparison.Ordinal))
                {
                    result.Add(CreateNodeInfo(node));
                }
            }

            return result.ToArray();
        }

        /// <summary>
        /// 按类型查询系统节点。
        /// </summary>
        public SystemNodeInfo[] GetNodes<T>() where T : ISystem
        {
            var result = new List<SystemNodeInfo>();
            for (int i = 0; i < _orderedNodes.Count; i++)
            {
                SystemNode node = _orderedNodes[i];
                if (node.System is T)
                {
                    result.Add(CreateNodeInfo(node));
                }
            }

            return result.ToArray();
        }

        /// <summary>
        /// 启用所有指定类型系统。
        /// </summary>
        public void Enable<T>() where T : ISystem
        {
            SetEnabledByType<T>(enabled: true);
        }

        /// <summary>
        /// 禁用所有指定类型系统。
        /// </summary>
        public void Disable<T>() where T : ISystem
        {
            SetEnabledByType<T>(enabled: false);
        }

        /// <summary>
        /// 判断指定类型系统是否全部处于层级启用状态。
        /// </summary>
        public bool IsEnabled<T>() where T : ISystem
        {
            bool found = false;

            for (int i = 0; i < _orderedNodes.Count; i++)
            {
                SystemNode node = _orderedNodes[i];
                if (node.System is not T)
                {
                    continue;
                }

                found = true;
                if (!IsEnabledInHierarchy(node))
                {
                    return false;
                }
            }

            return found;
        }

        private void SetEnabledByType<T>(bool enabled) where T : ISystem
        {
            EnsureMutationAllowed(enabled ? "Enable<T>" : "Disable<T>");
            bool found = false;
            bool anyChanged = false;

            if (_state == SystemSchedulerState.Ready)
            {
                for (int i = 0; i < _orderedNodes.Count; i++)
                {
                    SystemNode node = _orderedNodes[i];
                    if (node.System is T && node.EnabledSelf != enabled)
                    {
                        EnsureRuntimeToggleAllowed(node);
                    }
                }
            }

            for (int i = 0; i < _orderedNodes.Count; i++)
            {
                SystemNode node = _orderedNodes[i];
                if (node.System is T)
                {
                    found = true;
                    if (node.EnabledSelf == enabled)
                    {
                        continue;
                    }

                    node.EnabledSelf = enabled;
                    anyChanged = true;
                }
            }

            if (!found)
            {
                throw new InvalidOperationException($"未找到系统类型 {typeof(T).Name}。");
            }

            if (anyChanged && _state == SystemSchedulerState.Ready && _currentFrame != null)
            {
                ApplyActiveStateTransitions(_currentFrame);
            }
        }

        private void RegisterNode(ISystem system, SystemNode? parent, int depth)
        {
            if (_nodesByInstance.ContainsKey(system))
            {
                throw new InvalidOperationException($"系统实例 '{system.Name}' 已注册，不能重复添加。");
            }

            bool isGroup = system is SystemGroup;
            EnsureExecutionKindSupported(system, isGroup);
            var node = new SystemNode
            {
                System = system,
                Parent = parent,
                IsGroup = isGroup,
                Depth = depth,
                RegistrationIndex = _nextRegistrationIndex++,
                EnabledSelf = system.EnabledByDefault,
                Initialized = false,
                Active = false,
                Children = null
            };

            _nodesByInstance.Add(system, node);
            if (parent == null)
            {
                _rootNodes.Add(node);
            }
            else
            {
                parent.Children ??= new List<SystemNode>();
                parent.Children.Add(node);
            }

            if (system is not SystemGroup group)
            {
                RebuildOrderedNodes();
                return;
            }

            IReadOnlyList<ISystem> children = group.Systems;
            for (int i = 0; i < children.Count; i++)
            {
                RegisterNode(children[i], node, depth + 1);
            }

            RebuildOrderedNodes();
        }

        private void InitializePendingNodes(Frame frame)
        {
            for (int i = 0; i < _orderedNodes.Count; i++)
            {
                SystemNode node = _orderedNodes[i];
                if (node.Initialized)
                {
                    continue;
                }

                InvokeOnInit(node, frame);
            }

            ApplyActiveStateTransitions(frame);
        }

        private SystemNode GetRequiredNode(ISystem system)
        {
            ArgumentNullException.ThrowIfNull(system);

            if (_nodesByInstance.TryGetValue(system, out SystemNode? node))
            {
                return node;
            }

            throw new InvalidOperationException($"系统实例 '{system.Name}' 尚未注册。");
        }

        private static bool IsEnabledInHierarchy(SystemNode node)
        {
            SystemNode? current = node;
            while (current != null)
            {
                if (!current.EnabledSelf)
                {
                    return false;
                }

                current = current.Parent;
            }

            return true;
        }

        private void SetEnabled(ISystem system, bool enabled)
        {
            EnsureMutationAllowed(enabled ? "Enable" : "Disable");

            SystemNode node = GetRequiredNode(system);
            if (_state == SystemSchedulerState.Ready && node.EnabledSelf != enabled)
            {
                EnsureRuntimeToggleAllowed(node);
            }

            if (node.EnabledSelf == enabled)
            {
                return;
            }

            node.EnabledSelf = enabled;

            if (_state == SystemSchedulerState.Ready && _currentFrame != null)
            {
                ApplyActiveStateTransitions(_currentFrame);
            }
        }

        private void ApplyActiveStateTransitions(Frame frame)
        {
            bool[] nextActive = new bool[_orderedNodes.Count];
            for (int i = 0; i < _orderedNodes.Count; i++)
            {
                SystemNode node = _orderedNodes[i];
                nextActive[i] = node.Initialized && IsEnabledInHierarchy(node);
            }

            for (int i = _orderedNodes.Count - 1; i >= 0; i--)
            {
                SystemNode node = _orderedNodes[i];
                if (node.Active && !nextActive[i])
                {
                    InvokeOnDisabled(node, frame);
                }
            }

            for (int i = 0; i < _orderedNodes.Count; i++)
            {
                SystemNode node = _orderedNodes[i];
                if (!node.Active && nextActive[i])
                {
                    InvokeOnEnabled(node, frame);
                }
            }
        }

        private void InvokeOnInit(SystemNode node, Frame frame)
        {
            try
            {
                SetCurrentSystem(node);
                EmitTrace(SystemSchedulerTracePhase.InitEnter);
                node.System.OnInit(frame);
                node.Initialized = true;
                SetCurrentSystem(node);
                EmitTrace(SystemSchedulerTracePhase.InitExit);
            }
            finally
            {
                CurrentSystem = null;
            }
        }

        private void InvokeOnEnabled(SystemNode node, Frame frame)
        {
            try
            {
                SetCurrentSystem(node);
                EmitTrace(SystemSchedulerTracePhase.EnabledEnter);
                node.System.OnEnabled(frame);
                node.Active = true;
                SetCurrentSystem(node);
                EmitTrace(SystemSchedulerTracePhase.EnabledExit);
            }
            finally
            {
                CurrentSystem = null;
            }
        }

        private void InvokeOnDisabled(SystemNode node, Frame frame)
        {
            try
            {
                SetCurrentSystem(node);
                EmitTrace(SystemSchedulerTracePhase.DisabledEnter);
                node.System.OnDisabled(frame);
                node.Active = false;
                SetCurrentSystem(node);
                EmitTrace(SystemSchedulerTracePhase.DisabledExit);
            }
            finally
            {
                CurrentSystem = null;
            }
        }

        private void InvokeUpdate(SystemNode node, Frame frame, FP deltaTime)
        {
            try
            {
                SetCurrentSystem(node);
                EmitTrace(SystemSchedulerTracePhase.UpdateEnter);
                node.System.OnUpdate(frame, deltaTime);
                SetCurrentSystem(node);
                EmitTrace(SystemSchedulerTracePhase.UpdateExit);
            }
            finally
            {
                CurrentSystem = null;
            }
        }

        private void InvokeOnDispose(SystemNode node, Frame frame)
        {
            try
            {
                SetCurrentSystem(node);
                EmitTrace(SystemSchedulerTracePhase.DisposeEnter);
                node.System.OnDispose(frame);
                node.Initialized = false;
                SetCurrentSystem(node);
                EmitTrace(SystemSchedulerTracePhase.DisposeExit);
            }
            finally
            {
                CurrentSystem = null;
            }
        }

        private SystemNodeInfo CreateNodeInfo(SystemNode node)
        {
            SystemMetadata metadata = node.System.Metadata;
            return new SystemNodeInfo(
                node.System,
                node.System.Name,
                node.System.GetType().Name,
                metadata.Order,
                metadata.Kind,
                metadata.Category,
                metadata.DebugCategory,
                metadata.AllowRuntimeToggle,
                node.Parent?.System.Name,
                node.Depth,
                node.IsGroup,
                node.Parent == null,
                node.EnabledSelf,
                IsEnabledInHierarchy(node),
                node.Initialized,
                node.Active);
        }

        private void SetCurrentSystem(SystemNode node)
        {
            CurrentSystem = CreateNodeInfo(node);
        }

        private void EmitTrace(SystemSchedulerTracePhase phase)
        {
            Action<SystemSchedulerTraceEvent>? trace = Trace;
            if (trace == null || CurrentSystem is not SystemNodeInfo current)
            {
                return;
            }

            trace(new SystemSchedulerTraceEvent(current, phase, _state));
        }

        private void EnsureMutationAllowed(string operation)
        {
            if (_state is SystemSchedulerState.Initializing or SystemSchedulerState.Updating or SystemSchedulerState.Disposing)
            {
                throw new InvalidOperationException($"SystemScheduler 在 {_state} 状态下不允许执行 {operation}。");
            }
        }

        private void RebuildOrderedNodes()
        {
            _orderedNodes.Clear();

            foreach (SystemNode node in EnumerateInStableOrder(_rootNodes))
            {
                AppendNodeRecursive(node);
            }
        }

        private void AppendNodeRecursive(SystemNode node)
        {
            _orderedNodes.Add(node);

            if (node.Children == null || node.Children.Count == 0)
            {
                return;
            }

            foreach (SystemNode child in EnumerateInStableOrder(node.Children))
            {
                AppendNodeRecursive(child);
            }
        }

        private static IEnumerable<SystemNode> EnumerateInStableOrder(List<SystemNode> nodes)
        {
            var ordered = new List<SystemNode>(nodes);
            ordered.Sort(CompareNodes);
            return ordered;
        }

        private static int CompareNodes(SystemNode left, SystemNode right)
        {
            int byOrder = left.System.Metadata.Order.CompareTo(right.System.Metadata.Order);
            if (byOrder != 0)
            {
                return byOrder;
            }

            return left.RegistrationIndex.CompareTo(right.RegistrationIndex);
        }

        private static void EnsureRuntimeToggleAllowed(SystemNode node)
        {
            if (!node.System.Metadata.AllowRuntimeToggle)
            {
                throw new InvalidOperationException(
                    $"系统实例 '{node.System.Name}' 禁止在运行时切换启停。");
            }
        }

        private static void EnsureExecutionKindSupported(ISystem system, bool isGroup)
        {
            if (!isGroup && system.Metadata.Kind == SystemExecutionKind.ParallelReserved)
            {
                throw new InvalidOperationException(
                    $"系统实例 '{system.Name}' 声明为 ParallelReserved，但当前 SystemScheduler 只支持 MainThread、HotPath、Signal 与 Group。");
            }
        }
    }
}
