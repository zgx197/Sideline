using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Lattice.Core;
using Lattice.Math;

namespace Lattice.ECS.Core
{
    /// <summary>
    /// 单帧完整状态 - FrameSync 风格
    /// 
    /// 包含某一时刻的所有 ECS 数据：
    /// - 实体管理（EntityRegistry）
    /// - 组件存储（ComponentStorage 字典）
    /// - 每个实体的组件集合（ComponentSet 数组）
    /// - 全局数据（Tick、DeltaTime、随机种子等）
    /// </summary>
    public sealed class Frame : IDisposable
    {
        #region 字段

        /// <summary>帧号（从 0 开始）</summary>
        public int Tick { get; internal set; }

        /// <summary>固定时间步长</summary>
        public FP DeltaTime { get; private set; }

        /// <summary>实体管理器</summary>
        public EntityRegistry Entities { get; }

        /// <summary>组件类型注册表</summary>
        private readonly ComponentTypeRegistry _typeRegistry;

        /// <summary>组件存储字典：TypeId -> Storage</summary>
        private readonly Dictionary<int, object> _storages;

        /// <summary>每个实体的组件集合（稀疏数组）</summary>
        private ComponentSet[] _entityComponentSets;

        /// <summary>全局随机数生成器（确定性）</summary>
        public DeterministicRandom Random { get; }

        /// <summary>帧是否已验证（服务器确认）</summary>
        public bool IsVerified { get; set; }

        #endregion

        #region 构造函数

        public Frame(int tick, FP deltaTime, ComponentTypeRegistry typeRegistry, int initialEntityCapacity = 256)
        {
            Tick = tick;
            DeltaTime = deltaTime;
            _typeRegistry = typeRegistry;
            
            Entities = new EntityRegistry(initialEntityCapacity);
            _storages = new Dictionary<int, object>();
            
            // 初始化组件集合数组
            _entityComponentSets = new ComponentSet[initialEntityCapacity];
            for (int i = 0; i < initialEntityCapacity; i++)
            {
                _entityComponentSets[i] = ComponentSet.Empty;
            }

            // 确定性随机数（基于帧号种子）
            Random = new DeterministicRandom(tick);
        }

        #endregion

        #region 实体操作

        /// <summary>
        /// 创建新实体
        /// </summary>
        public Entity CreateEntity()
        {
            var entity = Entities.Create();
            EnsureComponentSetCapacity(entity.Index + 1);
            return entity;
        }

        /// <summary>
        /// 销毁实体（及所有组件）
        /// </summary>
        public bool DestroyEntity(Entity entity)
        {
            if (!Entities.IsValid(entity))
                return false;

            // 获取实体的组件集合
            var componentSet = _entityComponentSets[entity.Index];
            
            // 移除该实体的所有组件
            for (int typeId = 0; typeId < ComponentSet.MaxComponents; typeId++)
            {
                if (componentSet.Contains(typeId))
                {
                    RemoveComponentInternal(entity, typeId);
                }
            }

            // 清空组件集合
            _entityComponentSets[entity.Index] = ComponentSet.Empty;

            // 销毁实体
            return Entities.Destroy(entity);
        }

        /// <summary>
        /// 检查实体是否存在且有效
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsValid(Entity entity)
        {
            return Entities.IsValid(entity);
        }

        #endregion

        #region 组件操作

        /// <summary>
        /// 添加组件
        /// </summary>
        public void AddComponent<T>(Entity entity, in T component) where T : struct
        {
            if (!Entities.IsValid(entity))
                throw new ArgumentException($"Entity {entity} is not valid");

            int typeId = _typeRegistry.GetTypeId<T>();
            var storage = GetOrCreateStorage<T>(typeId);
            
            storage.Add(entity, component);
            
            // 更新实体的组件集合
            _entityComponentSets[entity.Index].Add(typeId);
        }

        /// <summary>
        /// 移除组件
        /// </summary>
        public bool RemoveComponent<T>(Entity entity) where T : struct
        {
            if (!Entities.IsValid(entity))
                return false;

            int typeId = _typeRegistry.GetTypeId<T>();
            return RemoveComponentInternal(entity, typeId);
        }

        /// <summary>
        /// 获取组件引用
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T GetComponent<T>(Entity entity) where T : struct
        {
            if (!Entities.IsValid(entity))
                throw new ArgumentException($"Entity {entity} is not valid");

            int typeId = _typeRegistry.GetTypeId<T>();
            if (!_storages.TryGetValue(typeId, out var storage))
                throw new KeyNotFoundException($"Component type {typeof(T).Name} not found");
            
            return ref ((ComponentStorage<T>)storage).Get(entity);
        }

        /// <summary>
        /// 尝试获取组件
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetComponent<T>(Entity entity, out T component) where T : struct
        {
            int typeId = _typeRegistry.GetTypeId<T>();
            if (!_storages.TryGetValue(typeId, out var storage))
            {
                component = default;
                return false;
            }
            return ((ComponentStorage<T>)storage).TryGet(entity, out component);
        }

        /// <summary>
        /// 检查实体是否有指定组件
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasComponent<T>(Entity entity) where T : struct
        {
            if (!Entities.IsValid(entity))
                return false;

            int typeId = _typeRegistry.GetTypeId<T>();
            return _entityComponentSets[entity.Index].Contains(typeId);
        }

        /// <summary>
        /// 获取实体的组件集合
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ComponentSet GetComponentSet(Entity entity)
        {
            if ((uint)entity.Index >= (uint)_entityComponentSets.Length)
                return ComponentSet.Empty;
            return _entityComponentSets[entity.Index];
        }

        /// <summary>
        /// 检查实体是否匹配组件查询条件
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MatchesQuery(Entity entity, in ComponentSet required, in ComponentSet excluded = default)
        {
            if (!Entities.IsValid(entity))
                return false;

            var entitySet = _entityComponentSets[entity.Index];
            
            // 必须包含所有 required 组件
            if (!entitySet.IsSupersetOf(required))
                return false;
            
            // 必须不包含任何 excluded 组件
            if (!excluded.IsEmpty && entitySet.Overlaps(excluded))
                return false;
            
            return true;
        }

        #endregion

        #region 查询支持

        /// <summary>
        /// 获取组件类型 ID
        /// </summary>
        public int GetTypeId<T>() where T : struct
        {
            return _typeRegistry.GetTypeId<T>();
        }

        /// <summary>
        /// 获取指定类型的所有组件存储
        /// </summary>
        public ComponentStorage<T> GetStorage<T>() where T : struct
        {
            int typeId = _typeRegistry.GetTypeId<T>();
            if (!_storages.TryGetValue(typeId, out var storage))
            {
                throw new KeyNotFoundException($"Component type {typeof(T).Name} not registered");
            }
            return (ComponentStorage<T>)storage;
        }

        /// <summary>
        /// 尝试获取存储（可能不存在）
        /// </summary>
        public bool TryGetStorage<T>(out ComponentStorage<T> storage) where T : struct
        {
            int typeId = _typeRegistry.GetTypeId<T>();
            if (!_storages.TryGetValue(typeId, out var obj))
            {
                storage = null!;
                return false;
            }
            storage = (ComponentStorage<T>)obj;
            return true;
        }

        /// <summary>
        /// 获取所有实体的 Span（用于遍历）
        /// </summary>
        public int GetAllEntities(Span<Entity> buffer)
        {
            int count = 0;
            foreach (var entity in Entities.GetAllAliveEntities())
            {
                if (count >= buffer.Length) break;
                buffer[count++] = entity;
            }
            return count;
        }

        #endregion

        #region 校验和（确定性验证）

        /// <summary>
        /// 计算帧的校验和（用于确定性验证）
        /// </summary>
        public long CalculateChecksum()
        {
            long checksum = Tick;
            
            // 包含所有组件数据的哈希
            foreach (var (typeId, storage) in _storages)
            {
                // 简化：只包含组件数量和版本信息
                // 实际实现需要序列化所有组件数据
                checksum = HashCode.Combine(checksum, typeId);
            }
            
            // 包含实体数量
            checksum = HashCode.Combine(checksum, Entities.AliveCount);
            
            return checksum;
        }

        #endregion

        #region 快照支持

        /// <summary>
        /// 创建当前帧的浅拷贝（用于预测/回滚）
        /// </summary>
        public Frame Clone()
        {
            var clone = new Frame(Tick, DeltaTime, _typeRegistry, Entities.Capacity);
            
            // 复制实体状态
            // 注意：这需要 EntityRegistry 支持克隆，简化起见先不实现完整克隆
            
            return clone;
        }

        /// <summary>
        /// 从另一帧复制状态（用于回滚）
        /// </summary>
        public void CopyFrom(Frame other)
        {
            if (other.Tick != Tick)
                throw new InvalidOperationException("Cannot copy from frame with different tick");
            
            // 复制实体状态
            // 复制组件数据
            // 简化实现，实际需要深拷贝所有存储
        }

        #endregion

        #region 辅助方法

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ComponentStorage<T> GetOrCreateStorage<T>(int typeId) where T : struct
        {
            if (!_storages.TryGetValue(typeId, out var storage))
            {
                storage = new ComponentStorage<T>(Entities.Capacity);
                _storages[typeId] = storage;
            }
            return (ComponentStorage<T>)storage;
        }

        private bool RemoveComponentInternal(Entity entity, int typeId)
        {
            if (!_storages.TryGetValue(typeId, out var storage))
                return false;

            // 使用反射或动态调用，这里简化处理
            // 实际应该根据 typeId 获取对应的存储类型
            
            _entityComponentSets[entity.Index].Remove(typeId);
            return true;
        }

        private void EnsureComponentSetCapacity(int required)
        {
            if (required <= _entityComponentSets.Length)
                return;

            int newSize = System.Math.Max(required, _entityComponentSets.Length * 2);
            Array.Resize(ref _entityComponentSets, newSize);
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            foreach (var storage in _storages.Values)
            {
                (storage as IDisposable)?.Dispose();
            }
            _storages.Clear();
            Entities.Dispose();
        }

        #endregion
    }

    /// <summary>
    /// 组件类型注册表 - 管理组件类型到 ID 的映射
    /// </summary>
    public sealed class ComponentTypeRegistry
    {
        private readonly Dictionary<Type, int> _typeToId = new();
        private readonly Dictionary<int, Type> _idToType = new();
        private int _nextId = 0;

        /// <summary>
        /// 注册组件类型，返回类型 ID
        /// </summary>
        public int Register<T>() where T : struct
        {
            var type = typeof(T);
            if (_typeToId.TryGetValue(type, out var id))
                return id;

            id = _nextId++;
            if (id >= ComponentSet.MaxComponents)
                throw new InvalidOperationException($"Maximum component types ({ComponentSet.MaxComponents}) exceeded");

            _typeToId[type] = id;
            _idToType[id] = type;
            return id;
        }

        /// <summary>
        /// 获取组件类型 ID（必须已注册）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetTypeId<T>() where T : struct
        {
            if (!_typeToId.TryGetValue(typeof(T), out var id))
            {
                throw new KeyNotFoundException($"Component type {typeof(T).Name} not registered. Call Register<{typeof(T).Name}>() first.");
            }
            return id;
        }

        /// <summary>
        /// 根据 ID 获取类型
        /// </summary>
        public Type GetType(int id)
        {
            return _idToType.TryGetValue(id, out var type) ? type : null!;
        }

        /// <summary>
        /// 检查类型是否已注册
        /// </summary>
        public bool IsRegistered<T>() where T : struct
        {
            return _typeToId.ContainsKey(typeof(T));
        }
    }

    /// <summary>
    /// 确定性随机数生成器
    /// </summary>
    public sealed class DeterministicRandom
    {
        private uint _state;

        public DeterministicRandom(int seed)
        {
            _state = (uint)seed;
            // 预热
            for (int i = 0; i < 10; i++) Next();
        }

        /// <summary>
        /// 生成下一个随机数（0 到 int.MaxValue）
        /// </summary>
        public int Next()
        {
            // Xorshift 算法（确定性）
            _state ^= _state << 13;
            _state ^= _state >> 17;
            _state ^= _state << 5;
            return (int)(_state & 0x7FFFFFFF);
        }

        /// <summary>
        /// 生成指定范围内的随机数
        /// </summary>
        public int Next(int min, int max)
        {
            if (min >= max) throw new ArgumentException("min must be less than max");
            return min + (int)(Next() % (max - min));
        }

        /// <summary>
        /// 生成 0-1 范围内的定点数
        /// </summary>
        public FP NextFP()
        {
            return (FP)Next() / (FP)int.MaxValue;
        }
    }
}
