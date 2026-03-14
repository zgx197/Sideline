using System;
using System.Linq;
using Lattice.Core;
using Xunit;

namespace Lattice.Tests.Core
{
    /// <summary>
    /// Entity 结构体的单元测试
    /// </summary>
    public class EntityTests
    {
        [Fact]
        public void Entity_Default_IsNone()
        {
            var entity = default(Entity);
            Assert.Equal(Entity.None, entity);
            Assert.False(entity.IsValid);
            Assert.Equal(0u, (uint)entity.Index);
            Assert.Equal(0u, (uint)entity.Version);
        }

        [Fact]
        public void Entity_Create_IsValid()
        {
            var entity = new Entity(0, 1 | EntityRegistry.ActiveBit);
            Assert.True(entity.IsValid);
            Assert.Equal(0, entity.Index);
            Assert.True((entity.Version & EntityRegistry.ActiveBit) != 0);
        }

        [Fact]
        public void Entity_RawValue_CombinesIndexAndVersion()
        {
            var entity = new Entity(42, 0x12345678);
            ulong expectedRaw = 42uL | (0x12345678uL << 32);
            Assert.Equal(expectedRaw, entity.Raw);
        }

        [Fact]
        public void Entity_Equality_SameIndexAndVersion()
        {
            var a = new Entity(5, 10 | EntityRegistry.ActiveBit);
            var b = new Entity(5, 10 | EntityRegistry.ActiveBit);
            var c = new Entity(5, 11 | EntityRegistry.ActiveBit);
            var d = new Entity(6, 10 | EntityRegistry.ActiveBit);

            Assert.Equal(a, b);
            Assert.True(a == b);
            Assert.False(a == c);
            Assert.False(a == d);
        }

        [Fact]
        public void Entity_HashCode_Consistent()
        {
            var a = new Entity(100, 200 | EntityRegistry.ActiveBit);
            var b = new Entity(100, 200 | EntityRegistry.ActiveBit);

            Assert.Equal(a.GetHashCode(), b.GetHashCode());
        }

        [Fact]
        public void Entity_ObjectEquals_Works()
        {
            var entity = new Entity(1, 2 | EntityRegistry.ActiveBit);
            object obj = new Entity(1, 2 | EntityRegistry.ActiveBit);

            Assert.True(entity.Equals(obj));
        }

        [Fact]
        public void Entity_ToString_Formatted()
        {
            var active = new Entity(42, 5 | EntityRegistry.ActiveBit);
            var inactive = new Entity(42, 5);  // 非活跃

            Assert.Equal("E.00042.005", active.ToString());
            Assert.Equal("E.00042.005-", inactive.ToString());
        }

        [Fact]
        public void Entity_TryParse_Active()
        {
            var str = "E.00123.045";
            Assert.True(Entity.TryParse(str.AsSpan(), out var entity));
            Assert.Equal(123, entity.Index);
            Assert.Equal(45 | EntityRegistry.ActiveBit, entity.Version);
        }

        [Fact]
        public void Entity_TryParse_Inactive()
        {
            var str = "E.00123.045-";
            Assert.True(Entity.TryParse(str.AsSpan(), out var entity));
            Assert.Equal(123, entity.Index);
            Assert.Equal(45, entity.Version & EntityRegistry.VersionMask);
            Assert.False((entity.Version & EntityRegistry.ActiveBit) != 0);
        }

        [Fact]
        public void Entity_TryParse_Invalid()
        {
            Assert.False(Entity.TryParse("invalid".AsSpan(), out _));
            Assert.False(Entity.TryParse("X.1.2".AsSpan(), out _));
            Assert.False(Entity.TryParse("E.abc.2".AsSpan(), out _));
        }
    }

    /// <summary>
    /// EntityRegistry 的单元测试（SOA 布局 + 嵌入空闲链表）
    /// </summary>
    public class EntityRegistryTests
    {
        [Fact]
        public void Registry_CreateEntity_ReturnsValidEntity()
        {
            var registry = new EntityRegistry();
            var entity = registry.Create();

            Assert.True(entity.IsValid);
            Assert.True(registry.IsValid(entity));
            Assert.Equal(1, registry.AliveCount);
            Assert.Equal(1, registry.TotalCreated);
        }

        [Fact]
        public void Registry_DestroyEntity_RemovesEntity()
        {
            var registry = new EntityRegistry();
            var entity = registry.Create();

            Assert.True(registry.Destroy(entity));
            Assert.False(registry.IsValid(entity));
            Assert.Equal(0, registry.AliveCount);
            Assert.Equal(1, registry.TotalDestroyed);
        }

        [Fact]
        public void Registry_DestroyInvalidEntity_ReturnsFalse()
        {
            var registry = new EntityRegistry();
            var invalid = new Entity(999, 1 | EntityRegistry.ActiveBit);

            Assert.False(registry.Destroy(invalid));
        }

        [Fact]
        public void Registry_DoubleDestroy_ReturnsFalse()
        {
            var registry = new EntityRegistry();
            var entity = registry.Create();

            Assert.True(registry.Destroy(entity));
            Assert.False(registry.Destroy(entity)); // 第二次销毁应失败
        }

        [Fact]
        public void Registry_ReuseSlot_VersionIncrements()
        {
            var registry = new EntityRegistry();
            var entity1 = registry.Create();
            int index = entity1.Index;
            int version1 = entity1.Version & EntityRegistry.VersionMask;

            registry.Destroy(entity1);
            var entity2 = registry.Create();

            Assert.Equal(index, entity2.Index); // 复用相同槽位
            int version2 = entity2.Version & EntityRegistry.VersionMask;
            Assert.True(version2 > version1); // 版本号递增
        }

        [Fact]
        public void Registry_ReuseSlot_OldReferenceInvalid()
        {
            var registry = new EntityRegistry();
            var oldEntity = registry.Create();
            registry.Destroy(oldEntity);
            var newEntity = registry.Create();

            // 旧引用应失效
            Assert.False(registry.IsValid(oldEntity));
            Assert.True(registry.IsValid(newEntity));
        }

        [Fact]
        public void Registry_MultipleEntities_TrackedCorrectly()
        {
            var registry = new EntityRegistry();
            var e1 = registry.Create();
            var e2 = registry.Create();
            var e3 = registry.Create();

            Assert.Equal(3, registry.AliveCount);

            registry.Destroy(e2);

            Assert.Equal(2, registry.AliveCount);
            Assert.True(registry.IsValid(e1));
            Assert.False(registry.IsValid(e2));
            Assert.True(registry.IsValid(e3));
        }

        [Fact]
        public void Registry_Clear_RemovesAll()
        {
            var registry = new EntityRegistry();
            var e1 = registry.Create();
            var e2 = registry.Create();

            registry.Clear();

            Assert.Equal(0, registry.AliveCount);
            Assert.Equal(0, registry.Count);
            Assert.False(registry.IsValid(e1));
            Assert.False(registry.IsValid(e2));
        }

        [Fact]
        public void Registry_GetAliveEntities_ReturnsOnlyAlive()
        {
            var registry = new EntityRegistry();
            var e1 = registry.Create();
            var e2 = registry.Create();
            var e3 = registry.Create();
            registry.Destroy(e2);

            Span<Entity> buffer = stackalloc Entity[10];
            int count = registry.GetAliveEntities(buffer);

            Assert.Equal(2, count);
            
            // 转换为数组方便断言
            var alive = buffer.Slice(0, count).ToArray();
            Assert.Contains(e1, alive);
            Assert.Contains(e3, alive);
            Assert.DoesNotContain(e2, alive);
        }

        [Fact]
        public void Registry_Grows_WhenCapacityExceeded()
        {
            var registry = new EntityRegistry(4); // 小初始容量
            int initialCapacity = registry.Capacity;

            // 创建超过初始容量的实体
            for (int i = 0; i < 10; i++)
            {
                registry.Create();
            }

            Assert.True(registry.Capacity > initialCapacity);
            Assert.Equal(10, registry.AliveCount);
        }

        [Fact]
        public void Registry_Enumerator_IteratesAliveEntities()
        {
            var registry = new EntityRegistry();
            var e1 = registry.Create();
            var e2 = registry.Create();
            var e3 = registry.Create();
            registry.Destroy(e2);

            var alive = new System.Collections.Generic.List<Entity>();
            foreach (var entity in registry.GetAliveEnumerable())
            {
                alive.Add(entity);
            }

            Assert.Equal(2, alive.Count);
            Assert.Contains(e1, alive);
            Assert.Contains(e3, alive);
            Assert.DoesNotContain(e2, alive);
        }

        [Fact]
        public void Registry_CreateBatch_CreatesMultiple()
        {
            var registry = new EntityRegistry();
            Span<Entity> entities = stackalloc Entity[100];
            
            registry.CreateBatch(entities);

            Assert.Equal(100, registry.AliveCount);
            
            // 验证所有实体都有效
            foreach (var entity in entities)
            {
                Assert.True(registry.IsValid(entity));
            }
        }

        [Fact]
        public void Registry_CreateBatch_ReusesSlots()
        {
            var registry = new EntityRegistry();
            
            // 创建并销毁一些实体（按 0-9 顺序销毁）
            Span<Entity> first = stackalloc Entity[10];
            registry.CreateBatch(first);
            foreach (var e in first) registry.Destroy(e);
            
            // 再次批量创建，应复用槽位（LIFO 顺序：9,8,7,6,5...）
            Span<Entity> second = stackalloc Entity[5];
            registry.CreateBatch(second);

            Assert.Equal(5, registry.AliveCount);
            // LIFO: 最后销毁的先复用，所以 second[0] 应该是 first[9]
            for (int i = 0; i < 5; i++)
            {
                Assert.Equal(first[9 - i].Index, second[i].Index);
                // 但版本号应不同
                Assert.NotEqual(first[9 - i].Version, second[i].Version);
            }
        }

        [Fact]
        public void Registry_IsAlive_ChecksActiveBit()
        {
            var registry = new EntityRegistry();
            var entity = registry.Create();
            
            Assert.True(registry.IsAlive(entity.Index));
            
            registry.Destroy(entity);
            
            Assert.False(registry.IsAlive(entity.Index));
        }

        [Fact]
        public void Registry_GetSetArchetypeId_Works()
        {
            var registry = new EntityRegistry();
            var entity = registry.Create();
            
            Assert.Equal(0, registry.GetArchetypeId(entity.Index));
            
            registry.SetArchetypeId(entity.Index, 42);
            
            Assert.Equal(42, registry.GetArchetypeId(entity.Index));
        }

        [Fact]
        public void Registry_Stats_TracksCorrectly()
        {
            var registry = new EntityRegistry();
            
            var e1 = registry.Create();
            var e2 = registry.Create();
            var e3 = registry.Create();
            registry.Destroy(e2);
            
            var stats = registry.GetStats();
            
            Assert.Equal(3, stats.TotalCreated);
            Assert.Equal(1, stats.TotalDestroyed);
            Assert.Equal(2, stats.CurrentAlive);
            Assert.Equal(1, stats.CurrentFree);
            Assert.Equal(3, stats.PeakAlive);
            Assert.True(stats.ReuseRatio > 0);
            Assert.True(stats.MemoryUsed > 0);
        }

        [Fact]
        public void Registry_EnsureCapacity_PreventsGrowth()
        {
            var registry = new EntityRegistry(4);
            registry.EnsureCapacity(100);
            
            Assert.True(registry.Capacity >= 100);
            
            // 创建 50 个实体不应触发扩容
            for (int i = 0; i < 50; i++)
            {
                registry.Create();
            }
            
            Assert.True(registry.Capacity >= 100);
        }

        [Fact]
        public void Registry_IsValidFast_BypassesChecks()
        {
            var registry = new EntityRegistry();
            var entity = registry.Create();
            
            // 在有效范围内，IsValidFast 应与 IsValid 结果相同
            Assert.Equal(registry.IsValid(entity), registry.IsValidFast(entity));
        }

        [Fact]
        public void Registry_MassiveReuse_HandlesCorrectly()
        {
            var registry = new EntityRegistry();
            
            // 大量创建销毁循环，测试版本号管理
            for (int cycle = 0; cycle < 100; cycle++)
            {
                var entity = registry.Create();
                int index = entity.Index;
                int version = entity.Version & EntityRegistry.VersionMask;
                
                registry.Destroy(entity);
                
                var newEntity = registry.Create();
                Assert.Equal(index, newEntity.Index);
                int newVersion = newEntity.Version & EntityRegistry.VersionMask;
                
                // 版本号应递增
                Assert.True(newVersion > version || (version == 0x7FFFFFFF && newVersion == 1),
                    $"版本号应递增或循环: old={version}, new={newVersion}");
                
                registry.Destroy(newEntity);
            }
        }

        [Fact]
        public void Registry_FreeList_EmbeddedCorrectly()
        {
            var registry = new EntityRegistry();
            
            // 创建 3 个实体
            var e1 = registry.Create();
            var e2 = registry.Create();
            var e3 = registry.Create();
            
            // 销毁 e1 和 e3（LIFO 顺序：e3 先入链表，然后 e1）
            registry.Destroy(e3);
            registry.Destroy(e1);
            
            // 复用应遵循 LIFO：先 e1，后 e3
            var new1 = registry.Create();
            var new2 = registry.Create();
            
            Assert.Equal(e1.Index, new1.Index);  // e1 先被复用
            Assert.Equal(e3.Index, new2.Index);  // e3 后被复用
        }
    }

    /// <summary>
    /// EntityMeta 的单元测试
    /// </summary>
    public class EntityMetaTests
    {
        [Fact]
        public void EntityMeta_FromRegistry_ReadsCorrectly()
        {
            var registry = new EntityRegistry();
            var entity = registry.Create();
            registry.SetArchetypeId(entity.Index, 42);
            
            var meta = EntityMeta.FromRegistry(registry, entity);
            
            Assert.Equal(entity, meta.Ref);
            Assert.Equal(42, meta.ArchetypeId);
            Assert.True(meta.IsActive);
        }

        [Fact]
        public void EntityMeta_FromRegistry_ByIndex()
        {
            var registry = new EntityRegistry();
            var entity = registry.Create();
            
            var meta = EntityMeta.FromRegistry(registry, entity.Index);
            
            Assert.Equal(entity.Index, meta.Ref.Index);
            Assert.True(meta.IsActive);
        }

        [Fact]
        public void EntityMeta_Generation_ExtractsCorrectly()
        {
            var registry = new EntityRegistry();
            var entity = registry.Create();
            
            var meta = EntityMeta.FromRegistry(registry, entity);
            int gen = meta.Generation;
            
            Assert.True(gen > 0);
            Assert.Equal(gen, entity.Version & EntityRegistry.VersionMask);
        }
    }
}
