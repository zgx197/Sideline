// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

using Lattice.ECS.Core;
using Lattice.Math;

namespace Lattice.ECS.Framework
{
    /// <summary>
    /// 系统基础基类。
    /// </summary>
    public abstract class SystemBase : ISystem
    {
        /// <summary>系统名称默认使用类型名。</summary>
        public virtual string Name => GetType().Name;

        /// <summary>系统元数据默认使用主线程系统配置。</summary>
        public virtual SystemMetadata Metadata => SystemMetadata.Default;

        /// <summary>系统默认启用。</summary>
        public virtual bool EnabledByDefault => true;

        /// <summary>
        /// 系统初始化。
        /// </summary>
        public virtual void OnInit(Frame frame)
        {
        }

        /// <summary>
        /// 系统进入有效启用状态。
        /// </summary>
        public virtual void OnEnabled(Frame frame)
        {
        }

        /// <summary>
        /// 系统退出有效启用状态。
        /// </summary>
        public virtual void OnDisabled(Frame frame)
        {
        }

        /// <summary>
        /// 系统逐帧更新。
        /// </summary>
        public abstract void OnUpdate(Frame frame, FP deltaTime);

        /// <summary>
        /// 系统销毁。
        /// </summary>
        public virtual void OnDispose(Frame frame)
        {
        }

        /// <summary>
        /// 与当前最小 `ISystem` 生命周期契约对齐的销毁入口。
        /// </summary>
        public virtual void OnDestroy(Frame frame)
        {
            OnDispose(frame);
        }
    }
}
