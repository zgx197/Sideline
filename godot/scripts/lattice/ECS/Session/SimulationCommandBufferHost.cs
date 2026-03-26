// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

using Lattice.ECS.Framework;

namespace Lattice.ECS.Session
{
    /// <summary>
    /// 为系统暴露当前模拟 tick 使用的命令缓冲。
    /// </summary>
    public sealed class SimulationCommandBufferHost
    {
        public CommandBuffer Buffer;
    }
}
