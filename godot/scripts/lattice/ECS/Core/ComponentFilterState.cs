// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

using System;
using System.Runtime.CompilerServices;

namespace Lattice.ECS.Core
{
    /// <summary>
    /// 组件过滤状态，对齐 FrameSync ComponentFilterState 设计
    /// 封装位集过滤逻辑，配合迭代器批量过滤实体
    /// </summary>
    public unsafe struct ComponentFilterState
    {
        // 必需组件集合（实体必须拥有所有这些组件）
        private ComponentSet _requiredComponents;

        // 排除组件集合（实体不能拥有这些组件）
        private ComponentSet _excludedComponents;

        // 任意组件集合（实体必须拥有至少一个）- 用于 Or 查询
        private ComponentSet _anyComponents;

        // 是否启用 Any 检查
        private bool _hasAnyConstraint;

        /// <summary>
        /// 创建过滤状态
        /// </summary>
        public ComponentFilterState(in ComponentSet required, in ComponentSet excluded)
        {
            _requiredComponents = required;
            _excludedComponents = excluded;
            _anyComponents = default;
            _hasAnyConstraint = false;
        }

        /// <summary>
        /// 创建带有 Any 约束的过滤状态
        /// </summary>
        public ComponentFilterState(in ComponentSet required, in ComponentSet excluded, in ComponentSet any)
        {
            _requiredComponents = required;
            _excludedComponents = excluded;
            _anyComponents = any;
            _hasAnyConstraint = !any.IsEmpty;
        }

        /// <summary>
        /// 检查实体是否满足过滤条件
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Matches(in ComponentSet entityComponents)
        {
            // 检查必需组件：entity 必须是 required 的超集
            if (!_requiredComponents.IsSubsetOf(entityComponents))
                return false;

            // 检查排除组件：entity 和 excluded 不能有任何交集
            if (_excludedComponents.Overlaps(entityComponents))
                return false;

            // 检查任意组件（如果有）
            if (_hasAnyConstraint && !_anyComponents.Overlaps(entityComponents))
                return false;

            return true;
        }

        /// <summary>
        /// 快速检查：仅检查前 64 个组件类型
        /// 适用于大多数游戏只有少量组件类型的情况
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool MatchesFast(in ComponentSet64 entityComponents64)
        {
            // 如果 required 或 excluded 超出了前 64 位，需要完整检查
            if (_requiredComponents.Rank > 1 || _excludedComponents.Rank > 1)
                return false; // 通知调用者需要使用完整检查

            // 检查必需组件
            ComponentSet64 required64 = (ComponentSet64)_requiredComponents;
            if (!required64.IsSubsetOf(entityComponents64))
                return false;

            // 检查排除组件
            ComponentSet64 excluded64 = (ComponentSet64)_excludedComponents;
            if (excluded64.Overlaps(entityComponents64))
                return false;

            return true;
        }

        /// <summary>
        /// 检查实体是否满足条件（通过组件集指针）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Matches(ComponentSet* entityComponents)
        {
            return Matches(*entityComponents);
        }

        /// <summary>
        /// 获取必需组件集合
        /// </summary>
        public ComponentSet RequiredComponents => _requiredComponents;

        /// <summary>
        /// 获取排除组件集合
        /// </summary>
        public ComponentSet ExcludedComponents => _excludedComponents;

        /// <summary>
        /// 获取任意组件集合
        /// </summary>
        public ComponentSet AnyComponents => _anyComponents;

        /// <summary>
        /// 是否设置了 Any 约束
        /// </summary>
        public bool HasAnyConstraint => _hasAnyConstraint;
    }

    /// <summary>
    /// 查询过滤器构建器（简化查询创建）
    /// </summary>
    public struct QueryFilterBuilder
    {
        private ComponentSet _with;
        private ComponentSet _without;
        private ComponentSet _any;

        /// <summary>
        /// 添加必需组件
        /// </summary>
        public QueryFilterBuilder With<T>() where T : unmanaged, IComponent
        {
            _with.Add<T>();
            return this;
        }

        /// <summary>
        /// 添加多个必需组件
        /// </summary>
        public QueryFilterBuilder With(ComponentSet components)
        {
            _with.UnionWith(components);
            return this;
        }

        /// <summary>
        /// 添加排除组件
        /// </summary>
        public QueryFilterBuilder Without<T>() where T : unmanaged, IComponent
        {
            _without.Add<T>();
            return this;
        }

        /// <summary>
        /// 添加多个排除组件
        /// </summary>
        public QueryFilterBuilder Without(ComponentSet components)
        {
            _without.UnionWith(components);
            return this;
        }

        /// <summary>
        /// 添加任意组件（Or 条件）
        /// </summary>
        public QueryFilterBuilder WithAny<T>() where T : unmanaged, IComponent
        {
            _any.Add<T>();
            return this;
        }

        /// <summary>
        /// 添加多个任意组件
        /// </summary>
        public QueryFilterBuilder WithAny(ComponentSet components)
        {
            _any.UnionWith(components);
            return this;
        }

        /// <summary>
        /// 构建过滤状态
        /// </summary>
        public ComponentFilterState Build()
        {
            return new ComponentFilterState(_with, _without, _any);
        }

        /// <summary>
        /// 隐式转换为过滤状态
        /// </summary>
        public static implicit operator ComponentFilterState(QueryFilterBuilder builder)
        {
            return builder.Build();
        }
    }
}
