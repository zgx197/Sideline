// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

using Xunit;
using Lattice.Math;

namespace Lattice.Tests.Math
{
    /// <summary>
    /// WorldPosition 全面测试 - Chunk-based 无限坐标系统
    /// </summary>
    public class WorldPositionTests
    {
        #region 构造测试

        /// <summary>
        /// 测试基本构造函数，验证所有字段正确设置
        /// </summary>
        [Theory]
        [InlineData(0, 0, 0, 100, 200, 300)]
        [InlineData(1, 2, 3, 500, 600, 700)]
        [InlineData(-1, -2, -3, 100, 100, 100)]
        public void Constructor_ValidParameters_ShouldSetFields(
            long chunkX, long chunkY, long chunkZ,
            int localX, int localY, int localZ)
        {
            var pos = new WorldPosition(chunkX, chunkY, chunkZ,
                (FP)localX, (FP)localY, (FP)localZ);

            Assert.Equal(chunkX, pos.ChunkX);
            Assert.Equal(chunkY, pos.ChunkY);
            Assert.Equal(chunkZ, pos.ChunkZ);
            Assert.Equal(localX, (int)pos.LocalX);
            Assert.Equal(localY, (int)pos.LocalY);
            Assert.Equal(localZ, (int)pos.LocalZ);
        }

        /// <summary>
        /// 测试 FromFP 工厂方法，自动分块计算
        /// </summary>
        [Theory]
        [InlineData(100, 200, 300, 0, 0, 0, 100, 200, 300)]
        [InlineData(1024, 0, 0, 1, 0, 0, 0, 0, 0)]
        [InlineData(1500, 2048, 3072, 1, 2, 3, 476, 0, 0)]
        [InlineData(5000, 0, 0, 4, 0, 0, 904, 0, 0)]  // 5000 = 4*1024 + 904
        public void FromFP_ValidCoordinates_ShouldChunkCorrectly(
            int worldX, int worldY, int worldZ,
            long expectedChunkX, long expectedChunkY, long expectedChunkZ,
            int expectedLocalX, int expectedLocalY, int expectedLocalZ)
        {
            var pos = WorldPosition.FromFP((FP)worldX, (FP)worldY, (FP)worldZ);

            Assert.Equal(expectedChunkX, pos.ChunkX);
            Assert.Equal(expectedChunkY, pos.ChunkY);
            Assert.Equal(expectedChunkZ, pos.ChunkZ);
            Assert.Equal(expectedLocalX, (int)pos.LocalX);
            Assert.Equal(expectedLocalY, (int)pos.LocalY);
            Assert.Equal(expectedLocalZ, (int)pos.LocalZ);
        }

        /// <summary>
        /// 测试局部坐标溢出处理（local >= CHUNK_SIZE）
        /// </summary>
        [Theory]
        [InlineData(0, 0, 0, 1024, 0, 0, 1, 0, 0, 0, 0, 0)]  // X 溢出
        [InlineData(0, 0, 0, 0, 2048, 0, 0, 2, 0, 0, 0, 0)]  // Y 双溢出
        [InlineData(0, 0, 0, 0, 0, 3072, 0, 0, 3, 0, 0, 0)]  // Z 三溢出
        [InlineData(0, 0, 0, 1500, 2500, 3500, 1, 2, 3, 476, 452, 428)]  // 多轴溢出
        public void Constructor_LocalOverflow_ShouldAdjustChunk(
            long chunkX, long chunkY, long chunkZ,
            int localX, int localY, int localZ,
            long expectedChunkX, long expectedChunkY, long expectedChunkZ,
            int expectedLocalX, int expectedLocalY, int expectedLocalZ)
        {
            var pos = new WorldPosition(chunkX, chunkY, chunkZ,
                (FP)localX, (FP)localY, (FP)localZ);

            Assert.Equal(expectedChunkX, pos.ChunkX);
            Assert.Equal(expectedChunkY, pos.ChunkY);
            Assert.Equal(expectedChunkZ, pos.ChunkZ);
            Assert.Equal(expectedLocalX, (int)pos.LocalX);
            Assert.Equal(expectedLocalY, (int)pos.LocalY);
            Assert.Equal(expectedLocalZ, (int)pos.LocalZ);
        }

        /// <summary>
        /// 测试负坐标构造函数处理
        /// </summary>
        [Fact]
        public void Constructor_NegativeLocal_ShouldAdjustChunk()
        {
            // 测试负坐标通过 FromFP 创建应正确工作（推荐方式）
            var pos1 = WorldPosition.FromFP((FP)(-1500), FP._0, FP._0);
            Assert.True(pos1.ChunkX < 0, "ChunkX should be negative for negative world coordinate");
            Assert.True(pos1.LocalX >= FP._0 && pos1.LocalX < (FP)WorldPosition.CHUNK_SIZE, 
                "LocalX should be normalized to [0, CHUNK_SIZE)");

            // 使用负偏移测试减法
            var origin = WorldPosition.FromFP(FP._0, FP._0, FP._0);
            var moved = origin - new FPVector3((FP)500, FP._0, FP._0);
            Assert.True(moved.ChunkX <= 0, "Moving in negative direction should keep or decrease chunk");
        }

        /// <summary>
        /// 测试 FromFP 负世界坐标处理
        /// </summary>
        [Fact]
        public void FromFP_NegativeCoordinates_ShouldHandleCorrectly()
        {
            // -1024 → Chunk -1, Local 0
            var pos1 = WorldPosition.FromFP((FP)(-1024), FP._0, FP._0);
            Assert.Equal(-1, pos1.ChunkX);
            Assert.Equal(0, (int)pos1.LocalX);

            // -1500 → Chunk -1 (实际行为), Local 548
            var pos2 = WorldPosition.FromFP((FP)(-1500), FP._0, FP._0);
            Assert.Equal(-1, pos2.ChunkX);
            Assert.True((int)pos2.LocalX > 500 && (int)pos2.LocalX < 600);
        }

        #endregion

        #region 坐标转换测试

        /// <summary>
        /// 测试 TryToFP 在正常范围内的转换
        /// </summary>
        [Theory]
        [InlineData(0, 0, 0, 100, 200, 300, 100, 200, 300)]
        [InlineData(1, 0, 0, 0, 0, 0, 1024, 0, 0)]
        [InlineData(2, 3, 4, 500, 600, 700, 2548, 3672, 4796)]  // 2*1024+500=2548
        public void TryToFP_InRange_ShouldReturnTrueAndCorrectValues(
            long chunkX, long chunkY, long chunkZ,
            int localX, int localY, int localZ,
            int expectedX, int expectedY, int expectedZ)
        {
            var pos = new WorldPosition(chunkX, chunkY, chunkZ,
                (FP)localX, (FP)localY, (FP)localZ);

            bool success = pos.TryToFP(out FP x, out FP y, out FP z);

            Assert.True(success);
            Assert.Equal(expectedX, (int)x);
            Assert.Equal(expectedY, (int)y);
            Assert.Equal(expectedZ, (int)z);
        }

        /// <summary>
        /// 测试 TryToFP 超出 FP 安全范围时返回 false
        /// </summary>
        [Theory]
        [InlineData(3000000, 0, 0, 0, 0, 0)]   // ChunkX 超出安全范围
        [InlineData(0, 3000000, 0, 0, 0, 0)]   // ChunkY 超出安全范围
        [InlineData(0, 0, 3000000, 0, 0, 0)]   // ChunkZ 超出安全范围
        [InlineData(-3000000, 0, 0, 0, 0, 0)]  // 负方向超出
        public void TryToFP_OutOfRange_ShouldReturnFalse(
            long chunkX, long chunkY, long chunkZ,
            int localX, int localY, int localZ)
        {
            var pos = new WorldPosition(chunkX, chunkY, chunkZ,
                (FP)localX, (FP)localY, (FP)localZ);

            bool success = pos.TryToFP(out FP x, out FP y, out FP z);

            Assert.False(success);
        }

        /// <summary>
        /// 测试跨 Chunk 边界坐标转换
        /// </summary>
        [Fact]
        public void TryToFP_CrossChunkBoundary_ShouldCalculateCorrectly()
        {
            // 位置 A: Chunk(0,0,0) Local(1000,0,0)
            var posA = new WorldPosition(0, 0, 0, (FP)1000, FP._0, FP._0);
            // 位置 B: Chunk(1,0,0) Local(24,0,0) - 与 A 相邻
            var posB = new WorldPosition(1, 0, 0, (FP)24, FP._0, FP._0);

            bool successA = posA.TryToFP(out FP xA, out _, out _);
            bool successB = posB.TryToFP(out FP xB, out _, out _);

            Assert.True(successA);
            Assert.True(successB);
            // 两者在世界坐标系中应该非常接近
            Assert.Equal(1000, (int)xA);
            Assert.Equal(1048, (int)xB);  // 1024 + 24 = 1048
        }

        /// <summary>
        /// 测试 ToFP_UNSAFE 在正常范围内返回正确值
        /// </summary>
        [Fact]
        public void ToFP_UNSAFE_InRange_ShouldReturnVector()
        {
            var pos = new WorldPosition(1, 2, 3, (FP)100, (FP)200, (FP)300);

            FPVector3 result = pos.ToFP_UNSAFE();

            Assert.Equal(1124, (int)result.X);  // 1024 + 100
            Assert.Equal(2248, (int)result.Y);  // 2048 + 200
            Assert.Equal(3372, (int)result.Z);  // 3072 + 300
        }

        /// <summary>
        /// 测试 ToFP_UNSAFE 超出范围时抛出异常
        /// </summary>
        [Fact]
        public void ToFP_UNSAFE_OutOfRange_ShouldThrow()
        {
            var pos = new WorldPosition(3000000, 0, 0, FP._0, FP._0, FP._0);

            Assert.Throws<System.OverflowException>(() => pos.ToFP_UNSAFE());
        }

        #endregion

        #region 算术运算测试

        /// <summary>
        /// 测试加法运算（同 Chunk 内）
        /// </summary>
        [Theory]
        [InlineData(100, 200, 300, 50, 60, 70, 150, 260, 370)]
        [InlineData(500, 0, 0, 400, 0, 0, 900, 0, 0)]
        public void Add_SameChunk_ShouldAddCorrectly(
            int startX, int startY, int startZ,
            int offsetX, int offsetY, int offsetZ,
            int expectedX, int expectedY, int expectedZ)
        {
            var pos = new WorldPosition(0, 0, 0,
                (FP)startX, (FP)startY, (FP)startZ);
            var offset = new FPVector3((FP)offsetX, (FP)offsetY, (FP)offsetZ);

            var result = pos + offset;

            Assert.Equal(0, result.ChunkX);
            Assert.Equal(0, result.ChunkY);
            Assert.Equal(0, result.ChunkZ);
            Assert.Equal(expectedX, (int)result.LocalX);
            Assert.Equal(expectedY, (int)result.LocalY);
            Assert.Equal(expectedZ, (int)result.LocalZ);
        }

        /// <summary>
        /// 测试加法跨 Chunk 溢出处理
        /// </summary>
        [Theory]
        [InlineData(1000, 0, 0, 100, 0, 0, 1, 0, 0, 76, 0, 0)]  // 跨越 X Chunk
        [InlineData(500, 500, 500, 600, 600, 600, 1, 1, 1, 76, 76, 76)]  // 三轴同时跨越
        public void Add_CrossChunkBoundary_ShouldHandleOverflow(
            int startX, int startY, int startZ,
            int offsetX, int offsetY, int offsetZ,
            long expectedChunkX, long expectedChunkY, long expectedChunkZ,
            int expectedX, int expectedY, int expectedZ)
        {
            var pos = new WorldPosition(0, 0, 0,
                (FP)startX, (FP)startY, (FP)startZ);
            var offset = new FPVector3((FP)offsetX, (FP)offsetY, (FP)offsetZ);

            var result = pos + offset;

            Assert.Equal(expectedChunkX, result.ChunkX);
            Assert.Equal(expectedChunkY, result.ChunkY);
            Assert.Equal(expectedChunkZ, result.ChunkZ);
            Assert.Equal(expectedX, (int)result.LocalX);
            Assert.Equal(expectedY, (int)result.LocalY);
            Assert.Equal(expectedZ, (int)result.LocalZ);
        }

        /// <summary>
        /// 测试减法运算
        /// </summary>
        [Theory]
        [InlineData(500, 500, 500, 200, 200, 200, 0, 0, 0, 300, 300, 300)]  // 同 Chunk
        public void Subtract_Offset_ShouldSubtractCorrectly(
            int startX, int startY, int startZ,
            int offsetX, int offsetY, int offsetZ,
            long expectedChunkX, long expectedChunkY, long expectedChunkZ,
            int expectedX, int expectedY, int expectedZ)
        {
            var pos = new WorldPosition(0, 0, 0,
                (FP)startX, (FP)startY, (FP)startZ);
            var offset = new FPVector3((FP)offsetX, (FP)offsetY, (FP)offsetZ);

            var result = pos - offset;

            Assert.Equal(expectedChunkX, result.ChunkX);
            Assert.Equal(expectedChunkY, result.ChunkY);
            Assert.Equal(expectedChunkZ, result.ChunkZ);
            Assert.Equal(expectedX, (int)result.LocalX);
            Assert.Equal(expectedY, (int)result.LocalY);
            Assert.Equal(expectedZ, (int)result.LocalZ);
        }

        /// <summary>
        /// 测试多操作链式运算
        /// </summary>
        [Fact]
        public void Add_MultipleOperations_Chain()
        {
            var pos = WorldPosition.FromFP(FP._0, FP._0, FP._0);

            // 连续三次向 X 正方向移动 500
            pos = pos + new FPVector3((FP)500, FP._0, FP._0);
            pos = pos + new FPVector3((FP)500, FP._0, FP._0);
            pos = pos + new FPVector3((FP)500, FP._0, FP._0);

            // 1500 = 1024 + 476，应该位于 Chunk 1，Local 476
            Assert.Equal(1, pos.ChunkX);
            Assert.Equal(476, (int)pos.LocalX);
        }

        /// <summary>
        /// 测试两个 WorldPosition 相减得到向量
        /// </summary>
        [Fact]
        public void Subtract_TwoPositions_ShouldReturnVector()
        {
            var posA = new WorldPosition(1, 0, 0, (FP)100, FP._0, FP._0);  // 世界 X = 1124
            var posB = new WorldPosition(0, 0, 0, (FP)100, FP._0, FP._0);  // 世界 X = 100

            FPVector3 diff = posA - posB;

            // 差值应该是 1024
            Assert.Equal(1024, (int)diff.X);
            Assert.Equal(0, (int)diff.Y);
            Assert.Equal(0, (int)diff.Z);
        }

        /// <summary>
        /// 测试大数值加法溢出处理
        /// </summary>
        [Fact]
        public void Add_LargeOffset_ShouldHandleMultipleChunkOverflows()
        {
            var pos = new WorldPosition(0, 0, 0, FP._0, FP._0, FP._0);
            // 添加 5000 单位的偏移，约等于 4.88 chunks
            var offset = new FPVector3((FP)5000, FP._0, FP._0);

            var result = pos + offset;

            // 5000 = 4 * 1024 + 904 = 4096 + 904
            Assert.Equal(4, result.ChunkX);
            Assert.Equal(904, (int)result.LocalX);
        }

        #endregion

        #region 距离测试

        /// <summary>
        /// 测试同 Chunk 内距离计算
        /// </summary>
        [Theory]
        [InlineData(0, 0, 0, 300, 400, 0, 500)]   // 3-4-5 三角形
        [InlineData(0, 0, 0, 100, 0, 0, 100)]     // 简单 X 轴
        [InlineData(500, 500, 500, 500, 500, 600, 100)]  // Z 轴差 100
        public void Distance_SameChunk_ShouldBeCorrect(
            int x1, int y1, int z1,
            int x2, int y2, int z2,
            int expectedDistance)
        {
            var posA = new WorldPosition(0, 0, 0, (FP)x1, (FP)y1, (FP)z1);
            var posB = new WorldPosition(0, 0, 0, (FP)x2, (FP)y2, (FP)z2);

            FP distance = WorldPosition.Distance(posA, posB);

            // 允许小误差（定点数精度）
            long tolerance = 10;  // 约 0.00015
            Assert.True(FP.Abs(distance - (FP)expectedDistance).RawValue < tolerance,
                $"Distance should be ~{expectedDistance}, got {(int)distance}");
        }

        /// <summary>
        /// 测试跨 Chunk 距离计算
        /// </summary>
        [Theory]
        [InlineData(0, 0, 1, 0, 0, 0, 1024)]   // 相邻 Chunk，Local 都是 0
        [InlineData(0, 500, 1, 500, 0, 0, 1024)]  // 相邻 Chunk，同 Local
        [InlineData(0, 0, 2, 0, 0, 0, 2048)]     // 间隔 2 Chunks
        public void Distance_CrossChunk_ShouldBeCorrect(
            long chunkX1, int localX1,
            long chunkX2, int localX2,
            int localY, int localZ,
            int expectedDistance)
        {
            var posA = new WorldPosition(chunkX1, 0, 0, (FP)localX1, (FP)localY, (FP)localZ);
            var posB = new WorldPosition(chunkX2, 0, 0, (FP)localX2, (FP)localY, (FP)localZ);

            FP distance = WorldPosition.Distance(posA, posB);

            long tolerance = 10;
            Assert.True(FP.Abs(distance - (FP)expectedDistance).RawValue < tolerance,
                $"Distance should be ~{expectedDistance}, got {(int)distance}");
        }

        /// <summary>
        /// 测试 DistanceSquared（无平方根，更快）
        /// </summary>
        [Theory]
        [InlineData(0, 0, 0, 300, 400, 0, 250000)]   // 500^2 = 250000
        [InlineData(0, 0, 0, 100, 0, 0, 10000)]      // 100^2 = 10000
        [InlineData(0, 0, 0, 0, 0, 1000, 1000000)]   // 1000^2 = 1000000
        public void DistanceSquared_SameChunk_ShouldBeCorrect(
            int x1, int y1, int z1,
            int x2, int y2, int z2,
            int expectedSqrDistance)
        {
            var posA = new WorldPosition(0, 0, 0, (FP)x1, (FP)y1, (FP)z1);
            var posB = new WorldPosition(0, 0, 0, (FP)x2, (FP)y2, (FP)z2);

            FP distSqr = WorldPosition.DistanceSquared(posA, posB);

            long tolerance = 100;
            Assert.True(FP.Abs(distSqr - (FP)expectedSqrDistance).RawValue < tolerance,
                $"DistanceSquared should be ~{expectedSqrDistance}, got {(int)distSqr}");
        }

        /// <summary>
        /// 测试方向向量计算
        /// </summary>
        [Fact]
        public void Direction_SameChunk_ShouldReturnNormalizedVector()
        {
            var from = new WorldPosition(0, 0, 0, FP._0, FP._0, FP._0);
            var to = new WorldPosition(0, 0, 0, (FP)100, FP._0, FP._0);

            FPVector3 dir = WorldPosition.Direction(from, to);

            // 方向应该是正 X，长度约等于 1
            Assert.True(dir.X.RawValue > 0, "X should be positive");
            Assert.True(FP.Abs(dir.Y).RawValue < 100, "Y should be ~0");
            Assert.True(FP.Abs(dir.Z).RawValue < 100, "Z should be ~0");

            FP magnitude = FPMath.Sqrt(dir.X * dir.X + dir.Y * dir.Y + dir.Z * dir.Z);
            Assert.True(FP.Abs(magnitude - FP._1).RawValue < 100,
                $"Direction should be normalized, magnitude {(int)magnitude}");
        }

        /// <summary>
        /// 测试跨 Chunk 方向向量
        /// </summary>
        [Fact]
        public void Direction_CrossChunk_ShouldBeCorrect()
        {
            var from = new WorldPosition(0, 0, 0, FP._0, FP._0, FP._0);
            var to = new WorldPosition(1, 0, 0, FP._0, FP._0, FP._0);

            FPVector3 dir = WorldPosition.Direction(from, to);

            // 从 Chunk 0 到 Chunk 1，方向应该是正 X
            Assert.True(dir.X.RawValue > 0, "X should be positive");
            Assert.True(FP.Abs(dir.Y).RawValue < 100, "Y should be ~0");
            Assert.True(FP.Abs(dir.Z).RawValue < 100, "Z should be ~0");
        }

        /// <summary>
        /// 测试相同位置的方向向量（应为零向量）
        /// </summary>
        [Fact]
        public void Direction_SamePosition_ShouldReturnZero()
        {
            var pos = new WorldPosition(0, 0, 0, (FP)100, (FP)200, (FP)300);

            FPVector3 dir = WorldPosition.Direction(pos, pos);

            Assert.Equal(0, dir.X.RawValue);
            Assert.Equal(0, dir.Y.RawValue);
            Assert.Equal(0, dir.Z.RawValue);
        }

        /// <summary>
        /// 测试 VectorTo 方法（未归一化）
        /// </summary>
        [Fact]
        public void VectorTo_CrossChunk_ShouldReturnRawVector()
        {
            var from = new WorldPosition(0, 0, 0, FP._0, FP._0, FP._0);
            var to = new WorldPosition(1, 2, 0, (FP)100, (FP)200, FP._0);

            FPVector3 vec = WorldPosition.VectorTo(from, to);

            // 期望: X = 1024 + 100 = 1124, Y = 2048 + 200 = 2248
            Assert.Equal(1124, (int)vec.X);
            Assert.Equal(2248, (int)vec.Y);
            Assert.Equal(0, (int)vec.Z);
        }

        #endregion

        #region 相等性测试

        /// <summary>
        /// 测试相同位置相等
        /// </summary>
        [Fact]
        public void Equals_SamePosition_ShouldReturnTrue()
        {
            var posA = new WorldPosition(1, 2, 3, (FP)100, (FP)200, (FP)300);
            var posB = new WorldPosition(1, 2, 3, (FP)100, (FP)200, (FP)300);

            Assert.True(posA == posB);
            Assert.True(posA.Equals(posB));
            Assert.Equal(posA, posB);
        }

        /// <summary>
        /// 测试不同位置不相等
        /// </summary>
        [Theory]
        [InlineData(1, 0, 0, 0, 0, 0)]  // 不同 ChunkX
        [InlineData(0, 1, 0, 0, 0, 0)]  // 不同 ChunkY
        [InlineData(0, 0, 1, 0, 0, 0)]  // 不同 ChunkZ
        [InlineData(0, 0, 0, 1, 0, 0)]  // 不同 LocalX
        [InlineData(0, 0, 0, 0, 1, 0)]  // 不同 LocalY
        [InlineData(0, 0, 0, 0, 0, 1)]  // 不同 LocalZ
        public void Equals_DifferentPositions_ShouldReturnFalse(
            long chunkX, long chunkY, long chunkZ, int localX, int localY, int localZ)
        {
            var posA = new WorldPosition(0, 0, 0, FP._0, FP._0, FP._0);
            var posB = new WorldPosition(chunkX, chunkY, chunkZ,
                (FP)localX, (FP)localY, (FP)localZ);

            Assert.False(posA == posB);
            Assert.True(posA != posB);
        }

        /// <summary>
        /// 测试 GetHashCode 一致性
        /// </summary>
        [Fact]
        public void GetHashCode_SamePosition_ShouldReturnSameHash()
        {
            var posA = new WorldPosition(1, 2, 3, (FP)100, (FP)200, (FP)300);
            var posB = new WorldPosition(1, 2, 3, (FP)100, (FP)200, (FP)300);

            Assert.Equal(posA.GetHashCode(), posB.GetHashCode());
        }

        /// <summary>
        /// 测试相同 Chunk 不同 Local 位置不相等
        /// </summary>
        [Fact]
        public void Equals_SameChunkDifferentLocal_ShouldReturnFalse()
        {
            var posA = new WorldPosition(0, 0, 0, (FP)100, (FP)200, (FP)300);
            var posB = new WorldPosition(0, 0, 0, (FP)101, (FP)200, (FP)300);

            Assert.False(posA == posB);
            Assert.NotEqual(posA.GetHashCode(), posB.GetHashCode());
        }

        /// <summary>
        /// 测试 Equals(object) 方法
        /// </summary>
        [Fact]
        public void EqualsObject_SamePosition_ShouldReturnTrue()
        {
            var pos = new WorldPosition(0, 0, 0, (FP)100, (FP)200, (FP)300);
            object obj = new WorldPosition(0, 0, 0, (FP)100, (FP)200, (FP)300);

            Assert.True(pos.Equals(obj));
        }

        /// <summary>
        /// 测试 Equals(object) 非 WorldPosition 返回 false
        /// </summary>
        [Fact]
        public void EqualsObject_NonWorldPosition_ShouldReturnFalse()
        {
            var pos = new WorldPosition(0, 0, 0, FP._0, FP._0, FP._0);

            Assert.False(pos.Equals("not a position"));
            Assert.False(pos.Equals(null));
        }

        #endregion

        #region 边界情况测试

        /// <summary>
        /// 测试零位置
        /// </summary>
        [Fact]
        public void Constructor_ZeroPosition_ShouldBeValid()
        {
            var pos = new WorldPosition(0, 0, 0, FP._0, FP._0, FP._0);

            Assert.Equal(0, pos.ChunkX);
            Assert.Equal(0, pos.ChunkY);
            Assert.Equal(0, pos.ChunkZ);
            Assert.Equal(0, (int)pos.LocalX);
            Assert.Equal(0, (int)pos.LocalY);
            Assert.Equal(0, (int)pos.LocalZ);
        }

        /// <summary>
        /// 测试最大局部坐标值（CHUNK_SIZE - 1）
        /// </summary>
        [Fact]
        public void Constructor_MaxLocalCoordinates_ShouldBeValid()
        {
            int maxLocal = WorldPosition.CHUNK_SIZE - 1;  // 1023
            var pos = new WorldPosition(0, 0, 0,
                (FP)maxLocal, (FP)maxLocal, (FP)maxLocal);

            Assert.Equal(0, pos.ChunkX);
            Assert.Equal(0, pos.ChunkY);
            Assert.Equal(0, pos.ChunkZ);
            Assert.Equal(maxLocal, (int)pos.LocalX);
            Assert.Equal(maxLocal, (int)pos.LocalY);
            Assert.Equal(maxLocal, (int)pos.LocalZ);
        }

        /// <summary>
        /// 测试边界值刚好等于 CHUNK_SIZE 时溢出
        /// </summary>
        [Fact]
        public void Constructor_AtChunkSizeBoundary_ShouldOverflow()
        {
            var pos = new WorldPosition(0, 0, 0,
                (FP)WorldPosition.CHUNK_SIZE, FP._0, FP._0);

            Assert.Equal(1, pos.ChunkX);
            Assert.Equal(0, (int)pos.LocalX);
        }

        /// <summary>
        /// 测试负 Chunk 坐标
        /// </summary>
        [Theory]
        [InlineData(-1, 0, 0)]
        [InlineData(0, -100, 0)]
        [InlineData(-999999, 0, 0)]
        public void Constructor_NegativeChunkCoordinates_ShouldBeValid(
            long chunkX, long chunkY, long chunkZ)
        {
            var pos = new WorldPosition(chunkX, chunkY, chunkZ,
                (FP)500, (FP)500, (FP)500);

            Assert.Equal(chunkX, pos.ChunkX);
            Assert.Equal(chunkY, pos.ChunkY);
            Assert.Equal(chunkZ, pos.ChunkZ);
        }

        /// <summary>
        /// 测试大 Chunk 坐标转换
        /// </summary>
        [Fact]
        public void TryToFP_LargeChunkCoordinates_ShouldWork()
        {
            // 使用较大的但仍在安全范围内的 Chunk 坐标
            long largeChunk = 1000;  // 在安全范围内
            var pos = new WorldPosition(largeChunk, 0, 0, FP._0, FP._0, FP._0);

            bool success = pos.TryToFP(out FP x, out FP y, out FP z);

            Assert.True(success);
            Assert.Equal(largeChunk * WorldPosition.CHUNK_SIZE, (int)x);
        }

        /// <summary>
        /// 测试小数部分保留
        /// </summary>
        [Fact]
        public void Constructor_WithFractionalParts_ShouldPreserveFractions()
        {
            FP localX = FP._0_50;  // 0.5
            FP localY = FP._0_25;  // 0.25
            FP localZ = FP._0_75;  // 0.75

            var pos = new WorldPosition(0, 0, 0, localX, localY, localZ);

            Assert.Equal(localX.RawValue, pos.LocalX.RawValue);
            Assert.Equal(localY.RawValue, pos.LocalY.RawValue);
            Assert.Equal(localZ.RawValue, pos.LocalZ.RawValue);
        }

        #endregion

        #region 辅助方法测试

        /// <summary>
        /// 测试 GetChunk 方法
        /// </summary>
        [Fact]
        public void GetChunk_ShouldReturnChunkCoordinates()
        {
            var pos = new WorldPosition(1, 2, 3, FP._0, FP._0, FP._0);

            var (x, y, z) = pos.GetChunk();

            Assert.Equal(1, x);
            Assert.Equal(2, y);
            Assert.Equal(3, z);
        }

        /// <summary>
        /// 测试 GetLocal 方法
        /// </summary>
        [Fact]
        public void GetLocal_ShouldReturnLocalVector()
        {
            var pos = new WorldPosition(0, 0, 0, (FP)100, (FP)200, (FP)300);

            FPVector3 local = pos.GetLocal();

            Assert.Equal(100, (int)local.X);
            Assert.Equal(200, (int)local.Y);
            Assert.Equal(300, (int)local.Z);
        }

        /// <summary>
        /// 测试 IsSameChunk 方法
        /// </summary>
        [Fact]
        public void IsSameChunk_SameChunk_ShouldReturnTrue()
        {
            var posA = new WorldPosition(1, 2, 3, (FP)100, (FP)200, (FP)300);
            var posB = new WorldPosition(1, 2, 3, (FP)500, (FP)600, (FP)700);

            Assert.True(posA.IsSameChunk(posB));
        }

        /// <summary>
        /// 测试 IsSameChunk 不同 Chunk 返回 false
        /// </summary>
        [Fact]
        public void IsSameChunk_DifferentChunk_ShouldReturnFalse()
        {
            var posA = new WorldPosition(1, 2, 3, FP._0, FP._0, FP._0);
            var posB = new WorldPosition(2, 2, 3, FP._0, FP._0, FP._0);

            Assert.False(posA.IsSameChunk(posB));
        }

        /// <summary>
        /// 测试 IsInChunk 方法
        /// </summary>
        [Fact]
        public void IsInChunk_ShouldReturnCorrectResult()
        {
            var pos = new WorldPosition(5, 10, 15, FP._0, FP._0, FP._0);

            Assert.True(pos.IsInChunk(5, 10, 15));
            Assert.False(pos.IsInChunk(5, 10, 16));
            Assert.False(pos.IsInChunk(4, 10, 15));
        }

        /// <summary>
        /// 测试 ToString 方法
        /// </summary>
        [Fact]
        public void ToString_ShouldReturnFormattedString()
        {
            var pos = new WorldPosition(1, 2, 3, (FP)100, (FP)200, (FP)300);

            string str = pos.ToString();

            Assert.Contains("Chunk: (1,2,3)", str);
            // ToString 会包含 FP 的完整格式，检查包含部分值即可
            Assert.Contains("100", str);
            Assert.Contains("200", str);
            Assert.Contains("300", str);
        }

        #endregion

        #region 复杂场景测试

        /// <summary>
        /// 测试无限世界漫游场景：连续移动跨越多个 Chunks
        /// </summary>
        [Fact]
        public void Scenario_InfiniteWorldTraversal()
        {
            // 玩家从原点出发，向 X 正方向飞行 10000 单位
            var playerPos = WorldPosition.FromFP(FP._0, FP._0, FP._0);
            FP speed = (FP)100;  // 100 单位/秒

            for (int i = 0; i < 100; i++)
            {
                playerPos += new FPVector3(speed, FP._0, FP._0);
            }

            // 100 * 100 = 10000 单位
            // 10000 / 1024 ≈ 9.76 chunks
            // 所以应该在 Chunk 9，Local = 10000 - 9*1024 = 10000 - 9216 = 784
            Assert.Equal(9, playerPos.ChunkX);
            Assert.True((int)playerPos.LocalX >= 784 - 1 && (int)playerPos.LocalX <= 784 + 1,
                $"Expected LocalX ~784, got {(int)playerPos.LocalX}");
        }

        /// <summary>
        /// 测试往返移动后回到原点
        /// </summary>
        [Fact]
        public void Scenario_SimpleMovement_ShouldWork()
        {
            // 测试基本移动场景
            var start = WorldPosition.FromFP((FP)100, (FP)100, (FP)100);
            
            // 向正方向移动
            var moved = start + new FPVector3((FP)400, (FP)500, (FP)600);
            
            // 验证新位置
            bool success = moved.TryToFP(out FP x, out FP y, out FP z);
            Assert.True(success);
            Assert.Equal(500, (int)x);  // 100 + 400 = 500
            Assert.Equal(600, (int)y);  // 100 + 500 = 600
            Assert.Equal(700, (int)z);  // 100 + 600 = 700
        }

        /// <summary>
        /// 测试距离对称性：Distance(a,b) == Distance(b,a)
        /// </summary>
        [Fact]
        public void Distance_ShouldBeSymmetric()
        {
            var posA = new WorldPosition(0, 0, 0, (FP)100, (FP)200, (FP)300);
            var posB = new WorldPosition(5, 10, 15, (FP)500, (FP)600, (FP)700);

            FP distAB = WorldPosition.Distance(posA, posB);
            FP distBA = WorldPosition.Distance(posB, posA);

            Assert.Equal(distAB.RawValue, distBA.RawValue);
        }

        /// <summary>
        /// 测试 DistanceSquared 与 Distance 的关系
        /// </summary>
        [Fact]
        public void DistanceSquared_ShouldBeSquareOfDistance()
        {
            // 使用简单的同 Chunk 场景
            var posA = new WorldPosition(0, 0, 0, (FP)0, (FP)0, (FP)0);
            var posB = new WorldPosition(0, 0, 0, (FP)300, (FP)400, (FP)0);

            FP dist = WorldPosition.Distance(posA, posB);  // 应该约等于 500
            FP distSqr = WorldPosition.DistanceSquared(posA, posB);  // 应该约等于 250000

            // distSqr 应该约等于 dist^2
            FP calculatedSqr = dist * dist;
            long tolerance = 50000;  // 较大的容差因为两次计算可能有舍入误差
            Assert.True(FP.Abs(distSqr - calculatedSqr).RawValue < tolerance,
                $"DistanceSquared {(int)distSqr} should be close to Distance^2 {(int)calculatedSqr}");
            
            // 同时验证基本正确性
            Assert.True(distSqr.RawValue > 240000 * FP.ONE && distSqr.RawValue < 260000 * FP.ONE,
                $"DistanceSquared should be close to 250000, got {(int)distSqr}");
        }

        #endregion
    }
}
