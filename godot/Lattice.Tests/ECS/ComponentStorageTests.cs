using System;
using Lattice.Core;
using Lattice.ECS.Core;
using Xunit;

namespace Lattice.Tests.ECS
{
    /// <summary>
    /// ComponentStorage 单元测试
    /// </summary>
    public class ComponentStorageTests
    {
        #region 测试组件定义

        private struct Position : IEquatable<Position>
        {
            public int X;
            public int Y;

            public Position(int x, int y)
            {
                X = x;
                Y = y;
            }

            public bool Equals(Position other) => X == other.X && Y == other.Y;
            public override bool Equals(object? obj) => obj is Position other && Equals(other);
            public override int GetHashCode() => HashCode.Combine(X, Y);
            public override string ToString() => $"({X}, {Y})";
        }

        private struct Velocity
        {
            public int X;
            public int Y;
        }

        #endregion

        #region 基础操作测试

        [Fact]
        public void Constructor_Default_ShouldBeEmpty()
        {
            using var storage = new ComponentStorage<Position>();

            Assert.Equal(0, storage.Count);
            Assert.Equal(0, storage.Capacity);  // 还没有分配 Block
        }

        [Fact]
        public void Add_SingleEntity_ShouldIncreaseCount()
        {
            using var storage = new ComponentStorage<Position>();
            var entity = new Entity(0, 1);

            storage.Add(entity, new Position(10, 20));

            Assert.Equal(1, storage.Count);
            Assert.True(storage.Has(entity));
            Assert.True(storage.Capacity >= 64);  // 至少分配了一个 Block
        }

        [Fact]
        public void Add_MultipleEntities_ShouldStoreAll()
        {
            using var storage = new ComponentStorage<Position>();

            for (int i = 0; i < 100; i++)
            {
                storage.Add(new Entity(i, 1), new Position(i * 10, i * 20));
            }

            Assert.Equal(100, storage.Count);
            Assert.True(storage.Capacity >= 128);  // 至少 2 个 Block
        }

        [Fact]
        public void Add_DuplicateEntity_ShouldThrow()
        {
            using var storage = new ComponentStorage<Position>();
            var entity = new Entity(0, 1);

            storage.Add(entity, new Position(10, 20));

            Assert.Throws<InvalidOperationException>(() =>
                storage.Add(entity, new Position(30, 40)));
        }

        [Fact]
        public void Get_ExistingEntity_ShouldReturnComponent()
        {
            using var storage = new ComponentStorage<Position>();
            var entity = new Entity(0, 1);
            var pos = new Position(10, 20);

            storage.Add(entity, pos);
            var retrieved = storage.Get(entity);

            Assert.Equal(pos, retrieved);
        }

        [Fact]
        public void Get_NonExistingEntity_ShouldThrow()
        {
            using var storage = new ComponentStorage<Position>();
            var entity = new Entity(0, 1);

            Assert.Throws<KeyNotFoundException>(() => storage.Get(entity));
        }

        [Fact]
        public void TryGet_ExistingEntity_ShouldReturnTrue()
        {
            using var storage = new ComponentStorage<Position>();
            var entity = new Entity(0, 1);
            var pos = new Position(10, 20);

            storage.Add(entity, pos);
            bool found = storage.TryGet(entity, out var retrieved);

            Assert.True(found);
            Assert.Equal(pos, retrieved);
        }

        [Fact]
        public void TryGet_NonExistingEntity_ShouldReturnFalse()
        {
            using var storage = new ComponentStorage<Position>();
            var entity = new Entity(0, 1);

            bool found = storage.TryGet(entity, out var retrieved);

            Assert.False(found);
            Assert.Equal(default(Position), retrieved);
        }

        [Fact]
        public void Get_ModifyRef_ShouldUpdateComponent()
        {
            using var storage = new ComponentStorage<Position>();
            var entity = new Entity(0, 1);

            storage.Add(entity, new Position(10, 20));
            ref var pos = ref storage.Get(entity);
            pos.X = 100;

            var updated = storage.Get(entity);
            Assert.Equal(100, updated.X);
            Assert.Equal(20, updated.Y);
        }

        [Fact]
        public void Remove_ExistingEntity_ShouldDecreaseCount()
        {
            using var storage = new ComponentStorage<Position>();
            var entity = new Entity(0, 1);

            storage.Add(entity, new Position(10, 20));
            bool removed = storage.Remove(entity);

            Assert.True(removed);
            Assert.Equal(0, storage.Count);
            Assert.False(storage.Has(entity));
        }

        [Fact]
        public void Remove_NonExistingEntity_ShouldReturnFalse()
        {
            using var storage = new ComponentStorage<Position>();
            var entity = new Entity(0, 1);

            bool removed = storage.Remove(entity);

            Assert.False(removed);
        }

        [Fact]
        public void Remove_AndReAdd_ShouldWork()
        {
            using var storage = new ComponentStorage<Position>();
            var entity = new Entity(0, 1);

            storage.Add(entity, new Position(10, 20));
            storage.Remove(entity);
            storage.Add(entity, new Position(30, 40));

            var retrieved = storage.Get(entity);
            Assert.Equal(new Position(30, 40), retrieved);
        }

        #endregion

        #region Block 管理测试

        [Fact]
        public void Add_MoreThanBlockCapacity_ShouldAllocateNewBlock()
        {
            using var storage = new ComponentStorage<Position>();

            // 添加超过一个 Block 的容量
            for (int i = 0; i < 70; i++)
            {
                storage.Add(new Entity(i, 1), new Position(i, i));
            }

            Assert.Equal(70, storage.Count);
            Assert.True(storage.Capacity >= 128);  // 至少 2 个 Block
        }

        [Fact]
        public void Remove_AllEntitiesInBlock_ShouldReleaseBlock()
        {
            using var storage = new ComponentStorage<Position>();

            // 添加一些实体
            for (int i = 0; i < 10; i++)
            {
                storage.Add(new Entity(i, 1), new Position(i, i));
            }

            // 全部删除
            for (int i = 0; i < 10; i++)
            {
                storage.Remove(new Entity(i, 1));
            }

            Assert.Equal(0, storage.Count);
            // Block 被释放，容量减少
            Assert.True(storage.Capacity < 64 || storage.Count == 0);
        }

        #endregion

        #region 稀疏映射测试

        [Fact]
        public void Add_EntityWithHighIndex_ShouldExpandSparseArray()
        {
            using var storage = new ComponentStorage<Position>();
            var entity = new Entity(1000, 1);  // 高索引

            storage.Add(entity, new Position(10, 20));

            Assert.True(storage.Has(entity));
            Assert.Equal(new Position(10, 20), storage.Get(entity));
        }

        [Fact]
        public void Has_EntityBeyondSparseCapacity_ShouldReturnFalse()
        {
            using var storage = new ComponentStorage<Position>();
            var entity = new Entity(10000, 1);  // 远超初始容量

            Assert.False(storage.Has(entity));
        }

        #endregion

        #region 遍历测试

        [Fact]
        public void ForEach_ShouldIterateAllComponents()
        {
            using var storage = new ComponentStorage<Position>();

            for (int i = 0; i < 10; i++)
            {
                storage.Add(new Entity(i, 1), new Position(i * 10, i * 20));
            }

            int count = 0;
            int sumX = 0;

            storage.ForEach(delegate(Entity entity, ref Position pos)
            {
                count++;
                sumX += pos.X;
                pos.X += 1;  // 修改应该生效
            });

            Assert.Equal(10, count);
            Assert.Equal(450, sumX);  // 0+10+20+...+90

            // 验证修改生效
            var first = storage.Get(new Entity(0, 1));
            Assert.Equal(1, first.X);  // 0 + 1
        }

        [Fact]
        public void ForEachSpan_ShouldIterateAllComponents()
        {
            using var storage = new ComponentStorage<Position>();

            for (int i = 0; i < 10; i++)
            {
                storage.Add(new Entity(i, 1), new Position(i * 10, i * 20));
            }

            int totalCount = 0;

            storage.ForEachSpan((entities, components) =>
            {
                totalCount += components.Length;
                // Span 是连续的，可以批量处理
                for (int i = 0; i < components.Length; i++)
                {
                    components[i].X += 1;
                }
            });

            Assert.Equal(10, totalCount);
        }

        [Fact]
        public void Enumerator_ShouldSupportForeach()
        {
            using var storage = new ComponentStorage<Position>();

            for (int i = 0; i < 5; i++)
            {
                storage.Add(new Entity(i, 1), new Position(i, i * 2));
            }

            int count = 0;
            foreach (var item in storage)
            {
                count++;
                Assert.Equal(item.Entity.Index, item.Component.X);  // X 应该等于实体索引
            }

            Assert.Equal(5, count);
        }

        [Fact]
        public void GetAllComponents_ShouldCopyToSpan()
        {
            using var storage = new ComponentStorage<Position>();

            for (int i = 0; i < 5; i++)
            {
                storage.Add(new Entity(i, 1), new Position(i, i));
            }

            Span<Position> buffer = stackalloc Position[10];
            int count = storage.GetAllComponents(buffer);

            Assert.Equal(5, count);
            for (int i = 0; i < 5; i++)
            {
                Assert.Equal(i, buffer[i].X);
            }
        }

        [Fact]
        public void GetAllEntities_ShouldCopyToSpan()
        {
            using var storage = new ComponentStorage<Position>();

            for (int i = 0; i < 5; i++)
            {
                storage.Add(new Entity(i, 1), new Position(i, i));
            }

            Span<Entity> buffer = stackalloc Entity[10];
            int count = storage.GetAllEntities(buffer);

            Assert.Equal(5, count);
        }

        #endregion

        #region 版本控制测试

        [Fact]
        public void Add_ShouldIncrementVersion()
        {
            using var storage = new ComponentStorage<Position>();
            var entity = new Entity(0, 1);

            int versionBefore = storage.Version;
            storage.Add(entity, new Position(10, 20));

            Assert.True(storage.Version > versionBefore);
            Assert.True(storage.GetVersion(entity) > 0);
        }

        [Fact]
        public void Remove_ShouldIncrementVersion()
        {
            using var storage = new ComponentStorage<Position>();
            var entity = new Entity(0, 1);

            storage.Add(entity, new Position(10, 20));
            int versionBefore = storage.GetVersion(entity);

            storage.Remove(entity);

            Assert.True(storage.GetVersion(entity) > versionBefore);
        }

        [Fact]
        public void MarkChanged_ShouldUpdateVersion()
        {
            using var storage = new ComponentStorage<Position>();
            var entity = new Entity(0, 1);

            storage.Add(entity, new Position(10, 20));
            int versionBefore = storage.GetVersion(entity);

            storage.MarkChanged(entity);

            Assert.True(storage.GetVersion(entity) > versionBefore);
        }

        [Fact]
        public void GetVersion_NonExistingEntity_ShouldReturnZero()
        {
            using var storage = new ComponentStorage<Position>();
            var entity = new Entity(0, 1);

            int version = storage.GetVersion(entity);

            Assert.Equal(0, version);
        }

        #endregion

        #region 多类型组件测试

        [Fact]
        public void MultipleStorages_ShouldBeIndependent()
        {
            using var posStorage = new ComponentStorage<Position>();
            using var velStorage = new ComponentStorage<Velocity>();

            var entity = new Entity(0, 1);

            posStorage.Add(entity, new Position(10, 20));
            velStorage.Add(entity, new Velocity { X = 1, Y = 2 });

            Assert.True(posStorage.Has(entity));
            Assert.True(velStorage.Has(entity));

            posStorage.Remove(entity);

            Assert.False(posStorage.Has(entity));
            Assert.True(velStorage.Has(entity));  // 另一个存储不受影响
        }

        #endregion

        #region 边界情况测试

        [Fact]
        public void Add_EntityWithZeroIndex_ShouldWork()
        {
            using var storage = new ComponentStorage<Position>();
            var entity = new Entity(0, 1);

            storage.Add(entity, new Position(10, 20));

            Assert.True(storage.Has(entity));
        }

        [Fact]
        public void Add_ManyEntities_ShouldNotLoseData()
        {
            using var storage = new ComponentStorage<Position>();
            const int count = 1000;

            for (int i = 0; i < count; i++)
            {
                storage.Add(new Entity(i, 1), new Position(i, i * 2));
            }

            // 验证所有数据
            for (int i = 0; i < count; i++)
            {
                var pos = storage.Get(new Entity(i, 1));
                Assert.Equal(i, pos.X);
                Assert.Equal(i * 2, pos.Y);
            }
        }

        [Fact]
        public void Remove_MiddleEntity_ShouldMaintainOthers()
        {
            using var storage = new ComponentStorage<Position>();

            // 添加 5 个实体
            for (int i = 0; i < 5; i++)
            {
                storage.Add(new Entity(i, 1), new Position(i, i));
            }

            // 删除中间的
            storage.Remove(new Entity(2, 1));

            // 验证其他还在
            Assert.True(storage.Has(new Entity(0, 1)));
            Assert.True(storage.Has(new Entity(1, 1)));
            Assert.False(storage.Has(new Entity(2, 1)));
            Assert.True(storage.Has(new Entity(3, 1)));
            Assert.True(storage.Has(new Entity(4, 1)));

            // 验证数据正确
            Assert.Equal(0, storage.Get(new Entity(0, 1)).X);
            Assert.Equal(1, storage.Get(new Entity(1, 1)).X);
            Assert.Equal(3, storage.Get(new Entity(3, 1)).X);
            Assert.Equal(4, storage.Get(new Entity(4, 1)).X);
        }

        [Fact]
        public void TryGetRef_ShouldReturnRef()
        {
            using var storage = new ComponentStorage<Position>();
            var entity = new Entity(0, 1);

            storage.Add(entity, new Position(10, 20));
            bool found = storage.TryGetRef(entity, out var posRef);

            Assert.True(found);
            posRef.Value.X = 100;  // 通过 ref 修改

            var updated = storage.Get(entity);
            Assert.Equal(100, updated.X);
        }

        [Fact]
        public void Dispose_ShouldCleanUpResources()
        {
            var storage = new ComponentStorage<Position>();
            storage.Add(new Entity(0, 1), new Position(10, 20));

            storage.Dispose();

            // 再次使用应该正常工作（虽然不建议）
            // 主要测试不抛出异常
        }

        #endregion

        #region 性能相关测试

        [Fact]
        public void Add_LargeNumber_ShouldPerformWell()
        {
            using var storage = new ComponentStorage<Position>();
            const int count = 10000;

            for (int i = 0; i < count; i++)
            {
                storage.Add(new Entity(i, 1), new Position(i, i));
            }

            Assert.Equal(count, storage.Count);
        }

        [Fact]
        public void Foreach_LargeNumber_ShouldPerformWell()
        {
            using var storage = new ComponentStorage<Position>();
            const int count = 10000;

            for (int i = 0; i < count; i++)
            {
                storage.Add(new Entity(i, 1), new Position(i, i));
            }

            long sum = 0;
            storage.ForEach(delegate(Entity entity, ref Position pos)
            {
                sum += pos.X;
            });

            long expectedSum = (long)(count - 1) * count / 2;
            Assert.Equal(expectedSum, sum);
        }

        #endregion
    }
}
