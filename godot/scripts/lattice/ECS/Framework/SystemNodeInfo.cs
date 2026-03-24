// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

namespace Lattice.ECS.Framework
{
    /// <summary>
    /// 系统节点只读快照。
    /// </summary>
    public readonly record struct SystemNodeInfo(
        ISystem System,
        string Name,
        string TypeName,
        int Order,
        SystemExecutionKind Kind,
        string? Category,
        string? DebugCategory,
        bool AllowRuntimeToggle,
        string? ParentName,
        int Depth,
        bool IsGroup,
        bool IsRoot,
        bool EnabledSelf,
        bool EnabledInHierarchy,
        bool Initialized,
        bool Active);
}
