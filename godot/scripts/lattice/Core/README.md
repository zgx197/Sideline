# Lattice ECS Core 核心模块

> 确定性 ECS (Entity Component System) 框架
> 
> 设计参考：FrameSyncEngine、Unity DOTS、Bevy ECS

---

## 🎯 设计理念

### 渐进式迭代开发

不追求一次性完美，采用**循环迭代**方式，每个阶段都有可用成果：

```
迭代 1: 基础骨架 → 能跑简单 Demo
迭代 2: 完善查询 → 支持复杂逻辑
迭代 3: 优化性能 → Archetype + SIMD
迭代 4: 网络同步 → 预测回滚
```

### 核心原则

1. **简单优先**：先实现能用，再优化性能
2. **独立测试**：每个模块可独立验证
3. **向后兼容**：迭代不破坏已有 API
4. **确定性**：相同输入必然相同输出

---

## 🏗️ 模块架构

### 五大独立模块

```
┌─────────────────────────────────────────────────────────┐
│  应用层 (Application)                                    │
│  ├── Game Logic                                         │
│  ├── Mod System                                         │
│  └── Replay System                                      │
├─────────────────────────────────────────────────────────┤
│  调度层 (Scheduling)      ← 迭代 2                      │
│  ├── System Scheduler                                   │
│  ├── Update / FixedUpdate / Render                      │
│  └── Event Bus                                          │
├─────────────────────────────────────────────────────────┤
│  查询层 (Query)           ← 迭代 2                      │
│  ├── Query<T>                                           │
│  ├── Query<T1, T2>                                      │
│  └── Query Builder                                      │
├─────────────────────────────────────────────────────────┤
│  数据层 (Data)            ← 迭代 1（核心）               │
│  ├── World                                              │
│  │   └── Frame (Snapshot)                               │
│  │       ├── Entity Manager                             │
│  │       └── Component Storage                          │
│  │           └── Dense Array [Entity + Components]      │
│  └── Archetype (Optional)                               │
├─────────────────────────────────────────────────────────┤
│  接口层 (Interface)       ← 迭代 1（基础）               │
│  ├── Entity (ID + Version)                              │
│  ├── IComponent (Marker)                                │
│  └── ISystem (Behavior)                                 │
└─────────────────────────────────────────────────────────┘
```

---

## 🚀 迭代路线图

### 迭代 1：基础骨架（2-3 周）

**目标**：能创建实体、添加组件、遍历更新

```csharp
// 期望用法
var world = new World();
var entity = world.CreateEntity();
world.Add<Position>(entity, new Position { X = FP._1, Y = FP._2 });

// 简单遍历
foreach (var (e, pos) in world.Query<Position>())
{
    Console.WriteLine($"Entity {e} at {pos.X}");
}
```

#### 1.1 Entity Module（✅ 已完成）

**职责**：轻量级 ID 标识 + 生命周期管理

**核心实现**：
```csharp
// Entity.cs - 8字节轻量级标识符
[StructLayout(LayoutKind.Explicit)]
public readonly struct Entity : IEquatable<Entity>
{
    [FieldOffset(0)] public readonly int Index;    // 数组索引
    [FieldOffset(4)] public readonly int Version;  // 版本号（防止ABA问题）
    [FieldOffset(0)] public readonly ulong Raw;    // 快速比较
    
    public bool IsValid => Raw != 0;
    public static readonly Entity None = default;
}

// EntityRegistry.cs - ID分配与回收
internal sealed class EntityRegistry
{
    public Entity Create();              // 分配新ID（优先复用空闲槽位）
    public bool Destroy(Entity entity);  // 销毁并回收ID
    public bool IsValid(Entity entity);  // 验证引用有效性
    
    // 关键特性：
    // - LIFO空闲列表（缓存友好）
    // - 版本号递增（防止悬空引用）
    // - 活跃标志位（复用Version最高位）
}

// EntityMeta.cs - 实体元数据（内部使用）
internal struct EntityMeta
{
    public Entity Ref;          // 实体引用
    public int ArchetypeId;     // 所在Archetype（预留）
    public int ArchetypeRow;    // 在Archetype中的行（预留）
    
    public const int ActiveBit = int.MinValue;   // 0x80000000
    public const int VersionMask = int.MaxValue; // 0x7FFFFFFF
}
```

**设计要点**：
- **Generational Index**：Index + Version 组合，复用槽位时版本递增，防止 ABA 问题
- **空闲列表**：`Stack<int>` LIFO 结构，缓存友好
- **版本管理**：31位版本号（约21亿次复用），最高位作活跃标志
- **快速验证**：`Raw` 字段 64位比较，O(1) 有效性检查

**测试覆盖**：`Lattice.Tests/Core/EntityTests.cs`
- Entity 创建/销毁/验证
- ID 回收与版本递增
- 悬空引用检测

**测试要点**：
- ID 唯一性
- 版本递增
- Null 实体处理

#### 1.2 Component Storage（5 天）⭐

**职责**：单类型组件的增删查改

**核心设计**：
```csharp
public class ComponentStorage<T> where T : struct
{
    private T[] _components;           // 组件数据
    private Entity[] _entities;        // 对应实体
    private Dictionary<Entity, int> _entityToIndex; // 查找表
    
    public void Add(Entity entity, in T component);
    public void Remove(Entity entity);
    public ref T Get(Entity entity);   // ref 允许修改
    public bool Has(Entity entity);
    
    // 遍历支持
    public Span<T> GetSpan();
    public Span<Entity> GetEntities();
}
```

**测试要点**：
- 增删查改正确性
- 引用稳定性（ref 修改后持久化）
- 遍历顺序一致性

#### 1.3 Frame（3 天）

**职责**：单帧完整状态

**核心设计**：
```csharp
public class Frame
{
    // 每种组件类型一个存储
    private readonly Dictionary<Type, object> _storages = new();
    private readonly EntityManager _entityManager;
    
    // 实体操作
    public Entity CreateEntity();
    public void DestroyEntity(Entity entity);
    
    // 组件操作
    public void Add<T>(Entity entity, in T component) where T : struct;
    public void Remove<T>(Entity entity) where T : struct;
    public ref T Get<T>(Entity entity) where T : struct;
    public bool Has<T>(Entity entity) where T : struct;
}
```

**测试要点**：
- 实体生命周期
- 组件增删查改
- 多类型组件共存

#### 1.4 World + 简单系统（3 天）

**职责**：入口 + 简单遍历

**核心设计**：
```csharp
public class World
{
    public Frame CurrentFrame { get; }
    
    // 简单系统委托（迭代 1 先用 Action，迭代 2 再抽象接口）
    private readonly List<Action<World>> _systems = new();
    
    public void AddSystem(Action<World> system);
    public void Tick(FP deltaTime);
    
    // 简单查询（迭代 1 只支持单类型）
    public IEnumerable<(Entity, T)> Query<T>() where T : struct;
}
```

**测试要点**：
- 系统执行顺序
- 数据流正确性
- Tick 循环稳定性

#### 迭代 1 验收标准

```csharp
// 能运行这个 Demo 即通过
var world = new World();

// 创建 1000 个实体
for (int i = 0; i < 1000; i++)
{
    var e = world.CreateEntity();
    world.Add<Position>(e, new Position { X = FP.FromInt(i), Y = FP.Zero });
    if (i % 2 == 0)
        world.Add<Velocity>(e, new Velocity { X = FP._0_10, Y = FP.Zero });
}

// 系统更新
world.AddSystem(world =>
{
    foreach (var (e, pos) in world.Query<Position>())
    {
        if (world.TryGet<Velocity>(e, out var vel))
        {
            pos.X += vel.X;
        }
    }
});

// 运行 60 帧
for (int i = 0; i < 60; i++)
    world.Tick(FP.FromRaw(FP.Raw._0_16)); // 16ms
```

---

### 迭代 2：完善查询与系统（2 周）

**目标**：支持多组件查询、系统接口抽象、事件总线

#### 2.1 Query Module（5 天）⭐

**职责**：高效多组件查询

**核心设计**：
```csharp
// 多组件查询
public ref struct Query<T1, T2> where T1 : struct where T2 : struct
{
    public bool MoveNext();
    public (Entity, ref T1, ref T2) Current { get; }
}

// 查询条件（可选）
public ref struct Query<T> where T : struct
{
    public Query<T> With<TOther>() where TOther : struct;
    public Query<T> Without<TOther>() where TOther : struct;
}
```

**测试要点**：
- 多组件联合查询正确性
- 性能基准（vs 迭代 1 的简单遍历）
- 内存分配零 GC

#### 2.2 System Interface（3 天）

**职责**：抽象系统接口

**核心设计**：
```csharp
public interface ISystem
{
    void OnInit(World world);
    void OnUpdate(World world);
    void OnDestroy(World world);
}

// 系统分组
public enum SystemGroup { Update, FixedUpdate, Render }

[SystemGroup(SystemGroup.Update)]
public class MovementSystem : ISystem
{
    public void OnUpdate(World world)
    {
        foreach (var (e, pos, vel) in world.Query<Position, Velocity>())
        {
            pos.X += vel.X * world.DeltaTime;
        }
    }
}
```

#### 2.3 Scheduler（4 天）

**职责**：系统调度与分组

**核心设计**：
```csharp
public class Scheduler
{
    // 分组执行
    public void Update(World world);       // 每帧
    public void FixedUpdate(World world);  // 固定时间步
    public void Render(World world);       // 渲染
    
    // 顺序控制
    public void SetExecutionOrder<TBefore, TAfter>();
}
```

#### 迭代 2 验收标准

- 支持 `Query<Position, Velocity>` 联合查询
- 系统生命周期管理（Init → Update → Destroy）
- 系统分组执行（Update / FixedUpdate）

---

### 迭代 3：性能优化（2 周）

**目标**：Archetype 分块、SIMD 加速、缓存优化

#### 3.1 Archetype Module（5 天）⭐

**职责**：组件组合类型管理

**核心设计**：
```csharp
public class Archetype
{
    public ComponentType[] ComponentTypes { get; }
    public int EntityCount { get; }
    
    // 分块存储
    internal Chunk[] Chunks;
    
    // 实体迁移
    public void MoveEntity(Entity entity, Archetype newArchetype);
}

public class Chunk
{
    public const int Capacity = 128; // 每块容纳实体数
    public byte[] Memory;            // 原始内存
    public int Count;                // 当前实体数
}
```

**测试要点**：
- 实体迁移正确性
- 内存连续性
- 遍历性能提升（vs 迭代 1）

#### 3.2 SIMD Optimization（3 天）

**职责**：批量组件 SIMD 处理

**核心设计**：
```csharp
public static class BatchOperations
{
    public static void AddBatch(Span<FP> a, Span<FP> b, Span<FP> result);
    public static void MultiplyBatch(Span<FP> a, Span<FP> b, Span<FP> result);
}
```

#### 3.3 Query Cache（2 天）

**职责**：查询结果缓存

**核心设计**：
```csharp
public class QueryCache
{
    // 缓存符合条件的 Archetype 列表
    private readonly Dictionary<QueryDesc, Archetype[]> _cache = new();
}
```

#### 迭代 3 验收标准

- 1000 实体遍历性能比迭代 1 提升 3x+
- 内存分配零 GC
- 缓存未命中率 < 10%

---

### 迭代 4：网络同步（2-3 周）

**目标**：预测回滚、状态快照、确定性验证

#### 4.1 Snapshot Module（3 天）

**职责**：帧状态序列化

**核心设计**：
```csharp
public class FrameSnapshot
{
    public byte[] Serialize(Frame frame);
    public Frame Deserialize(byte[] data);
    public long CalculateChecksum(Frame frame);
}
```

#### 4.2 Multi-Frame World（4 天）⭐

**职责**：多帧管理（Verified / Predicted）

**核心设计**：
```csharp
public class World
{
    public Frame VerifiedFrame { get; }   // 服务器确认帧
    public Frame PredictedFrame { get; }  // 本地预测帧
    public Frame PreviousFrame { get; }   // 上一帧（插值用）
    
    public void Rollback(int toFrameNumber, FrameSnapshot serverFrame);
    public void Resimulate(int fromFrameNumber);
}
```

#### 4.3 Determinism Validation（2 天）

**职责**：确定性验证

**核心设计**：
```csharp
public class DeterminismValidator
{
    public void RecordInput(int frame, InputCommand input);
    public void ValidateChecksum(int frame, long expectedChecksum);
}
```

#### 迭代 4 验收标准

- 支持回滚到任意历史帧
- 回滚后重新模拟结果一致
- 跨平台校验和一致

---

## 📁 目录结构

```
lattice/Core/                       # ECS 核心模块
├── README.md                       # 本文档
│
├── Entity.cs                       # ✅ Entity ID 实现
├── EntityMeta.cs                   # ✅ 实体元数据
├── EntityRegistry.cs               # ✅ 实体注册表
│
├── ComponentStorage.cs             # 🚧 组件存储（迭代 1.2）
├── Frame.cs                        # 🚧 单帧数据（迭代 1.3）
└── World.cs                        # 🚧 世界管理器（迭代 1.4）

lattice/Query/                      # 查询模块（迭代 2）
├── Query.cs
├── Query{T}.cs
└── Query{T1,T2}.cs

lattice/System/                     # 系统模块（迭代 2）
├── ISystem.cs
└── Scheduler.cs

lattice/Archetype/                  # Archetype 优化（迭代 3）
├── Archetype.cs
└── Chunk.cs

lattice/Snapshot/                   # 快照同步（迭代 4）
└── FrameSnapshot.cs
```

---

## 🧪 测试策略

### 单元测试（每个模块）

```
Lattice.Tests/Core/
├── Iteration1/
│   ├── EntityTests.cs
│   ├── ComponentStorageTests.cs
│   ├── FrameTests.cs
│   └── WorldTests.cs
└── ...
```

### 集成测试（每个迭代）

```csharp
// 迭代 1 集成测试：简单 Demo
[Fact]
public void Iteration1_Demo_ShouldWork()
{
    // 创建世界 → 添加实体 → 运行系统 → 验证结果
}

// 迭代 4 集成测试：回滚
[Fact]
public void Iteration4_Rollback_ShouldBeDeterministic()
{
    // 运行 100 帧 → 回滚到 50 帧 → 重新模拟 → 校验和一致
}
```

### 性能基准

```csharp
[MemoryDiagnoser]
public class QueryBenchmark
{
    [Benchmark(Baseline = true)]
    public void Iteration1_SimpleQuery() { /* ... */ }
    
    [Benchmark]
    public void Iteration2_MultiComponentQuery() { /* ... */ }
    
    [Benchmark]
    public void Iteration3_ArchetypeQuery() { /* ... */ }
}
```

---

## 🤔 待决策问题

### 迭代 1 必须决策

1. **~~Entity 版本号~~**：✅ 已确定 - 使用 Generational Index（Index + Version），参考 FrameSync/Bevy 设计
2. **组件存储扩容**：固定容量 vs 动态扩容？（建议：固定，避免引用失效）
3. **查询返回值**：`IEnumerable` vs `Span` vs 回调？（建议：迭代器模式，零分配）

### 迭代 2 必须决策

1. **多组件查询算法**：遍历小集合 + HashSet 检查 vs 位图索引？
2. **系统执行顺序**：显式指定 vs 依赖注入自动排序？

### 迭代 3 可选优化

1. **Archetype 是否必须**：迭代 1-2 不用，迭代 3 引入作为优化
2. **Source Generator**：迭代 3 引入，自动生成组件元数据

---

## 📊 当前状态

### 已实现 ✅

| 模块 | 文件 | 状态 | 测试 |
|------|------|------|------|
| Entity | `Entity.cs` | ✅ 完成 | `EntityTests.cs` |
| EntityMeta | `EntityMeta.cs` | ✅ 完成 | `EntityMetaTests.cs` |
| EntityRegistry | `EntityRegistry.cs` | ✅ 完成 | `EntityRegistryTests.cs` |

### 进行中 🚧

| 模块 | 依赖 | 预计时间 |
|------|------|----------|
| Component Storage | Entity | 5天 |
| Frame | Entity, Storage | 3天 |
| World | Frame, Storage | 3天 |

---

## 🎯 下一步行动

1. **实现 Component Storage**（迭代 1.2）
   - 单类型组件的增删查改
   - ref 返回支持原地修改
   - 遍历支持（Span/Enumerator）

2. **设计决策**：
   - 存储扩容策略（固定 vs 动态）
   - 查询返回方式（IEnumerable vs ref struct Enumerator）

准备好继续 **迭代 1.2：Component Storage** 的设计讨论吗？