# Lattice ECS 架构设计（历史归档）

> 历史归档文档：
> 本文反映的是较早阶段对“完整游戏框架版 ECS”的设想，其中大量 `SystemGroup`、多线程、旧快照与完整产品层能力描述不等同于当前主干。
> 当前实现状态请优先以 `godot/scripts/lattice/README.md` 和 `godot/scripts/lattice/ECS/Framework/SystemDesignNotes.md` 为准。

## 文档版本
- **Version**: 1.0（历史归档）
- **Last Updated**: 2026-03-18
- **Status**: Archived / 非当前主干实现说明

---

## 目录
1. [设计目标](#设计目标)
2. [架构概览](#架构概览)
3. [核心设计决策](#核心设计决策)
4. [性能优化策略](#性能优化策略)
5. [预测回滚支持](#预测回滚支持)
6. [多线程架构](#多线程架构)
7. [与 FrameSync 对比](#与-framesync-对比)
8. [使用指南](#使用指南)

---

## 设计目标

### 1. 高性能
- **单线程性能优先**：批量处理、缓存友好、SIMD 加速
- **可预测的性能**：无 GC、无动态分配、固定时间复杂度

### 2. 预测回滚
- **确定性**：完全可重现的行为，支持帧同步
- **状态快照**：O(1) 快照创建，快速回滚

### 3. 可扩展性
- **模块化**：Core / Framework / Game 分层
- **渐进式**：基础功能可用，高级特性可选

---

## 架构概览

```
┌─────────────────────────────────────────────────────────────┐
│                    Game Layer（游戏层）                       │
│  - 自定义 Systems（物理、AI、渲染）                           │
│  - 自定义 Components（Position, Health...）                   │
├─────────────────────────────────────────────────────────────┤
│                  Framework Layer（框架层）                    │
│  - CommandBuffer（命令缓冲，用于预测回滚）                     │
│  - CullingSystem（裁剪系统）                                  │
│  - Query / Query 兼容层（强类型查询系统）                     │
├─────────────────────────────────────────────────────────────┤
│                    Core Layer（核心层）                       │
│  - Storage<T>（组件存储，SOA）                                │
│  - FullOwningGroup（终极性能 AOS）                            │
│  - SIMDUtils（SIMD 加速）                                     │
│  - ComponentBlockIterator（批量迭代）                         │
├─────────────────────────────────────────────────────────────┤
│                   Memory Layer（内存层）                      │
│  - Allocator（自定义分配器，64KB chunks）                      │
│  - NativeArray（非托管数组包装）                              │
└─────────────────────────────────────────────────────────────┘
```

---

## 核心设计决策

### 1. SOA vs AOS：为什么选择分离存储？

**问题背景**：ECS 中有两种主要的内存布局方式：
- **SOA (Structure of Arrays)**：每种组件类型一个数组
- **AOS (Array of Structures)**：每个实体一个结构，包含所有组件

**Lattice 的选择**：

| 场景 | 选择 | 原因 |
|------|------|------|
| 通用存储 | **SOA** (`Storage<T>`) | 预测回滚友好、Delta 压缩、SIMD 友好 |
| 性能关键 | **AOS** (`FullOwningGroup`) | 极致缓存命中率、批量更新 |

**详细对比**：

```
SOA (Storage<T>):
  Position: [P0, P1, P2, P3, ...]  ← 连续
  Velocity: [V0, V1, V2, V3, ...]  ← 连续
  访问 Position 时：缓存中只有 Position（无 Velocity 污染）
  适合：随机访问、单独序列化

AOS (FullOwningGroup):
  Group[0]: [P0, V0]  ← 同一缓存行
  Group[1]: [P1, V1]
  访问 P0 时：V0 也在缓存中（一举两得）
  适合：批量更新 P 和 V
```

**决策依据**：
1. **预测回滚**：SOA 可以单独快照 Position，而不需要复制 Velocity
2. **网络同步**：只传输变化的组件（Position 变了，Velocity 没变）
3. **缓存效率**：批量访问同类型组件时，SOA 缓存命中率更高
4. **SIMD**：连续的同类型数据便于向量化处理

### 2. ushort vs int：为什么稀疏数组用 2 字节？

**问题**：`ushort` (2 字节) vs `int` (4 字节) 的稀疏数组索引

**选择**：`ushort* _sparse`

**原因**：
1. **内存节省 50%**：65536 个实体的稀疏数组
   - ushort: 128 KB
   - int: 256 KB
2. **足够使用**：65535 最大索引支持 64K 个组件实例
   - 大多数游戏 < 10K 实体
   - 如果有 100K 实体，需要特殊处理（分页）
3. **缓存友好**：同样的缓存行可以追踪更多实体
4. **与 FrameSync 一致**：验证过的设计

**局限**：
- 不支持超过 65535 个组件实例
-  workaround: 分页或使用多个 Storage

### 3. Block 容量 128：为什么是 2 的幂？

**选择**：`DefaultBlockCapacity = 128`

**原因**：
1. **位运算优化**：
   ```csharp
   int block = index / 128;   // 慢：除法
   int block = index >> 7;    // 快：移位
   int offset = index % 128;  // 慢：取模
   int offset = index & 127;  // 快：位与
   ```
2. **缓存行对齐**：
   - 假设组件大小 16 字节
   - 128 × 16 = 2048 字节 ≈ 32 个缓存行（64 字节/行）
   - 填满 L1 缓存（32KB）的 1/16，不会污染其他数据
3. **SIMD 友好**：
   - AVX2 一次处理 4 个 Vector4FP（128 位 × 4 = 512 位）
   - 128 个组件可以分成 32 个 SIMD 批次
4. **与 FrameSync 一致**：便于对比和迁移

### 4. 版本号检测：为什么需要 _version？

**机制**：
```csharp
int versionBefore = storage.Version;
// 迭代...
if (storage.Version != versionBefore)
    throw new InvalidOperationException("Collection modified");
```

**原因**：
1. **调试友好**：快速失败，给出清晰的错误信息
2. **与 C# 一致**：`List<T>.Enumerator` 使用相同模式
3. **低开销**：DEBUG 模式检查，RELEASE 模式无开销
4. **防止崩溃**：避免迭代中删除导致的未定义行为

---

## 性能优化策略

### 优化层次

```
L1: 算法优化（大 O 复杂度）
    └─ O(1) 添加/删除/访问

L2: 数据结构优化（缓存局部性）
    └─ Block 存储、SOA/AOS 选择

L3: 微架构优化（CPU 特性）
    ├─ 预取：PrefetchL1/PrefetchL2
    ├─ 分支预测：Likely/Unlikely hints
    ├─ 无分支：位运算技巧
    └─ SIMD：AVX2/SSE2
```

### 各优化技术详解

#### 1. SIMD 向量化

**适用场景**：
- 批量向量加法（位置更新）
- 批量缩放（速度缩放）
- 批量距离计算（碰撞检测）

**实现方式**：
```csharp
// 自动选择最佳指令集
if (Avx2.IsSupported)
    // 256-bit 处理
else if (Sse2.IsSupported)
    // 128-bit 处理
else
    // 标量回退
```

**性能提升**：
- AVX2: 4x-8x（理论）
- 实际：1.5x-4x（受内存带宽限制）

#### 2. 预取优化

**原理**：在 CPU 需要数据之前，提前从内存加载到缓存

**实现**：
```csharp
// 处理 Block[i] 时，预取 Block[i+2]
SIMDUtils.PrefetchL2(nextBlockPtr);
```

**效果**：
- 缓存未命中率：从 10-20% 降低到 < 1%
- 适合：顺序遍历大数据集

#### 3. Full-Owning Group

**最佳场景**：每帧一起更新的组件组合

**性能对比**：
```
传统手写双组件遍历:
  10000 实体 × 50 周期 = 500,000 周期

FullOwningGroup<T1, T2>:
  10000 实体 × 10 周期 = 100,000 周期

提升：5x
```

---

## 预测回滚支持

### 核心机制

```
┌─────────────┐     ┌─────────────┐     ┌─────────────┐
│   Frame N   │────▶│  Command    │────▶│   Frame     │
│  (当前状态)  │     │   Buffer    │     │  N+1 (预测) │
└─────────────┘     └─────────────┘     └─────────────┘
        │                                     │
        │ Snapshot                            │
        ▼                                     ▼
┌─────────────┐                       ┌─────────────┐
│  Memory     │◀────── Rollback ─────│  Snapshot   │
│  Snapshot   │                       │  Compare    │
└─────────────┘                       └─────────────┘
```

### 快照实现

```csharp
// 只复制实际使用的数据
int size = storage.GetSnapshotSize();
storage.WriteSnapshot(buffer, size);  // O(组件数量)

// 对比 FrameSync：复制整个 Block（包括空洞）
```

### 延迟删除

**原因**：预测期间不应真正删除，避免状态不一致

**流程**：
1. `Remove(entity)` → 标记为待删除，实体仍可见
2. 预测执行（可能访问该实体）
3. 确认预测正确 → `CommitRemovals()` 真正删除
4. 或预测错误 → 回滚到之前状态，实体恢复

---

## 多线程架构

### 当前实现

```csharp
// 单线程（当前）
foreach (var entity in filter)
    system.Update(entity);

// 多线程（已实现）
parallelFilter.ForEach((entity, component) =>
    system.Update(entity, component));
```

### 线程安全保证

1. **只读多线程**：`FrameReadOnly` 包装器
2. **范围分割**：每个线程处理不重叠的索引范围
3. **原子操作**：`AtomicCounter`, `AtomicFlagArray`
4. **无锁队列**：`JobSystem` 使用 MPMC 队列

### 与 FrameSync 的对比

| 特性 | Lattice | FrameSync |
|------|---------|-----------|
| 任务调度 | 中心队列（简单） | Work Stealing（复杂） |
| 负载均衡 | 静态分片 | 动态偷取 |
| 适用场景 | 批量组件更新 | 复杂任务图 |

**建议**：当前 Lattice 的实现对于 ECS 批量更新足够，复杂任务调度可后期升级。

---

## 与 FrameSync 对比

### 架构对比表

| 维度 | Lattice | FrameSync | 差异说明 |
|------|---------|-----------|----------|
| **存储策略** | 分离（SOA） | 联合（AOS） | Lattice 更适合回滚 |
| **类型安全** | 泛型 T* | byte* | Lattice 更安全 |
| **SIMD** | ✅ 完整支持 | ❌ 无 | Lattice 更先进 |
| **单例/延迟删除** | ✅ 内置 | ✅ 内置 | 功能相同 |
| **多线程** | 基础实现 | 完整实现 | FrameSync 更成熟 |
| **序列化** | 基础 | 完整 | FrameSync 更丰富 |
| **物理集成** | 计划中 | 完整 | FrameSync 有优势 |

### 性能对比（理论）

| 场景 | Lattice | FrameSync | 胜出者 |
|------|---------|-----------|--------|
| 单组件批量更新 | 10 周期/实体 | 15 周期/实体 | Lattice |
| 多组件批量更新 | 10 周期/实体 | 50 周期/实体 | Lattice (Full-Owning) |
| 随机访问 | 5 周期/实体 | 5 周期/实体 | 平手 |
| 快照大小 | 实际使用 | 整个 Block | Lattice |

---

## 使用指南

### 选择正确的存储方式

```csharp
// 场景 1: 随机访问、单组件、频繁增删
// 选择: Storage<T>
var storage = new Storage<Health>();
storage.Initialize(maxEntities);

// 场景 2: 批量更新、多组件一起访问、固定数量
// 选择: FullOwningGroup
var group = new FullOwningGroup<Position, Velocity>();
group.Initialize(allocator);

// 场景 3: 查询满足条件的实体
// 选择: Query
var query = frame.Query<Position, Velocity>();
var enumerator = query.GetEnumerator();
while (enumerator.MoveNext())
{
    ref var pos = ref enumerator.Component1;
    ref var vel = ref enumerator.Component2;
}
```

### 性能优化检查清单

- [ ] 使用 `ComponentBlockIterator` 而不是 `foreach`
- [ ] 对热点代码使用 `FullOwningGroup`
- [ ] 启用 SIMD（检查 `Avx2.IsSupported`）
- [ ] 使用预取迭代器处理大数据集
- [ ] 添加 `BranchHints.Likely` 到热点分支

---

## 后续规划

### Phase 9: System 调度器
- 依赖管理
- 执行顺序
- 并行执行安全策略

### Phase 10: 内置数据结构
- QuadTree（四叉树空间查询）
- FPHashSet（确定性哈希集合）
- DeterministicSort（确定性排序）

### Phase 11: Godot 集成
- 物理同步（Lattice ↔ Godot Physics）
- 渲染同步（Transform → Node2D）
- 输入处理

---

## 附录

### A. 参考文档
- [FrameSync Documentation](https://doc.photonengine.com)
- [Data-Oriented Design](https://www.dataorienteddesign.com)
- [SIMD Intrinsics Guide](https://www.intel.com/content/www/us/en/docs/intrinsics-guide/index.html)

### B. 性能测试数据
见 `Tests/Performance/` 目录下的基准测试。

### C. 代码规范
- 所有公共 API 必须有 XML 文档注释
- 使用中文注释（与项目保持一致）
- DEBUG 模式启用边界检查
- RELEASE 模式启用所有优化
