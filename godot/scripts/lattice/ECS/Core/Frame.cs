using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Lattice.Core;
using Lattice.Math;

namespace Lattice.ECS.Core
{
    /// <summary>
    /// 单帧完整状�?- FrameSync 风格
    /// 
    /// 包含某一时刻的所�?ECS 数据�?    /// - 实体管理（EntityRegistry�?    /// - 组件存储（ComponentStorage 字典�?    /// - 每个实体的组件集合（ComponentSet 数组�?    /// - 全局数据（Tick、DeltaTime、随机种子等�?    /// </summary>
    public unsafe partial class Frame : IDisposable
    {
        #region 字段

        /// <summary>帧号（从 0 开始，只读�?/summary>
        public int Tick { get; }

        /// <summary>固定时间步长</summary>
        public FP DeltaTime { get; private set; }

        /// <summary>实体管理�?/summary>
        public EntityRegistry Entities { get; }

        /// <summary>组件类型注册�?/summary>
        private readonly ComponentTypeRegistry _typeRegistry;

        /// <summary>组件存储字典：TypeId -> Storage</summary>
        private readonly Dictionary<int, object> _storages;

        /// <summary>每个实体的组件集合（稀疏数组）</summary>
        private ComponentSet[] _entityComponentSets;

        /// <summary>全局随机数生成器（确定性）</summary>
        public DeterministicRandom Random { get; }

        /// <summary>帧是否已验证（服务器确认�?/summary>
        public bool IsVerified { get; set; }

        #endregion

        #region 构造函�?
        public Frame(int tick, FP deltaTime, ComponentTypeRegistry typeRegistry, int initialEntityCapacity = 256)
        {
            Tick = tick;
            DeltaTime = deltaTime;
            _typeRegistry = typeRegistry;

            Entities = new EntityRegistry(initialEntityCapacity);
            _storages = new Dictionary<int, object>();

            // 初始化组件集合数�?            _entityComponentSets = new ComponentSet[initialEntityCapacity];
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
        /// 创建新实�?        /// </summary>
        public EntityRef CreateEntity()
        {
            var EntityRef = Entities.Create();
            EnsureComponentSetCapacity(EntityRef.Index + 1);
            return EntityRef;
        }

        /// <summary>
        /// 销毁实体（及所有组件）
        /// </summary>
        public bool DestroyEntity(EntityRef EntityRef)
        {
            if (!Entities.IsValid(EntityRef))
                return false;

            // 获取实体的组件集�?            var componentSet = _entityComponentSets[EntityRef.Index];

            // 移除该实体的所有组�?            for (int typeId = 0; typeId < ComponentSet.MaxComponents; typeId++)
            {
                if (componentSet.IsSet(typeId))
                {
                    RemoveComponentInternal(EntityRef, typeId);
                }
            }

            // 清空组件集合
            _entityComponentSets[EntityRef.Index] = ComponentSet.Empty;

            // 销毁实�?            return Entities.Destroy(EntityRef);
        }

        /// <summary>
        /// 检查实体是否存在且有效
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsValid(EntityRef EntityRef)
        {
            return Entities.IsValid(EntityRef);
        }

        #endregion

        #region 组件操作

        /// <summary>
        /// 添加组件
        /// </summary>
        public void AddComponent<T>(EntityRef EntityRef, in T component) where T : unmanaged, IComponent
        {
            if (!Entities.IsValid(EntityRef))
                throw new ArgumentException($"EntityRef {EntityRef} is not valid");

            int typeId = _typeRegistry.GetTypeId<T>();
            var storage = GetOrCreateStorage<T>(typeId);

            storage.Add(EntityRef, component);

            // 更新实体的组件集�?            _entityComponentSets[EntityRef.Index].Add(typeId);

            // 调用 OnAdded 回调（如果存在）
            var callbacks = ComponentTypeId<T>.Callbacks;
            if (callbacks.OnAdded != null)
            {
                T temp = component;
                callbacks.OnAdded(EntityRef, &temp, this);
            }
        }

        /// <summary>
        /// 移除组件
        /// </summary>
        public bool RemoveComponent<T>(EntityRef EntityRef) where T : unmanaged, IComponent
        {
            if (!Entities.IsValid(EntityRef))
                return false;

            // 调用 OnRemoved 回调（如果存在）
            var callbacks = ComponentTypeId<T>.Callbacks;
            if (callbacks.OnRemoved != null)
            {
                if (TryGetComponent<T>(EntityRef, out var component))
                {
                    callbacks.OnRemoved(EntityRef, &component, this);
                }
            }

            int typeId = _typeRegistry.GetTypeId<T>();
            return RemoveComponentInternal(EntityRef, typeId);
        }

        /// <summary>
        /// 获取组件引用
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T GetComponent<T>(EntityRef EntityRef) where T : unmanaged, IComponent
        {
            if (!Entities.IsValid(EntityRef))
                throw new ArgumentException($"EntityRef {EntityRef} is not valid");

            int typeId = _typeRegistry.GetTypeId<T>();
            if (!_storages.TryGetValue(typeId, out var storage))
                throw new KeyNotFoundException($"Component type {typeof(T).Name} not found");

            return ref ((ComponentStorage<T>)storage).Get(EntityRef);
        }

        /// <summary>
        /// 尝试获取组件
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetComponent<T>(EntityRef EntityRef, out T component) where T : unmanaged, IComponent
        {
            int typeId = _typeRegistry.GetTypeId<T>();
            if (!_storages.TryGetValue(typeId, out var storage))
            {
                component = default;
                return false;
            }
            return ((ComponentStorage<T>)storage).TryGet(EntityRef, out component);
        }

        /// <summary>
        /// 尝试获取组件引用（高性能，避免拷贝）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetComponentRef<T>(EntityRef EntityRef, out Ref<T> component) where T : unmanaged, IComponent
        {
            int typeId = _typeRegistry.GetTypeId<T>();
            if (!_storages.TryGetValue(typeId, out var storage))
            {
                component = default;
                return false;
            }
            return ((ComponentStorage<T>)storage).TryGetRef(EntityRef, out component);
        }

        /// <summary>
        /// 检查实体是否有指定组件
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasComponent<T>(EntityRef EntityRef) where T : unmanaged, IComponent
        {
            if (!Entities.IsValid(EntityRef))
                return false;

            int typeId = _typeRegistry.GetTypeId<T>();
            return _entityComponentSets[EntityRef.Index].IsSet(typeId);
        }

        /// <summary>
        /// 获取实体的组件集�?        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ComponentSet GetComponentSet(EntityRef EntityRef)
        {
            if ((uint)EntityRef.Index >= (uint)_entityComponentSets.Length)
                return ComponentSet.Empty;
            return _entityComponentSets[EntityRef.Index];
        }

        /// <summary>
        /// 检查实体是否匹配组件查询条�?        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MatchesQuery(EntityRef EntityRef, in ComponentSet required, in ComponentSet excluded = default)
        {
            if (!Entities.IsValid(EntityRef))
                return false;

            var entitySet = _entityComponentSets[EntityRef.Index];

            // 必须包含所�?required 组件
            if (!entitySet.IsSupersetOf(required))
                return false;

            // 必须不包含任�?excluded 组件
            if (!excluded.IsEmpty && entitySet.Overlaps(excluded))
                return false;

            return true;
        }

        #endregion

        #region 查询支持

        /// <summary>
        /// 获取组件类型 ID
        /// </summary>
        public int GetTypeId<T>() where T : unmanaged, IComponent
        {
            return _typeRegistry.GetTypeId<T>();
        }

        /// <summary>
        /// 获取指定类型的所有组件存�?        /// </summary>
        public ComponentStorage<T> GetStorage<T>() where T : unmanaged, IComponent
        {
            int typeId = _typeRegistry.GetTypeId<T>();
            if (!_storages.TryGetValue(typeId, out var storage))
            {
                throw new KeyNotFoundException($"Component type {typeof(T).Name} not registered");
            }
            return (ComponentStorage<T>)storage;
        }

        /// <summary>
        /// 尝试获取存储（可能不存在�?        /// </summary>
        public bool TryGetStorage<T>(out ComponentStorage<T> storage) where T : unmanaged, IComponent
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
        public int GetAllEntities(Span<EntityRef> buffer)
        {
            int count = 0;
            foreach (var EntityRef in Entities.GetAllAliveEntities())
            {
                if (count >= buffer.Length) break;
                buffer[count++] = EntityRef;
            }
            return count;
        }

        #endregion

        #region 校验和（确定性验证）

        /// <summary>
        /// 计算帧的校验和（用于确定性验证）
        /// </summary>
        public ulong CalculateChecksum()
        {
            return FrameSnapshot.CalculateFrameChecksum(this, _typeRegistry);
        }

        #endregion

        #region 快照支持（Phase 3�?
        /// <summary>
        /// 创建当前帧的快照
        /// </summary>
        public FrameSnapshot CreateSnapshot()
        {
            return FrameSnapshot.Capture(this, _typeRegistry);
        }

        /// <summary>
        /// 从快照恢复（回滚�?        /// </summary>
        public void RestoreFromSnapshot(FrameSnapshot snapshot)
        {
            snapshot.Restore(this, _typeRegistry);
        }

        /// <summary>
        /// 克隆当前�?        /// </summary>
        public Frame Clone()
        {
            var clone = new Frame(Tick, DeltaTime, _typeRegistry, Entities.Capacity);
            clone.CopyFrom(this);
            return clone;
        }

        /// <summary>
        /// 从另一帧复制状态（用于回滚�?        /// </summary>
        public void CopyFrom(Frame other)
        {
            if (other.Tick != Tick)
                throw new InvalidOperationException("Cannot copy from frame with different tick");

            // 使用快照机制进行完整复制
            var snapshot = other.CreateSnapshot();
            RestoreFromSnapshot(snapshot);
        }

        #endregion

        #region 辅助方法

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ComponentStorage<T> GetOrCreateStorage<T>(int typeId) where T : unmanaged, IComponent
        {
            if (!_storages.TryGetValue(typeId, out var storage))
            {
                storage = new ComponentStorage<T>(Entities.Capacity);
                _storages[typeId] = storage;
            }
            return (ComponentStorage<T>)storage;
        }

        private bool RemoveComponentInternal(EntityRef entity, int typeId)
        {
            if (!_storages.TryGetValue(typeId, out var storage))
                return false;

            // �Ӵ洢���Ƴ����
            var removed = ((IComponentStorage)storage).Remove(entity);
            if (removed)
            {
                // ����ʵ����������λͼ
                if (entity.Index < _entityComponentSets.Length)
                {
                    _entityComponentSets[entity.Index].Remove(typeId);
                }
            }
            return removed;
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
    /// 确定性随机数生成�?    /// </summary>
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
        /// 生成下一个随机数�? �?int.MaxValue�?        /// </summary>
        public int Next()
        {
            // Xorshift 算法（确定性）
            _state ^= _state << 13;
            _state ^= _state >> 17;
            _state ^= _state << 5;
            return (int)(_state & 0x7FFFFFFF);
        }

        /// <summary>
        /// 生成指定范围内的随机�?        /// </summary>
        public int Next(int min, int max)
        {
            if (min >= max) throw new ArgumentException("min must be less than max");
            return min + (int)(Next() % (max - min));
        }

        /// <summary>
        /// 生成 0-1 范围内的定点�?        /// </summary>
        public FP NextFP()
        {
            return (FP)Next() / (FP)int.MaxValue;
        }
    }
}
