using System;
using System.Threading;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Lattice.Core;
using Lattice.ECS.Core;

namespace Lattice.Benchmarks
{
    internal struct PositionComponent : IComponent
    {
        public int X;
        public int Y;

        public PositionComponent(int x, int y)
        {
            X = x;
            Y = y;
        }
    }

    internal struct VelocityComponent : IComponent
    {
        public int X;
        public int Y;

        public VelocityComponent(int x, int y)
        {
            X = x;
            Y = y;
        }
    }

    internal struct HealthComponent : IComponent
    {
        public int Value;

        public HealthComponent(int value)
        {
            Value = value;
        }
    }

    internal struct DeferredHealthComponent : IComponent
    {
        public int Value;

        public DeferredHealthComponent(int value)
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
                RegisterDeferredIfNeeded<DeferredHealthComponent>();

                Volatile.Write(ref _initialized, 1);
            }
        }

        private static void RegisterIfNeeded<T>() where T : unmanaged, IComponent
        {
            if (!ComponentTypeId<T>.IsRegistered)
            {
                ComponentRegistry.Register<T>();
            }
        }

        private static void RegisterDeferredIfNeeded<T>() where T : unmanaged, IComponent
        {
            if (!ComponentTypeId<T>.IsRegistered)
            {
                ComponentRegistry.Register<T>(
                    ComponentFlags.None,
                    ComponentCallbacks.Empty,
                    StorageFlags.DeferredRemoval);
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
    [SimpleJob(RuntimeMoniker.Net80, launchCount: 1, warmupCount: 3, iterationCount: 5)]
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
    [SimpleJob(RuntimeMoniker.Net80, launchCount: 1, warmupCount: 3, iterationCount: 5)]
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

    /// <summary>
    /// 查询热路径基准测试。
    /// </summary>
    [MemoryDiagnoser]
    [InProcess]
    [WarmupCount(3)]
    [IterationCount(5)]
    public unsafe class QueryBenchmarks
    {
        private Frame _frame = null!;
        private OwningGroup<PositionComponent, VelocityComponent> _pairGroup = null!;
        private OwningGroup<PositionComponent, VelocityComponent, HealthComponent> _tripleGroup = null!;
        private int _queryPairSum;

        [Params(1000, 10000)]
        public int EntityCount { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            BenchmarkComponentRegistration.EnsureRegistered();

            _frame = new Frame(EntityCount + 8);
            for (int i = 0; i < EntityCount; i++)
            {
                EntityRef entity = _frame.CreateEntity();
                PositionComponent position = new PositionComponent(i, i + 1);
                VelocityComponent velocity = new VelocityComponent(i + 2, i + 3);
                HealthComponent health = new HealthComponent(100 + i);

                _frame.Add(entity, position);

                if ((i & 1) == 0)
                {
                    _frame.Add(entity, velocity);
                }

                if ((i % 3) == 0)
                {
                    _frame.Add(entity, health);
                }
            }

            _pairGroup = _frame.RegisterOwningGroup<PositionComponent, VelocityComponent>();
            _tripleGroup = _frame.RegisterOwningGroup<PositionComponent, VelocityComponent, HealthComponent>();
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            _frame.Dispose();
        }

        [Benchmark(Baseline = true)]
        public int ManualIterator_PositionVelocity()
        {
            int sum = 0;
            var iterator = _frame.GetComponentBlockIterator<PositionComponent>();
            while (iterator.Next(out EntityRef entity, out PositionComponent* position))
            {
                if (!_frame.Has<VelocityComponent>(entity))
                {
                    continue;
                }

                VelocityComponent* velocity = _frame.GetPointer<VelocityComponent>(entity);
                if (velocity == null)
                {
                    continue;
                }

                sum += position->X + velocity->X;
            }

            return sum;
        }

        [Benchmark]
        public int QueryEnumerator_PositionVelocity()
        {
            int sum = 0;
            var query = _frame.Query<PositionComponent, VelocityComponent>();
            var enumerator = query.GetEnumerator();
            while (enumerator.MoveNext())
            {
                sum += enumerator.Component1.X + enumerator.Component2.X;
            }

            return sum;
        }

        [Benchmark]
        public int QueryForEachFunctionPointer_PositionVelocity()
        {
            _queryPairSum = 0;
            _current = this;
            _frame.Query<PositionComponent, VelocityComponent>().ForEach(&AccumulatePositionVelocity);
            _current = null!;
            return _queryPairSum;
        }

        [Benchmark]
        public int QueryEnumerator_PositionVelocityHealth()
        {
            int sum = 0;
            var query = _frame.Query<PositionComponent, VelocityComponent, HealthComponent>();
            var enumerator = query.GetEnumerator();
            while (enumerator.MoveNext())
            {
                sum += enumerator.Component1.X + enumerator.Component2.X + enumerator.Component3.Value;
            }

            return sum;
        }

        [Benchmark]
        public int FullOwningGroupSequential_PositionVelocity()
        {
            int sum = 0;
            int index = 0;
            while (_pairGroup.Next(&index, out EntityRef _, out PositionComponent* position, out VelocityComponent* velocity))
            {
                sum += position->X + velocity->X;
            }

            return sum;
        }

        [Benchmark]
        public int FullOwningGroupBlock_PositionVelocity()
        {
            int sum = 0;
            int blockIndex = 0;
            while (_pairGroup.NextBlock(&blockIndex, out EntityRef* _, out PositionComponent* positions, out VelocityComponent* velocities, out int count))
            {
                for (int i = 0; i < count; i++)
                {
                    sum += positions[i].X + velocities[i].X;
                }
            }

            return sum;
        }

        [Benchmark]
        public int FullOwningGroupBlock_PositionVelocityHealth()
        {
            int sum = 0;
            int blockIndex = 0;
            while (_tripleGroup.NextBlock(&blockIndex, out EntityRef* _, out PositionComponent* positions, out VelocityComponent* velocities, out HealthComponent* healths, out int count))
            {
                for (int i = 0; i < count; i++)
                {
                    sum += positions[i].X + velocities[i].X + healths[i].Value;
                }
            }

            return sum;
        }

        private static QueryBenchmarks _current = null!;

        private static void AccumulatePositionVelocity(EntityRef entity, PositionComponent* position, VelocityComponent* velocity)
        {
            QueryBenchmarks current = _current;
            current._queryPairSum += position->X + velocity->X;
        }
    }

    /// <summary>
    /// 组件基座的代表性性能基准：
    /// 快照、延迟删除、OwningGroup 构建与增量同步。
    /// </summary>
    [MemoryDiagnoser]
    [InProcess]
    [WarmupCount(3)]
    [IterationCount(5)]
    public unsafe class ComponentArchitectureBenchmarks
    {
        private Frame _snapshotSource = null!;
        private PackedFrameSnapshot _snapshot = null!;
        private Frame _restoreTarget = null!;
        private Frame _baselineMutationFrame = null!;
        private Frame _owningMutationFrame = null!;
        private OwningGroup<PositionComponent, VelocityComponent> _mutationGroup = null!;
        private EntityRef[] _mutationEntities = Array.Empty<EntityRef>();
        private int _mutationVersion;

        [Params(1000, 10000)]
        public int EntityCount { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            BenchmarkComponentRegistration.EnsureRegistered();

            _snapshotSource = new Frame(EntityCount + 8);
            for (int i = 0; i < EntityCount; i++)
            {
                EntityRef entity = _snapshotSource.CreateEntity();
                _snapshotSource.Add(entity, new PositionComponent(i, i + 1));
                _snapshotSource.Add(entity, new VelocityComponent(i + 2, i + 3));
                _snapshotSource.Add(entity, new HealthComponent(100 + i));
            }

            _snapshot = _snapshotSource.CapturePackedSnapshot(ComponentSerializationMode.Checkpoint);
            _restoreTarget = new Frame(EntityCount + 8);

            _baselineMutationFrame = new Frame(EntityCount + 8);
            _owningMutationFrame = new Frame(EntityCount + 8);
            _mutationEntities = new EntityRef[EntityCount];

            for (int i = 0; i < EntityCount; i++)
            {
                EntityRef baselineEntity = _baselineMutationFrame.CreateEntity();
                _baselineMutationFrame.Add(baselineEntity, new PositionComponent(i, i));
                _baselineMutationFrame.Add(baselineEntity, new VelocityComponent(i + 1, i + 2));

                EntityRef owningEntity = _owningMutationFrame.CreateEntity();
                _owningMutationFrame.Add(owningEntity, new PositionComponent(i, i));
                _owningMutationFrame.Add(owningEntity, new VelocityComponent(i + 1, i + 2));
                _mutationEntities[i] = owningEntity;
            }

            _mutationGroup = _owningMutationFrame.RegisterOwningGroup<PositionComponent, VelocityComponent>();
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            _snapshotSource.Dispose();
            _restoreTarget.Dispose();
            _baselineMutationFrame.Dispose();
            _owningMutationFrame.Dispose();
        }

        [Benchmark(Baseline = true)]
        public ulong CreateSnapshot_RawDenseTripleComponent()
        {
            PackedFrameSnapshot snapshot = _snapshotSource.CapturePackedSnapshot(ComponentSerializationMode.Checkpoint);
            return snapshot.SchemaManifest.Fingerprint ^ (ulong)snapshot.Length;
        }

        [Benchmark]
        public ulong RestoreSnapshot_RawDenseTripleComponent()
        {
            _restoreTarget.RestoreFromPackedSnapshot(_snapshot, ComponentSerializationMode.Checkpoint);
            return _restoreTarget.CalculateChecksum(ComponentSerializationMode.Checkpoint);
        }

        [Benchmark]
        public int RegisterOwningGroup_Rebuild_PositionVelocity()
        {
            using var frame = new Frame(EntityCount + 8);
            for (int i = 0; i < EntityCount; i++)
            {
                EntityRef entity = frame.CreateEntity();
                frame.Add(entity, new PositionComponent(i, i + 1));

                if ((i & 1) == 0)
                {
                    frame.Add(entity, new VelocityComponent(i + 2, i + 3));
                }
            }

            return frame.RegisterOwningGroup<PositionComponent, VelocityComponent>().Count;
        }

        [Benchmark]
        public int DeferredRemoval_RemoveAndCommit_FullCycle()
        {
            using var frame = new Frame(EntityCount + 8);
            EntityRef[] entities = new EntityRef[EntityCount];

            for (int i = 0; i < EntityCount; i++)
            {
                EntityRef entity = frame.CreateEntity();
                entities[i] = entity;
                frame.Add(entity, new DeferredHealthComponent(100 + i));
            }

            for (int i = 0; i < entities.Length; i++)
            {
                frame.Remove<DeferredHealthComponent>(entities[i]);
            }

            frame.CommitDeferredRemovals();
            return frame.PendingDeferredRemovalCount;
        }

        [Benchmark]
        public int SetQualifiedPosition_NoOwningGroup()
        {
            int version = ++_mutationVersion;
            int sum = 0;

            for (int i = 0; i < _mutationEntities.Length; i++)
            {
                EntityRef entity = _mutationEntities[i];
                _baselineMutationFrame.Set(entity, new PositionComponent(version + i, version - i));
                sum += _baselineMutationFrame.Get<PositionComponent>(entity).X;
            }

            return sum;
        }

        [Benchmark]
        public int SetQualifiedPosition_WithOwningGroup()
        {
            int version = ++_mutationVersion;
            int sum = 0;

            for (int i = 0; i < _mutationEntities.Length; i++)
            {
                EntityRef entity = _mutationEntities[i];
                _owningMutationFrame.Set(entity, new PositionComponent(version + i, version - i));
            }

            int index = 0;
            while (_mutationGroup.Next(&index, out EntityRef _, out PositionComponent* position, out VelocityComponent* velocity))
            {
                _ = velocity;
                sum += position->X;
            }

            return sum;
        }
    }
}
