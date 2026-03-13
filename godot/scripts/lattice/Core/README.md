# ECS Core Module

Lattice ECS (Entity Component System) 核心模块

## 架构设计

基于 FrameSyncEngine 和 Unity DOTS 的设计思想，实现轻量级、确定性的 ECS 框架。

## 核心组件

| 组件 | 职责 | 状态 |
|------|------|------|
| Entity | 实体 ID 管理与分配 | 🔴 待实现 |
| IComponent | 组件接口定义 | 🔴 待实现 |
| Archetype | 原型分块存储，相同组件组合的实体分组 | 🔴 待实现 |
| Frame | 单帧数据容器，存储所有组件数据 | 🔴 待实现 |
| World | 世界入口，管理多帧和系统调度 | 🔴 待实现 |
| SystemBase | 系统基类 | 🔴 待实现 |
| SystemGroup | 系统组，按顺序执行系统 | 🔴 待实现 |

## 开发计划

### Phase 1: 基础骨架 (1-2 周)
- [ ] Entity ID 管理（对象池复用）
- [ ] IComponent 接口与组件注册
- [ ] Archetype 分块存储实现

### Phase 2: 数据容器 (1 周)
- [ ] Frame 单帧数据容器
- [ ] 组件查询 API
- [ ] 实体增删操作

### Phase 3: 世界管理 (1 周)
- [ ] World 入口类
- [ ] 多帧管理（Verified / Predicted）
- [ ] 系统调度基础

### Phase 4: 系统调度 (1 周)
- [ ] SystemBase 基类
- [ ] SystemGroup 顺序执行
- [ ] 查询优化

## 确定性保证

- 所有集合使用确定性遍历顺序（FSList, FSDictionary）
- 禁止 Dictionary/HashSet 的非确定性遍历
- 固定时间步长（Fixed Timestep）

## 使用示例（目标）

```csharp
// 创建世界
var world = new World();

// 创建实体
var entity = world.CreateEntity();

// 添加组件
world.AddComponent<Position>(entity, new Position { X = FP._1, Y = FP._2 });
world.AddComponent<Velocity>(entity, new Velocity { X = FP._0_10, Y = FP._0 });

// 创建系统
var moveSystem = new MovementSystem();
world.RegisterSystem(moveSystem);

// 更新世界
world.Tick(FP.FromRaw(FP.Raw._0_16)); // 16ms = 60fps
```
