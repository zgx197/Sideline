// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Lattice.ECS.Core
{
    /// <summary>
    /// 组件类型注册表 - FrameSync 风格
    /// 
    /// 管理所有组件类型的元数据：
    /// - 类型 ID（从 1 开始，0 保留为 null）
    /// - 类型大小
    /// - 组件标志
    /// - 回调（序列化/生命周期）
    /// </summary>
    public sealed class ComponentTypeRegistry
    {
        #region 字段

        /// <summary>全局单例实例</summary>
        public static readonly ComponentTypeRegistry Global = new();

        /// <summary>Type -> ID 映射（确定性）</summary>
        private DeterministicTypeMap<int> _typeToId = new();

        /// <summary>ID -> Type 映射（数组实现，O(1)访问）</summary>
        private List<Type> _idToType = new();

        /// <summary>类型名称 -> ID 映射（构建阶段使用普通字典，完成后冻结）</summary>
        private Dictionary<string, int> _nameToId = new();

        /// <summary>类型元数据数组（索引 0 保留为 null）</summary>
        private ComponentTypeInfo[] _typeInfos = new ComponentTypeInfo[16];

        /// <summary>下一个可用的类型 ID（从 1 开始）</summary>
        private int _nextId = 1;

        /// <summary>线程锁</summary>
        private readonly object _lock = new();

        #endregion

        #region 属性

        /// <summary>已注册的组件类型数量（不包括索引 0）</summary>
        public int Count => _nextId - 1;

        /// <summary>最大支持的组件类型数</summary>
        public int MaxTypes => ComponentSet.MaxComponents;

        #endregion

        #region Builder 模式

        /// <summary>
        /// 创建类型注册 Builder - FrameSync 风格
        /// </summary>
        public Builder CreateBuilder(int expectedTypeCount = 64)
        {
            // 重置确定性集合
            _typeToId = new DeterministicTypeMap<int>(expectedTypeCount);
            _nameToId = new Dictionary<string, int>(expectedTypeCount);
            _idToType = new List<Type>(expectedTypeCount);
            return new Builder(this, expectedTypeCount);
        }

        /// <summary>
        /// 组件类型注册 Builder
        /// </summary>
        public readonly struct Builder
        {
            private readonly ComponentTypeRegistry _registry;

            internal Builder(ComponentTypeRegistry registry, int expectedTypeCount)
            {
                _registry = registry;
                _registry.EnsureCapacity(expectedTypeCount + 1); // +1 因为索引 0 保留
            }

            /// <summary>
            /// 添加组件类型
            /// </summary>
            /// <typeparam name="T">组件类型</typeparam>
            /// <param name="callbacks">回调集合</param>
            /// <param name="flags">组件标志</param>
            public Builder Add<T>(ComponentCallbacks callbacks, ComponentFlags flags = ComponentFlags.None)
                where T : unmanaged, IComponent
            {
                _registry.RegisterInternal<T>(callbacks, flags);
                return this;
            }

            /// <summary>
            /// 添加组件类型（简化版）
            /// </summary>
            public Builder Add<T>(ComponentSerializeDelegate serialize,
                ComponentAddedDelegate onAdded = null,
                ComponentRemovedDelegate onRemoved = null,
                ComponentFlags flags = ComponentFlags.None)
                where T : unmanaged, IComponent
            {
                return Add<T>(new ComponentCallbacks(serialize, onAdded, onRemoved), flags);
            }

            /// <summary>
            /// 完成注册 - 冻结确定性集合
            /// </summary>
            public void Finish()
            {
                // 冻结字典，确保后续遍历顺序固定
                _registry._typeToId.Freeze();
                // _nameToId 不冻结，仅用于查找

                // 验证所有类型已正确注册
                _registry.ValidateAllTypesRegistered();
            }
        }

        #endregion

        #region 注册方法

        /// <summary>
        /// 注册组件类型（内部实现）
        /// </summary>
        private unsafe void RegisterInternal<T>(ComponentCallbacks callbacks, ComponentFlags flags)
            where T : unmanaged, IComponent
        {
            var type = typeof(T);

            lock (_lock)
            {
                if (_typeToId.TryGetValue(type, out int existingId))
                {
                    throw new InvalidOperationException(
                        $"Component type {type.Name} is already registered with ID {existingId}");
                }

                int id = _nextId++;
                if (id >= _typeInfos.Length)
                {
                    EnsureCapacity(_typeInfos.Length * 2);
                }

                // 创建类型信息
                _typeInfos[id] = new ComponentTypeInfo
                {
                    Id = id,
                    Type = type,
                    Name = type.Name,
                    Size = sizeof(T),
                    Flags = flags,
                    Callbacks = callbacks,
                    BlockIndex = id >> 6,        // id / 64
                    BitOffset = id & 0x3F,       // id % 64
                    BitMask = 1UL << (id & 0x3F)
                };

                _typeToId.Add(type, id);
                _idToType.Add(type);
                _nameToId.Add(type.Name, id);

                // 设置 ComponentTypeId<T> 的静态字段（通过反射或预编译生成）
                ComponentTypeId<T>.Initialize(id, sizeof(T), flags, callbacks);
            }
        }

        /// <summary>
        /// 注册组件类型（自动方式，懒加载）
        /// </summary>
        public unsafe int Register<T>() where T : unmanaged, IComponent
        {
            var type = typeof(T);

            lock (_lock)
            {
                if (_typeToId.TryGetValue(type, out int existingId))
                    return existingId;

                int id = _nextId++;
                if (id >= _typeInfos.Length)
                {
                    EnsureCapacity(_typeInfos.Length * 2);
                }

                // 创建默认类型信息（无回调）
                _typeInfos[id] = new ComponentTypeInfo
                {
                    Id = id,
                    Type = type,
                    Name = type.Name,
                    Size = sizeof(T),
                    Flags = ComponentFlags.None,
                    Callbacks = ComponentCallbacks.Empty,
                    BlockIndex = id >> 6,
                    BitOffset = id & 0x3F,
                    BitMask = 1UL << (id & 0x3F)
                };

                _typeToId.Add(type, id);
                _idToType.Add(type);
                _nameToId.Add(type.Name, id);

                ComponentTypeId<T>.Initialize(id, sizeof(T), ComponentFlags.None, ComponentCallbacks.Empty);

                return id;
            }
        }

        #endregion

        #region 查询方法

        /// <summary>
        /// 获取类型 ID（必须已注册）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetTypeId<T>() where T : unmanaged, IComponent
        {
            return ComponentTypeId<T>.Id;
        }

        /// <summary>
        /// 根据 Type 获取 ID
        /// </summary>
        public int GetTypeId(Type type)
        {
            if (_typeToId.TryGetValue(type, out int id))
                return id;
            throw new KeyNotFoundException($"Component type {type.Name} is not registered");
        }

        /// <summary>
        /// 根据类型名称获取 ID
        /// </summary>
        public int GetTypeId(string typeName)
        {
            if (_nameToId.TryGetValue(typeName, out int id))
                return id;
            throw new KeyNotFoundException($"Component type with name '{typeName}' is not registered");
        }

        /// <summary>
        /// 根据 ID 获取 Type
        /// </summary>
        public Type GetType(int id)
        {
            if (id <= 0 || id >= _nextId)
                throw new ArgumentOutOfRangeException(nameof(id), $"Invalid component type ID: {id}");

            return _idToType[id - 1]; // id 从 1 开始，List 从 0 开始
        }

        /// <summary>
        /// 根据 ID 获取类型名称
        /// </summary>
        public string GetTypeName(int id)
        {
            if (id <= 0 || id >= _nextId)
                return null;

            return _typeInfos[id].Name;
        }

        /// <summary>
        /// 获取类型信息
        /// </summary>
        public ref ComponentTypeInfo GetTypeInfo(int id)
        {
            if (id <= 0 || id >= _nextId)
                throw new ArgumentOutOfRangeException(nameof(id));

            return ref _typeInfos[id];
        }

        /// <summary>
        /// 获取类型大小
        /// </summary>
        public int GetTypeSize(int id)
        {
            if (id <= 0 || id >= _nextId)
                throw new ArgumentOutOfRangeException(nameof(id));

            return _typeInfos[id].Size;
        }

        /// <summary>
        /// 获取类型标志
        /// </summary>
        public ComponentFlags GetTypeFlags(int id)
        {
            if (id <= 0 || id >= _nextId)
                throw new ArgumentOutOfRangeException(nameof(id));

            return _typeInfos[id].Flags;
        }

        /// <summary>
        /// 获取类型回调
        /// </summary>
        public ComponentCallbacks GetTypeCallbacks(int id)
        {
            if (id <= 0 || id >= _nextId)
                throw new ArgumentOutOfRangeException(nameof(id));

            return _typeInfos[id].Callbacks;
        }

        /// <summary>
        /// 检查类型是否已注册
        /// </summary>
        public bool IsRegistered<T>() where T : unmanaged, IComponent
        {
            return _typeToId.ContainsKey(typeof(T));
        }

        /// <summary>
        /// 检查类型是否已注册
        /// </summary>
        public bool IsRegistered(Type type)
        {
            return _typeToId.ContainsKey(type);
        }

        #endregion

        #region 辅助方法

        private void EnsureCapacity(int capacity)
        {
            if (capacity <= _typeInfos.Length)
                return;

            Array.Resize(ref _typeInfos, capacity);
        }

        private void ValidateAllTypesRegistered()
        {
            // 验证所有类型都有有效的回调
            for (int i = 1; i < _nextId; i++)
            {
                if (_typeInfos[i].Callbacks.Serialize == null)
                {
                    throw new InvalidOperationException(
                        $"Component type {_typeInfos[i].Name} (ID={i}) does not have a serialize callback");
                }
            }
        }

        #endregion
    }

    /// <summary>
    /// 组件类型信息结构
    /// </summary>
    public struct ComponentTypeInfo
    {
        /// <summary>类型 ID（从 1 开始）</summary>
        public int Id;

        /// <summary>CLR 类型</summary>
        public Type Type;

        /// <summary>类型名称（短名称）</summary>
        public string Name;

        /// <summary>类型大小（字节）</summary>
        public int Size;

        /// <summary>组件标志</summary>
        public ComponentFlags Flags;

        /// <summary>回调集合</summary>
        public ComponentCallbacks Callbacks;

        /// <summary>在 ComponentSet 中的块索引（Id / 64）</summary>
        public int BlockIndex;

        /// <summary>在 ComponentSet 中的位偏移（Id % 64）</summary>
        public int BitOffset;

        /// <summary>在 ComponentSet 中的位掩码</summary>
        public ulong BitMask;
    }

    /// <summary>
    /// 泛型组件类型 ID - 零开销类型安全访问
    /// </summary>
    public static class ComponentTypeId<T> where T : unmanaged, IComponent
    {
        #region 静态字段

        /// <summary>类型 ID（从 1 开始，0 表示未初始化）</summary>
        public static int Id { get; internal set; }

        /// <summary>类型大小（字节）</summary>
        public static int Size { get; internal set; }

        /// <summary>组件标志</summary>
        public static ComponentFlags Flags { get; internal set; }

        /// <summary>回调集合</summary>
        public static ComponentCallbacks Callbacks { get; internal set; }

        /// <summary>在 ComponentSet 中的块索引</summary>
        public static int BlockIndex { get; internal set; }

        /// <summary>在 ComponentSet 中的位偏移</summary>
        public static int BitOffset { get; internal set; }

        /// <summary>在 ComponentSet 中的位掩码</summary>
        public static ulong BitMask { get; internal set; }

        /// <summary>是否已初始化</summary>
        public static bool IsInitialized => Id > 0;

        #endregion

        #region 初始化

        /// <summary>
        /// 初始化类型 ID（由 ComponentTypeRegistry 调用）
        /// </summary>
        internal static void Initialize(int id, int size, ComponentFlags flags, ComponentCallbacks callbacks)
        {
            if (Id != 0)
                throw new InvalidOperationException($"ComponentTypeId<{typeof(T).Name}> is already initialized");

            Id = id;
            Size = size;
            Flags = flags;
            Callbacks = callbacks;
            BlockIndex = id >> 6;        // id / 64
            BitOffset = id & 0x3F;       // id % 64
            BitMask = 1UL << BitOffset;
        }

        /// <summary>
        /// 静态构造函数 - 懒加载自动注册（向后兼容）
        /// </summary>
        static ComponentTypeId()
        {
            // 如果尚未通过 Builder 注册，则自动注册
            if (Id == 0)
            {
                int id = ComponentTypeRegistry.Global.Register<T>();
                // Register<T> 会调用 Initialize
            }
        }

        #endregion
    }
}
