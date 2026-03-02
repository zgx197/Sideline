# Sideline — 游戏设计与技术架构文档

> 版本：v0.1 | 日期：2026-03-03

---

## 游戏概念

**Sideline** 是一款 2D 俯视角独立游戏，目标平台为 **Steam**。

核心创意来自于"工作间隙偷偷经营一个地下世界"的双重生活体验。玩家可以在工作时将游戏以无边框小窗口挂在屏幕角落进行养成，在空闲时切换到大窗口模式进入地下城战斗。

### 玩法循环

```
养成（挂机模式）→ 刷宝（战斗模式）→ 网络排行榜
```

### 双模式设计

| 模式 | 窗口形态 | 核心玩法 |
|------|---------|---------|
| **挂机模式** | 无边框小窗口，可拖拽至屏幕任意位置，始终置顶 | 资源自动收集，养成要素 |
| **刷宝模式** | 正常大窗口（全屏或窗口化） | 暗黑风格地下城，随机地图，装备刷取 |

> 挂机模式参考《Rusty's Retirement》的窗口体验，不显示系统窗口标题栏。

### 后续扩展方向（Roadmap）

- **基础版**：单机，挂机养成 + 地下城刷宝 + Steam 排行榜
- **扩展包 1**：自动战斗、战斗回放（帧同步天然支持）
- **DLC：联机模式**：基于 Lattice 框架的 Lockstep 联机

---

## 技术选型

### 渲染层：Godot 4 + C#

选择理由：
- 完全开源（MIT），独立游戏无授权费顾虑
- `DisplayServer` API 对无边框窗口、始终置顶支持完整
- C# (.NET 6+) 与逻辑层同语言，集成无缝
- 2D 功能成熟，适合俯视角独立游戏

渲染器：**兼容（Compatibility）**（2D 游戏首选，性能更好，兼容性更广）

### 逻辑层：Lattice（自研确定性 ECS 帧同步框架）

详见 [Lattice 框架设计](#lattice-框架设计) 章节。

---

## 仓库结构

```
Sideline/                   # 私有仓库（GitHub Private）
├── godot/                  # Godot 4 项目（渲染层）
│   ├── project.godot
│   ├── scenes/
│   ├── scripts/
│   └── assets/
├── src/                    # 纯 C# 逻辑层（Lattice 接入点）
├── docs/
│   └── design/
│       └── GameDesign.md   # 本文档
├── .gitignore
└── README.md
```

---

## Lattice 框架设计

**Lattice** 是为 Sideline 自研的确定性 ECS 帧同步框架，以独立 C# 项目形式维护，开源发布（AGPL-3.0）。

仓库：`https://github.com/[username]/lattice`（Public，AGPL-3.0）

### 核心设计目标

- **确定性**：相同输入在任意平台产生相同输出（联机/回放基础）
- **纯 C#**：无引擎依赖，可接入 Godot / Unity 等任意渲染层
- **轻量**：适合独立游戏规模，不过度设计

### 分层架构

```
┌──────────────────────────────────────┐
│          渲染层（Godot 4）            │
│  Node2D 纯渲染，无逻辑               │
├──────────────────────────────────────┤
│       GodotRenderBridge              │
│  每帧同步 SimulationWorld 状态到 Node │
├──────────────────────────────────────┤
│       SimulationWorld（Lattice）      │
│  Entity / Component / System         │
│  FixedPoint Math / 确定性物理         │
│  StateSnapshot / InputBuffer         │
├──────────────────────────────────────┤
│       网络层（后期 DLC）              │
│  Lockstep / Steam Relay              │
└──────────────────────────────────────┘
```

### 关键设计决策

#### 1. 数据层
- `Entity`：纯 ID（`uint32`）
- `Component`：纯数据结构体（`struct`，无方法）
- `Archetype`：同类型组件组合的连续内存块（SOA 布局）

#### 2. 确定性保障
- 禁止 `float` / `double`，全部使用定点数（`FP`，Q16.16 或 Q32.32）
- 确定性随机：线性同余 / Xorshift，seed 随帧传入
- 物理：AABB + 圆形碰撞，不依赖引擎物理

#### 3. 帧调度
```
InputBuffer → Simulation.Tick() → StateSnapshot
```
- 初期单机：本地 Input 直接 Tick
- 后期联机：Lockstep 直接套上去，核心不动

### License 说明

Lattice 使用 **AGPL-3.0**：
- 使用者修改后分发或用于网络服务，必须开源修改内容
- 商业使用需向作者购买商业授权（双授权模式）

---

## 开发路线图

### Phase 0 — 技术验证（2~4 周）
- [ ] Godot 4 无边框窗口原型（挂机模式可行性验证）
- [ ] 最小 ECS 框架：Entity / Component / System 跑通
- [ ] FP 定点数库验证

### Phase 1 — 核心玩法（2~3 个月）
- [ ] 随机地图生成（BSP 分割房间）
- [ ] 战斗系统（移动 / 攻击 / 技能）
- [ ] 基础装备系统 + 存档
- [ ] 挂机窗口 + 资源收集

### Phase 2 — EA 发布准备
- [ ] Steam SDK 接入（成就 / 排行榜）
- [ ] 战斗回放
- [ ] 内容量完善

### Phase 3 — 联机 DLC
- [ ] Lockstep 联机（核心框架不动，新增 NetworkLayer）
- [ ] Steam Relay 集成

---

## 技术要点备忘

| 要点 | 说明 |
|------|------|
| 无边框窗口 | Godot `DisplayServer.window_set_flag(BORDERLESS)` + `ALWAYS_ON_TOP` |
| 窗口模式切换 | 运行时动态修改窗口尺寸和边框模式，同一进程 |
| 随机地图 | BSP（二叉空间分割），seed 确定性生成 |
| 排行榜 | Steam Leaderboards API |
| 存档 | 本地 JSON / SQLite，挂机离线收益用时间差计算 |
