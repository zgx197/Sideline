// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

using Lattice.ECS.Core;
using Lattice.Math;

namespace Lattice.ECS.Framework
{
    /// <summary>
    /// ECS 系统接口。
    /// </summary>
    public interface ISystem
    {
        /// <summary>系统名称，用于日志与调试。</summary>
        string Name { get; }

        /// <summary>系统元数据。</summary>
        SystemMetadata Metadata { get; }

        /// <summary>系统默认是否启用。</summary>
        bool EnabledByDefault { get; }

        /// <summary>系统初始化。</summary>
        void OnInit(Frame frame);

        /// <summary>系统变为有效启用状态时触发。</summary>
        void OnEnabled(Frame frame);

        /// <summary>系统失去有效启用状态时触发。</summary>
        void OnDisabled(Frame frame);

        /// <summary>系统逐帧更新。</summary>
        void OnUpdate(Frame frame, FP deltaTime);

        /// <summary>系统销毁。</summary>
        void OnDispose(Frame frame);
    }
}
