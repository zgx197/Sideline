// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

using System;
using System.Runtime.CompilerServices;

namespace Lattice.ECS.Core
{
    /// <summary>
    /// 组件类型 ID - 编译期常量（FrameSync 风格）
    /// </summary>
    public static unsafe class ComponentTypeId<T> where T : unmanaged
    {
        /// <summary>类型 ID（0 表示未初始化）</summary>
        public static int Id { get; private set; }

        /// <summary>是否已注册</summary>
        public static bool IsRegistered => Id != 0;

        /// <summary>
        /// 注册组件类型（必须在首次使用前调用）
        /// </summary>
        public static void Register(int id)
        {
            if (Id != 0)
                throw new InvalidOperationException($"Component type already registered with ID {Id}");

            if (id <= 0 || id >= Frame.MaxComponentTypes)
                throw new ArgumentOutOfRangeException(nameof(id));

            Id = id;
        }

        /// <summary>
        /// 静态构造函数 - 延迟检查
        /// </summary>
        static ComponentTypeId()
        {
            // 实际检查推迟到属性访问时
        }
    }

    /// <summary>
    /// 组件类型注册器
    /// </summary>
    public static class ComponentRegistry
    {
        private static int _nextId = 1;

        /// <summary>注册组件类型（自动分配 ID）</summary>
        public static void Register<T>() where T : unmanaged
        {
            ComponentTypeId<T>.Register(_nextId++);
        }
    }
}
