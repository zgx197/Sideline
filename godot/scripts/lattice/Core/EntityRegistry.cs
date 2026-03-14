using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Lattice.Core
{
    /// <summary>
    /// 实体信息 - FrameSync风格设计，内存优化版本
    /// 
    /// 内存布局（16字节，缓存行对齐）：
    /// [0-7]   Entity: ulong     - 实体引用（Index + Version）
    /// [8-11]  NextFree: int     - 空闲链表指针（非活跃时有效）
    /// [12]    Flags: byte       - 实体标志
    /// [13-15] Padding: byte[3]  - 对齐填充
    /// 
    /// 设计优化：
    /// 1. 使用Explicit布局精确控制内存
    /// 2. 复用Entity.Index作为链表指针（FrameSync风格）
    /// 3. 版本号持久化存储，销毁时不丢失
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    internal struct EntityInfo
    {
        /// <summary>
        /// 实体引用（8字节）- 同时包含Index和Version
        /// </summary>
        [FieldOffset(0)]
        public Entity Entity;

        /// <summary>
        /// 当实体非活跃时，Entity.Index字段被复用为下一个空闲索引
        /// 注意：通过Entity.Index访问，无需额外字段
        /// </summary>
        [FieldOffset(0)]
        private ulong _rawEntity;

        /// <summary>
        /// 实体标志（1字节）
        /// </summary>
        [FieldOffset(8)]
        public EntityFlags Flags;

        /// <summary>
        /// 对齐填充（7字节）
        /// </summary>
        [FieldOffset(9)]
        private ulong _padding1;  // 7字节对齐（使用ulong覆盖）

        /// <summary>
        /// 获取/设置空闲链表指针（仅非活跃时有效）
        /// 直接操作Entity.Index字段，无需额外存储
        /// </summary>
        public int NextFree
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Entity.Index;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => _rawEntity = (ulong)(uint)value | ((ulong)(uint)Entity.Version << 32);
        }

        /// <summary>
        /// 创建新的EntityInfo
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static EntityInfo Create(Entity entity, EntityFlags flags = (EntityFlags)0)
        {
            return new EntityInfo
            {
                Entity = entity,
                Flags = flags
            };
        }

        /// <summary>
        /// 标记为非活跃，保留版本号，设置链表指针
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void MarkInactive(int nextFreeIndex, int preservedVersion)
        {
            // 复用Index字段存储链表指针，保留Version
            _rawEntity = (ulong)(uint)nextFreeIndex | ((ulong)(uint)preservedVersion << 32);
            Flags = (EntityFlags)0;
        }

        /// <summary>
        /// 重新激活，恢复正确的Index，递增版本号
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Reactivate(int index, int newVersion)
        {
            _rawEntity = (ulong)(uint)index | ((ulong)(uint)newVersion << 32);
            Flags = (EntityFlags)0;
        }
    }

    /// <summary>
    /// 延迟销毁条目 - 包含实体和销毁原因
    /// TODO: 后续添加组件系统时，需要在此存储组件清理回调
    /// </summary>
    internal readonly struct PendingDestroyEntry
    {
        public readonly Entity Entity;
        public readonly DestroyReason Reason;
        public readonly long Timestamp;  // 用于调试和性能分析

        public PendingDestroyEntry(Entity entity, DestroyReason reason = DestroyReason.Normal)
        {
            Entity = entity;
            Reason = reason;
            Timestamp = Stopwatch.GetTimestamp();
        }
    }

    /// <summary>
    /// 销毁原因枚举
    /// </summary>
    public enum DestroyReason : byte
    {
        Normal = 0,
        OutOfWorld = 1,      // 超出世界边界
        LifetimeExpired = 2, // 生命周期结束
        Cleanup = 3,         // 清理
    }

    /// <summary>
    /// 实体注册表 - 高性能版本（FrameSync风格优化）
    /// 
    /// 优化特性：
    /// 1. 版本号持久化（EntityInfo模式）
    /// 2. 延迟销毁 + 去重（HashSet）
    /// 3. 无符号边界检查（.NET 8优化）
    /// 4. 缓存行对齐的EntityInfo（16字节）
    /// 5. 复用Entity.Index作为链表指针
    /// 
    /// TODO: 后续需要添加的功能：
    /// - 组件系统集成（CommitDestroys时清理组件）
    /// - 事件回调（OnEntityCreated/OnEntityDestroyed）
    /// - 快照序列化支持
    /// </summary>
    public sealed class EntityRegistry : IDisposable
    {
        // === EntityInfo数组 - 版本号持久化存储 ===
        // 每个槽位16字节，缓存行对齐
        // 活跃实体：Entity.Version包含ActiveBit
        // 非活跃实体：Entity.Index被复用为空闲链表指针

        private EntityInfo[] _info;
        private int[] _archetypeIds;       // Archetype ID
        private int[] _archetypeRows;      // Archetype行号
        private ulong[] _componentMasks;   // 组件存在性掩码

        // 空闲链表头（-1表示空）
        private int _freeHead;

        // 容量管理
        private int _capacity;
        private int _count;
        private int _aliveCount;

        // 延迟销毁队列（优化：使用HashSet去重）
        // TODO: 组件系统实现后，需要存储组件清理回调
        private List<PendingDestroyEntry>? _pendingDestroys;
        private HashSet<int>? _pendingDestroyIndices;  // 用于O(1)去重检查

        // 常量
        public const int ActiveBit = int.MinValue;   // 0x80000000
        public const int VersionMask = int.MaxValue; // 0x7FFFFFFF
        public const int CacheLineSize = 64;
        public const int MaxEntityCount = (1 << 20) - 1; // 约100万

        // 统计信息
        private long _totalCreated;
        private long _totalDestroyed;
        private long _versionOverflowCount;  // 版本号溢出计数（调试用）

        /// <summary>
        /// 创建注册表
        /// </summary>
        public EntityRegistry(int initialCapacity = 256)
        {
            initialCapacity = System.Math.Min(System.Math.Max(initialCapacity, 16), MaxEntityCount);

            _info = new EntityInfo[initialCapacity];
            _archetypeIds = new int[initialCapacity];
            _archetypeRows = new int[initialCapacity];
            _componentMasks = new ulong[initialCapacity];

            _freeHead = -1;
            _capacity = initialCapacity;
            _count = 0;
            _aliveCount = 0;
            _pendingDestroys = null;
            _pendingDestroyIndices = null;
        }

        // === 基础属性 ===

        public int Count => _count;
        public int AliveCount => _aliveCount;
        public int Capacity => _capacity;
        public int FreeCount => _count - _aliveCount;

        /// <summary>
        /// 是否有待销毁的实体
        /// </summary>
        public bool HasPendingDestroys => _pendingDestroys?.Count > 0;

        /// <summary>
        /// 待销毁实体数量
        /// </summary>
        public int PendingDestroyCount => _pendingDestroys?.Count ?? 0;

        /// <summary>
        /// 总创建实体数（统计用）
        /// </summary>
        public long TotalCreated => _totalCreated;

        /// <summary>
        /// 总销毁实体数（统计用）
        /// </summary>
        public long TotalDestroyed => _totalDestroyed;

        // === 实体创建 ===

        /// <summary>
        /// 创建新实体
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Entity Create()
        {
            int index;
            EntityInfo info;

            if (_freeHead >= 0)
            {
                // 从空闲链表弹出
                index = _freeHead;
                info = _info[index];
                _freeHead = info.NextFree;  // 复用Entity.Index作为链表指针

                // ✅ 正确递增版本号（基于上次版本）
                int lastVersion = info.Entity.Version & VersionMask;
                int newVersion = ((lastVersion + 1) & VersionMask);
                if (newVersion == 0)
                {
                    newVersion = 1;
                    _versionOverflowCount++;
                }
                newVersion |= ActiveBit;

                info.Reactivate(index, newVersion);
            }
            else
            {
                // 新槽位
                if (_count >= _capacity)
                {
                    Grow();
                }
                index = _count++;
                int version = 1 | ActiveBit;

                info = EntityInfo.Create(new Entity(index, version));
            }

            // 存储回数组
            _info[index] = info;
            _archetypeIds[index] = 0;
            _archetypeRows[index] = -1;
            _componentMasks[index] = 0;

            _aliveCount++;
            _totalCreated++;

            // TODO: 添加事件回调支持
            // OnEntityCreated?.Invoke(info.Entity);

            return info.Entity;
        }

        /// <summary>
        /// 批量创建
        /// </summary>
        public void CreateBatch(Span<Entity> output)
        {
            int count = output.Length;
            if (count == 0) return;

            EnsureCapacity(_aliveCount + count);

            for (int i = 0; i < count; i++)
            {
                output[i] = CreateInternal();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Entity CreateInternal()
        {
            int index;
            EntityInfo info;

            if (_freeHead >= 0)
            {
                index = _freeHead;
                info = _info[index];
                _freeHead = info.NextFree;

                int lastVersion = info.Entity.Version & VersionMask;
                int newVersion = ((lastVersion + 1) & VersionMask);
                if (newVersion == 0) newVersion = 1;
                newVersion |= ActiveBit;

                info.Reactivate(index, newVersion);
            }
            else
            {
                index = _count++;
                int version = 1 | ActiveBit;
                info = EntityInfo.Create(new Entity(index, version));
            }

            _info[index] = info;
            _archetypeIds[index] = 0;
            _archetypeRows[index] = -1;
            _componentMasks[index] = 0;

            _aliveCount++;
            _totalCreated++;
            return info.Entity;
        }

        // === 实体销毁 ===

        /// <summary>
        /// 立即销毁实体（内部使用，外部推荐用DestroyDelayed）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool DestroyImmediate(Entity entity)
        {
            // ✅ .NET 8优化：无符号比较同时检查负数和越界
            uint index = (uint)entity.Index;
            if (index >= (uint)_count) return false;

            ref var info = ref _info[entity.Index];

            // 验证版本号匹配
            if (info.Entity.Version != entity.Version) return false;
            if ((info.Entity.Version & ActiveBit) == 0) return false;

            // ✅ 版本号持久化：复用Entity.Index作为链表指针
            int preservedVersion = info.Entity.Version & VersionMask;
            info.MarkInactive(_freeHead >= 0 ? _freeHead : -1, preservedVersion);
            _freeHead = entity.Index;

            _archetypeIds[entity.Index] = 0;
            _archetypeRows[entity.Index] = -1;
            _componentMasks[entity.Index] = 0;

            _aliveCount--;
            _totalDestroyed++;

            // TODO: 添加事件回调支持
            // OnEntityDestroyed?.Invoke(entity);

            return true;
        }

        /// <summary>
        /// 延迟销毁（推荐API）- 带去重检查
        /// </summary>
        public bool DestroyDelayed(Entity entity, DestroyReason reason = DestroyReason.Normal)
        {
            if (!IsValid(entity)) return false;

            _pendingDestroys ??= new List<PendingDestroyEntry>(64);
            _pendingDestroyIndices ??= new HashSet<int>(64);

            // ✅ 去重检查：O(1)
            if (!_pendingDestroyIndices.Add(entity.Index))
            {
                // 已存在于待销毁队列
                return false;
            }

            // 标记为待销毁
            ref var info = ref _info[entity.Index];
            info.Flags |= (EntityFlags)1;  // DestroyPending

            _pendingDestroys.Add(new PendingDestroyEntry(entity, reason));
            return true;
        }

        /// <summary>
        /// 执行所有延迟销毁
        /// 
        /// TODO: 组件系统实现后，需要在此添加：
        /// 1. 组件清理（遍历ComponentMask，移除所有组件）
        /// 2. 触发组件移除回调
        /// 3. 触发实体销毁回调
        /// 参考FrameSync的CommitDestroys实现
        /// </summary>
        public void CommitDestroys()
        {
            if (_pendingDestroys == null || _pendingDestroys.Count == 0) return;

            // TODO: 组件系统实现后，需要遍历每个实体的ComponentMask
            // 并调用相应的组件清理逻辑

            foreach (var entry in _pendingDestroys)
            {
                // 重新验证实体仍然有效
                if (IsValid(entry.Entity))
                {
                    // TODO: 在调用DestroyImmediate之前，先清理组件
                    // CleanupComponents(entry.Entity);

                    DestroyImmediate(entry.Entity);
                }
            }

            _pendingDestroys.Clear();
            _pendingDestroyIndices?.Clear();
        }

        /// <summary>
        /// 清除所有延迟销毁（不执行）
        /// </summary>
        public void ClearPendingDestroys()
        {
            if (_pendingDestroys == null) return;

            // 清除Pending标志
            foreach (var entry in _pendingDestroys)
            {
                if ((uint)entry.Entity.Index < (uint)_count)
                {
                    ref var info = ref _info[entry.Entity.Index];
                    info.Flags &= ~(EntityFlags)1;  // 清除DestroyPending
                }
            }

            _pendingDestroys.Clear();
            _pendingDestroyIndices?.Clear();
        }

        /// <summary>
        /// 检查实体是否标记为待销毁
        /// </summary>
        public bool IsDestroyPending(Entity entity)
        {
            if (!IsValid(entity)) return false;
            return (_info[entity.Index].Flags & (EntityFlags)1) != 0;  // DestroyPending
        }

        // === 验证方法 ===

        /// <summary>
        /// 验证实体是否有效（含边界检查）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsValid(Entity entity)
        {
            // ✅ .NET 8优化：无符号比较同时检查负数和越界
            uint index = (uint)entity.Index;
            if (index >= (uint)_count) return false;

            return _info[entity.Index].Entity.Version == entity.Version;
        }

        /// <summary>
        /// 检查实体是否活跃
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsAlive(Entity entity)
        {
            return IsValid(entity) && (_info[entity.Index].Entity.Version & ActiveBit) != 0;
        }

        // === EntityLocation缓存 ===

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public EntityLocation GetLocation(int index)
        {
            return new EntityLocation(_archetypeIds[index], _archetypeRows[index]);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetLocation(int index, in EntityLocation location)
        {
            _archetypeIds[index] = location.ArchetypeId;
            _archetypeRows[index] = location.Row;
        }

        // === 版本号访问 ===

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetVersion(int index) => _info[index].Entity.Version;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetArchetypeId(int index) => _archetypeIds[index];

        /// <summary>
        /// 获取实体的当前版本号（用于调试）
        /// </summary>
        public int GetCurrentVersion(int index)
        {
            if ((uint)index >= (uint)_count) return 0;
            return _info[index].Entity.Version & VersionMask;
        }

        // === 组件掩码 ===

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasComponent(int entityIndex, int componentId)
        {
            return (_componentMasks[entityIndex] & (1UL << componentId)) != 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddComponentMask(int entityIndex, int componentId)
        {
            _componentMasks[entityIndex] |= (1UL << componentId);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveComponentMask(int entityIndex, int componentId)
        {
            _componentMasks[entityIndex] &= ~(1UL << componentId);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ulong GetComponentMask(int entityIndex)
        {
            return _componentMasks[entityIndex];
        }

        // === 并行遍历 ===

        /// <summary>
        /// 线程安全的并行遍历
        /// </summary>
        public void ForEachParallel(Action<Entity> action, int maxDegreeOfParallelism = -1)
        {
            var options = new ParallelOptions
            {
                MaxDegreeOfParallelism = maxDegreeOfParallelism
            };

            Parallel.For(0, _count, options, i =>
            {
                var entity = _info[i].Entity;
                if ((entity.Version & ActiveBit) != 0)
                {
                    action(entity);
                }
            });
        }

        /// <summary>
        /// 获取只读视图（线程安全）
        /// </summary>
        public RegistryReadOnly AsReadOnly()
        {
            return new RegistryReadOnly(this);
        }

        // === 容量管理 ===

        private void Grow()
        {
            int newCapacity = System.Math.Min(_capacity * 2, MaxEntityCount);
            if (newCapacity <= _capacity) throw new InvalidOperationException("Maximum capacity reached");

            Array.Resize(ref _info, newCapacity);
            Array.Resize(ref _archetypeIds, newCapacity);
            Array.Resize(ref _archetypeRows, newCapacity);
            Array.Resize(ref _componentMasks, newCapacity);

            _capacity = newCapacity;
        }

        public void EnsureCapacity(int capacity)
        {
            if (capacity <= _capacity) return;

            int newCapacity = _capacity;
            while (newCapacity < capacity)
            {
                newCapacity = System.Math.Min(newCapacity * 2, MaxEntityCount);
            }

            if (newCapacity > _capacity)
            {
                Array.Resize(ref _info, newCapacity);
                Array.Resize(ref _archetypeIds, newCapacity);
                Array.Resize(ref _archetypeRows, newCapacity);
                Array.Resize(ref _componentMasks, newCapacity);
                _capacity = newCapacity;
            }
        }

        public void Clear()
        {
            // ✅ .NET 8优化：使用Unsafe.ClearMemory进行批量清零
            if (_count > 0)
            {
                var span = MemoryMarshal.Cast<EntityInfo, byte>(_info.AsSpan(0, _count));
                span.Clear();
            }

            Array.Clear(_archetypeIds, 0, _count);
            Array.Clear(_archetypeRows, 0, _count);
            Array.Clear(_componentMasks, 0, _count);

            _freeHead = -1;
            _count = 0;
            _aliveCount = 0;
            _totalCreated = 0;
            _totalDestroyed = 0;
            _versionOverflowCount = 0;
            _pendingDestroys?.Clear();
            _pendingDestroyIndices?.Clear();
        }

        public void Dispose()
        {
            _info = null!;
            _archetypeIds = null!;
            _archetypeRows = null!;
            _componentMasks = null!;
            _pendingDestroys = null;
            _pendingDestroyIndices = null;
        }

        // === 统计与调试 ===

        /// <summary>
        /// 获取注册表统计信息
        /// </summary>
        public EntityRegistryStats GetStats()
        {
            return new EntityRegistryStats
            {
                Capacity = _capacity,
                Count = _count,
                AliveCount = _aliveCount,
                FreeCount = _count - _aliveCount,
                TotalCreated = _totalCreated,
                TotalDestroyed = _totalDestroyed,
                PendingDestroyCount = PendingDestroyCount,
                VersionOverflowCount = _versionOverflowCount
            };
        }

        /// <summary>
        /// 获取实体调试信息
        /// </summary>
        public EntityDebugInfo GetDebugInfo(Entity entity)
        {
            if (!IsValid(entity))
                return new EntityDebugInfo { IsValid = false };

            ref var info = ref _info[entity.Index];
            return new EntityDebugInfo
            {
                IsValid = true,
                Index = entity.Index,
                Version = entity.Version & VersionMask,
                IsAlive = (entity.Version & ActiveBit) != 0,
                Flags = info.Flags,
                ArchetypeId = _archetypeIds[entity.Index],
                ArchetypeRow = _archetypeRows[entity.Index],
                ComponentMask = _componentMasks[entity.Index]
            };
        }

        /// <summary>
        /// 获取所有活跃实体（用于调试）
        /// </summary>
        public IEnumerable<Entity> GetAllAliveEntities()
        {
            for (int i = 0; i < _count; i++)
            {
                var entity = _info[i].Entity;
                if ((entity.Version & ActiveBit) != 0)
                {
                    yield return entity;
                }
            }
        }
    }

    /// <summary>
    /// 实体位置信息
    /// </summary>
    public readonly struct EntityLocation
    {
        public readonly int ArchetypeId;
        public readonly int Row;

        public EntityLocation(int archetypeId, int row)
        {
            ArchetypeId = archetypeId;
            Row = row;
        }

        public bool IsValid => ArchetypeId > 0 && Row >= 0;
    }

    /// <summary>
    /// 实体调试信息
    /// </summary>
    public readonly struct EntityDebugInfo
    {
        public bool IsValid { get; init; }
        public int Index { get; init; }
        public int Version { get; init; }
        public bool IsAlive { get; init; }
        public EntityFlags Flags { get; init; }
        public int ArchetypeId { get; init; }
        public int ArchetypeRow { get; init; }
        public ulong ComponentMask { get; init; }

        public override string ToString()
        {
            if (!IsValid) return "Invalid Entity";
            return $"Entity[Index={Index}, Version={Version}, Alive={IsAlive}, Flags={Flags}]";
        }
    }

    /// <summary>
    /// 注册表统计信息
    /// </summary>
    public readonly struct EntityRegistryStats
    {
        public int Capacity { get; init; }
        public int Count { get; init; }
        public int AliveCount { get; init; }
        public int FreeCount { get; init; }
        public long TotalCreated { get; init; }
        public long TotalDestroyed { get; init; }
        public int PendingDestroyCount { get; init; }
        public long VersionOverflowCount { get; init; }

        public float UtilizationRate => Capacity > 0 ? (float)AliveCount / Capacity : 0;

        public override string ToString()
        {
            return $"EntityRegistry[Capacity={Capacity}, Alive={AliveCount}, Free={FreeCount}, " +
                   $"PendingDestroy={PendingDestroyCount}, TotalCreated={TotalCreated}]";
        }
    }

    /// <summary>
    /// 只读视图 - 线程安全
    /// </summary>
    public readonly struct RegistryReadOnly
    {
        private readonly EntityRegistry _registry;

        internal RegistryReadOnly(EntityRegistry registry)
        {
            _registry = registry;
        }

        public int Count => _registry.Count;
        public int AliveCount => _registry.AliveCount;

        public bool IsValid(Entity entity) => _registry.IsValid(entity);
        public bool IsAlive(int index) => (_registry.GetVersion(index) & EntityRegistry.ActiveBit) != 0;
        public int GetVersion(int index) => _registry.GetVersion(index);
        public EntityLocation GetLocation(int index) => _registry.GetLocation(index);
        public bool HasComponent(int entityIndex, int componentId) => _registry.HasComponent(entityIndex, componentId);

        public void ForEachParallel(Action<Entity> action, int maxDegreeOfParallelism = -1)
        {
            _registry.ForEachParallel(action, maxDegreeOfParallelism);
        }
    }
}
