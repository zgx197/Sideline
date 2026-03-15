// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

using System;
using System.Runtime.CompilerServices;

namespace Lattice.ECS.Core
{
    /// <summary>
    /// 组件类型ID泛型单例，提供零开销的组件类型ID获取
    /// 对齐 FrameSync 设计，替代字典查找
    /// </summary>
    /// <typeparam name="T">组件类型，必须是 unmanaged 类型</typeparam>
    public static class ComponentTypeId<T> where T : unmanaged
    {
        /// <summary>
        /// 组件类型ID（编译期缓存，零开销获取）
        /// </summary>
        public static readonly int Id;

        /// <summary>
        /// 组件类型在 ComponentSet 中的块索引（index / 64）
        /// </summary>
        public static readonly int BlockIndex;

        /// <summary>组件类型在 ComponentSet 中的位偏移（index % 64）</summary>
        public static readonly int BitOffset;

        /// <summary>组件类型在 ComponentSet 中的位掩码</summary>
        public static readonly ulong BitMask;

        static ComponentTypeId()
        {
            Id = ComponentTypeRegistry.Global.Register<T>();
            BlockIndex = Id >> 6;        // Id / 64
            BitOffset = Id & 0x3F;       // Id % 64
            BitMask = 1UL << BitOffset;
        }
    }

    /// <summary>
    /// 全局组件类型注册表（单例）
    /// </summary>
    public sealed class ComponentTypeRegistry
    {
        /// <summary>
        /// 全局实例
        /// </summary>
        public static readonly ComponentTypeRegistry Global = new();

        private readonly object _lock = new();
        private readonly System.Collections.Generic.Dictionary<Type, int> _typeToId = new();
        private readonly System.Collections.Generic.Dictionary<int, Type> _idToType = new();
        private int _nextId;

        /// <summary>
        /// 创建注册表实例（主要用于测试，生产环境请使用 Global）
        /// </summary>
        public ComponentTypeRegistry() { }

        /// <summary>
        /// 注册组件类型，返回类型ID
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Register<T>() where T : unmanaged
        {
            var type = typeof(T);

            // 先不加锁检查，避免大部分情况下的锁开销
            if (_typeToId.TryGetValue(type, out int id))
                return id;

            lock (_lock)
            {
                // 双重检查
                if (_typeToId.TryGetValue(type, out id))
                    return id;

                id = _nextId++;
                if (id >= 512)
                    throw new InvalidOperationException($"组件类型数量超过最大限制 512，类型: {type.Name}");

                _typeToId[type] = id;
                _idToType[id] = type;
                return id;
            }
        }

        /// <summary>
        /// 通过ID获取类型
        /// </summary>
        public Type? GetType(int id)
        {
            _idToType.TryGetValue(id, out var type);
            return type;
        }

        /// <summary>
        /// 获取组件类型ID（向后兼容）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetTypeId<T>() where T : unmanaged
        {
            return Register<T>();
        }

        /// <summary>
        /// 已注册类型数量
        /// </summary>
        public int Count => _nextId;

        /// <summary>
        /// 清除所有注册（主要用于测试）
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                _typeToId.Clear();
                _idToType.Clear();
                _nextId = 0;
            }
        }
    }
}
