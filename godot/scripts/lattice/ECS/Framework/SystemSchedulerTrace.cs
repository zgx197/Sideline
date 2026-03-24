// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

namespace Lattice.ECS.Framework
{
    /// <summary>
    /// 调度器 trace 事件阶段。
    /// </summary>
    public enum SystemSchedulerTracePhase
    {
        InitEnter,
        InitExit,
        EnabledEnter,
        EnabledExit,
        UpdateEnter,
        UpdateExit,
        DisabledEnter,
        DisabledExit,
        DisposeEnter,
        DisposeExit
    }

    /// <summary>
    /// 调度器 trace 只读事件。
    /// </summary>
    public readonly record struct SystemSchedulerTraceEvent(
        SystemNodeInfo Node,
        SystemSchedulerTracePhase Phase,
        SystemSchedulerState SchedulerState)
    {
        public ISystem System => Node.System;

        public string Name => Node.Name;

        public string TypeName => Node.TypeName;

        public int Order => Node.Order;

        public SystemExecutionKind Kind => Node.Kind;

        public string? Category => Node.Category;

        public string? DebugCategory => Node.DebugCategory;

        public int Depth => Node.Depth;

        public bool IsGroup => Node.IsGroup;

        public bool Initialized => Node.Initialized;

        public bool Active => Node.Active;
    }
}
