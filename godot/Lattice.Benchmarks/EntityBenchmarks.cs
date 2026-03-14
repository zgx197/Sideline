using System;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Lattice.Core;

namespace Lattice.Benchmarks
{
    /// <summary>
    /// EntityRegistry 性能基准测试
    /// 
    /// 运行方式:
    ///   dotnet run --configuration Release -- --filter "*Entity*"
    /// </summary>
    [MemoryDiagnoser]
    [RankColumn]
    [SimpleJob(RuntimeMoniker.Net80, launchCount: 1, warmupCount: 3, iterationCount: 5)]
    public class EntityRegistryBenchmarks
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
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            _registry.Clear();
        }

        /// <summary>
        /// 测试：顺序创建实体
        /// </summary>
        [Benchmark(Baseline = true)]
        public void CreateSequential()
        {
            var registry = new EntityRegistry(EntityCount);
            for (int i = 0; i < EntityCount; i++)
            {
                registry.Create();
            }
        }

        /// <summary>
        /// 测试：批量创建实体（SOA优化）
        /// </summary>
        [Benchmark]
        public void CreateBatch()
        {
            var registry = new EntityRegistry(EntityCount);
            Span<Entity> entities = stackalloc Entity[EntityCount];
            registry.CreateBatch(entities);
        }

        /// <summary>
        /// 测试：创建并立即销毁（典型游戏循环模式）
        /// </summary>
        [Benchmark]
        public void CreateAndDestroy()
        {
            var registry = new EntityRegistry(EntityCount);
            for (int i = 0; i < EntityCount; i++)
            {
                var entity = registry.Create();
                registry.Destroy(entity);
            }
        }

        /// <summary>
        /// 测试：ID复用性能（创建-销毁-再创建）
        /// </summary>
        [Benchmark]
        public void ReuseIds()
        {
            var registry = new EntityRegistry(EntityCount / 2);
            
            // 第一轮创建
            for (int i = 0; i < EntityCount / 2; i++)
            {
                _entities[i] = registry.Create();
            }
            
            // 全部销毁
            for (int i = 0; i < EntityCount / 2; i++)
            {
                registry.Destroy(_entities[i]);
            }
            
            // 第二轮创建（应复用ID）
            for (int i = 0; i < EntityCount / 2; i++)
            {
                registry.Create();
            }
        }

        /// <summary>
        /// 测试：实体验证性能（高频操作）
        /// </summary>
        [Benchmark]
        public void ValidateEntities()
        {
            // 预创建实体
            for (int i = 0; i < EntityCount; i++)
            {
                _entities[i] = _registry.Create();
            }
            
            // 验证所有实体
            for (int i = 0; i < EntityCount; i++)
            {
                _ = _registry.IsValid(_entities[i]);
            }
        }

        /// <summary>
        /// 测试：快速验证（无边界检查）
        /// </summary>
        [Benchmark]
        public void ValidateFast()
        {
            // 预创建实体
            for (int i = 0; i < EntityCount; i++)
            {
                _entities[i] = _registry.Create();
            }
            
            // 快速验证所有实体
            for (int i = 0; i < EntityCount; i++)
            {
                _ = _registry.IsValidFast(_entities[i]);
            }
        }

        /// <summary>
        /// 测试：遍历活跃实体（foreach）
        /// </summary>
        [Benchmark]
        public void IterateAliveForeach()
        {
            // 预创建实体
            for (int i = 0; i < EntityCount; i++)
            {
                _registry.Create();
            }
            
            int count = 0;
            foreach (var entity in _registry.GetAliveEnumerable())
            {
                count++;
            }
        }

        /// <summary>
        /// 测试：获取活跃实体到Span
        /// </summary>
        [Benchmark]
        public void GetAliveSpan()
        {
            // 预创建实体
            for (int i = 0; i < EntityCount; i++)
            {
                _registry.Create();
            }
            
            Span<Entity> buffer = stackalloc Entity[EntityCount];
            _registry.GetAliveEntities(buffer);
        }

        /// <summary>
        /// 测试：混合操作（模拟真实游戏场景）
        /// 60%创建，30%销毁，10%验证
        /// </summary>
        [Benchmark]
        public void MixedOperations()
        {
            var registry = new EntityRegistry(EntityCount);
            var entities = new Entity[EntityCount];
            int entityCount = 0;
            
            for (int i = 0; i < EntityCount; i++)
            {
                int op = i % 10;
                if (op < 6) // 60% 创建
                {
                    entities[entityCount++] = registry.Create();
                }
                else if (op < 9 && entityCount > 0) // 30% 销毁
                {
                    registry.Destroy(entities[--entityCount]);
                }
                else if (entityCount > 0) // 10% 验证
                {
                    _ = registry.IsValid(entities[i % entityCount]);
                }
            }
        }

        /// <summary>
        /// 测试：ArchetypeID读写性能
        /// </summary>
        [Benchmark]
        public void ArchetypeIdOperations()
        {
            // 预创建实体
            for (int i = 0; i < EntityCount; i++)
            {
                _entities[i] = _registry.Create();
            }
            
            // 读写ArchetypeID
            for (int i = 0; i < EntityCount; i++)
            {
                _registry.SetArchetypeId(_entities[i].Index, i % 10);
                _ = _registry.GetArchetypeId(_entities[i].Index);
            }
        }
    }

    /// <summary>
    /// 内存分配对比测试
    /// </summary>
    [MemoryDiagnoser]
    [RankColumn]
    public class EntityMemoryBenchmarks
    {
        [Params(100, 1000, 10000)]
        public int EntityCount { get; set; }

        /// <summary>
        /// 测试：创建大量实体的内存分配
        /// </summary>
        [Benchmark(Baseline = true)]
        public int CreateEntitiesMemory()
        {
            var registry = new EntityRegistry(EntityCount);
            for (int i = 0; i < EntityCount; i++)
            {
                registry.Create();
            }
            return registry.AliveCount;
        }

        /// <summary>
        /// 测试：获取活跃实体列表的内存分配（旧方式）
        /// 注：这是为了对比，我们实际使用Span方式
        /// </summary>
        [Benchmark]
        public int GetAliveEntitiesArray()
        {
            var registry = new EntityRegistry(EntityCount);
            for (int i = 0; i < EntityCount; i++)
            {
                registry.Create();
            }
            
            // 模拟返回数组（会有分配）
            var array = new Entity[registry.AliveCount];
            Span<Entity> span = stackalloc Entity[registry.AliveCount];
            registry.GetAliveEntities(span);
            span.CopyTo(array);
            return array.Length;
        }

        /// <summary>
        /// 测试：获取活跃实体的零分配方式
        /// </summary>
        [Benchmark]
        public int GetAliveEntitiesSpan()
        {
            var registry = new EntityRegistry(EntityCount);
            for (int i = 0; i < EntityCount; i++)
            {
                registry.Create();
            }
            
            Span<Entity> buffer = stackalloc Entity[EntityCount];
            return registry.GetAliveEntities(buffer);
        }
    }

    /// <summary>
    /// 与理论最优性能对比的微观测试
    /// </summary>
    [MemoryDiagnoser]
    public class EntityMicroBenchmarks
    {
        private EntityRegistry _registry = null!;
        private Entity _entity;

        [GlobalSetup]
        public void Setup()
        {
            _registry = new EntityRegistry(1000);
            _entity = _registry.Create();
        }

        /// <summary>
        /// 测试：单次创建的最小开销
        /// </summary>
        [Benchmark]
        public Entity SingleCreate() => _registry.Create();

        /// <summary>
        /// 测试：单次验证的最小开销
        /// </summary>
        [Benchmark]
        public bool SingleValidate() => _registry.IsValid(_entity);

        /// <summary>
        /// 测试：单次快速验证的最小开销
        /// </summary>
        [Benchmark]
        public bool SingleValidateFast() => _registry.IsValidFast(_entity);

        /// <summary>
        /// 测试：获取版本号的最小开销
        /// </summary>
        [Benchmark]
        public int GetVersion() => _registry.GetVersion(_entity.Index);

        /// <summary>
        /// 测试：检查活跃状态的最小开销
        /// </summary>
        [Benchmark]
        public bool IsAlive() => _registry.IsAlive(_entity.Index);
    }
}
