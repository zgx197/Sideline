using System.Runtime.CompilerServices;

namespace Lattice.Core
{
    /// <summary>
    /// 实体元数据访问器：SOA 布局下的便捷访问结构
    /// 
    /// 注意：这不是实际的存储格式，而是对 EntityRegistry 中 SOA 数组的聚合视图。
    /// 仅用于需要同时访问多个字段的场景，高频操作应直接访问 registry 的方法。
    /// </summary>
    public readonly struct EntityMeta
    {
        /// <summary>
        /// 实体引用
        /// </summary>
        public Entity Ref { get; init; }

        /// <summary>
        /// 实体所在的 Archetype ID
        /// </summary>
        public int ArchetypeId { get; init; }

        /// <summary>
        /// 实体在 Archetype 内的行索引
        /// </summary>
        public int ArchetypeRow { get; init; }

        /// <summary>
        /// 实体标志位
        /// </summary>
        public byte Flags { get; init; }

        /// <summary>
        /// 判断实体是否处于活跃状态
        /// </summary>
        public bool IsActive => (Ref.Version & EntityRegistry.ActiveBit) != 0;

        /// <summary>
        /// 获取纯版本号（去除活跃标志位）
        /// </summary>
        public int Generation => Ref.Version & EntityRegistry.VersionMask;

        /// <summary>
        /// 从 EntityRegistry 获取实体元数据
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static EntityMeta FromRegistry(EntityRegistry registry, int index)
        {
            return new EntityMeta
            (
                new Entity(index, registry.GetVersion(index)),
                registry.GetArchetypeId(index),
                -1,  // TODO: 添加 row 存储
                0    // TODO: 添加 flags 存储
            );
        }

        /// <summary>
        /// 从 EntityRegistry 获取实体元数据
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static EntityMeta FromRegistry(EntityRegistry registry, Entity entity)
        {
            return new EntityMeta
            (
                entity,
                registry.GetArchetypeId(entity.Index),
                -1,
                0
            );
        }

        public EntityMeta(Entity entity, int archetypeId, int archetypeRow, byte flags)
        {
            Ref = entity;
            ArchetypeId = archetypeId;
            ArchetypeRow = archetypeRow;
            Flags = flags;
        }
    }

    /// <summary>
    /// 实体标志位枚举
    /// </summary>
    public enum EntityFlags : byte
    {
        None = 0,
        
        /// <summary>
        /// 实体已被标记为待销毁
        /// </summary>
        DestroyPending = 1 << 0,
        
        /// <summary>
        /// 实体不可被裁剪（Culling）
        /// </summary>
        NotCullable = 1 << 1,
        
        /// <summary>
        /// 实体是静态的（不会移动）
        /// </summary>
        Static = 1 << 2,
        
        /// <summary>
        /// 实体已禁用（不参与更新但保留数据）
        /// </summary>
        Disabled = 1 << 3,
    }
}
