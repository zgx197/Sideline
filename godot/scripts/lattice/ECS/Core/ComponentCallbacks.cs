// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

using Lattice.Core;

namespace Lattice.ECS.Core
{
    /// <summary>
    /// 组件序列化委托
    /// </summary>
    public unsafe delegate void ComponentSerializeDelegate(void* component, byte* buffer, ref int offset);

    /// <summary>
    /// 组件添加回调委托
    /// </summary>
    public unsafe delegate void ComponentAddedDelegate(EntityRef entity, void* component, FrameBase* frame);

    /// <summary>
    /// 组件移除回调委托
    /// </summary>
    public unsafe delegate void ComponentRemovedDelegate(EntityRef entity, void* component, FrameBase* frame);

    /// <summary>
    /// 组件回调集合 - FrameSync 风格
    /// </summary>
    public readonly struct ComponentCallbacks
    {
        /// <summary>序列化回调</summary>
        public readonly ComponentSerializeDelegate? Serialize;

        /// <summary>组件添加回调</summary>
        public readonly ComponentAddedDelegate? OnAdded;

        /// <summary>组件移除回调</summary>
        public readonly ComponentRemovedDelegate? OnRemoved;

        /// <summary>
        /// 创建回调集合（仅序列化）
        /// </summary>
        public ComponentCallbacks(ComponentSerializeDelegate serialize)
        {
            Serialize = serialize;
            OnAdded = null;
            OnRemoved = null;
        }

        /// <summary>
        /// 创建回调集合
        /// </summary>
        public ComponentCallbacks(
            ComponentSerializeDelegate serialize,
            ComponentAddedDelegate? onAdded,
            ComponentRemovedDelegate? onRemoved)
        {
            Serialize = serialize;
            OnAdded = onAdded;
            OnRemoved = onRemoved;
        }

        /// <summary>
        /// 空回调
        /// </summary>
        public static unsafe ComponentCallbacks Empty => new(
            (void* ptr, byte* buf, ref int off) => { },
            null,
            null
        );
    }
}
