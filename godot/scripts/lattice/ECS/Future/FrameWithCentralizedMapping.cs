// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

// 这是未来可能的优化方向：将稀疏映射集中到 Frame 级别
// 当前仅作为设计参考，未实际使用

using System;
using System.Runtime.CompilerServices;
using Lattice.Core;
using Lattice.Math;

namespace Lattice.ECS.Future
{
    /// <summary>
    /// 集中式稀疏映射设计（FrameSync 风格）
    /// 
    /// 适用场景：
    /// 1. 需要极致的组件访问性能
    /// 2. 实体数量极大（10万+）
    /// 3. 频繁的跨组件查询
    /// </summary>
    public unsafe class FrameWithCentralizedMapping
    {
        #region 核心数据结构

        /// <summary>
        /// 实体数据 - 连续存储，缓存友好（FrameSync 风格）
        /// </summary>
        public struct EntitySlot
        {
            public Entity Ref;
            public int Version;
            public ComponentSet Components;
            public EntityFlags Flags;
        }

        /// <summary>
        /// 实体槽位数组（集中管理）
        /// </summary>
        private EntitySlot[] _slots;

        /// <summary>
        /// 组件存储数组（按 TypeId 索引）
        /// </summary>
        private IComponentStorage[] _storages;

        #endregion

        #region 性能优势说明

        /*
         * 当前 Lattice（分散式）：
         * 
         * Frame
         * ├── EntityRegistry (实体管理)
         * ├── Dictionary<TypeId, Storage> (存储查找)
         * │   └── StorageA: 稀疏数组 Entity->Component
         * │   └── StorageB: 稀疏数组 Entity->Component
         * └── 访问路径：
         *     Type -> Dictionary[TypeId] -> Storage -> Storage._sparseBlockIndex[Entity]
         * 
         * 集中式（FrameSync 风格）：
         * 
         * Frame
         * ├── EntitySlot[] (连续数组，包含 ComponentSet)
         * ├── Storage[] (按 TypeId 索引的数组)
         * │   └── StorageA: 连续数组，EntityIndex -> Component
         * │   └── StorageB: 连续数组，EntityIndex -> Component
         * └── 访问路径：
         *     Entity.Index -> _slots[Entity.Index] -> _storages[TypeId] -> Component
         * 
         * 关键差异：
         * 1. Dictionary 查找 -> 数组索引（快 5-10 倍）
         * 2. 分散存储 -> 连续存储（缓存命中率高 20-30%）
         * 3. 存储层级少一层（减少间接寻址）
         */

        #endregion
    }

    public unsafe delegate void ComponentAction<T>(Entity entity, T* component) where T : unmanaged;
}
