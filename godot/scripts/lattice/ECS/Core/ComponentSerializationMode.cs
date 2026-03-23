// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

namespace Lattice.ECS.Core
{
    /// <summary>
    /// 组件序列化场景。
    /// </summary>
    public enum ComponentSerializationMode : byte
    {
        /// <summary>常规序列化。</summary>
        Default = 0,

        /// <summary>预测帧序列化。</summary>
        Prediction = 1,

        /// <summary>检查点/快照序列化。</summary>
        Checkpoint = 2,
    }
}
