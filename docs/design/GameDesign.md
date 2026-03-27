# Sideline — 游戏设计与技术架构文档

> 版本：v0.3 | 日期：2026-03-27
>
> 说明：
>
> 本文档保留为游戏设计与路线图主文档，但其中部分早期 Lattice 命名和仓库结构示意已落后于当前实现。
>
> 当前 Lattice 主干实现请优先参考：
>
> - `godot/scripts/lattice/README.md`
> - `godot/scripts/lattice/ECS/Framework/SystemDesignNotes.md`

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

#### 环境配置

| 项目 | 版本/说明 |
|------|----------|
| **Godot 版本** | 4.6.1-stable **mono** 版（必须是 mono/.NET 版，非 Steam 版）|
| **安装路径** | `D:\GodotCSharp\Godot_v4.6.1-stable_mono_win64\` |
| **.NET SDK** | 9.0.202（兼容 Godot 4.x，要求 .NET 8+）|
| **IDE** | Windsurf（基于 VS Code），配合 C# Dev Kit + Godot Tools 插件 |
| **语言约定** | 项目内**只使用 C#**，禁止创建 `.gd` 脚本文件 |

> 注意：Steam 版 Godot 为标准版，不含 C# 支持，需从 [godotengine.org](https://godotengine.org/download) 单独下载 `.NET` 版。

### 逻辑层：Lattice（自研确定性 ECS 帧同步框架）

详见 [Lattice 框架设计](#lattice-框架设计) 章节。

---

## 仓库结构

```
Sideline/                        # 私有仓库（GitHub Private）
├── godot/                       # Godot 4 项目（渲染层，C# only）
│   ├── project.godot
│   ├── scenes/                  # 场景文件 (.tscn)
│   │   ├── main/                # 主场景、启动场景
│   │   ├── ui/                  # UI 场景
│   │   └── gameplay/            # 游戏玩法场景
│   ├── scripts/                 # C# 脚本（渲染/桥接层）
│   │   ├── bridge/              # GodotRenderBridge，同步 Lattice 状态到 Node
│   │   ├── ui/                  # UI 逻辑
│   │   └── window/              # 窗口管理（无边框/置顶/模式切换）
│   ├── assets/                  # 游戏资源
│   │   ├── sprites/             # 精灵图（手工或 AI 生成）
│   │   ├── animations/          # 动画序列帧
│   │   ├── audio/               # 音效/音乐
│   │   ├── fonts/               # 字体
│   │   └── shaders/             # 着色器
│   └── addons/                  # Godot 插件
│
├── src/                         # 纯 C# 逻辑层（无引擎依赖）
│   └── Sideline.Logic/          # 游戏逻辑项目，引用 Lattice
│
├── ai/                          # AI 辅助开发相关
│   ├── mcp/                     # Godot MCP 配置与工具脚本
│   ├── prompts/                 # 复用的 AI Prompt 模板
│   │   ├── art/                 # 图片生成提示词（角色/地图/UI）
│   │   └── code/                # 代码生成提示词模板
│   ├── generated/               # AI 生成的原始素材（待处理）
│   │   ├── sprites/             # AI 生成的精灵图原稿
│   │   └── sequences/           # AI 生成的序列帧原稿
│   └── workflows/               # AI 自动化流程定义（如序列帧生成流水线）
│
├── .windsurf/                   # Windsurf IDE 配置
│   └── workflows/               # Windsurf 工作流
│
├── docs/
│   └── design/
│       └── GameDesign.md        # 本文档
│
├── .gitignore
├── .windsurfignore
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
│   Godot 桥接层 / 宿主层（持续演进）   │
│  每帧同步 Lattice SessionRuntime 状态到 Node │
├──────────────────────────────────────┤
│       Lattice（确定性运行时）         │
│  Frame / Entity / System / Session   │
│  FP Math / Checkpoint / History      │
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
PlayerInput / SessionInputSet → SessionRuntime.Update() → History / Checkpoint
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
- [x] Godot 4 无边框窗口原型（挂机模式已实现：小窗口、无边框、置顶、拖拽、ESC 切换）
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

## AI 辅助开发规划

### 已采用
- **Windsurf IDE**：AI 代码补全、代码生成、重构辅助

### 规划中
| 工具/方案 | 用途 | 优先级 |
|---------|------|-------|
| **Godot MCP** | 让 AI 直接操作 Godot 编辑器（创建场景、节点、设置属性） | Phase 1 |
| **AI 序列帧生成** | 使用 Stable Diffusion / ComfyUI 生成 2D 角色动画序列帧 | Phase 1 |
| **Windsurf Workflows** | 将重复开发任务固化为一键执行的 AI 工作流 | 持续 |
| **AI Agent** | 自动化测试、代码审查、文档生成 | Phase 2 |

### ai/ 目录约定
- `ai/prompts/` — 存放经过验证的高质量提示词，复用而非每次重写
- `ai/generated/` — AI 生成的**原始素材**，经人工筛选后移入 `godot/assets/`
- `ai/workflows/` — ComfyUI workflow JSON、序列帧处理脚本等自动化流程
- `ai/mcp/` — Godot MCP 服务配置、自定义工具定义

---

## 技术要点备忘

| 要点 | 说明 |
|------|------|
| 无边框窗口 | Godot `DisplayServer.window_set_flag(BORDERLESS)` + `ALWAYS_ON_TOP` |
| 窗口模式切换 | 运行时动态修改窗口尺寸和边框模式，同一进程 |
| 随机地图 | BSP（二叉空间分割），seed 确定性生成 |
| 排行榜 | Steam Leaderboards API |
| 存档 | 本地 JSON / SQLite，挂机离线收益用时间差计算 |
| C# 脚本约定 | 只使用 C#，禁止 .gd 文件；Godot .NET 版必须使用 mono 构建 |

### Phase 0 运行心得
- 已生成 `Sideline.csproj`/`Sideline.sln`，在 Windsurf 中 `dotnet build` 成功后可在 Godot 中正常编译运行
- 注意编辑器设置：`Run → Window Placement → Embed Game in Editor` 需关闭，确保游戏以独立无边框窗口运行
- 按 `F5` 启动后，ESC 或面板按钮可以在挂机/刷宝模式间切换，挂机模式支持拖拽与界面交互
