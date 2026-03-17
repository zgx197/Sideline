// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Lattice.Core
{
    /// <summary>
    /// 实体调试代理 - 提供丰富的调试体验
    ///
    /// 设计参考：FrameSync的DebuggerProxy
    /// </summary>
    public static class EntityDebugger
    {
        /// <summary>
        /// 获取实体名称的自定义委托
        /// 可由用户设置以提供更有意义的实体名称
        /// </summary>
        public delegate string EntityNameProviderDelegate(EntityRef entity, EntityRegistry? registry);

        /// <summary>
        /// 全局实体名称提供器
        /// </summary>
        public static EntityNameProviderDelegate? EntityNameProvider { get; set; }

        /// <summary>
        /// 获取实体的显示名称
        /// </summary>
        public static string GetName(EntityRef entity, EntityRegistry? registry = null)
        {
            if (EntityNameProvider != null)
            {
                try
                {
                    var name = EntityNameProvider(entity, registry);
                    if (!string.IsNullOrEmpty(name))
                        return name;
                }
                catch
                {
                    // 忽略用户委托的异常
                }
            }

            return entity.ToString();
        }

        /// <summary>
        /// 获取实体的详细调试信息
        /// </summary>
        public static string GetDetailedInfo(EntityRef entity, EntityRegistry registry)
        {
            if (!registry.IsValid(entity))
            {
                return $"EntityRef {entity} is INVALID";
            }

            var info = registry.GetDebugInfo(entity);
            var components = new List<string>();

            return $"EntityRef({entity.Index}, {entity.Version})\n" +
                   $"  Created: {info.CreationTime}\n" +
                   $"  Components: {string.Join(", ", components)}";
        }

        /// <summary>
        /// 验证实体引用有效性（调试版本带详细信息）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AssertValid(EntityRef entity, EntityRegistry registry, string message = "")
        {
#if DEBUG
            if (!registry.IsValid(entity))
            {
                throw new InvalidOperationException(
                    $"Invalid EntityRef {entity}: {message}\n{GetDetailedInfo(entity, registry)}");
            }
#endif
        }
    }
}
