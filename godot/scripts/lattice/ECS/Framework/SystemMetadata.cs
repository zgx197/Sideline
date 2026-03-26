// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

namespace Lattice.ECS.Framework
{
    /// <summary>
    /// 系统执行类型。
    /// </summary>
    public enum SystemExecutionKind
    {
        MainThread,
        HotPath,
        ParallelReserved,
        Signal,
        Group
    }

    /// <summary>
    /// 系统元数据。
    /// </summary>
    public readonly record struct SystemMetadata(
        int Order,
        SystemExecutionKind Kind,
        string? Category,
        string? DebugCategory,
        bool AllowRuntimeToggle)
    {
        /// <summary>默认普通主线程系统元数据。</summary>
        public static SystemMetadata Default => new(
            Order: 0,
            Kind: SystemExecutionKind.MainThread,
            Category: null,
            DebugCategory: null,
            AllowRuntimeToggle: true);

        /// <summary>默认系统组元数据。</summary>
        public static SystemMetadata GroupDefault => new(
            Order: 0,
            Kind: SystemExecutionKind.Group,
            Category: "Group",
            DebugCategory: "Group",
            AllowRuntimeToggle: true);
    }
}
