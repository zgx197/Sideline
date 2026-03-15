// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

using System;

namespace Lattice.ECS.Core
{
    /// <summary>
    /// 实体标志，对齐 FrameSync EntityFlags
    /// </summary>
    [Flags]
    public enum EntityFlags
    {
        /// <summary>无标志</summary>
        None = 0,

        /// <summary>实体不可裁剪（始终同步）</summary>
        NotCullable = 1 << 0,

        /// <summary>实体标记为待销毁</summary>
        DestroyPending = 1 << 1,

        /// <summary>实体已禁用（不参与更新）</summary>
        Disabled = 1 << 2,
    }
}
