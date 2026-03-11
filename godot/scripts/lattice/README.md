# Lattice - 确定性 ECS 帧同步框架

> 在 Sideline 项目中孵化的自研 ECS 框架，目标是为独立游戏提供轻量、确定性的游戏逻辑基础。
>
> **设计参考**: 本框架设计大量参考了成熟的 [FrameSyncEngine](https://github.com/ifreetalk) 帧同步框架（Unity 商业级方案），取其精华并针对 Godot 和独立游戏场景进行简化。

---

## 核心设计原则

1. **确定性优先** - 相同输入必然产生相同输出，这是帧同步和回放的基础
2. **零外部依赖** - 纯 C# 实现，不依赖 Godot 或其他引擎，便于移植和测试
3. **渐进式抽象** - 在真实项目中生长，而非凭空设计
4. **分层隔离** - 严格区分纯逻辑层、桥接层和渲染层

---

## 架构设计（参考 FrameSyncEngine）

```
┌─────────────────────────────────────────────────────────────┐
│                     Godot 渲染层                             │
│  (Sprite2D, Animation, Input, SceneTree, Camera...)          │
├─────────────────────────────────────────────────────────────┤
│                     Bridge（桥接层）                          │
│  ┌─────────────┐  ┌──────────────┐  ┌─────────────────────┐ │
│  │LatticeBridge│  │  EntityView  │  │   InputCollector    │ │
│  │ (同步入口)   │  │ (Entity→Node)│  │  (Godot输入→Command)│ │
│  └─────────────┘  └──────────────┘  └─────────────────────┘ │
├─────────────────────────────────────────────────────────────┤
│                   Simulation（纯 C# 模拟层）                  │
│  ┌─────────────┐  ┌──────────────┐  ┌─────────────────────┐ │
│  │   World     │  │    Frame     │  │       Systems       │ │
│  │  (世界入口)  │  │  (帧数据容器) │  │  - MovementSystem   │ │
│  │             │  │              │  │  - CombatSystem     │ │
│  │ Frames:     │  │ - Components │  │  - 用户自定义系统    │ │
│  │  Verified   │  │ - Entities   │  │                     │ │
│  │  Predicted  │  │ - Global data│  │                     │ │
│  │  Previous   │  │              │  │                     │ │
│  └─────────────┘  └──────────────┘  └─────────────────────┘ │
├─────────────────────────────────────────────────────────────┤
│                     Core（核心基础设施）                      │
│  ┌──────────────┐  ┌──────────────┐  ┌────────────────────┐ │
│  │    Math      │  │  Collections │  │   Serialization    │ │
│  │   - FP       │  │   - FSList   │  │   - StateSnapshot  │ │
│  │   - FPVector2│  │   - FSDict   │  │   - InputBuffer    │ │
│  │   - FPMath   │  │   - FSHashSet│  │   - Command        │ │
│  └──────────────┘  └──────────────┘  └────────────────────┘ │
└─────────────────────────────────────────────────────────────┘
```

---

## 模块划分

```
lattice/
├── Core/                   # ECS 核心：World, Entity, Archetype, Frame
│   ├── Entity.cs           # 实体 ID 管理
│   ├── Component.cs        # 组件基类/接口
│   ├── Frame.cs            # 单帧数据容器（组件存储）
│   └── World.cs            # 世界管理器，多帧支持
│
├── Systems/                # 系统调度
│   ├── SystemBase.cs       # 系统基类
│   └── SystemGroup.cs      # 系统组（按顺序执行）
│
├── Math/                   # 定点数与确定性数学
│   ├── FP.cs               # 定点数 (Q16.16)
│   ├── FPVector2.cs        # 2D 定点向量
│   ├── FPMath.cs           # 数学函数（含查找表）
│   └── FPLut.cs            # 查找表（三角函数等）
│
├── Collections/            # 确定性集合（避免非确定性遍历）
│   ├── FSList.cs           # 确定性列表
│   ├── FSDictionary.cs     # 确定性字典（有序遍历）
│   └── RingBuffer.cs       # 环形缓冲区（用于 InputBuffer）
│
├── Serialization/          # 状态快照与序列化
│   ├── StateSnapshot.cs    # 状态快照保存/恢复
│   ├── InputBuffer.cs      # 输入缓冲区（用于联机）
│   └── Command.cs          # 命令基类
│
└── Bridge/                 # Godot 桥接层（可选，与 Godot 耦合）
    ├── LatticeBridge.cs    # 主桥接入口
    ├── EntityView.cs       # 实体视图同步
    └── InputCollector.cs   # 输入收集
```

---

## 核心概念

### 1. Entity（实体）

- 纯 ID，使用 `uint32` 表示
- 无数据、无行为，只是组件的容器标识
- 从对象池分配，避免 GC

### 2. Component（组件）

- 纯数据结构（`struct`），禁止包含方法
- 所有字段必须是值类型或 `struct`
- 支持的数据类型：
  - 基础类型：`bool`, `byte`, `short`, `int`, `long`
  - 定点数：`FP`, `FPVector2`
  - 枚举：必须显式指定底层类型
  - 固定数组：`FP[10]`（长度编译期确定）
- **禁止**：引用类型、`string`、变长集合、`float`/`double`

### 3. System（系统）

- 包含游戏逻辑，按特定顺序处理组件
- 每帧对所有匹配的 Entity-Component 组合执行逻辑
- 支持多线程（后期），初期单线程顺序执行

```csharp
public class MovementSystem : SystemBase
{
    public override void Update(Frame frame)
    {
        var query = frame.Query<Position, Velocity>();
        foreach (var (entity, pos, vel) in query)
        {
            pos.Value += vel.Value * frame.DeltaTime;
        }
    }
}
```

### 4. Frame（帧）- **借鉴 FrameSyncEngine**

Frame 是 ECS 的数据容器，存储某一时刻的所有组件数据：

```csharp
public class Frame
{
    // 实体管理
    public EntityManager Entities { get; }
    
    // 组件存储（Archetype/Chunk 布局）
    public ComponentStore Components { get; }
    
    // 全局数据（如随机数种子、游戏时间）
    public GlobalData Globals { get; }
    
    // 当前帧信息
    public int Tick { get; }           // 帧号
    public FP DeltaTime { get; }       // 固定时间步长
}
```

### 5. World（世界）- **借鉴 FrameSyncEngine**

World 管理多个 Frame，支持预测-回滚：

```csharp
public class World
{
    // 多帧容器（关键设计，参考 FrameSyncEngine）
    public Frame Verified { get; }      // 服务器确认的权威帧
    public Frame Predicted { get; }     // 本地预测的最新帧
    public Frame Previous { get; }      // 上一预测帧（用于插值）
    
    // 推进一帧
    public void Tick(InputCommand input);
    
    // 状态快照
    public StateSnapshot SaveSnapshot();
    public void LoadSnapshot(StateSnapshot snapshot);
    
    // 校验和（用于验证确定性）
    public ulong CalculateChecksum();
}
```

### 6. FP（定点数）- **Q16.16 格式**

确定性计算的基础，替代 `float`/`double`：

```csharp
public struct FP
{
    private long _raw;  // Q16.16: 高48位整数，低16位小数
    
    public static FP FromFloat(float f) => new((long)(f * ONE));
    public float ToFloat() => _raw / (float)ONE;
    
    // 运算
    public static FP operator +(FP a, FP b) => new(a._raw + b._raw);
    public static FP operator *(FP a, FP b) => new((a._raw * b._raw) >> FRACTIONAL_BITS);
    
    // 常用常量
    public static readonly FP Zero = new(0);
    public static readonly FP One = new(ONE);
    public static readonly FP Pi = new(205887L);  // 预计算
}
```

**数值范围**：
- 整数部分：约 -32768 ~ 32767（安全乘法范围）
- 小数精度：约 0.000015（1/65536）

---

## 与 Godot 的边界

| Lattice（纯逻辑） | Bridge（桥接层） | Godot（渲染） |
|------------------|-----------------|--------------|
| `World.Tick()` | 收集输入、调用 Tick | `_Process()` |
| `Position (FP)` | `FP → float` 转换 | `Sprite2D.Position` |
| `FPVector2` | 转换为 `Vector2` | 视觉呈现 |
| 组件数据 | 同步到 Node 属性 | 动画、粒子效果 |
| 确定性随机 (`RNG`) | 种子管理 | 无关（Godot 随机仅用于视觉） |
| `Command` 输入 | `Input → Command` 转换 | `Input.IsActionPressed()` |

---

## 参考 FrameSyncEngine 的设计决策

### 借鉴的内容

| 特性 | FrameSyncEngine 实现 | Lattice 计划实现 |
|------|---------------------|-----------------|
| **分层架构** | Core / Simulation / Runtime / Editor | Core / Simulation / Bridge（简化）|
| **多帧设计** | Verified / Predicted / PredictedPrevious / PreviousUpdatePredicted | Verified / Predicted / Previous（简化）|
| **定点数格式** | Q16.16 (`long` 存储) | 相同，直接借鉴 |
| **查找表** | `FPLut` 预计算三角函数 | 相同，预计算 sin/cos/atan2 |
| **系统分类** | SystemBase / SystemGroup / SystemMainThread / SystemThreaded | SystemBase / SystemGroup（初期单线程）|
| **输入缓冲** | `InputBuffer` 支持预测和回滚 | 相同实现 |
| **校验和** | 每帧计算 Checksum 验证确定性 | 相同实现 |

### 简化的内容

| 特性 | FrameSyncEngine | Lattice（简化）|
|------|-----------------|---------------|
| **内存管理** | 自定义 `FrameHeap` / `Heap` Allocator | .NET 默认 + `ArrayPool`（后期再优化）|
| **DSL 代码生成** | `.qtn` 文件编译为 C# | 手写 C#（后期考虑 Source Generator）|
| **物理系统** | 内置确定性 2D/3D 物理 | 简单 AABB 碰撞起步 |
| **导航寻路** | 内置 NavMesh 系统 | 无（或后期添加简单寻路）|
| **多线程** | 支持 SystemThreaded | 初期单线程 |
| **实体原型** | 完整的 EntityPrototype 系统 | 运行时创建 Entity（简化）|

---

## 确定性保障清单

为确保帧同步的确定性，必须遵守以下规则：

- [x] **数学运算**: 全部使用 `FP` 替代 `float`/`double`
- [x] **随机数**: 使用种子化的 `RNG`，每帧确定性地生成
- [x] **集合遍历**: 使用 `FSDictionary`/`FSList` 确保顺序一致
- [x] **逻辑更新**: 使用固定时间步长（Fixed Timestep）
- [x] **组件数据**: 全部使用值类型（`struct`），禁止引用类型
- [x] **系统执行**: 按固定顺序执行，不依赖 `Dictionary` 遍历顺序
- [ ] **浮点文本**: 不使用浮点数字面量（用 `FP.FromFloat(1.5f)`）

---

## 开发路线图

### Phase 0 - 骨架验证（当前）
- [x] 项目结构搭建
- [x] 设计文档编写
- [ ] 最小 ECS 跑通：Entity + Component + System
- [ ] 确定性验证：相同输入跑两次，状态哈希一致

### Phase 1 - 定点数与数学
- [ ] `FP` 定点数实现（Q16.16）
- [ ] 三角函数查找表（`FPLut.Sin`, `FPLut.Cos`）
- [ ] `FPVector2` 2D 向量运算
- [ ] 与 Godot `Vector2` 的双向转换

### Phase 2 - 核心 ECS
- [ ] `Frame` 单帧数据容器
- [ ] `World` 多帧管理（Verified / Predicted）
- [ ] `SystemGroup` 系统调度
- [ ] 确定性集合 `FSList` / `FSDictionary`

### Phase 3 - 游戏集成
- [ ] Sideline 角色移动（纯 Lattice 驱动）
- [ ] Input 收集 → `Command` → 执行
- [ ] `LatticeBridge` Godot 桥接层
- [ ] `EntityView` 实体-节点同步

### Phase 4 - 联机准备
- [ ] `StateSnapshot` 状态快照
- [ ] `InputBuffer` 输入缓冲区
- [ ] 每帧 `Checksum` 计算
- [ ] Lockstep 网络适配层

---

## 命名约定

- 接口：`I` 前缀，如 `IComponent`, `ISystem`
- 结构体：PascalCase，如 `Position`, `Velocity`
- 方法：PascalCase（遵循 C# 规范）
- 私有字段：`_` 前缀 + camelCase，如 `_raw`, `_entities`
- 定点数类型：前缀 `FP`，如 `FP`, `FPVector2`, `FPMath`
- 确定性集合：前缀 `FS`，如 `FSList`, `FSDictionary`（FrameSync）

---

## 参考资料

### 框架参考
- [FrameSyncEngine](https://github.com/ifreetalk) - Unity 商业级帧同步框架（主要参考）
  - 架构设计：分层隔离、多帧管理
  - 定点数实现：Q16.16 格式、查找表
  - 系统调度：SystemGroup、执行顺序

### 理论文章
- [Overwatch Gameplay Architecture and Netcode](https://www.youtube.com/watch?v=W3aieHjyNvw)
- [Unity DOTS 设计理念](https://unity.com/dots)
- [Deterministic Lockstep 模式](https://www.gabrielgambetta.com/client-server-game-architecture.html)
- [1500 Archers on a 28.8: Network Programming in Age of Empires and Beyond](https://www.gamedeveloper.com/programming/1500-archers-on-a-28-8-network-programming-in-age-of-empires-and-beyond)

---

**状态**: 🚧 开发中（Sideline Phase 0）  
**最后更新**: 2026-03-11
