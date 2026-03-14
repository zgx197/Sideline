using System;
using System.Collections.Generic;
using Lattice.Core;
using Lattice.Math;
using Xunit;

namespace Lattice.Tests.Core
{
    /// <summary>
    /// EntityRegistry P2/P3 高级功能单元测试
    /// 覆盖：Branchless Validation、Entity Reservation、Statistics、Cache Line Alignment
    /// </summary>
    public class EntityBranchlessTests
    {
        [Fact]
        public void IsValidBranchless_ValidEntity_ReturnsTrue()
        {
            var registry = new EntityRegistry();
            var entity = registry.Create();
            
            Assert.True(registry.IsValidBranchless(entity));
        }

        [Fact]
        public void IsValidBranchless_InvalidEntity_ReturnsFalse()
        {
            var registry = new EntityRegistry();
            var entity = registry.Create();
            registry.Destroy(entity);
            
            Assert.False(registry.IsValidBranchless(entity));
        }

        [Fact]
        public void IsValidBranchless_EmptyEntity_ReturnsFalse()
        {
            var registry = new EntityRegistry();
            
            Assert.False(registry.IsValidBranchless(Entity.None));
        }

        [Fact]
        public void IsValidFast_ValidEntity_ReturnsTrue()
        {
            var registry = new EntityRegistry();
            var entity = registry.Create();
            
            Assert.True(registry.IsValidFast(entity));
        }

        [Fact]
        public void ValidateBatch_MixedEntities_ReturnsCorrectResults()
        {
            var registry = new EntityRegistry();
            var e1 = registry.Create();
            var e2 = registry.Create();
            var e3 = registry.Create();
            registry.Destroy(e2);
            
            Span<Entity> entities = stackalloc Entity[] { e1, e2, e3 };
            Span<bool> results = stackalloc bool[3];
            
            registry.ValidateBatch(entities, results);
            
            Assert.True(results[0]);  // e1 有效
            Assert.False(results[1]); // e2 已销毁
            Assert.True(results[2]);  // e3 有效
        }
    }

    /// <summary>
    /// 实体预留功能测试
    /// </summary>
    public class EntityReservationTests
    {
        [Fact]
        public void Reserve_CreatesNonActiveEntity()
        {
            var registry = new EntityRegistry();
            var reserved = registry.Reserve();
            
            Assert.True(reserved.IsValid);
            Assert.False(registry.IsAlive(reserved.Index));
            Assert.Equal(0, registry.AliveCount);
            Assert.Equal(1, registry.ReservedCount);
        }

        [Fact]
        public void Reserve_IncrementsTotalReserved()
        {
            var registry = new EntityRegistry();
            
            registry.Reserve();
            registry.Reserve();
            registry.Reserve();
            
            var stats = registry.GetStats();
            Assert.Equal(3, stats.TotalReserved);
        }

        [Fact]
        public void ActivateReserved_MakesEntityAlive()
        {
            var registry = new EntityRegistry();
            var reserved = registry.Reserve();
            
            Assert.True(registry.ActivateReserved(reserved));
            
            Assert.True(registry.IsAlive(reserved.Index));
            Assert.Equal(1, registry.AliveCount);
            Assert.Equal(0, registry.ReservedCount);
        }

        [Fact]
        public void ActivateReserved_UpdatesVersion()
        {
            var registry = new EntityRegistry();
            var reserved = registry.Reserve();
            int oldVersion = reserved.Version;
            
            registry.ActivateReserved(reserved);
            
            // 激活后版本号应设置活跃标志
            Assert.True((registry.GetVersion(reserved.Index) & EntityRegistry.ActiveBit) != 0);
        }

        [Fact]
        public void ActivateReserved_InvalidEntity_ReturnsFalse()
        {
            var registry = new EntityRegistry();
            var invalid = new Entity(999, 1);
            
            Assert.False(registry.ActivateReserved(invalid));
        }

        [Fact]
        public void ActivateReserved_AlreadyActive_ReturnsFalse()
        {
            var registry = new EntityRegistry();
            var active = registry.Create(); // 直接创建是活跃的
            
            Assert.False(registry.ActivateReserved(active));
        }

        [Fact]
        public void CancelReservation_ReturnsToFreeList()
        {
            var registry = new EntityRegistry();
            var reserved = registry.Reserve();
            int index = reserved.Index;
            
            Assert.True(registry.CancelReservation(reserved));
            
            // 取消后应该可以复用该槽位
            var newEntity = registry.Create();
            Assert.Equal(index, newEntity.Index);
        }

        [Fact]
        public void CancelReservation_InvalidEntity_ReturnsFalse()
        {
            var registry = new EntityRegistry();
            var invalid = new Entity(999, 1);
            
            Assert.False(registry.CancelReservation(invalid));
        }

        [Fact]
        public void ReserveBatch_CreatesMultiple()
        {
            var registry = new EntityRegistry();
            Span<Entity> reserved = stackalloc Entity[10];
            
            registry.ReserveBatch(reserved);
            
            Assert.Equal(10, registry.ReservedCount);
            foreach (var entity in reserved)
            {
                Assert.True(entity.IsValid);
                Assert.False(registry.IsAlive(entity.Index));
            }
        }

        [Fact]
        public void GetReservedEntities_ReturnsCorrectCount()
        {
            var registry = new EntityRegistry();
            registry.Reserve();
            registry.Reserve();
            registry.Reserve();
            
            Span<Entity> buffer = stackalloc Entity[5];
            int count = registry.GetReservedEntities(buffer);
            
            Assert.Equal(3, count);
        }

        [Fact]
        public void Reserved_DoesNotAffectAliveCount()
        {
            var registry = new EntityRegistry();
            var active = registry.Create();
            var reserved = registry.Reserve();
            
            Assert.Equal(1, registry.AliveCount);
            Assert.Equal(1, registry.ReservedCount);
            Assert.Equal(2, registry.Count);
        }

        [Fact]
        public void ReserveBatch_LargeCount_ExpandsReservedArray()
        {
            var registry = new EntityRegistry(4); // 小初始容量
            Span<Entity> reserved = stackalloc Entity[100];
            
            registry.ReserveBatch(reserved);
            
            Assert.Equal(100, registry.ReservedCount);
        }

        [Fact]
        public void ActivateReserved_IncrementsTotalActivated()
        {
            var registry = new EntityRegistry();
            var reserved = registry.Reserve();
            
            registry.ActivateReserved(reserved);
            
            var stats = registry.GetStats();
            Assert.Equal(1, stats.TotalActivated);
        }
    }

    /// <summary>
    /// 统计与诊断测试
    /// </summary>
    public class EntityStatisticsTests
    {
        [Fact]
        public void GetStats_InitialState_IsZero()
        {
            var registry = new EntityRegistry();
            var stats = registry.GetStats();
            
            Assert.Equal(0, stats.CurrentAlive);
            Assert.Equal(0, stats.CurrentFree);
            Assert.Equal(0, stats.TotalCreated);
            Assert.Equal(0, stats.FragmentationRatio);
        }

        [Fact]
        public void GetStats_AfterCreate_UpdatesCorrectly()
        {
            var registry = new EntityRegistry();
            registry.Create();
            registry.Create();
            
            var stats = registry.GetStats();
            
            Assert.Equal(2, stats.CurrentAlive);
            Assert.Equal(2, stats.TotalCreated);
            Assert.Equal(0, stats.FragmentationRatio); // 无碎片
        }

        [Fact]
        public void GetStats_AfterDestroy_ShowsFragmentation()
        {
            var registry = new EntityRegistry();
            var e1 = registry.Create();
            var e2 = registry.Create();
            registry.Destroy(e1);
            
            var stats = registry.GetStats();
            
            Assert.Equal(1, stats.CurrentAlive);
            Assert.Equal(1, stats.CurrentFree);
            Assert.True(stats.FragmentationRatio > 0);
        }

        [Fact]
        public void GetStats_ReuseRatio_CalculatesCorrectly()
        {
            var registry = new EntityRegistry();
            var entity = registry.Create();
            registry.Destroy(entity);
            registry.Create(); // 复用
            
            var stats = registry.GetStats();
            
            Assert.True(stats.ReuseRatio > 0);
        }

        [Fact]
        public void GetStats_MemoryUsed_IsPositive()
        {
            var registry = new EntityRegistry();
            
            var stats = registry.GetStats();
            
            Assert.True(stats.MemoryUsed > 0);
        }

        [Fact]
        public void GetStats_CacheLineEfficiency_WhenAllActive_IsOne()
        {
            var registry = new EntityRegistry();
            // 创建少量实体并全部保持活跃
            for (int i = 0; i < 16; i++)
            {
                registry.Create();
            }
            
            var stats = registry.GetStats();
            
            // 所有实体活跃，缓存效率应为 1
            Assert.True(stats.CacheLineEfficiency == (FP)1);
        }

        [Fact]
        public void GetStats_ReservedRatio_CalculatesCorrectly()
        {
            var registry = new EntityRegistry();
            registry.Reserve();
            registry.Reserve();
            var r3 = registry.Reserve();
            registry.ActivateReserved(r3);
            
            var stats = registry.GetStats();
            
            Assert.Equal(3, stats.TotalReserved);
            Assert.Equal(1, stats.TotalActivated);
            Assert.True(stats.ReservedRatio > 0);
        }

        [Fact]
        public void GetDiagnosticsReport_ContainsExpectedSections()
        {
            var registry = new EntityRegistry();
            registry.Create();
            registry.Reserve();
            
            var report = registry.GetDiagnosticsReport();
            
            Assert.Contains("Entity Registry Diagnostics", report);
            Assert.Contains("Alive:", report);
            Assert.Contains("Reserved:", report);
            Assert.Contains("Used:", report);
            Assert.Contains("Recommendations", report);
        }

        [Fact]
        public void PeakAlive_TracksMaximum()
        {
            var registry = new EntityRegistry();
            var e1 = registry.Create();
            var e2 = registry.Create();
            var e3 = registry.Create();
            registry.Destroy(e2);
            
            var stats = registry.GetStats();
            
            Assert.Equal(3, stats.PeakAlive);
            Assert.Equal(2, stats.CurrentAlive);
        }

        [Fact]
        public void PeakReserved_TracksMaximum()
        {
            var registry = new EntityRegistry();
            registry.Reserve();
            registry.Reserve();
            registry.Reserve();
            
            var stats = registry.GetStats();
            
            Assert.Equal(3, stats.PeakReserved);
        }
    }

    /// <summary>
    /// 缓存行对齐测试
    /// </summary>
    public class EntityCacheLineTests
    {
        [Fact]
        public void ForEachAliveAligned_ProcessesAllActive()
        {
            var registry = new EntityRegistry();
            var entities = new List<Entity>();
            
            // 创建多个实体
            for (int i = 0; i < 100; i++)
            {
                entities.Add(registry.Create());
            }
            
            // 销毁一些（非对齐边界）
            for (int i = 0; i < 50; i++)
            {
                registry.Destroy(entities[i]);
            }
            
            var found = new List<Entity>();
            registry.ForEachAliveAligned(e => found.Add(e));
            
            Assert.Equal(50, found.Count);
        }

        [Fact]
        public void CacheLineSize_Is64()
        {
            // 标准 x86_64 缓存行大小
            Assert.Equal(64, EntityRegistry.CacheLineSize);
        }

        [Fact]
        public void ForEachAliveAligned_EmptyRegistry_DoesNothing()
        {
            var registry = new EntityRegistry();
            int count = 0;
            
            registry.ForEachAliveAligned(_ => count++);
            
            Assert.Equal(0, count);
        }

        [Fact]
        public void ForEachAliveAligned_CrossesCacheLines()
        {
            var registry = new EntityRegistry();
            const int batchSize = EntityRegistry.CacheLineSize / sizeof(int); // 16
            
            // 创建跨越多个缓存行的实体
            for (int i = 0; i < batchSize * 3; i++)
            {
                registry.Create();
            }
            
            var found = new List<Entity>();
            registry.ForEachAliveAligned(e => found.Add(e));
            
            Assert.Equal(batchSize * 3, found.Count);
        }
    }

    /// <summary>
    /// 综合场景测试
    /// </summary>
    public class EntityIntegrationTests
    {
        [Fact]
        public void ComplexLifecycle_CreateReserveActivateDestroy()
        {
            var registry = new EntityRegistry();
            
            // 1. 创建一些活跃实体
            var active1 = registry.Create();
            var active2 = registry.Create();
            
            // 2. 预留一些实体
            var reserved1 = registry.Reserve();
            var reserved2 = registry.Reserve();
            
            // 3. 销毁一个活跃实体
            registry.Destroy(active1);
            
            // 4. 激活一个预留实体
            registry.ActivateReserved(reserved1);
            
            // 5. 取消另一个预留
            registry.CancelReservation(reserved2);
            
            // 验证状态
            Assert.Equal(2, registry.AliveCount); // active2 + reserved1
            Assert.Equal(0, registry.ReservedCount); // reserved1 已激活，reserved2 已取消
            Assert.Equal(2, registry.FreeCount); // active1 + reserved2 的位置
            
            // 统计验证
            var stats = registry.GetStats();
            Assert.Equal(2, stats.CurrentAlive);
            Assert.Equal(2, stats.TotalReserved);
            Assert.Equal(1, stats.TotalActivated);
        }

        [Fact]
        public void BulkOperations_MixedScenario()
        {
            var registry = new EntityRegistry(1024);
            
            // 批量创建
            Span<Entity> batch1 = stackalloc Entity[100];
            registry.CreateBatch(batch1);
            
            // 批量预留
            Span<Entity> reserved = stackalloc Entity[50];
            registry.ReserveBatch(reserved);
            
            // 销毁一半
            for (int i = 0; i < 50; i++)
            {
                registry.Destroy(batch1[i]);
            }
            
            // 激活一半预留
            for (int i = 0; i < 25; i++)
            {
                registry.ActivateReserved(reserved[i]);
            }
            
            // 验证
            Assert.Equal(75, registry.AliveCount); // 50 (batch1剩余) + 25 (激活)
            Assert.Equal(25, registry.ReservedCount); // 50 - 25 未激活
            
            // 诊断报告应无警告
            var report = registry.GetDiagnosticsReport();
            Assert.NotNull(report);
        }

        [Fact]
        public void ReuseScenario_CreateDestroyRecreate()
        {
            var registry = new EntityRegistry();
            
            // 模拟游戏循环：创建-销毁-再创建
            for (int cycle = 0; cycle < 10; cycle++)
            {
                var entity = registry.Create();
                int index = entity.Index;
                int version = entity.Version & EntityRegistry.VersionMask;
                
                registry.Destroy(entity);
                
                var newEntity = registry.Create();
                
                // 应复用相同索引
                Assert.Equal(index, newEntity.Index);
                // 版本号应递增
                int newVersion = newEntity.Version & EntityRegistry.VersionMask;
                Assert.True(newVersion > version || (version == 0x7FFFFFFF && newVersion == 1));
                
                // 清理
                registry.Destroy(newEntity);
            }
            
            var stats = registry.GetStats();
            Assert.Equal(20, stats.TotalCreated); // 每个循环创建 2 个
            Assert.Equal(20, stats.TotalDestroyed); // 每个循环销毁 2 个
            Assert.True(stats.ReuseRatio > FP.FromRaw(58982)); // 0.9 in FP raw
        }
    }
}
