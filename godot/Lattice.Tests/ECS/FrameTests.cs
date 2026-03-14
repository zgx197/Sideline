using System;
using Lattice.Core;
using Lattice.ECS.Core;
using Xunit;

namespace Lattice.Tests.ECS
{
    /// <summary>
    /// Frame 单元测试
    /// </summary>
    public class FrameTests
    {
        private struct Position : IEquatable<Position>
        {
            public int X, Y;
            public Position(int x, int y) { X = x; Y = y; }
            public bool Equals(Position other) => X == other.X && Y == other.Y;
            public override bool Equals(object? obj) => obj is Position p && Equals(p);
            public override int GetHashCode() => HashCode.Combine(X, Y);
        }

        private struct Velocity
        {
            public int X, Y;
        }

        private ComponentTypeRegistry CreateRegistry()
        {
            var registry = new ComponentTypeRegistry();
            registry.Register<Position>();
            registry.Register<Velocity>();
            return registry;
        }

        [Fact]
        public void Constructor_ShouldInitializeCorrectly()
        {
            var registry = CreateRegistry();
            var frame = new Frame(0, 16, registry);

            Assert.Equal(0, frame.Tick);
            Assert.NotNull(frame.Entities);
            Assert.Equal(0, frame.Entities.AliveCount);
            Assert.False(frame.IsVerified);
        }

        [Fact]
        public void CreateEntity_ShouldIncreaseCount()
        {
            var registry = CreateRegistry();
            using var frame = new Frame(0, 16, registry);

            var entity = frame.CreateEntity();

            Assert.True(entity.IsValid);
            Assert.Equal(1, frame.Entities.AliveCount);
        }

        [Fact]
        public void DestroyEntity_ShouldRemoveEntity()
        {
            var registry = CreateRegistry();
            using var frame = new Frame(0, 16, registry);

            var entity = frame.CreateEntity();
            bool removed = frame.DestroyEntity(entity);

            Assert.True(removed);
            Assert.Equal(0, frame.Entities.AliveCount);
            Assert.False(frame.IsValid(entity));
        }

        [Fact]
        public void AddComponent_ShouldStoreComponent()
        {
            var registry = CreateRegistry();
            using var frame = new Frame(0, 16, registry);

            var entity = frame.CreateEntity();
            frame.AddComponent(entity, new Position { X = 10, Y = 20 });

            Assert.True(frame.HasComponent<Position>(entity));
            var pos = frame.GetComponent<Position>(entity);
            Assert.Equal(10, pos.X);
            Assert.Equal(20, pos.Y);
        }

        [Fact]
        public void AddComponent_MultipleTypes_ShouldStoreAll()
        {
            var registry = CreateRegistry();
            using var frame = new Frame(0, 16, registry);

            var entity = frame.CreateEntity();
            frame.AddComponent(entity, new Position { X = 10, Y = 20 });
            frame.AddComponent(entity, new Velocity { X = 1, Y = 2 });

            Assert.True(frame.HasComponent<Position>(entity));
            Assert.True(frame.HasComponent<Velocity>(entity));

            var pos = frame.GetComponent<Position>(entity);
            var vel = frame.GetComponent<Velocity>(entity);

            Assert.Equal(10, pos.X);
            Assert.Equal(1, vel.X);
        }

        [Fact]
        public void RemoveComponent_ShouldRemoveOnlySpecifiedType()
        {
            var registry = CreateRegistry();
            using var frame = new Frame(0, 16, registry);

            var entity = frame.CreateEntity();
            frame.AddComponent(entity, new Position { X = 10, Y = 20 });
            frame.AddComponent(entity, new Velocity { X = 1, Y = 2 });

            bool removed = frame.RemoveComponent<Position>(entity);

            Assert.True(removed);
            Assert.False(frame.HasComponent<Position>(entity));
            Assert.True(frame.HasComponent<Velocity>(entity));
        }

        [Fact]
        public void GetComponent_InvalidEntity_ShouldThrow()
        {
            var registry = CreateRegistry();
            using var frame = new Frame(0, 16, registry);

            var invalidEntity = new Entity(999, 1);

            Assert.Throws<ArgumentException>(() => frame.GetComponent<Position>(invalidEntity));
        }

        [Fact]
        public void TryGetComponent_Existing_ShouldReturnTrue()
        {
            var registry = CreateRegistry();
            using var frame = new Frame(0, 16, registry);

            var entity = frame.CreateEntity();
            frame.AddComponent(entity, new Position { X = 10, Y = 20 });

            bool found = frame.TryGetComponent(entity, out Position pos);

            Assert.True(found);
            Assert.Equal(10, pos.X);
        }

        [Fact]
        public void TryGetComponent_NonExisting_ShouldReturnFalse()
        {
            var registry = CreateRegistry();
            using var frame = new Frame(0, 16, registry);

            var entity = frame.CreateEntity();

            bool found = frame.TryGetComponent(entity, out Position pos);

            Assert.False(found);
        }

        [Fact]
        public void GetComponentSet_ShouldReturnCorrectSet()
        {
            var registry = CreateRegistry();
            using var frame = new Frame(0, 16, registry);

            var entity = frame.CreateEntity();
            frame.AddComponent(entity, new Position { X = 10, Y = 20 });
            frame.AddComponent(entity, new Velocity { X = 1, Y = 2 });

            var set = frame.GetComponentSet(entity);

            Assert.Equal(2, set.Count);
        }

        [Fact]
        public void GetComponentSet_InvalidEntity_ShouldReturnEmpty()
        {
            var registry = CreateRegistry();
            using var frame = new Frame(0, 16, registry);

            var invalidEntity = new Entity(9999, 1);
            var set = frame.GetComponentSet(invalidEntity);

            Assert.True(set.IsEmpty);
        }

        [Fact]
        public void MatchesQuery_WithRequiredComponents_ShouldReturnTrue()
        {
            var registry = CreateRegistry();
            using var frame = new Frame(0, 16, registry);

            var entity = frame.CreateEntity();
            frame.AddComponent(entity, new Position { X = 10, Y = 20 });
            frame.AddComponent(entity, new Velocity { X = 1, Y = 2 });

            var required = ComponentSet.Create(0, 1);  // Position 和 Velocity

            bool matches = frame.MatchesQuery(entity, required);

            Assert.True(matches);
        }

        [Fact]
        public void MatchesQuery_MissingRequired_ShouldReturnFalse()
        {
            var registry = CreateRegistry();
            using var frame = new Frame(0, 16, registry);

            var entity = frame.CreateEntity();
            frame.AddComponent(entity, new Position { X = 10, Y = 20 });
            // 没有 Velocity

            var required = ComponentSet.Create(0, 1);  // 需要 Position 和 Velocity

            bool matches = frame.MatchesQuery(entity, required);

            Assert.False(matches);
        }

        [Fact]
        public void DeterministicRandom_SameSeed_ShouldProduceSameSequence()
        {
            var rand1 = new DeterministicRandom(42);
            var rand2 = new DeterministicRandom(42);

            for (int i = 0; i < 10; i++)
            {
                Assert.Equal(rand1.Next(), rand2.Next());
            }
        }

        [Fact]
        public void DeterministicRandom_DifferentSeeds_ShouldProduceDifferentSequences()
        {
            var rand1 = new DeterministicRandom(42);
            var rand2 = new DeterministicRandom(43);

            // 统计相同值的概率应该极低
            int sameCount = 0;
            for (int i = 0; i < 10; i++)
            {
                if (rand1.Next() == rand2.Next()) sameCount++;
            }

            Assert.True(sameCount < 5);  // 应该大部分都不同
        }

        [Fact]
        public void CalculateChecksum_ShouldReturnConsistentValue()
        {
            var registry = CreateRegistry();
            using var frame = new Frame(0, 16, registry);

            // 创建相同状态的两个帧
            for (int i = 0; i < 5; i++)
            {
                var entity = frame.CreateEntity();
                frame.AddComponent(entity, new Position { X = i, Y = i * 2 });
            }

            long checksum1 = frame.CalculateChecksum();
            long checksum2 = frame.CalculateChecksum();

            Assert.Equal(checksum1, checksum2);
        }

        [Fact]
        public void ComponentTypeRegistry_Register_ShouldReturnUniqueIds()
        {
            var registry = new ComponentTypeRegistry();

            int id1 = registry.Register<Position>();
            int id2 = registry.Register<Velocity>();

            Assert.NotEqual(id1, id2);
            Assert.True(id1 >= 0 && id1 < ComponentSet.MaxComponents);
            Assert.True(id2 >= 0 && id2 < ComponentSet.MaxComponents);
        }

        [Fact]
        public void ComponentTypeRegistry_RegisterDuplicate_ShouldReturnSameId()
        {
            var registry = new ComponentTypeRegistry();

            int id1 = registry.Register<Position>();
            int id2 = registry.Register<Position>();

            Assert.Equal(id1, id2);
        }

        [Fact]
        public void ComponentTypeRegistry_GetTypeId_ShouldReturnCorrectId()
        {
            var registry = new ComponentTypeRegistry();
            int registeredId = registry.Register<Position>();

            int retrievedId = registry.GetTypeId<Position>();

            Assert.Equal(registeredId, retrievedId);
        }

        [Fact]
        public void ComponentTypeRegistry_GetTypeId_NotRegistered_ShouldThrow()
        {
            var registry = new ComponentTypeRegistry();

            Assert.Throws<KeyNotFoundException>(() => registry.GetTypeId<Position>());
        }

        [Fact]
        public void ComponentTypeRegistry_IsRegistered_ShouldReturnCorrectStatus()
        {
            var registry = new ComponentTypeRegistry();

            Assert.False(registry.IsRegistered<Position>());
            registry.Register<Position>();
            Assert.True(registry.IsRegistered<Position>());
        }

        [Fact]
        public void Frame_ManyEntities_ShouldHandleCorrectly()
        {
            var registry = CreateRegistry();
            using var frame = new Frame(0, 16, registry);
            const int count = 1000;

            for (int i = 0; i < count; i++)
            {
                var entity = frame.CreateEntity();
                frame.AddComponent(entity, new Position { X = i, Y = i });
            }

            Assert.Equal(count, frame.Entities.AliveCount);

            // 验证所有数据
            for (int i = 0; i < count; i++)
            {
                var entity = new Entity(i, 1 | EntityRegistry.ActiveBit);
                if (frame.IsValid(entity))
                {
                    var pos = frame.GetComponent<Position>(entity);
                    Assert.Equal(i, pos.X);
                }
            }
        }
    }
}
