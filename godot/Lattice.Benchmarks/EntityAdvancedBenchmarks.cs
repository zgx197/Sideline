using System;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Lattice.Core;

namespace Lattice.Benchmarks
{
    /// <summary>
    /// EntityRegistry P2/P3 高级功能基准测试
    /// </summary>
    [MemoryDiagnoser]
    [RankColumn]
    [SimpleJob(RuntimeMoniker.Net80, launchCount: 1, warmupCount: 3, iterationCount: 5)]
    public class EntityBranchlessBenchmarks
    {
        private EntityRegistry _registry = null!;
        private Entity[] _entities = null!;
        
        [Params(100, 1000, 10000)]
        public int EntityCount { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            _registry = new EntityRegistry(EntityCount);
            _entities = new Entity[EntityCount];
            
            for (int i = 0; i < EntityCount; i++)
            {
                _entities[i] = _registry.Create();
            }
        }

        /// <summary>
        /// 标准验证（有分支）
        /// </summary>
        [Benchmark(Baseline = true)]
        public int ValidateStandard()
        {
            int count = 0;
            for (int i = 0; i < EntityCount; i++)
            {
                if (_registry.IsValid(_entities[i])) count++;
            }
            return count;
        }

        /// <summary>
        /// 快速验证（无边界检查）
        /// </summary>
        [Benchmark]
        public int ValidateFast()
        {
            int count = 0;
            for (int i = 0; i < EntityCount; i++)
            {
                if (_registry.IsValidFast(_entities[i])) count++;
            }
            return count;
        }

        /// <summary>
        /// 无分支验证（位运算）
        /// </summary>
        [Benchmark]
        public int ValidateBranchless()
        {
            int count = 0;
            for (int i = 0; i < EntityCount; i++)
            {
                if (_registry.IsValidBranchless(_entities[i])) count++;
            }
            return count;
        }

        /// <summary>
        /// 批量验证
        /// </summary>
        [Benchmark]
        public int ValidateBatch()
        {
            Span<bool> results = stackalloc bool[EntityCount];
            return _registry.ValidateBatch(_entities, results);
        }
    }

    /// <summary>
    /// 实体预留功能基准测试
    /// </summary>
    [MemoryDiagnoser]
    [RankColumn]
    public class EntityReservationBenchmarks
    {
        private EntityRegistry _registry = null!;
        private Entity[] _reserved = null!;

        [Params(100, 1000)]
        public int Count { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            _registry = new EntityRegistry(Count * 2);
        }

        /// <summary>
        /// 顺序预留
        /// </summary>
        [Benchmark]
        public int ReserveSequential()
        {
            for (int i = 0; i < Count; i++)
            {
                _registry.Reserve();
            }
            return _registry.ReservedCount;
        }

        /// <summary>
        /// 批量预留
        /// </summary>
        [Benchmark]
        public int ReserveBatch()
        {
            Span<Entity> reserved = stackalloc Entity[Count];
            _registry.ReserveBatch(reserved);
            return _registry.ReservedCount;
        }

        /// <summary>
        /// 预留并激活
        /// </summary>
        [Benchmark]
        public int ReserveAndActivate()
        {
            _reserved = new Entity[Count];
            
            // 预留
            for (int i = 0; i < Count; i++)
            {
                _reserved[i] = _registry.Reserve();
            }
            
            // 激活
            for (int i = 0; i < Count; i++)
            {
                _registry.ActivateReserved(_reserved[i]);
            }
            
            return _registry.AliveCount;
        }

        /// <summary>
        /// 对比：直接创建 vs 预留+激活
        /// </summary>
        [Benchmark(Baseline = true)]
        public int DirectCreate()
        {
            for (int i = 0; i < Count; i++)
            {
                _registry.Create();
            }
            return _registry.AliveCount;
        }
    }

    /// <summary>
    /// 缓存行对齐遍历基准测试
    /// </summary>
    [MemoryDiagnoser]
    [RankColumn]
    public class EntityCacheLineBenchmarks
    {
        private EntityRegistry _registry = null!;
        private const int CacheLineSize = 64;
        private const int EntitiesPerCacheLine = CacheLineSize / sizeof(int); // 16

        [Params(16, 64, 256, 1024)]
        public int EntityCount { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            _registry = new EntityRegistry(EntityCount);
            
            // 创建实体，模拟混合模式（部分活跃）
            for (int i = 0; i < EntityCount; i++)
            {
                _registry.Create();
            }
        }

        /// <summary>
        /// 标准遍历（foreach）
        /// </summary>
        [Benchmark(Baseline = true)]
        public int IterateStandard()
        {
            int count = 0;
            foreach (var entity in _registry.GetAliveEnumerable())
            {
                count++;
            }
            return count;
        }

        /// <summary>
        /// 缓存行对齐遍历
        /// </summary>
        [Benchmark]
        public int IterateCacheLineAligned()
        {
            int count = 0;
            _registry.ForEachAliveAligned(_ => count++);
            return count;
        }

        /// <summary>
        /// 获取活跃实体到 Span
        /// </summary>
        [Benchmark]
        public int GetAliveSpan()
        {
            Span<Entity> buffer = stackalloc Entity[EntityCount];
            return _registry.GetAliveEntities(buffer);
        }
    }

    /// <summary>
    /// 统计与诊断基准测试
    /// </summary>
    [MemoryDiagnoser]
    [RankColumn]
    public class EntityDiagnosticsBenchmarks
    {
        private EntityRegistry _registry = null!;

        [Params(100, 1000, 10000)]
        public int EntityCount { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            _registry = new EntityRegistry(EntityCount);
            
            // 创建混合场景
            for (int i = 0; i < EntityCount; i++)
            {
                if (i % 4 == 0)
                {
                    _registry.Reserve(); // 25% 预留
                }
                else
                {
                    var entity = _registry.Create();
                    if (i % 3 == 0)
                    {
                        _registry.Destroy(entity); // 25% 销毁
                    }
                }
            }
        }

        /// <summary>
        /// 获取统计信息
        /// </summary>
        [Benchmark]
        public EntityStats GetStats()
        {
            return _registry.GetStats();
        }

        /// <summary>
        /// 生成诊断报告
        /// </summary>
        [Benchmark]
        public string GetDiagnosticsReport()
        {
            return _registry.GetDiagnosticsReport();
        }

        /// <summary>
        /// 计算内存使用
        /// </summary>
        [Benchmark]
        public int CalculateMemoryUsage()
        {
            var stats = _registry.GetStats();
            return stats.MemoryUsed;
        }

        /// <summary>
        /// 计算缓存效率
        /// </summary>
        [Benchmark]
        public float CalculateCacheEfficiency()
        {
            var stats = _registry.GetStats();
            return stats.CacheLineEfficiency;
        }
    }

    /// <summary>
    /// 综合场景基准测试
    /// </summary>
    [MemoryDiagnoser]
    [RankColumn]
    public class EntityScenarioBenchmarks
    {
        private const int EntityCount = 1000;

        /// <summary>
        /// 模拟游戏帧：创建、销毁、验证混合
        /// </summary>
        [Benchmark]
        public int GameFrameSimulation()
        {
            var registry = new EntityRegistry(EntityCount);
            var entities = new Entity[EntityCount];
            int entityCount = 0;
            int processed = 0;

            // 模拟 100 帧
            for (int frame = 0; frame < 100; frame++)
            {
                // 每帧创建 5 个新实体
                for (int i = 0; i < 5 && entityCount < EntityCount; i++)
                {
                    entities[entityCount++] = registry.Create();
                }

                // 每帧销毁 3 个旧实体
                for (int i = 0; i < 3 && entityCount > 10; i++)
                {
                    int idx = (frame * 3 + i) % (entityCount - 10) + 10;
                    if (registry.Destroy(entities[idx]))
                    {
                        entities[idx] = entities[--entityCount];
                    }
                }

                // 验证所有实体
                for (int i = 0; i < entityCount; i++)
                {
                    if (registry.IsValid(entities[i])) processed++;
                }
            }

            return processed;
        }

        /// <summary>
        /// 批量创建销毁场景
        /// </summary>
        [Benchmark]
        public int BulkCreateDestroy()
        {
            var registry = new EntityRegistry(EntityCount);
            Span<Entity> buffer = stackalloc Entity[100];
            int processed = 0;

            for (int cycle = 0; cycle < 10; cycle++)
            {
                // 批量创建
                for (int i = 0; i < 10; i++)
                {
                    registry.CreateBatch(buffer);
                    processed += buffer.Length;
                }

                // 批量销毁
                for (int i = 0; i < registry.Count; i++)
                {
                    var entity = new Entity(i, registry.GetVersion(i));
                    if ((entity.Version & EntityRegistry.ActiveBit) != 0)
                    {
                        registry.Destroy(entity);
                    }
                }
            }

            return processed;
        }

        /// <summary>
        /// 预留激活场景
        /// </summary>
        [Benchmark]
        public int ReserveActivateScenario()
        {
            var registry = new EntityRegistry(EntityCount);
            Span<Entity> reserved = stackalloc Entity[50];
            int processed = 0;

            for (int cycle = 0; cycle < 20; cycle++)
            {
                // 批量预留
                registry.ReserveBatch(reserved);

                // 处理部分预留（模拟异步准备）
                for (int i = 0; i < 25; i++)
                {
                    processed++;
                }

                // 激活已处理的
                for (int i = 0; i < 25; i++)
                {
                    registry.ActivateReserved(reserved[i]);
                }

                // 取消未处理的
                for (int i = 25; i < 50; i++)
                {
                    registry.CancelReservation(reserved[i]);
                }
            }

            return processed;
        }
    }
}
