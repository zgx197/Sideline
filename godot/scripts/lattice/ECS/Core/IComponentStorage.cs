// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

using Lattice.Core;

namespace Lattice.ECS.Core
{
    /// <summary>
    /// 组件存储非泛型接口 - 用于统一操作不同类型的组件存储
    /// </summary>
    public interface IComponentStorage
    {
        /// <summary>
        /// 删除指定实体的组件
        /// </summary>
        /// <param name="entity">实体引用</param>
        /// <returns>是否成功删除</returns>
        bool Remove(EntityRef entity);

        /// <summary>
        /// 检查存储中是否包含指定实体的组件
        /// </summary>
        bool Contains(EntityRef entity);

        /// <summary>
        /// 获取当前存储的组件数量
        /// </summary>
        int Count { get; }

        /// <summary>
        /// 清空所有组件
        /// </summary>
        void Clear();

        /// <summary>
        /// 获取存储的组件类型ID
        /// </summary>
        int TypeId { get; }

        /// <summary>
        /// 获取组件类型名称（用于调试）
        /// </summary>
        string TypeName { get; }
    }
}
