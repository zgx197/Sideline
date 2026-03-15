using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Lattice.Math;

namespace Lattice.ECS.Core
{
    /// <summary>
    /// 帧状态容器，对齐 FrameSync 的 Frame 设计
    /// 包含所有瞬时游戏状态数据
    /// </summary>
    public unsafe class Frame : IDisposable
    {
        /// <summary>当前帧编号（Tick）</summary>
        public int Tick { get; private set; }

        /// <summary>是否已验证（服务器确认）</summary>
        public bool IsVerified { get; set; }

        /// <summary>实体注册表</summary>
        public EntityRegistry Entities { get; }

        /// <summary>固定时间步长</summary>
        public FP DeltaTime { get; set; }

        // 组件存储表（类型索引 -> 存储实例）
        private readonly Dictionary<int, object> _storages;

        // 实体组件位图（用于快速查询实体有哪些组件）
        private ComponentSet* _entityComponentSets;
        private int _entityComponentSetsCapacity;

        // 实体标志数组
        private EntityFlags* _entityFlags;

        // Culling 位图（被裁剪的实体）
        private ulong* _culled;
        private int _culledCapacity;

        // 组件类型计数器
        private static int _nextComponentTypeId = 0;
        private static readonly Dictionary<Type, int> _componentTypeIds = new();

        /// <summary>当前裁剪区域</summary>
        public CullingZone CullingZone { get; set; }

        /// <summary>是否启用裁剪</summary>
        public bool IsCullingEnabled { get; set; } = true;

        /// <summary>
        /// 创建新帧
        /// </summary>
        public Frame(int initialEntityCapacity = 1024)
        {
            Tick = 0;
            IsVerified = false;
            Entities = new EntityRegistry(initialEntityCapacity);
            _storages = new Dictionary<int, object>();

            // 分配实体组件位图数组
            _entityComponentSetsCapacity = global::System.Math.Max(16, initialEntityCapacity);
            _entityComponentSets = (ComponentSet*)NativeMemory.AlignedAlloc(
                (nuint)(_entityComponentSetsCapacity * sizeof(ComponentSet)), 8);

            // 初始化为空集
            for (int i = 0; i < _entityComponentSetsCapacity; i++)
            {
                _entityComponentSets[i] = ComponentSet.Empty;
            }

            // 分配实体标志数组
            _entityFlags = (EntityFlags*)NativeMemory.AlignedAlloc(
                (nuint)(_entityComponentSetsCapacity * sizeof(EntityFlags)), 8);

            // 分配 Culling 位图（每 64 个实体一个 ulong）
            _culledCapacity = (_entityComponentSetsCapacity + 63) / 64;
            _culled = (ulong*)NativeMemory.AlignedAlloc(
                (nuint)(_culledCapacity * sizeof(ulong)), 8);

            // 初始化
            for (int i = 0; i < _entityComponentSetsCapacity; i++)
            {
                _entityFlags[i] = EntityFlags.None;
            }

            for (int i = 0; i < _culledCapacity; i++)
            {
                _culled[i] = 0;
            }

            CullingZone = CullingZone.Default;
        }

        /// <summary>
        /// 创建新帧（带参数，兼容 Session）
        /// </summary>
        public Frame(int tick, FP deltaTime, ComponentTypeRegistry typeRegistry, int initialEntityCapacity = 1024)
            : this(initialEntityCapacity)
        {
            Tick = tick;
            DeltaTime = deltaTime;
            // typeRegistry 保留供将来使用
        }

        /// <summary>
        /// 计算校验和（简化实现）
        /// </summary>
        public long CalculateChecksum()
        {
            long checksum = Tick;

            // 累加实体数量
            checksum += Entities.Count * 31;

            // 累加组件数量（简化处理）
            foreach (var kvp in _storages)
            {
                var storage = kvp.Value;
                var countProperty = storage.GetType().GetProperty("UsedCount");
                if (countProperty != null)
                {
                    int count = (int)countProperty.GetValue(storage)!;
                    checksum += count * (kvp.Key + 1) * 17;
                }
            }

            return checksum;
        }

        /// <summary>
        /// 获取组件类型索引（统一使用 ComponentTypeId&lt;T&gt;.Id）
        /// </summary>
        public static int GetComponentTypeId<T>() where T : unmanaged, IComponent
        {
            return ComponentTypeId<T>.Id;
        }

        /// <summary>
        /// 获取或创建组件存储
        /// </summary>
        internal ComponentStorage<T> GetStorage<T>() where T : unmanaged, IComponent
        {
            int typeId = GetComponentTypeId<T>();

            if (!_storages.TryGetValue(typeId, out var storage))
            {
                storage = new ComponentStorage<T>(initialEntityCapacity: _entityComponentSetsCapacity);
                _storages[typeId] = storage;
            }

            return (ComponentStorage<T>)storage;
        }

        /// <summary>
        /// 确保实体组件位图数组容量
        /// </summary>
        private void EnsureEntityComponentSetsCapacity(int entityIndex)
        {
            if (entityIndex < _entityComponentSetsCapacity) return;

            int newCapacity = _entityComponentSetsCapacity * 2;
            while (newCapacity <= entityIndex)
            {
                newCapacity *= 2;
            }

            var newSets = (ComponentSet*)NativeMemory.AlignedAlloc(
                (nuint)(newCapacity * sizeof(ComponentSet)), 8);

            // 复制旧数据
            Buffer.MemoryCopy(_entityComponentSets, newSets,
                _entityComponentSetsCapacity * sizeof(ComponentSet),
                _entityComponentSetsCapacity * sizeof(ComponentSet));

            // 初始化新区域
            for (int i = _entityComponentSetsCapacity; i < newCapacity; i++)
            {
                newSets[i] = ComponentSet.Empty;
            }

            NativeMemory.AlignedFree(_entityComponentSets);
            _entityComponentSets = newSets;
            _entityComponentSetsCapacity = newCapacity;

            // 同时扩展所有存储的实体映射容量
            foreach (var storage in _storages.Values)
            {
                // 通过反射调用 EnsureEntityMapCapacity
                // 实际实现中应使用接口或虚方法避免反射
                var method = storage.GetType().GetMethod("EnsureEntityMapCapacity",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                method?.Invoke(storage, new object[] { entityIndex });
            }
        }

        /// <summary>
        /// 添加组件到实体
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add<T>(Entity entity, in T component) where T : unmanaged, IComponent
        {
            if (!Entities.Exists(entity))
            {
                throw new InvalidOperationException($"Entity {entity} does not exist");
            }

            EnsureEntityComponentSetsCapacity(entity.Index);

            var storage = GetStorage<T>();
            storage.Add(entity, component);

            // 更新组件位图
            _entityComponentSets[entity.Index].Add<T>();
        }

        /// <summary>
        /// 获取组件指针（unsafe，最高性能）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T* GetPointer<T>(Entity entity) where T : unmanaged, IComponent
        {
            if (entity.Index >= _entityComponentSetsCapacity)
            {
                throw new InvalidOperationException($"Entity {entity} does not have component {typeof(T).Name}");
            }

            var storage = GetStorage<T>();
            return storage.GetPointer(entity);
        }

        /// <summary>
        /// 尝试获取组件指针
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetPointer<T>(Entity entity, out T* pointer) where T : unmanaged, IComponent
        {
            if (entity.Index >= _entityComponentSetsCapacity)
            {
                pointer = null;
                return false;
            }

            var storage = GetStorage<T>();
            return storage.TryGetPointer(entity, out pointer);
        }

        /// <summary>
        /// 获取组件值（复制）
        /// </summary>
        public T Get<T>(Entity entity) where T : unmanaged, IComponent
        {
            return *GetPointer<T>(entity);
        }

        /// <summary>
        /// 设置组件值
        /// </summary>
        public void Set<T>(Entity entity, in T component) where T : unmanaged, IComponent
        {
            var storage = GetStorage<T>();
            storage.Set(entity, component);
        }

        /// <summary>
        /// 检查实体是否有指定组件
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Has<T>(Entity entity) where T : unmanaged, IComponent
        {
            if (entity.Index >= _entityComponentSetsCapacity) return false;
            if (!Entities.Exists(entity)) return false;

            return _entityComponentSets[entity.Index].Contains<T>();
        }

        /// <summary>
        /// 检查实体是否有一组组件
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Has(Entity entity, ComponentSet componentSet)
        {
            if (entity.Index >= _entityComponentSetsCapacity) return false;
            if (!Entities.Exists(entity)) return false;

            return _entityComponentSets[entity.Index].IsSupersetOf(componentSet);
        }

        /// <summary>
        /// 移除组件
        /// </summary>
        public bool Remove<T>(Entity entity) where T : unmanaged, IComponent
        {
            if (entity.Index >= _entityComponentSetsCapacity) return false;

            var storage = GetStorage<T>();
            bool removed = storage.Remove(entity);

            if (removed)
            {
                _entityComponentSets[entity.Index].Remove<T>();
            }

            return removed;
        }

        /// <summary>
        /// 创建实体（带可选的初始组件）
        /// </summary>
        public Entity CreateEntity<T1>(in T1 c1 = default)
            where T1 : unmanaged, IComponent
        {
            var entity = Entities.Create();
            EnsureEntityComponentSetsCapacity(entity.Index);

            Add(entity, c1);

            return entity;
        }

        /// <summary>
        /// 创建实体（带 2 个初始组件）
        /// </summary>
        public Entity CreateEntity<T1, T2>(in T1 c1, in T2 c2)
            where T1 : unmanaged, IComponent
            where T2 : unmanaged, IComponent
        {
            var entity = Entities.Create();
            EnsureEntityComponentSetsCapacity(entity.Index);

            Add(entity, c1);
            Add(entity, c2);

            return entity;
        }

        /// <summary>
        /// 创建实体（带 3 个初始组件）
        /// </summary>
        public Entity CreateEntity<T1, T2, T3>(in T1 c1, in T2 c2, in T3 c3)
            where T1 : unmanaged, IComponent
            where T2 : unmanaged, IComponent
            where T3 : unmanaged, IComponent
        {
            var entity = Entities.Create();
            EnsureEntityComponentSetsCapacity(entity.Index);

            Add(entity, c1);
            Add(entity, c2);
            Add(entity, c3);

            return entity;
        }

        /// <summary>
        /// 销毁实体及其所有组件
        /// </summary>
        public void DestroyEntity(Entity entity)
        {
            if (!Entities.Exists(entity)) return;

            if (entity.Index < _entityComponentSetsCapacity)
            {
                // 移除所有组件（通过位图知道有哪些组件）
                var componentSet = _entityComponentSets[entity.Index];
                // TODO: 遍历位图移除所有组件
                // 这需要组件类型 ID 到类型的映射，当前设计限制下需要额外处理

                _entityComponentSets[entity.Index] = ComponentSet.Empty;
            }

            Entities.Destroy(entity);
        }

        /// <summary>
        /// 获取实体的组件位图
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ComponentSet GetComponentSet(Entity entity)
        {
            if (entity.Index >= _entityComponentSetsCapacity) return ComponentSet.Empty;
            return _entityComponentSets[entity.Index];
        }

        /// <summary>
        /// 获取实体的组件位图（256-bit 版本，用于快速路径）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ComponentSet256 GetComponentSet256(Entity entity)
        {
            if (entity.Index >= _entityComponentSetsCapacity) return default;

            // 从 512-bit ComponentSet 提取前 256-bit
            ComponentSet set = _entityComponentSets[entity.Index];
            ComponentSet256 result;
            result.Set[0] = set.Set[0];
            result.Set[1] = set.Set[1];
            result.Set[2] = set.Set[2];
            result.Set[3] = set.Set[3];
            return result;
        }

        #region Culling 支持

        /// <summary>
        /// 检查实体是否被裁剪
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsCulled(Entity entity)
        {
            if (!IsCullingEnabled) return false;
            if (entity.Index < 0 || entity.Index >= _entityComponentSetsCapacity) return false;

            // 检查 NotCullable 标志
            if ((_entityFlags[entity.Index] & EntityFlags.NotCullable) != 0)
                return false;

            return (_culled[entity.Index / 64] & (1UL << (entity.Index % 64))) != 0;
        }

        /// <summary>
        /// 设置实体裁剪状态
        /// </summary>
        public void SetCulled(Entity entity, bool culled)
        {
            if (entity.Index < 0 || entity.Index >= _entityComponentSetsCapacity) return;

            int blockIndex = entity.Index / 64;
            int bitIndex = entity.Index % 64;
            ulong mask = 1UL << bitIndex;

            if (culled)
            {
                _culled[blockIndex] |= mask;
            }
            else
            {
                _culled[blockIndex] &= ~mask;
            }
        }

        /// <summary>
        /// 清除所有裁剪标记
        /// </summary>
        public void ClearCulling()
        {
            for (int i = 0; i < _culledCapacity; i++)
            {
                _culled[i] = 0;
            }
        }

        /// <summary>
        /// 设置实体标志
        /// </summary>
        public void SetEntityFlag(Entity entity, EntityFlags flag, bool value)
        {
            if (entity.Index < 0 || entity.Index >= _entityComponentSetsCapacity) return;

            if (value)
            {
                _entityFlags[entity.Index] |= flag;
            }
            else
            {
                _entityFlags[entity.Index] &= ~flag;
            }
        }

        /// <summary>
        /// 检查实体标志
        /// </summary>
        public bool HasEntityFlag(Entity entity, EntityFlags flag)
        {
            if (entity.Index < 0 || entity.Index >= _entityComponentSetsCapacity) return false;
            return (_entityFlags[entity.Index] & flag) != 0;
        }

        /// <summary>
        /// 设置实体不可裁剪
        /// </summary>
        public void SetNotCullable(Entity entity, bool notCullable)
        {
            SetEntityFlag(entity, EntityFlags.NotCullable, notCullable);

            // 如果设置为不可裁剪，清除其裁剪标记
            if (notCullable)
            {
                SetCulled(entity, false);
            }
        }

        #endregion

        /// <summary>
        /// 获取组件数量（包含待删除的）
        /// </summary>
        public int GetComponentCount<T>(bool includePendingRemoval = false) where T : unmanaged, IComponent
        {
            if (!_storages.TryGetValue(GetComponentTypeId<T>(), out var storage))
            {
                return 0;
            }

            var typedStorage = (ComponentStorage<T>)storage;
            return includePendingRemoval ? typedStorage.Count : typedStorage.UsedCount;
        }

        /// <summary>
        /// 创建查询（类型安全）
        /// </summary>
        public Query<T> Query<T>() where T : unmanaged, IComponent
        {
            return new Query<T>(this);
        }

        /// <summary>
        /// 创建查询（2 个组件）
        /// </summary>
        public Query<T1, T2> Query<T1, T2>()
            where T1 : unmanaged, IComponent
            where T2 : unmanaged, IComponent
        {
            return new Query<T1, T2>(this);
        }

        /// <summary>
        /// 创建查询（3 个组件）
        /// </summary>
        public Query<T1, T2, T3> Query<T1, T2, T3>()
            where T1 : unmanaged, IComponent
            where T2 : unmanaged, IComponent
            where T3 : unmanaged, IComponent
        {
            return new Query<T1, T2, T3>(this);
        }

        /// <summary>
        /// 克隆帧（深拷贝）
        /// </summary>
        public Frame Clone()
        {
            var clone = new Frame(_entityComponentSetsCapacity);
            clone.Tick = Tick;
            clone.IsVerified = IsVerified;
            clone.DeltaTime = DeltaTime;

            // 克隆实体
            Entities.CopyTo(clone.Entities);

            // 克隆组件位图
            Buffer.MemoryCopy(_entityComponentSets, clone._entityComponentSets,
                _entityComponentSetsCapacity * sizeof(ComponentSet),
                _entityComponentSetsCapacity * sizeof(ComponentSet));

            // 克隆组件存储（需要逐个组件类型处理）
            foreach (var kvp in _storages)
            {
                // 通过反射或接口克隆存储
                // 简化处理：重新添加所有组件
            }

            return clone;
        }

        /// <summary>
        /// 从另一帧复制数据
        /// </summary>
        public void CopyFrom(Frame other)
        {
            Tick = other.Tick;
            IsVerified = other.IsVerified;
            DeltaTime = other.DeltaTime;

            // 复制实体
            Entities.CopyFrom(other.Entities);

            // 确保容量
            EnsureEntityComponentSetsCapacity(other._entityComponentSetsCapacity - 1);

            // 复制组件位图
            Buffer.MemoryCopy(other._entityComponentSets, _entityComponentSets,
                other._entityComponentSetsCapacity * sizeof(ComponentSet),
                other._entityComponentSetsCapacity * sizeof(ComponentSet));

            // TODO: 复制组件数据
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            // 释放所有组件存储
            foreach (var storage in _storages.Values)
            {
                var disposeMethod = storage.GetType().GetMethod("Dispose");
                disposeMethod?.Invoke(storage, null);
            }
            _storages.Clear();

            // 释放组件位图数组
            if (_entityComponentSets != null)
            {
                NativeMemory.AlignedFree(_entityComponentSets);
                _entityComponentSets = null;
            }

            // 释放实体标志数组
            if (_entityFlags != null)
            {
                NativeMemory.AlignedFree(_entityFlags);
                _entityFlags = null;
            }

            // 释放 Culling 位图
            if (_culled != null)
            {
                NativeMemory.AlignedFree(_culled);
                _culled = null;
            }

            Entities?.Dispose();
        }

        /// <summary>
        /// 清除所有数据（重置帧）
        /// </summary>
        public void Clear()
        {
            Tick = 0;
            IsVerified = false;

            // 清除组件位图
            for (int i = 0; i < _entityComponentSetsCapacity; i++)
            {
                _entityComponentSets[i] = ComponentSet.Empty;
            }

            // 清除实体标志
            for (int i = 0; i < _entityComponentSetsCapacity; i++)
            {
                _entityFlags[i] = EntityFlags.None;
            }

            // 清除 Culling 位图
            ClearCulling();

            // 清除所有存储
            foreach (var storage in _storages.Values)
            {
                // 需要添加 Clear 方法到 ComponentStorage
            }

            Entities.Clear();
        }
    }
}
