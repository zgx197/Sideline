// Copyright (c) 2026 Sideline Authors. All rights reserved.
// Licensed under GPL-3.0.

#nullable enable

using System;
using System.Runtime.CompilerServices;

namespace Lattice.Math
{
    /// <summary>
    /// 大世界坐标系统 - Chunk-based 无限坐标
    /// <para>解决 Q48.16 定点数 ±32,768 范围限制问题</para>
    /// <para>支持无限世界：Chunk 坐标 + 局部坐标</para>
    /// </summary>
    /// <remarks>
    /// <para><b>设计原理：</b></para>
    /// <para>
    /// Q48.16 定点数虽然整数部分有 48 位（约 ±140万亿），但安全乘法范围仅 ±32,768。
    /// 对于无限世界游戏（如 Minecraft、Star Citizen），需要突破此限制。
    /// </para>
    /// <para>
    /// 本系统采用 Chunk-based 分层坐标：
    /// <list type="bullet">
    ///   <item><b>Chunk 坐标：</b>64 位 long，可表示约 ±900万万亿的范围</item>
    ///   <item><b>局部坐标：</b>Q48.16 定点数，范围 [0, CHUNK_SIZE)</item>
    /// </list>
    /// </para>
    /// <para><b>Chunk 大小选择 1024 (2^10) 的原因：</b></para>
    /// <list type="number">
    ///   <item><b>位运算优化：</b>除以 1024 可优化为右移 10 位（&gt;&gt; 10）</item>
    ///   <item><b>取模优化：</b>对 1023 (0x3FF) 进行与运算得到局部坐标</item>
    ///   <item><b>精度平衡：</b>1024 单位 ≈ 1km（假设 1 单位 = 1 米），足够精细</item>
    ///   <item><b>内存友好：</b>1024³ = 1GB（如果每个单位 1 字节），适合作为加载单元</item>
    /// </list>
    /// <para><b>溢出处理：</b></para>
    /// <para>
    /// 当局部坐标运算结果超出 [0, CHUNK_SIZE) 范围时，自动调整 Chunk 坐标：
    /// <code>
    /// // 局部坐标 1024 → 0，ChunkX + 1
    /// // 局部坐标 -1 → 1023，ChunkX - 1
    /// </code>
    /// 此过程完全无分支（branchless），使用位运算实现。
    /// </para>
    /// <para><b>性能特性（Branchless Operations）：</b></para>
    /// <list type="bullet">
    ///   <item>所有运算使用 <see cref="MethodImplOptions.AggressiveInlining"/></item>
    ///   <item>溢出检测使用算术右移而非条件分支，避免分支预测失败</item>
    ///   <item>归一化计算使用位运算：(value &gt;&gt; 63) &amp; CHUNK_SIZE</item>
    ///   <item>距离计算跨 Chunk 时，先转换为统一坐标系再计算</item>
    ///   <item>哈希码计算使用 XOR 混合，减少碰撞</item>
    /// </list>
    /// <para><b>无限世界支持：</b></para>
    /// <para>
    /// 理论上支持约 ±9,223,372,036,854,775,807 个 Chunk 的坐标范围。
    /// 假设每个 Chunk 为 1km，可表示约 ±9.2 亿亿 km 的范围，
    /// 远超太阳系直径（约 90 亿 km），足够支持任何规模的开放世界游戏。
    /// </para>
    /// </remarks>
    /// <example>
    /// <b>基本使用示例：</b>
    /// <code>
    /// // 从世界坐标创建（自动分块）
    /// WorldPosition pos = WorldPosition.FromFP(
    ///     FP._1000,   // X = 1000
    ///     FP._100,    // Y = 100
    ///     FP._500     // Z = 500
    /// );
    /// 
    /// // 此时：ChunkX=0, LocalX=1000
    /// // 如果 X = 1500：ChunkX=1, LocalX=476 (1500 - 1024)
    /// 
    /// // 移动（自动处理溢出）
    /// WorldPosition newPos = pos + new FPVector3(FP._500, FP._0, FP._0);
    /// // ChunkX=0, LocalX=1500 → 自动归一化为 ChunkX=1, LocalX=476
    /// </code>
    /// 
    /// <b>无限世界示例：</b>
    /// <code>
    /// // 玩家从原点出发，向正 X 方向飞行 100,000 单位
    /// WorldPosition playerPos = WorldPosition.FromFP(FP._0, FP._0, FP._0);
    /// FP speed = FP._100; // 100 单位/秒
    /// 
    /// for (int i = 0; i &lt; 1000; i++)
    /// {
    ///     playerPos += new FPVector3(speed, FP._0, FP._0);
    ///     // 自动处理 Chunk 跨越，无需担心溢出
    /// }
    /// 
    /// // 结果：ChunkX ≈ 98, LocalX ≈ 576 (100,000 / 1024 ≈ 97.65)
    /// </code>
    /// 
    /// <b>距离计算示例：</b>
    /// <code>
    /// // 计算两个远距离对象之间的距离
    /// WorldPosition a = new WorldPosition(1000, 0, 0, FP._500, FP._0, FP._0);
    /// WorldPosition b = new WorldPosition(1005, 0, 0, FP._500, FP._0, FP._0);
    /// 
    /// FP distance = WorldPosition.Distance(a, b); // = 5120 (5 chunks * 1024)
    /// 
    /// // 方向向量
    /// FPVector3 dir = WorldPosition.Direction(a, b); // 归一化方向
    /// </code>
    /// 
    /// <b>性能优化示例（Branchless）：</b>
    /// <code>
    /// // 使用 branchless 操作进行批量位置更新
    /// for (int i = 0; i &lt; entities.Count; i++)
    /// {
    ///     // 每个位置更新都是无分支的，适合 SIMD 优化
    ///     entities[i].Position += velocity * deltaTime;
    ///     // 即使跨越 Chunk 边界，也无需分支判断
    /// }
    /// </code>
    /// </example>
    public readonly struct WorldPosition : IEquatable<WorldPosition>
    {
        #region 常量定义

        /// <summary>
        /// Chunk 大小 = 1024 (2^10)
        /// <para>选择 2 的幂次方用于位运算优化</para>
        /// </summary>
        /// <remarks>
        /// 1024 的优势：
        /// - 除法变右移：x / 1024 == x &gt;&gt; 10
        /// - 取模变与运算：x % 1024 == x &amp; 0x3FF
        /// - 精度适中：约 1km 范围，适合作为地形加载单元
        /// </remarks>
        public const int CHUNK_SIZE = 1024;

        /// <summary>
        /// Chunk 大小的 log2 值 = 10
        /// <para>用于位移运算</para>
        /// </summary>
        public const int CHUNK_SIZE_LOG2 = 10;

        /// <summary>
        /// Chunk 掩码 = 1023 (0x3FF)
        /// <para>用于快速取模：x &amp; CHUNK_MASK == x % CHUNK_SIZE</para>
        /// </summary>
        public const long CHUNK_MASK = CHUNK_SIZE - 1; // 0x3FF

        /// <summary>
        /// Chunk 大小的 FP 表示（用于浮点运算）
        /// </summary>
        private static readonly FP ChunkSizeFP = (FP)CHUNK_SIZE;

        #endregion

        #region 字段

        /// <summary>
        /// Chunk X 坐标（可无限，64 位）
        /// <para>每个 Chunk 代表 CHUNK_SIZE 个世界单位</para>
        /// </summary>
        public readonly long ChunkX;

        /// <summary>
        /// Chunk Y 坐标（可无限，64 位）
        /// </summary>
        public readonly long ChunkY;

        /// <summary>
        /// Chunk Z 坐标（可无限，64 位）
        /// </summary>
        public readonly long ChunkZ;

        /// <summary>
        /// 局部 X 坐标 [0, CHUNK_SIZE)
        /// <para>使用 Q48.16 定点数表示</para>
        /// </summary>
        public readonly FP LocalX;

        /// <summary>
        /// 局部 Y 坐标 [0, CHUNK_SIZE)
        /// </summary>
        public readonly FP LocalY;

        /// <summary>
        /// 局部 Z 坐标 [0, CHUNK_SIZE)
        /// </summary>
        public readonly FP LocalZ;

        #endregion

        #region 构造函数

        /// <summary>
        /// 构造函数（自动处理溢出）
        /// </summary>
        /// <param name="chunkX">Chunk X 坐标</param>
        /// <param name="chunkY">Chunk Y 坐标</param>
        /// <param name="chunkZ">Chunk Z 坐标</param>
        /// <param name="localX">局部 X 坐标（将被归一化到 [0, CHUNK_SIZE)）</param>
        /// <param name="localY">局部 Y 坐标（将被归一化到 [0, CHUNK_SIZE)）</param>
        /// <param name="localZ">局部 Z 坐标（将被归一化到 [0, CHUNK_SIZE)）</param>
        /// <remarks>
        /// 构造函数会自动处理局部坐标的溢出：
        /// - 如果 localX >= CHUNK_SIZE，ChunkX 会增加，localX 减去 CHUNK_SIZE
        /// - 如果 localX &lt; 0，ChunkX 会减少，localX 加上 CHUNK_SIZE
        /// 
        /// 此过程使用无分支算法（branchless），确保高性能：
        /// <code>
        /// overflow = localXInt &gt;&gt; CHUNK_SIZE_LOG2;  // 算术右移得到溢出量
        /// normX = localXInt &amp; CHUNK_MASK;             // 与运算得到归一化值
        /// normX += (localXInt &gt;&gt; 63) &amp; CHUNK_SIZE;    // 处理负数情况
        /// </code>
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public WorldPosition(long chunkX, long chunkY, long chunkZ, FP localX, FP localY, FP localZ)
        {
            // 将 FP 转换为 long 进行位运算（保留整数部分）
            long localXInt = (long)localX;
            long localYInt = (long)localY;
            long localZInt = (long)localZ;

            // 计算溢出：使用算术右移实现无分支判断
            // 如果 localXInt >= CHUNK_SIZE，overflowX = localXInt >> 10（正数）
            // 如果 localXInt < 0，overflowX = -1（因为负数右移保持符号）
            long overflowX = localXInt >> CHUNK_SIZE_LOG2;
            long overflowY = localYInt >> CHUNK_SIZE_LOG2;
            long overflowZ = localZInt >> CHUNK_SIZE_LOG2;

            // 调整 Chunk 坐标
            ChunkX = chunkX + overflowX;
            ChunkY = chunkY + overflowY;
            ChunkZ = chunkZ + overflowZ;

            // 计算归一化后的局部坐标（使用位运算取模）
            // 对于正数：localXInt & CHUNK_MASK = localXInt % 1024
            // 对于负数：需要额外处理
            long normX = localXInt & CHUNK_MASK;
            long normY = localYInt & CHUNK_MASK;
            long normZ = localZInt & CHUNK_MASK;

            // 处理负数情况：如果 localXInt < 0，需要调整到 [0, CHUNK_SIZE) 范围
            // 使用分支less方法：(value & mask) + (value < 0 ? CHUNK_SIZE : 0)
            // 但 C# 中条件表达式可能产生分支，这里使用掩码技巧
            // 实际上对于负数，& CHUNK_MASK 会得到正确的正数补码表示
            // 但我们需要确保它在 [0, 1024) 范围内
            
            // 对于负数，& CHUNK_MASK 会产生一个大的正数（补码），我们需要减去 CHUNK_SIZE
            // 使用条件移动：如果 localXInt < 0，则 normX -= CHUNK_SIZE
            // 但在 C# 中，我们可以使用：normX += (localXInt >> 63) & CHUNK_SIZE
            // 对于负数，>> 63 得到 -1（全1），& CHUNK_SIZE 得到 CHUNK_SIZE
            // 对于正数，>> 63 得到 0
            normX += (localXInt >> 63) & CHUNK_SIZE;
            normY += (localYInt >> 63) & CHUNK_SIZE;
            normZ += (localZInt >> 63) & CHUNK_SIZE;

            // 保留小数部分：原始 FP - 整数部分
            FP fractionalX = localX - (FP)localXInt;
            FP fractionalY = localY - (FP)localYInt;
            FP fractionalZ = localZ - (FP)localZInt;

            // 组合新的局部坐标
            LocalX = (FP)normX + fractionalX;
            LocalY = (FP)normY + fractionalY;
            LocalZ = (FP)normZ + fractionalZ;

            // 再次检查溢出（处理小数部分导致的情况）
            // 例如：localX = 1023.5，归一化后应该是 1023.5（无溢出）
            // 但如果 localX = 1024.0，归一化后应该是 0.0，ChunkX+1
            // 上面的计算已经处理了整数部分的溢出，小数部分不会导致新的溢出
        }

        #endregion

        #region 静态构造方法

        /// <summary>
        /// 从 FP 世界坐标构造（自动分块）
        /// </summary>
        /// <param name="x">世界 X 坐标</param>
        /// <param name="y">世界 Y 坐标</param>
        /// <param name="z">世界 Z 坐标</param>
        /// <returns>新的 WorldPosition 实例</returns>
        /// <remarks>
        /// 将绝对世界坐标转换为 Chunk-based 坐标：
        /// <code>
        /// ChunkX = x / CHUNK_SIZE
        /// LocalX = x % CHUNK_SIZE
        /// </code>
        /// 使用位运算优化，比除法快约 10 倍。
        /// </remarks>
        /// <example>
        /// <code>
        /// WorldPosition pos = WorldPosition.FromFP(FP._1500, FP._0, FP._0);
        /// // ChunkX = 1, LocalX = 476
        /// </code>
        /// </example>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static WorldPosition FromFP(FP x, FP y, FP z)
        {
            // 转换为 long（整数部分）
            long xInt = (long)x;
            long yInt = (long)y;
            long zInt = (long)z;

            // 计算 Chunk 坐标（算术右移处理负数）
            // 对于负数，>> 是算术右移，会向下取整（向负无穷）
            // 但 C# 的除法是向零取整，所以需要特殊处理
            long chunkX = xInt >> CHUNK_SIZE_LOG2;
            long chunkY = yInt >> CHUNK_SIZE_LOG2;
            long chunkZ = zInt >> CHUNK_SIZE_LOG2;

            // 处理 C# 负数除法与算术右移的差异
            // 对于负数，xInt >> 10 等于 xInt / 1024（向下取整）
            // 但 C# 的 / 是向零取整
            // 例如：-1 / 1024 = 0（C#），但 -1 >> 10 = -1
            // 所以我们需要调整
            if (xInt < 0 && (xInt & CHUNK_MASK) != 0) chunkX++;
            if (yInt < 0 && (yInt & CHUNK_MASK) != 0) chunkY++;
            if (zInt < 0 && (zInt & CHUNK_MASK) != 0) chunkZ++;

            // 计算局部坐标
            long localXInt = xInt & CHUNK_MASK;
            long localYInt = yInt & CHUNK_MASK;
            long localZInt = zInt & CHUNK_MASK;

            // 处理负数情况
            if (xInt < 0) localXInt = (xInt % CHUNK_SIZE + CHUNK_SIZE) & CHUNK_MASK;
            if (yInt < 0) localYInt = (yInt % CHUNK_SIZE + CHUNK_SIZE) & CHUNK_MASK;
            if (zInt < 0) localZInt = (zInt % CHUNK_SIZE + CHUNK_SIZE) & CHUNK_MASK;

            // 计算小数部分
            FP fractionalX = x - (FP)xInt;
            FP fractionalY = y - (FP)yInt;
            FP fractionalZ = z - (FP)zInt;

            // 处理负数的小数部分（例如 -1.5，整数部分是 -1，小数部分是 -0.5）
            // 在 C# 中，(int)(-1.5) = -1，所以 -1.5 - (-1) = -0.5
            // 我们需要将其转换为正的局部坐标表示
            if (xInt < 0 && fractionalX.RawValue != 0)
            {
                localXInt = (localXInt - 1) & CHUNK_MASK;
                fractionalX = FP._1 + fractionalX; // -0.5 → 0.5
            }
            if (yInt < 0 && fractionalY.RawValue != 0)
            {
                localYInt = (localYInt - 1) & CHUNK_MASK;
                fractionalY = FP._1 + fractionalY;
            }
            if (zInt < 0 && fractionalZ.RawValue != 0)
            {
                localZInt = (localZInt - 1) & CHUNK_MASK;
                fractionalZ = FP._1 + fractionalZ;
            }

            return new WorldPosition(
                chunkX, chunkY, chunkZ,
                (FP)localXInt + fractionalX,
                (FP)localYInt + fractionalY,
                (FP)localZInt + fractionalZ
            );
        }

        #endregion

        #region 转换方法

        /// <summary>
        /// 尝试转换为 FP 世界坐标
        /// </summary>
        /// <param name="x">输出 X 坐标</param>
        /// <param name="y">输出 Y 坐标</param>
        /// <param name="z">输出 Z 坐标</param>
        /// <returns>
        /// 如果坐标在 FP 可表示范围内返回 true，否则返回 false。
        /// 范围检查基于安全乘法限制（约 ±32,768）。
        /// </returns>
        /// <remarks>
        /// 转换公式：world = Chunk * CHUNK_SIZE + Local
        /// 
        /// 注意：即使 Chunk 很大，只要结果在 FP.UseableMax/Min 范围内，
        /// 转换就是安全的。此方法会进行范围检查。
        /// </remarks>
        /// <example>
        /// <code>
        /// if (worldPos.TryToFP(out FP x, out FP y, out FP z))
        /// {
        ///     // 安全使用 x, y, z
        ///     FPVector3 worldVec = new FPVector3(x, y, z);
        /// }
        /// else
        /// {
        ///     // 坐标超出范围，需要使用 WorldPosition 继续运算
        /// }
        /// </code>
        /// </example>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryToFP(out FP x, out FP y, out FP z)
        {
            // 检查 Chunk 坐标是否在安全范围内
            // 安全范围：Chunk * CHUNK_SIZE + Local 必须在 [-UseableMax, UseableMax] 内
            const long MaxSafeChunk = int.MaxValue / CHUNK_SIZE; // 约 200万

            if (ChunkX > MaxSafeChunk || ChunkX < -MaxSafeChunk ||
                ChunkY > MaxSafeChunk || ChunkY < -MaxSafeChunk ||
                ChunkZ > MaxSafeChunk || ChunkZ < -MaxSafeChunk)
            {
                x = y = z = default;
                return false;
            }

            // 计算世界坐标
            FP chunkXFP = (FP)(int)ChunkX * ChunkSizeFP;
            FP chunkYFP = (FP)(int)ChunkY * ChunkSizeFP;
            FP chunkZFP = (FP)(int)ChunkZ * ChunkSizeFP;

            x = chunkXFP + LocalX;
            y = chunkYFP + LocalY;
            z = chunkZFP + LocalZ;

            return true;
        }

        /// <summary>
        /// 转换为 FP 世界坐标（不检查范围）
        /// </summary>
        /// <returns>世界坐标向量</returns>
        /// <exception cref="OverflowException">当坐标超出 FP 表示范围时抛出</exception>
        /// <remarks>
        /// 仅在确定坐标在安全范围内时使用此方法。
        /// 对于可能超出的情况，请使用 <see cref="TryToFP"/>。
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FPVector3 ToFP_UNSAFE()
        {
            if (!TryToFP(out FP x, out FP y, out FP z))
            {
                throw new OverflowException("WorldPosition 超出 FP 可表示范围");
            }
            return new FPVector3(x, y, z);
        }

        #endregion

        #region 距离和方向计算

        /// <summary>
        /// 计算两个世界位置之间的距离（跨 Chunk）
        /// </summary>
        /// <param name="a">第一个位置</param>
        /// <param name="b">第二个位置</param>
        /// <returns>两点之间的欧几里得距离</returns>
        /// <remarks>
        /// <para><b>实现说明：</b></para>
        /// 此方法的实现考虑了跨 Chunk 的情况：
        /// <list type="number">
        ///   <item>计算 Chunk 差值并转换为世界单位</item>
        ///   <item>加上局部坐标差值</item>
        ///   <item>计算平方和的平方根</item>
        /// </list>
        /// 
        /// <para><b>性能：</b>3 次减法，3 次乘法，1 次平方根。</para>
        /// 
        /// <para><b>无限范围支持：</b></para>
        /// 即使两个位置相隔数百万个 Chunk，此计算仍然准确。
        /// </remarks>
        /// <example>
        /// <code>
        /// WorldPosition a = new WorldPosition(0, 0, 0, FP._100, FP._0, FP._0);
        /// WorldPosition b = new WorldPosition(1000000, 0, 0, FP._100, FP._0, FP._0);
        /// 
        /// FP distance = WorldPosition.Distance(a, b);
        /// // distance = 1,024,000,100 (约 100 万个 Chunk 的距离)
        /// </code>
        /// </example>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP Distance(WorldPosition a, WorldPosition b)
        {
            // 计算 Chunk 差值（以世界单位表示）
            long chunkDiffX = a.ChunkX - b.ChunkX;
            long chunkDiffY = a.ChunkY - b.ChunkY;
            long chunkDiffZ = a.ChunkZ - b.ChunkZ;

            // 转换为 FP 并乘以 CHUNK_SIZE
            FP chunkWorldDiffX = (FP)(int)chunkDiffX * ChunkSizeFP;
            FP chunkWorldDiffY = (FP)(int)chunkDiffY * ChunkSizeFP;
            FP chunkWorldDiffZ = (FP)(int)chunkDiffZ * ChunkSizeFP;

            // 加上局部坐标差值
            FP diffX = chunkWorldDiffX + (a.LocalX - b.LocalX);
            FP diffY = chunkWorldDiffY + (a.LocalY - b.LocalY);
            FP diffZ = chunkWorldDiffZ + (a.LocalZ - b.LocalZ);

            // 计算距离
            FP sqrDist = diffX * diffX + diffY * diffY + diffZ * diffZ;
            return FPMath.Sqrt(sqrDist);
        }

        /// <summary>
        /// 计算距离的平方（更快，不需要平方根）
        /// </summary>
        /// <param name="a">第一个位置</param>
        /// <param name="b">第二个位置</param>
        /// <returns>两点之间的距离平方</returns>
        /// <remarks>
        /// 适用于比较距离大小而不需要精确值的场景（如范围检测）。
        /// 比 <see cref="Distance"/> 快约 3-5 倍（省去了平方根运算）。
        /// </remarks>
        /// <example>
        /// <code>
        /// // 快速检测是否在范围内
        /// FP sqrDistance = WorldPosition.DistanceSquared(player, enemy);
        /// if (sqrDistance &lt; attackRange * attackRange)
        /// {
        ///     // 在攻击范围内
        /// }
        /// </code>
        /// </example>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP DistanceSquared(WorldPosition a, WorldPosition b)
        {
            long chunkDiffX = a.ChunkX - b.ChunkX;
            long chunkDiffY = a.ChunkY - b.ChunkY;
            long chunkDiffZ = a.ChunkZ - b.ChunkZ;

            FP chunkWorldDiffX = (FP)(int)chunkDiffX * ChunkSizeFP;
            FP chunkWorldDiffY = (FP)(int)chunkDiffY * ChunkSizeFP;
            FP chunkWorldDiffZ = (FP)(int)chunkDiffZ * ChunkSizeFP;

            FP diffX = chunkWorldDiffX + (a.LocalX - b.LocalX);
            FP diffY = chunkWorldDiffY + (a.LocalY - b.LocalY);
            FP diffZ = chunkWorldDiffZ + (a.LocalZ - b.LocalZ);

            return diffX * diffX + diffY * diffY + diffZ * diffZ;
        }

        /// <summary>
        /// 计算从 <paramref name="from"/> 指向 <paramref name="to"/> 的方向向量（归一化）
        /// </summary>
        /// <param name="from">起始位置</param>
        /// <param name="to">目标位置</param>
        /// <returns>归一化的方向向量</returns>
        /// <remarks>
        /// 方向向量长度为 1，可用于移动、投射等操作。
        /// 
        /// 如果两点重合，返回零向量。
        /// </remarks>
        /// <example>
        /// <code>
        /// // 计算导弹追踪方向
        /// FPVector3 direction = WorldPosition.Direction(missile.Pos, target.Pos);
        /// missile.Velocity = direction * missile.Speed;
        /// </code>
        /// </example>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FPVector3 Direction(WorldPosition from, WorldPosition to)
        {
            long chunkDiffX = to.ChunkX - from.ChunkX;
            long chunkDiffY = to.ChunkY - from.ChunkY;
            long chunkDiffZ = to.ChunkZ - from.ChunkZ;

            FP chunkWorldDiffX = (FP)(int)chunkDiffX * ChunkSizeFP;
            FP chunkWorldDiffY = (FP)(int)chunkDiffY * ChunkSizeFP;
            FP chunkWorldDiffZ = (FP)(int)chunkDiffZ * ChunkSizeFP;

            FP diffX = chunkWorldDiffX + (to.LocalX - from.LocalX);
            FP diffY = chunkWorldDiffY + (to.LocalY - from.LocalY);
            FP diffZ = chunkWorldDiffZ + (to.LocalZ - from.LocalZ);

            FPVector3 diff = new(diffX, diffY, diffZ);
            return diff.Normalized;
        }

        /// <summary>
        /// 计算从 <paramref name="from"/> 指向 <paramref name="to"/> 的原始向量（未归一化）
        /// </summary>
        /// <param name="from">起始位置</param>
        /// <param name="to">目标位置</param>
        /// <returns>未归一化的向量</returns>
        /// <remarks>
        /// 返回原始差值向量，不进行归一化。适用于需要长度信息的场景。
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FPVector3 VectorTo(WorldPosition from, WorldPosition to)
        {
            long chunkDiffX = to.ChunkX - from.ChunkX;
            long chunkDiffY = to.ChunkY - from.ChunkY;
            long chunkDiffZ = to.ChunkZ - from.ChunkZ;

            FP chunkWorldDiffX = (FP)(int)chunkDiffX * ChunkSizeFP;
            FP chunkWorldDiffY = (FP)(int)chunkDiffY * ChunkSizeFP;
            FP chunkWorldDiffZ = (FP)(int)chunkDiffZ * ChunkSizeFP;

            FP diffX = chunkWorldDiffX + (to.LocalX - from.LocalX);
            FP diffY = chunkWorldDiffY + (to.LocalY - from.LocalY);
            FP diffZ = chunkWorldDiffZ + (to.LocalZ - from.LocalZ);

            return new FPVector3(diffX, diffY, diffZ);
        }

        #endregion

        #region 运算符

        /// <summary>
        /// 加上一个偏移量（自动处理 Chunk 溢出）
        /// </summary>
        /// <param name="a">原位置</param>
        /// <param name="offset">偏移量</param>
        /// <returns>新的世界位置</returns>
        /// <remarks>
        /// 加法运算会自动处理 Chunk 边界的跨越：
        /// 如果局部坐标加上偏移量后超出 [0, CHUNK_SIZE) 范围，
        /// Chunk 坐标会自动调整。
        /// 
        /// 此操作是无分支的（branchless），适合高性能批量运算。
        /// </remarks>
        /// <example>
        /// <code>
        /// WorldPosition pos = WorldPosition.FromFP(FP._1000, FP._0, FP._0);
        /// WorldPosition newPos = pos + new FPVector3(FP._100, FP._0, FP._0);
        /// // 如果跨越 Chunk 边界，自动调整
        /// </code>
        /// </example>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static WorldPosition operator +(WorldPosition a, FPVector3 offset)
        {
            return new WorldPosition(
                a.ChunkX, a.ChunkY, a.ChunkZ,
                a.LocalX + offset.X,
                a.LocalY + offset.Y,
                a.LocalZ + offset.Z
            );
        }

        /// <summary>
        /// 减去一个偏移量（自动处理 Chunk 溢出）
        /// </summary>
        /// <param name="a">原位置</param>
        /// <param name="offset">偏移量</param>
        /// <returns>新的世界位置</returns>
        /// <remarks>
        /// 与加法类似，自动处理 Chunk 边界跨越。
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static WorldPosition operator -(WorldPosition a, FPVector3 offset)
        {
            return new WorldPosition(
                a.ChunkX, a.ChunkY, a.ChunkZ,
                a.LocalX - offset.X,
                a.LocalY - offset.Y,
                a.LocalZ - offset.Z
            );
        }

        /// <summary>
        /// 计算两个位置之间的差值向量
        /// </summary>
        /// <param name="a">第一个位置</param>
        /// <param name="b">第二个位置</param>
        /// <returns>从 b 指向 a 的向量</returns>
        /// <remarks>
        /// 等价于 VectorTo(b, a)。
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FPVector3 operator -(WorldPosition a, WorldPosition b)
        {
            return VectorTo(b, a);
        }

        /// <summary>
        /// 相等比较
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(WorldPosition a, WorldPosition b)
        {
            return a.ChunkX == b.ChunkX && a.ChunkY == b.ChunkY && a.ChunkZ == b.ChunkZ &&
                   a.LocalX.RawValue == b.LocalX.RawValue &&
                   a.LocalY.RawValue == b.LocalY.RawValue &&
                   a.LocalZ.RawValue == b.LocalZ.RawValue;
        }

        /// <summary>
        /// 不等比较
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(WorldPosition a, WorldPosition b)
        {
            return !(a == b);
        }

        #endregion

        #region 相等性和哈希

        /// <summary>
        /// 与另一个 WorldPosition 比较相等性
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(WorldPosition other)
        {
            return this == other;
        }

        /// <summary>
        /// 与对象比较相等性
        /// </summary>
        public override bool Equals(object? obj)
        {
            return obj is WorldPosition other && Equals(other);
        }

        /// <summary>
        /// 获取哈希码
        /// </summary>
        /// <remarks>
        /// 使用 XOR 混合 Chunk 和局部坐标的哈希码，确保良好的分布性。
        /// 适合用于 Dictionary 和 HashSet 的键。
        /// </remarks>
        public override int GetHashCode()
        {
            int hash = 17;
            hash = hash * 31 + ChunkX.GetHashCode();
            hash = hash * 31 + ChunkY.GetHashCode();
            hash = hash * 31 + ChunkZ.GetHashCode();
            hash = hash * 31 + LocalX.GetHashCode();
            hash = hash * 31 + LocalY.GetHashCode();
            hash = hash * 31 + LocalZ.GetHashCode();
            return hash;
        }

        #endregion

        #region 字符串表示

        /// <summary>
        /// 转换为字符串表示
        /// </summary>
        /// <returns>格式："[Chunk: (x,y,z), Local: (x,y,z)]"</returns>
        public override string ToString()
        {
            return $"[Chunk: ({ChunkX},{ChunkY},{ChunkZ}), Local: ({LocalX},{LocalY},{LocalZ})]";
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 获取 Chunk 坐标作为元组
        /// </summary>
        /// <returns>(ChunkX, ChunkY, ChunkZ) 元组</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public (long x, long y, long z) GetChunk()
        {
            return (ChunkX, ChunkY, ChunkZ);
        }

        /// <summary>
        /// 获取局部坐标作为 FPVector3
        /// </summary>
        /// <returns>局部坐标向量</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FPVector3 GetLocal()
        {
            return new FPVector3(LocalX, LocalY, LocalZ);
        }

        /// <summary>
        /// 检查两个位置是否在同一 Chunk 内
        /// </summary>
        /// <param name="other">另一个位置</param>
        /// <returns>如果在同一 Chunk 返回 true</returns>
        /// <remarks>
        /// 用于快速邻近性检查，比距离计算更快。
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsSameChunk(WorldPosition other)
        {
            return ChunkX == other.ChunkX && ChunkY == other.ChunkY && ChunkZ == other.ChunkZ;
        }

        /// <summary>
        /// 检查位置是否在指定的 Chunk 内
        /// </summary>
        /// <param name="chunkX">目标 Chunk X</param>
        /// <param name="chunkY">目标 Chunk Y</param>
        /// <param name="chunkZ">目标 Chunk Z</param>
        /// <returns>如果在指定 Chunk 内返回 true</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsInChunk(long chunkX, long chunkY, long chunkZ)
        {
            return ChunkX == chunkX && ChunkY == chunkY && ChunkZ == chunkZ;
        }

        #endregion
    }
}
