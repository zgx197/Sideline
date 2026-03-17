// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

using System;

namespace Lattice.ECS.Core
{
    /// <summary>
    /// 组件标志 - FrameSync 风格
    /// </summary>
    [Flags]
    public enum ComponentFlags : uint
    {
        /// <summary>无特殊标志</summary>
        None = 0,

        /// <summary>不序列化此组件（运行时临时数据）</summary>
        DontSerialize = 1 << 0,

        /// <summary>单例组件（每个 Frame 只有一个实例）</summary>
        Singleton = 1 << 1,

        /// <summary>预测时排除（只在验证帧执行）</summary>
        ExcludeFromPrediction = 1 << 2,

        /// <summary>在检查点中排除（不保存到快照）</summary>
        ExcludeFromCheckpoints = 1 << 3,

        /// <summary>组件变更时触发事件</summary>
        SignalOnChanged = 1 << 4,

        /// <summary>不清理（帧重置时保留）</summary>
        DontClearOnRollback = 1 << 5,

        /// <summary>在编辑器的帧转储中隐藏</summary>
        HideInDump = 1 << 6
    }
}
