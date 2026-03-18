using System;
using System.Threading;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Lattice.Core;
using Lattice.ECS.Core;

namespace Lattice.Benchmarks
{
    internal struct PositionComponent
    {
        public int X;
        public int Y;

        public PositionComponent(int x, int y)
        {
            X = x;
            Y = y;
        }
    }

    internal struct VelocityComponent
    {
        public int X;
        public int Y;

        public VelocityComponent(int x, int y)
        {
            X = x;
            Y = y;
        }
    }

    internal struct HealthComponent
    {
        public int Value;

        public HealthComponent(int value)
        {
            Value = value;
        }
    }

    internal static class BenchmarkComponentRegistration
    {
        private static int _initialized;

        public static void EnsureRegistered()
        {
            if (Volatile.Read(ref _initialized) != 0)
            {
                return;
            }

            lock (typeof(BenchmarkComponentRegistration))
            {
                if (_initialized != 0)
                {
                    return;
                }

                RegisterIfNeeded<PositionComponent>();
                RegisterIfNeeded<VelocityComponent>();
                RegisterIfNeeded<HealthComponent>();

                Volatile.Write(ref _initialized, 1);
            }
        }

        private static void RegisterIfNeeded<T>() where T : unmanaged
        {
            if (!ComponentTypeId<T>.IsRegistered)
            {
                ComponentRegistry.Register<T>();
            }
        }
    }

    /// <summary>
    /// 基于当前 Frame / EntityRef 架构的实体生命周期基准测试
    /// </summary>
    [MemoryDiagnoser]
    [RankColumn]
    [SimpleJob(RuntimeMoniker.Net80, launchCount: 1, warmupCount: 3, iterationCount: 5)]
    public class EntityRegistryBenchmarks
    {
        [Params(100, 1000, 10000)]
        public int EntityCount { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            BenchmarkComponentRegistration.EnsureRegistered();
        }

        [Benchmark(Baseline = true)]
        public int CreateSequential()
        {
            using var frame = new Frame(EntityCount);

            for (int i = 0; i < EntityCount; i++)
            {
                frame.CreateEntity();
            }

            return frame.EntityCount;
        }

        [Benchmark]
        public int CreateWithSingleComponent()
        {
            using var frame = new Frame(EntityCount);

            for (int i = 0; i < EntityCount; i++)
            {
                var entity = frame.CreateEntity();
                frame.Add(entity, new PositionComponent(i, i + 1));
            }

            return frame.EntityCount;
        }

        [Benchmark]
        public int CreateAndDestroy()
        {
            using var frame = new Frame(EntityCount);
            var entities = new EntityRef[EntityCount];

            for (int i = 0; i < EntityCount; i++)
            {
                entities[i] = frame.CreateEntity();
            }

            int destroyed = 0;
            for (int i = 0; i < EntityCount; i++)
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
        public int ReuseEntitySlots()
        {
            int initialCount = System.Math.Max(1, EntityCount / 2);
            using var frame = new Frame(initialCount + 16);
            var entities = new EntityRef[initialCount];

            for (int i = 0; i < initialCount; i++)
            {
                entities[i] = frame.CreateEntity();
            }

            for (int i = 0; i < initialCount; i++)
            {
                frame.DestroyEntity(entities[i]);
            }

            int reused = 0;
            for (int i = 0; i < initialCount; i++)
            {
                if (frame.CreateEntity().Index == entities[i].Index)
                {
                    reused++;
                }
            }

            return reused;
        }

        [Benchmark]
        public int ValidateEntities()
        {
            using var frame = new Frame(EntityCount);
            var entities = new EntityRef[EntityCount];

            for (int i = 0; i < EntityCount; i++)
            {
                entities[i] = frame.CreateEntity();
            }

            int validCount = 0;
            for (int i = 0; i < EntityCount; i++)
            {
                if (frame.IsValid(entities[i]))
                {
                    validCount++;
                }
            }

            return validCount;
        }

        [Benchmark]
        public int MixedOperations()
        {
            using var frame = new Frame(EntityCount);
            var entities = new EntityRef[EntityCount];
            int aliveCount = 0;
            int validations = 0;

            for (int i = 0; i < EntityCount; i++)
            {
                int operation = i % 10;
                if (operation < 6)
                {
                    if (aliveCount < entities.Length)
                    {
                        var entity = frame.CreateEntity();
                        frame.Add(entity, new PositionComponent(i, i));
                        entities[aliveCount++] = entity;
                    }
                }
                else if (operation < 8)
                {
                    if (aliveCount > 0)
                    {
                        int last = aliveCount - 1;
                        frame.Remove<PositionComponent>(entities[last]);
                        frame.DestroyEntity(entities[last]);
                        aliveCount = last;
                    }
                }
                else if (aliveCount > 0)
                {
                    var entity = entities[i % aliveCount];
                    if (frame.IsValid(entity) && frame.Has<PositionComponent>(entity))
                    {
                        validations++;
                    }
                }
            }

            return validations + aliveCount;
        }

        [Benchmark]
        public int AddRemoveComponents()
        {
            using var frame = new Frame(EntityCount);
            var entities = new EntityRef[EntityCount];

            for (int i = 0; i < EntityCount; i++)
            {
                entities[i] = frame.CreateEntity();
                frame.Add(entities[i], new PositionComponent(i, i));
            }

            int removed = 0;
            for (int i = 0; i < EntityCount; i++)
            {
                if (frame.Has<PositionComponent>(entities[i]))
                {
                    frame.Remove<PositionComponent>(entities[i]);
                    removed++;
                }
            }

            return removed;
        }
    }

    /// <summary>
    /// 实体与组件分配规模基准测试
    /// </summary>
    [MemoryDiagnoser]
    [RankColumn]
    public class EntityMemoryBenchmarks
    {
        [Params(100, 1000, 10000)]
        public int EntityCount { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            BenchmarkComponentRegistration.EnsureRegistered();
        }

        [Benchmark(Baseline = true)]
        public int CreateEntitiesOnly()
        {
            using var frame = new Frame(EntityCount);

            for (int i = 0; i < EntityCount; i++)
            {
                frame.CreateEntity();
            }

            return frame.EntityCount;
        }

        [Benchmark]
        public int CreateEntitiesWithOneComponent()
        {
            using var frame = new Frame(EntityCount);

            for (int i = 0; i < EntityCount; i++)
            {
                var entity = frame.CreateEntity();
                frame.Add(entity, new PositionComponent(i, i + 1));
            }

            return frame.EntityCount;
        }

        [Benchmark]
        public int CreateEntitiesWithThreeComponents()
        {
            using var frame = new Frame(EntityCount);

            for (int i = 0; i < EntityCount; i++)
            {
                var entity = frame.CreateEntity();
                frame.Add(entity, new PositionComponent(i, i + 1));
                frame.Add(entity, new VelocityComponent(i + 2, i + 3));
                frame.Add(entity, new HealthComponent(100));
            }

            return frame.EntityCount;
        }
    }

    /// <summary>
    /// 实体热路径微基准测试
    /// </summary>
    [MemoryDiagnoser]
    public class EntityMicroBenchmarks
    {
        private Frame _frame = null!;
        private EntityRef _entity;

        [GlobalSetup]
        public void Setup()
        {
            BenchmarkComponentRegistration.EnsureRegistered();

            _frame = new Frame(1024);
            _entity = _frame.CreateEntity();
            _frame.Add(_entity, new PositionComponent(10, 20));
            _frame.Add(_entity, new VelocityComponent(2, 3));
        }

        [Benchmark]
        public bool SingleValidate()
        {
            return _frame.IsValid(_entity);
        }

        [Benchmark]
        public bool HasPosition()
        {
            return _frame.Has<PositionComponent>(_entity);
        }

        [Benchmark]
        public int GetPositionX()
        {
            return _frame.Get<PositionComponent>(_entity).X;
        }

        [Benchmark]
        public bool TryGetVelocity()
        {
            return _frame.TryGet<VelocityComponent>(_entity, out _);
        }

        [Benchmark]
        public int SingleCreateDestroy()
        {
            var entity = _frame.CreateEntity();
            _frame.DestroyEntity(entity);
            return entity.Index;
        }
    }
}
