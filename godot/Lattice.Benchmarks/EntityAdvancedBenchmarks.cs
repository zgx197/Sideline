using System;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Lattice.Core;
using Lattice.ECS.Core;

namespace Lattice.Benchmarks
{
    /// <summary>
    /// 实体有效性与组件访问热路径基准测试
    /// </summary>
    [MemoryDiagnoser]
    [RankColumn]
    [SimpleJob(RuntimeMoniker.Net80, launchCount: 1, warmupCount: 3, iterationCount: 5)]
    public class EntityBranchlessBenchmarks
    {
        private Frame _frame = null!;
        private EntityRef[] _activeEntities = null!;
        private EntityRef[] _mixedEntities = null!;

        [Params(100, 1000, 10000)]
        public int EntityCount { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            BenchmarkComponentRegistration.EnsureRegistered();

            _frame = new Frame(EntityCount * 2);
            _activeEntities = new EntityRef[EntityCount];
            _mixedEntities = new EntityRef[EntityCount];

            for (int i = 0; i < EntityCount; i++)
            {
                var entity = _frame.CreateEntity();
                _frame.Add(entity, new PositionComponent(i, i + 1));
                _activeEntities[i] = entity;
                _mixedEntities[i] = entity;
            }

            for (int i = 0; i < EntityCount; i += 4)
            {
                _frame.DestroyEntity(_mixedEntities[i]);
            }
        }

        [Benchmark(Baseline = true)]
        public int ValidateActiveHandles()
        {
            int count = 0;
            for (int i = 0; i < _activeEntities.Length; i++)
            {
                if (_frame.IsValid(_activeEntities[i]))
                {
                    count++;
                }
            }

            return count;
        }

        [Benchmark]
        public int ValidateMixedHandles()
        {
            int count = 0;
            for (int i = 0; i < _mixedEntities.Length; i++)
            {
                if (_frame.IsValid(_mixedEntities[i]))
                {
                    count++;
                }
            }

            return count;
        }

        [Benchmark]
        public int HasPositionOnDenseSet()
        {
            int count = 0;
            for (int i = 0; i < _activeEntities.Length; i++)
            {
                if (_frame.Has<PositionComponent>(_activeEntities[i]))
                {
                    count++;
                }
            }

            return count;
        }

        [Benchmark]
        public int TryGetPositionOnMixedSet()
        {
            int count = 0;
            for (int i = 0; i < _mixedEntities.Length; i++)
            {
                if (_frame.TryGet<PositionComponent>(_mixedEntities[i], out _))
                {
                    count++;
                }
            }

            return count;
        }
    }

    /// <summary>
    /// 批量生命周期操作基准测试
    /// </summary>
    [MemoryDiagnoser]
    [RankColumn]
    [SimpleJob(RuntimeMoniker.Net80, launchCount: 1, warmupCount: 3, iterationCount: 5)]
    public class EntityReservationBenchmarks
    {
        [Params(100, 1000)]
        public int Count { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            BenchmarkComponentRegistration.EnsureRegistered();
        }

        [Benchmark(Baseline = true)]
        public int DirectCreate()
        {
            using var frame = new Frame(Count);

            for (int i = 0; i < Count; i++)
            {
                frame.CreateEntity();
            }

            return frame.EntityCount;
        }

        [Benchmark]
        public int CreateThenDestroyBurst()
        {
            using var frame = new Frame(Count);
            var entities = new EntityRef[Count];

            for (int i = 0; i < Count; i++)
            {
                entities[i] = frame.CreateEntity();
            }

            int destroyed = 0;
            for (int i = 0; i < Count; i++)
            {
                if (frame.IsValid(entities[i]))
                {
                    frame.DestroyEntity(entities[i]);
                    destroyed++;
                }
            }

            return destroyed;
        }

        [Benchmark]
        public int CreateWithComponentBurst()
        {
            using var frame = new Frame(Count);

            for (int i = 0; i < Count; i++)
            {
                var entity = frame.CreateEntity();
                frame.Add(entity, new PositionComponent(i, i));
                frame.Add(entity, new HealthComponent(100));
            }

            return frame.EntityCount;
        }

        [Benchmark]
        public int RecreateAfterDestroy()
        {
            using var frame = new Frame(Count);
            var entities = new EntityRef[Count];

            for (int i = 0; i < Count; i++)
            {
                entities[i] = frame.CreateEntity();
            }

            for (int i = 0; i < Count; i++)
            {
                frame.DestroyEntity(entities[i]);
            }

            int recreated = 0;
            for (int i = 0; i < Count; i++)
            {
                frame.CreateEntity();
                recreated++;
            }

            return recreated;
        }
    }

    /// <summary>
    /// 稠密访问与组件读取模式基准测试
    /// </summary>
    [MemoryDiagnoser]
    [RankColumn]
    [SimpleJob(RuntimeMoniker.Net80, launchCount: 1, warmupCount: 3, iterationCount: 5)]
    public class EntityCacheLineBenchmarks
    {
        private Frame _frame = null!;
        private EntityRef[] _entities = null!;

        [Params(16, 64, 256, 1024)]
        public int EntityCount { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            BenchmarkComponentRegistration.EnsureRegistered();

            _frame = new Frame(EntityCount);
            _entities = new EntityRef[EntityCount];

            for (int i = 0; i < EntityCount; i++)
            {
                var entity = _frame.CreateEntity();
                _frame.Add(entity, new PositionComponent(i, i + 1));

                if ((i & 1) == 0)
                {
                    _frame.Add(entity, new VelocityComponent(i + 2, i + 3));
                }

                _entities[i] = entity;
            }
        }

        [Benchmark(Baseline = true)]
        public int ReadDensePositionComponents()
        {
            int sum = 0;
            for (int i = 0; i < _entities.Length; i++)
            {
                sum += _frame.Get<PositionComponent>(_entities[i]).X;
            }

            return sum;
        }

        [Benchmark]
        public int ReadOptionalVelocityComponents()
        {
            int sum = 0;
            for (int i = 0; i < _entities.Length; i++)
            {
                if (_frame.TryGet<VelocityComponent>(_entities[i], out var velocity))
                {
                    sum += velocity.X;
                }
            }

            return sum;
        }

        [Benchmark]
        public int HasChecksBeforeRead()
        {
            int sum = 0;
            for (int i = 0; i < _entities.Length; i++)
            {
                if (_frame.Has<PositionComponent>(_entities[i]))
                {
                    sum += _frame.Get<PositionComponent>(_entities[i]).Y;
                }
            }

            return sum;
        }
    }

    /// <summary>
    /// 统计聚合类基准测试
    /// </summary>
    [MemoryDiagnoser]
    [RankColumn]
    [SimpleJob(RuntimeMoniker.Net80, launchCount: 1, warmupCount: 3, iterationCount: 5)]
    public class EntityDiagnosticsBenchmarks
    {
        private Frame _frame = null!;
        private EntityRef[] _entities = null!;

        [Params(100, 1000, 10000)]
        public int EntityCount { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            BenchmarkComponentRegistration.EnsureRegistered();

            _frame = new Frame(EntityCount);
            _entities = new EntityRef[EntityCount];

            for (int i = 0; i < EntityCount; i++)
            {
                var entity = _frame.CreateEntity();
                _frame.Add(entity, new PositionComponent(i, i));

                if ((i % 3) != 0)
                {
                    _frame.Add(entity, new HealthComponent(100 - (i % 50)));
                }

                _entities[i] = entity;
            }
        }

        [Benchmark]
        public int CountValidEntities()
        {
            int count = 0;
            for (int i = 0; i < _entities.Length; i++)
            {
                if (_frame.IsValid(_entities[i]))
                {
                    count++;
                }
            }

            return count;
        }

        [Benchmark]
        public int CountEntitiesWithHealth()
        {
            int count = 0;
            for (int i = 0; i < _entities.Length; i++)
            {
                if (_frame.Has<HealthComponent>(_entities[i]))
                {
                    count++;
                }
            }

            return count;
        }

        [Benchmark]
        public int SumHealthValues()
        {
            int total = 0;
            for (int i = 0; i < _entities.Length; i++)
            {
                if (_frame.TryGet<HealthComponent>(_entities[i], out var health))
                {
                    total += health.Value;
                }
            }

            return total;
        }

        [Benchmark]
        public int HashEntityRefs()
        {
            int hash = 17;
            for (int i = 0; i < _entities.Length; i++)
            {
                hash = HashCode.Combine(hash, _entities[i].GetHashCode());
            }

            return hash;
        }
    }

    /// <summary>
    /// 综合场景基准测试
    /// </summary>
    [MemoryDiagnoser]
    [RankColumn]
    [SimpleJob(RuntimeMoniker.Net80, launchCount: 1, warmupCount: 3, iterationCount: 5)]
    public class EntityScenarioBenchmarks
    {
        private const int MaxEntities = 1000;

        [GlobalSetup]
        public void Setup()
        {
            BenchmarkComponentRegistration.EnsureRegistered();
        }

        [Benchmark]
        public int GameFrameSimulation()
        {
            using var frame = new Frame(MaxEntities);
            var entities = new EntityRef[MaxEntities];
            int aliveCount = 0;
            int processed = 0;

            for (int frameIndex = 0; frameIndex < 100; frameIndex++)
            {
                for (int i = 0; i < 5 && aliveCount < MaxEntities; i++)
                {
                    var entity = frame.CreateEntity();
                    frame.Add(entity, new PositionComponent(frameIndex, i));
                    entities[aliveCount++] = entity;
                }

                for (int i = 0; i < 3 && aliveCount > 10; i++)
                {
                    int index = (frameIndex * 3 + i) % (aliveCount - 10) + 10;
                    frame.Remove<PositionComponent>(entities[index]);
                    frame.DestroyEntity(entities[index]);
                    entities[index] = entities[aliveCount - 1];
                    aliveCount--;
                }

                for (int i = 0; i < aliveCount; i++)
                {
                    if (frame.TryGet<PositionComponent>(entities[i], out _))
                    {
                        processed++;
                    }
                }
            }

            return processed;
        }

        [Benchmark]
        public int BulkCreateAddRemove()
        {
            using var frame = new Frame(MaxEntities);
            var entities = new EntityRef[MaxEntities];

            for (int i = 0; i < MaxEntities; i++)
            {
                var entity = frame.CreateEntity();
                frame.Add(entity, new PositionComponent(i, i + 1));
                entities[i] = entity;
            }

            int processed = 0;
            for (int i = 0; i < MaxEntities; i++)
            {
                if ((i & 1) == 0)
                {
                    frame.Add(entities[i], new HealthComponent(100));
                }
                else if (frame.Has<PositionComponent>(entities[i]))
                {
                    frame.Remove<PositionComponent>(entities[i]);
                }

                processed++;
            }

            return processed;
        }

        [Benchmark]
        public int ValidateAndReadScenario()
        {
            using var frame = new Frame(MaxEntities);
            var entities = new EntityRef[MaxEntities];

            for (int i = 0; i < MaxEntities; i++)
            {
                var entity = frame.CreateEntity();
                frame.Add(entity, new PositionComponent(i, i));
                if ((i % 5) == 0)
                {
                    frame.Add(entity, new VelocityComponent(i + 1, i + 2));
                }

                entities[i] = entity;
            }

            int processed = 0;
            for (int i = 0; i < MaxEntities; i++)
            {
                if (!frame.IsValid(entities[i]))
                {
                    continue;
                }

                if (frame.TryGet<VelocityComponent>(entities[i], out var velocity))
                {
                    processed += velocity.X;
                }
                else
                {
                    processed += frame.Get<PositionComponent>(entities[i]).X;
                }
            }

            return processed;
        }
    }
}
