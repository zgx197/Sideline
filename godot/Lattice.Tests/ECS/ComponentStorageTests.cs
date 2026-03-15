using System;
using Lattice.ECS.Core;
using Xunit;

namespace Lattice.Tests.ECS
{
    /// <summary>
    /// ComponentStorage 单元测试 - 适配 FrameSync 风格重构后的 API
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
            var storage = new ComponentStorage<Position>();

            Assert.Equal(0, storage.UsedCount);
            Assert.Equal(0, storage.Count);  // Count 包含待删除的，初始为 0
            Assert.Equal(0, storage.BlockCount);

            storage.Dispose();
        }

        [Fact]
        public void Add_SingleEntity_ShouldIncreaseCount()
        {
            var storage = new ComponentStorage<Position>();
            var entity = new Entity(0, 1);

            storage.Add(entity, new Position(10, 20));

            Assert.Equal(1, storage.UsedCount);
            Assert.True(storage.Contains(entity));
            Assert.True(storage.BlockCount >= 1);  // 至少分配了一个 Block

            storage.Dispose();
        }

        [Fact]
        public void Add_MultipleEntities_ShouldStoreAll()
        {
            var storage = new ComponentStorage<Position>();

            for (int i = 0; i < 100; i++)
            {
                storage.Add(new Entity(i, 1), new Position(i * 10, i * 20));
            }

            Assert.Equal(100, storage.UsedCount);
            Assert.True(storage.BlockCount >= 1);

            storage.Dispose();
        }

        [Fact]
        public void Add_DuplicateEntity_ShouldThrow()
        {
            var storage = new ComponentStorage<Position>();
            var entity = new Entity(0, 1);

            storage.Add(entity, new Position(10, 20));

            Assert.Throws<InvalidOperationException>(() =>
                storage.Add(entity, new Position(30, 40)));

            storage.Dispose();
        }

        [Fact]
        public unsafe void Get_ExistingEntity_ShouldReturnComponent()
        {
            var storage = new ComponentStorage<Position>();
            var entity = new Entity(0, 1);
            var pos = new Position(10, 20);

            storage.Add(entity, pos);
            var retrieved = storage.Get(entity);

            Assert.Equal(pos, retrieved);

            storage.Dispose();
        }

        [Fact]
        public void Get_NonExistingEntity_ShouldThrow()
        {
            var storage = new ComponentStorage<Position>();
            var entity = new Entity(0, 1);

            Assert.Throws<InvalidOperationException>(() => storage.Get(entity));

            storage.Dispose();
        }

        [Fact]
        public unsafe void TryGetPointer_ExistingEntity_ShouldReturnTrue()
        {
            var storage = new ComponentStorage<Position>();
            var entity = new Entity(0, 1);
            var pos = new Position(10, 20);

            storage.Add(entity, pos);
            bool found = storage.TryGetPointer(entity, out var pointer);

            Assert.True(found);
            Assert.Equal(pos, *pointer);

            storage.Dispose();
        }

        [Fact]
        public unsafe void TryGetPointer_NonExistingEntity_ShouldReturnFalse()
        {
            var storage = new ComponentStorage<Position>();
            var entity = new Entity(0, 1);

            bool found = storage.TryGetPointer(entity, out var pointer);

            Assert.False(found);
            Assert.True(pointer == null);

            storage.Dispose();
        }

        [Fact]
        public unsafe void GetPointer_Modify_ShouldUpdateComponent()
        {
            var storage = new ComponentStorage<Position>();
            var entity = new Entity(0, 1);

            storage.Add(entity, new Position(10, 20));
            var ptr = storage.GetPointer(entity);
            ptr->X = 100;

            var updated = storage.Get(entity);
            Assert.Equal(100, updated.X);
            Assert.Equal(20, updated.Y);

            storage.Dispose();
        }

        [Fact]
        public void Remove_ExistingEntity_ShouldDecreaseCount()
        {
            var storage = new ComponentStorage<Position>();
            var entity = new Entity(0, 1);

            storage.Add(entity, new Position(10, 20));
            bool removed = storage.Remove(entity);

            Assert.True(removed);
            Assert.Equal(0, storage.UsedCount);
            Assert.False(storage.Contains(entity));

            storage.Dispose();
        }

        [Fact]
        public void Remove_NonExistingEntity_ShouldReturnFalse()
        {
            var storage = new ComponentStorage<Position>();
            var entity = new Entity(0, 1);

            bool removed = storage.Remove(entity);

            Assert.False(removed);

            storage.Dispose();
        }

        [Fact]
        public void Remove_AndReAdd_ShouldReuseSlot()
        {
            var storage = new ComponentStorage<Position>();
            var entity = new Entity(0, 1);

            storage.Add(entity, new Position(10, 20));
            storage.Remove(entity);

            // 重新添加相同实体（版本已增加，需要新实体）
            var newEntity = new Entity(0, 2);  // 版本增加
            storage.Add(newEntity, new Position(30, 40));

            var retrieved = storage.Get(newEntity);
            Assert.Equal(new Position(30, 40), retrieved);

            storage.Dispose();
        }

        #endregion

        #region Block 管理测试

        [Fact]
        public void Add_MoreThanBlockCapacity_ShouldAllocateNewBlock()
        {
            var storage = new ComponentStorage<Position>(blockCapacity: 64);

            // 添加超过一个 Block 的容量（默认 128）
            for (int i = 0; i < 70; i++)
            {
                storage.Add(new Entity(i, 1), new Position(i, i));
            }

            Assert.Equal(70, storage.UsedCount);
            Assert.True(storage.BlockCount >= 2);  // 至少 2 个 Block

            storage.Dispose();
        }

        [Fact]
        public void Remove_AllEntities_ShouldKeepBlocks()
        {
            var storage = new ComponentStorage<Position>();

            // 添加一些实体
            for (int i = 0; i < 10; i++)
            {
                storage.Add(new Entity(i, 1), new Position(i, i));
            }

            int blockCountBefore = storage.BlockCount;

            // 全部删除
            for (int i = 0; i < 10; i++)
            {
                storage.Remove(new Entity(i, 1));
            }

            Assert.Equal(0, storage.UsedCount);
            // Block 不会被释放，只是槽位被标记为空闲
            Assert.Equal(blockCountBefore, storage.BlockCount);

            storage.Dispose();
        }

        #endregion

        #region 稀疏映射测试

        [Fact]
        public void Add_EntityWithHighIndex_ShouldExpandSparseArray()
        {
            var storage = new ComponentStorage<Position>();
            var entity = new Entity(1000, 1);  // 高索引

            storage.Add(entity, new Position(10, 20));

            Assert.True(storage.Contains(entity));
            Assert.Equal(new Position(10, 20), storage.Get(entity));

            storage.Dispose();
        }

        [Fact]
        public void Contains_EntityBeyondSparseCapacity_ShouldReturnFalse()
        {
            var storage = new ComponentStorage<Position>();
            var entity = new Entity(10000, 1);  // 远超初始容量

            Assert.False(storage.Contains(entity));

            storage.Dispose();
        }

        #endregion

        #region 遍历测试

        [Fact]
        public void GetEntityByIndex_ShouldReturnCorrectEntity()
        {
            var storage = new ComponentStorage<Position>();

            for (int i = 0; i < 10; i++)
            {
                storage.Add(new Entity(i, 1), new Position(i * 10, i * 20));
            }

            // 通过索引获取实体
            for (int i = 0; i < 10; i++)
            {
                var entity = storage.GetEntityByIndex(i);
                Assert.Equal(i, entity.Index);
            }

            storage.Dispose();
        }

        [Fact]
        public unsafe void GetPointerByIndex_ShouldReturnCorrectPointer()
        {
            var storage = new ComponentStorage<Position>();

            for (int i = 0; i < 10; i++)
            {
                storage.Add(new Entity(i, 1), new Position(i, i * 2));
            }

            int count = 0;
            for (int i = 0; i < storage.UsedCount; i++)
            {
                var ptr = storage.GetPointerByIndex(i);
                Assert.Equal(i, ptr->X);
                count++;
            }

            Assert.Equal(10, count);

            storage.Dispose();
        }

        #endregion

        #region 版本控制测试

        [Fact]
        public void GetVersion_ExistingEntity_ShouldReturnVersion()
        {
            var storage = new ComponentStorage<Position>();
            var entity = new Entity(0, 1);

            storage.Add(entity, new Position(10, 20));
            int version = storage.GetVersion(entity);

            Assert.True(version > 0);  // 版本应该大于 0

            storage.Dispose();
        }

        [Fact]
        public void Remove_ShouldInvalidateVersion()
        {
            var storage = new ComponentStorage<Position>();
            var entity = new Entity(0, 1);

            storage.Add(entity, new Position(10, 20));
            int versionBefore = storage.GetVersion(entity);

            storage.Remove(entity);

            // 删除后版本变为 -1
            Assert.Equal(-1, storage.GetVersion(entity));

            storage.Dispose();
        }

        [Fact]
        public void GetVersion_NonExistingEntity_ShouldReturnMinusOne()
        {
            var storage = new ComponentStorage<Position>();
            var entity = new Entity(0, 1);

            int version = storage.GetVersion(entity);

            Assert.Equal(-1, version);

            storage.Dispose();
        }

        #endregion

        #region 多类型组件测试

        [Fact]
        public void MultipleStorages_ShouldBeIndependent()
        {
            var posStorage = new ComponentStorage<Position>();
            var velStorage = new ComponentStorage<Velocity>();

            var entity = new Entity(0, 1);

            posStorage.Add(entity, new Position(10, 20));
            velStorage.Add(entity, new Velocity { X = 1, Y = 2 });

            Assert.True(posStorage.Contains(entity));
            Assert.True(velStorage.Contains(entity));

            posStorage.Remove(entity);

            Assert.False(posStorage.Contains(entity));
            Assert.True(velStorage.Contains(entity));  // 另一个存储不受影响

            posStorage.Dispose();
            velStorage.Dispose();
        }

        #endregion

        #region 边界情况测试

        [Fact]
        public void Add_EntityWithZeroIndex_ShouldWork()
        {
            var storage = new ComponentStorage<Position>();
            var entity = new Entity(0, 1);

            storage.Add(entity, new Position(10, 20));

            Assert.True(storage.Contains(entity));

            storage.Dispose();
        }

        [Fact]
        public void Add_ManyEntities_ShouldNotLoseData()
        {
            var storage = new ComponentStorage<Position>();
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

            storage.Dispose();
        }

        [Fact]
        public void Remove_MiddleEntity_ShouldMaintainOthers()
        {
            var storage = new ComponentStorage<Position>();

            // 添加 5 个实体
            for (int i = 0; i < 5; i++)
            {
                storage.Add(new Entity(i, 1), new Position(i, i));
            }

            // 删除中间的
            storage.Remove(new Entity(2, 1));

            // 验证其他还在
            Assert.True(storage.Contains(new Entity(0, 1)));
            Assert.True(storage.Contains(new Entity(1, 1)));
            Assert.False(storage.Contains(new Entity(2, 1)));
            Assert.True(storage.Contains(new Entity(3, 1)));
            Assert.True(storage.Contains(new Entity(4, 1)));

            // 验证数据正确
            Assert.Equal(0, storage.Get(new Entity(0, 1)).X);
            Assert.Equal(1, storage.Get(new Entity(1, 1)).X);
            Assert.Equal(3, storage.Get(new Entity(3, 1)).X);
            Assert.Equal(4, storage.Get(new Entity(4, 1)).X);

            storage.Dispose();
        }

        [Fact]
        public void Dispose_ShouldCleanUpResources()
        {
            var storage = new ComponentStorage<Position>();
            storage.Add(new Entity(0, 1), new Position(10, 20));

            storage.Dispose();

            // 主要测试不抛出异常
            Assert.True(true);
        }

        #endregion

        #region 性能相关测试

        [Fact]
        public void Add_LargeNumber_ShouldPerformWell()
        {
            var storage = new ComponentStorage<Position>();
            const int count = 10000;

            for (int i = 0; i < count; i++)
            {
                storage.Add(new Entity(i, 1), new Position(i, i));
            }

            Assert.Equal(count, storage.UsedCount);

            storage.Dispose();
        }

        [Fact]
        public unsafe void IndexAccess_LargeNumber_ShouldPerformWell()
        {
            var storage = new ComponentStorage<Position>();
            const int count = 10000;

            for (int i = 0; i < count; i++)
            {
                storage.Add(new Entity(i, 1), new Position(i, i));
            }

            long sum = 0;
            for (int i = 0; i < storage.UsedCount; i++)
            {
                var ptr = storage.GetPointerByIndex(i);
                sum += ptr->X;
            }

            long expectedSum = (long)(count - 1) * count / 2;
            Assert.Equal(expectedSum, sum);

            storage.Dispose();
        }

        #endregion
    }
}
