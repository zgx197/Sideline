# Lattice - 确定性 ECS 帧同步框架

> 在 Sideline 项目中孵化的自研 ECS 框架，目标是为独立游戏提供轻量、确定性的游戏逻辑基础。

---

## 核心设计原则

1. **确定性优先** - 相同输入必然产生相同输出，这是帧同步和回放的基础
2. **零外部依赖** - 纯 C# 实现，不依赖 Godot 或其他引擎，便于移植和测试
3. **渐进式抽象** - 在真实项目中生长，而非凭空设计

---

## 模块划分

```
lattice/
├── Core/           # ECS 核心：World, Entity, Archetype
├── Components/     # 组件定义接口与基类
├── Systems/        # 系统调度与接口
├── Math/           # 定点数 (FP) 与确定性数学运算
└── Serialization/  # 状态快照与输入序列化
```

---

## 核心概念

### 1. Entity（实体）

- 纯 ID，使用 `uint32` 表示
- 无数据、无行为，只是组件的容器标识

### 2. Component（组件）

- 纯数据结构（`struct`），禁止包含方法
- 所有字段必须是值类型或 `struct`
- 示例：`Position`, `Velocity`, `Health`

### 3. System（系统）

- 包含游戏逻辑，按特定顺序处理组件
- 每帧对所有匹配的 Entity-Component 组合执行逻辑
- 示例：`MovementSystem`, `CombatSystem`

### 4. World（世界）

- ECS 的入口，管理所有 Entity、Component、System
- 提供 `Tick()` 方法推进一帧
- 支持状态快照保存/恢复（用于回滚）

### 5. FP（定点数）

- 确定性计算的基础，替代 `float`/`double`
- 使用 Q16.16 格式（16位整数 + 16位小数）
- 支持基本运算：+ - * /，以及三角函数查找表

---

## 与 Godot 的边界

| Lattice（纯逻辑） | Bridge（桥接层） | Godot（渲染） |
|------------------|-----------------|--------------|
| World.Tick() | 收集输入、调用 Tick | _Process() |
| Position (FP) | FP → float 转换 | Sprite2D.Position |
| Component 数据 | 同步到 Node 属性 | 视觉呈现 |
| 确定性随机 | 种子管理 | 无关 |

---

## 开发路线图

### Phase 0 - 骨架验证
- [ ] 最小 ECS 跑通：创建 Entity，添加 Component，System 遍历
- [ ] 确定性验证：相同输入跑两次，状态哈希一致

### Phase 1 - 定点数
- [ ] FP 基础运算
- [ ] 三角函数查找表（sin/cos）
- [ ] 与 Unity/Godot 坐标转换

### Phase 2 - 游戏集成
- [ ] Sideline 角色移动（纯 Lattice 驱动）
- [ ] 输入收集 → Command → 执行
- [ ] 状态快照保存/加载

### Phase 3 - 联机准备
- [ ] InputBuffer 实现
- [ ] Lockstep 适配层（网络层）

---

## 命名约定

- 接口：`I` 前缀，如 `IComponent`, `ISystem`
- 结构体：PascalCase，如 `Position`, `Velocity`
- 方法：PascalCase（遵循 C# 规范）
- 私有字段：`_` 前缀 + camelCase

---

## 注意事项

1. **禁止在 Lattice 中使用**：
   - `float` / `double`（用 `FP` 替代）
   - `System.Random`（用确定性随机）
   - Godot 或 Unity 的任何 API
   - 非确定性集合（如 `Dictionary` 的遍历顺序）

2. **测试策略**：
   - 优先写确定性测试（相同输入 → 相同输出）
   - 状态快照测试（Save → 修改 → Load → 验证）

---

## 参考资料

- [Overwatch Gameplay Architecture and Netcode](https://www.youtube.com/watch?v=W3aieHjyNvw)
- [Unity DOTS 设计理念](https://unity.com/dots)
- [Deterministic Lockstep 模式](https://www.gabrielgambetta.com/client-server-game-architecture.html)

---

**状态**: 🚧 开发中（Sideline Phase 0）
