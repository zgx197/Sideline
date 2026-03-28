# Sideline

<p align="center">
  <strong>一款把“摸鱼挂机”与“地下城刷宝”拼在同一个客户端里的 2D 俯视角独立游戏</strong>
</p>

<p align="center">
  工作时挂在屏幕角落，空档时切进地下城。<br />
  双模式窗口体验 + Godot 4 C# 客户端 + 自研确定性运行时底座。
</p>

<p align="center">
  <a href="https://img.shields.io/badge/Godot-4.6.1--mono-478CBF?logo=godotengine&logoColor=white"><img alt="Godot 4.6.1 Mono" src="https://img.shields.io/badge/Godot-4.6.1--mono-478CBF?logo=godotengine&logoColor=white" /></a>
  <a href="https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white"><img alt=".NET 8" src="https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white" /></a>
  <a href="https://img.shields.io/badge/Renderer-GL%20Compatibility-2E8B57"><img alt="GL Compatibility" src="https://img.shields.io/badge/Renderer-GL%20Compatibility-2E8B57" /></a>
  <a href="https://img.shields.io/badge/Phase-0%20Tech%20Validation-F39C12"><img alt="Phase 0" src="https://img.shields.io/badge/Phase-0%20Tech%20Validation-F39C12" /></a>
  <a href="https://img.shields.io/badge/License-GPLv3-blue.svg"><img alt="GPLv3" src="https://img.shields.io/badge/License-GPLv3-blue.svg" /></a>
</p>

| 维度 | 说明 |
| --- | --- |
| 核心定位 | 用一个真正可玩的游戏项目，验证“双模式窗口 + 确定性运行时 + 客户端页面框架”的组合 |
| 玩法体验 | 挂机模式负责资源积累，地下城模式负责战斗与刷宝，两种体验在同一个进程内切换 |
| 技术策略 | Godot 4.6.1 Mono + C# 作为表现层，自研 `Lattice` 负责确定性运行时，自研 `Facet` 负责客户端页面运行时 |
| 当前阶段 | `Phase 0` 技术验证为主，窗口切换、Facet 页面运行时与部分调试链路已接入，核心 ECS 仍在持续收口 |

> 如果你第一次进入这个仓库，建议先看“快速导航”和“阅读建议”，再决定是从游戏本体、Facet，还是 Lattice 开始读。

<a id="quick-nav"></a>

## 快速导航

- [这是什么](#这是什么)
- [项目愿景](#项目愿景)
- [当前状态](#当前状态)
- [功能亮点](#功能亮点)
- [技术架构](#技术架构)
- [运行环境](#运行环境)
- [快速开始](#快速开始)
- [阅读建议](#阅读建议)
- [仓库结构](#仓库结构)
- [Roadmap](#roadmap)
- [相关文档](#相关文档)

<a id="what-is-this"></a>

## 这是什么

`Sideline` 不是单纯的 Godot 游戏壳，也不是单纯的底层框架实验仓库。它把三件事情放在同一个代码库里持续演进：

1. 一个强调“双重生活感”的 2D 俯视角独立游戏原型。
2. 一个面向未来回放与联机能力的确定性运行时底座 `Lattice`。
3. 一个把页面、绑定、Lua 控制器、诊断面板组织起来的客户端运行时 `Facet`。

项目想解决的不是“如何做一个普通游戏窗口”，而是下面这条体验链路：

```text
挂机小窗 -> 随时观察资源变化 -> 一键切到地下城大窗 -> 继续战斗 / 调试 / 验证运行时
```

当前仓库里的主体验重点如下：

- `挂机模式`：400 x 300 无边框小窗口，始终置顶，可拖拽到屏幕角落。
- `地下城模式`：1280 x 720 常规窗口，可作为战斗与调试视图。
- `Facet`：负责页面定义、绑定、Lua 控制器、运行时日志、红点和诊断快照。
- `Lattice`：负责确定性数据与会话运行时，为后续回放、长时模拟、Lockstep 打基础。

## 项目愿景

`Sideline` 想做的不是“一个窗口比较特别的小游戏”，而是把体验设计和工程设计绑在一起推进：

- 在体验层面，玩家应该明显感受到“上班摸鱼经营地下世界”的切换感。
- 在系统层面，挂机积累与地下城战斗不应该是两个彼此割裂的程序，而应共享同一套状态与运行时。
- 在工程层面，项目不只追求快速堆功能，而是希望留下可回放、可联机、可诊断、可持续迭代的底座。

换句话说，仓库的目标是同时回答三个问题：

| 问题 | 当前答案 |
| --- | --- |
| 游戏想提供什么体验 | 一个可以挂在角落经营、也可以放大进入战斗的双模式客户端 |
| 客户端想怎么组织自己 | 用 `Facet` 把页面、Binding、Lua 控制器、诊断工具收进统一运行时 |
| 底层状态想如何长期演进 | 用 `Lattice` 走确定性运行时路线，为回放、长时模拟和未来联机预留空间 |

## 当前状态

项目现在更接近“可验证的高保真原型”，而不是“已有完整玩法闭环的 EA 版本”。如果按今天仓库里的真实落点来看，已经跑通的重点包括：

| 模块 | 当前已具备 |
| --- | --- |
| 主客户端 | `Main` 已负责窗口模式切换、页面打开、Projection 发布与基本协调 |
| 窗口体验 | `WindowManager` 已实现挂机小窗 / 地下城大窗切换、置顶、拖拽与居中 |
| 挂机 / 地下城面板 | `IdlePanel` 与 `DungeonPanel` 已接入页面生命周期、Projection 消费和运行时验证内容 |
| Facet | 页面运行时、Binding、Lua 宿主、日志、红点、布局实验与诊断快照已形成主链路 |
| Lattice | 定点数、会话运行时、系统调度、历史帧、测试与 benchmark 治理持续收口中 |

如果你想快速判断“现在 clone 下来能看到什么”，可以把仓库理解成下面这个状态：

```text
可切换窗口的 Godot 客户端
    + 已接入的 Facet 页面运行时
    + 正在收口的 Lattice 确定性底座
    = 一个以技术验证为主、同时服务未来玩法落地的游戏原型
```

## 功能亮点

| 能力 | 说明 |
| --- | --- |
| 双模式窗口切换 | 在同一客户端里切换挂机小窗与地下城大窗，验证无边框、置顶、拖拽与居中切换链路 |
| Godot + C# 全栈约束 | 项目内只使用 C#，不引入 `.gd` 脚本，统一工具链和代码规范 |
| Facet 页面运行时 | 页面注册、生命周期、Binding、Lua 页面控制器、运行时日志、诊断快照已经形成主链路 |
| Lattice 确定性底座 | 定点数、会话运行时、系统调度、历史帧与基准治理在持续建设中 |
| 调试与验证导向 | 不是只追求“能跑”，而是强调可验证、可观察、可演进的工程结构 |

## 技术架构

从代码库组织上看，`Sideline` 可以粗略拆成四层：

| 层级 | 主要职责 | 当前落点 |
| --- | --- | --- |
| 表现层 | 场景、窗口、UI 面板、Godot 输入与渲染 | `godot/scenes`、`godot/scripts/main`、`godot/scripts/ui`、`godot/scripts/window` |
| 客户端运行时 | 页面注册、生命周期、Binding、Lua 控制器、日志、红点、诊断 | `godot/scripts/facet` |
| 确定性运行时 | 定点数、会话、系统调度、历史帧、checkpoint、测试与 benchmark | `godot/scripts/lattice` |
| 文档与治理 | 设计文档、开发流程、装配说明、报告与站点内容 | `docs` |

它们之间的关系不是简单的“工具库引用工具库”，更像下面这种职责传递：

```text
Godot Window / Scene / Input
    -> Main / Panels
    -> Facet Runtime
    -> Projection / Binding / Lua
    -> Lattice Runtime
    -> future replay / lockstep / long-running simulation
```

如果你更偏工程视角，可以这样理解：

- `Godot` 决定“画面如何出现、窗口如何切换、输入如何进入”。
- `Facet` 决定“页面如何被组织、被刷新、被调试、被热更新式控制”。
- `Lattice` 决定“核心状态如何推进、如何验证、以后如何支持回放与联机”。

## 运行环境

- Godot `4.6.1-stable mono`
- `.NET SDK 9.0.202`，实际项目目标框架为 `.NET 8.0`
- 渲染方式：`GL Compatibility`
- IDE：Windsurf / VS Code 系工具链，配合 C# Dev Kit 与 Godot Tools

运行前请确认：

- 必须使用 Godot 的 `.NET / Mono` 版本，而不是 Steam 标准版。
- 关闭 `Editor -> Editor Settings -> Run -> Window Placement -> Embed Game in Editor`。
- 从仓库根目录进入 `godot/` 后再执行 `dotnet` 命令。

## 快速开始

### 1. 构建项目

```bash
cd godot
dotnet build
```

如果需要发布配置：

```bash
cd godot
dotnet build -c Release
```

### 2. 从 Godot 编辑器运行

1. 用 Godot 4.6.1 Mono 打开 [godot/project.godot](./godot/project.godot)
2. 按 `F5` 运行项目
3. 用 `ESC` 触发 `ui_toggle_mode`，在挂机模式和地下城模式之间切换

### 3. 命令行验证

```bash
dotnet build godot/Sideline.sln -nologo
dotnet test godot/scripts/lattice/Tests/Lattice.Tests.csproj -nologo
```

## 阅读建议

| 你现在最关心什么 | 建议先看 |
| --- | --- |
| 我想快速理解这个项目在做什么 | [docs/design/GameDesign.md](./docs/design/GameDesign.md) |
| 我想知道客户端页面层是怎么组织的 | [godot/scripts/facet/README.md](./godot/scripts/facet/README.md) |
| 我想知道确定性底座当前到底做到哪了 | [godot/scripts/lattice/README.md](./godot/scripts/lattice/README.md) |
| 我想了解开发流程和分支规范 | [docs/development/Workflow.md](./docs/development/Workflow.md) |
| 我想了解装配结构与工程拆分 | [docs/development/AssemblyLayout.md](./docs/development/AssemblyLayout.md) |

> 提醒：部分历史设计文档保留了较早阶段的命名和设想。判断“当前真实状态”时，应优先以 `godot/scripts/facet`、`godot/scripts/lattice` 下的实现和文档为准。

## 两大部分

理解当前仓库，一个足够实用的方式是把它先拆成两部分：

1. `游戏客户端壳`
2. `确定性与页面运行时底座`

| 部分 | 关注点 | 当前内容 |
| --- | --- | --- |
| 游戏客户端壳 | 窗口模式、场景组织、面板切换、玩家可见交互 | `Main`、`WindowManager`、`IdlePanel`、`DungeonPanel` |
| 底座能力 | 页面运行时、Binding、Lua、Projection、确定性会话与测试链路 | `Facet` + `Lattice` |

它们的关系可以简化成下面这条链路：

```text
Godot 场景 / 窗口管理
    -> Facet 页面运行时
    -> Projection / Binding / Lua 控制器
    -> Lattice 会话与确定性数据
    -> 未来回放 / 联机能力
```

## 仓库结构

```text
Sideline/
├── godot/                       # Godot 4 项目根目录
│   ├── project.godot            # Godot 项目配置
│   ├── scenes/                  # 场景文件
│   ├── scripts/
│   │   ├── main/                # 主入口与客户端协调逻辑
│   │   ├── window/              # 双模式窗口管理
│   │   ├── ui/                  # 挂机 / 地下城面板
│   │   ├── facet/               # 客户端页面运行时、Binding、Lua、诊断
│   │   └── lattice/             # 确定性运行时、ECS、测试与基准
│   └── assets/                  # 游戏资源
├── docs/
│   ├── design/                  # 设计文档
│   ├── development/             # 开发流程、装配布局、优化指南
│   └── reports/                 # 基准与报告
└── README.md
```

## Roadmap

> 当前整体位置：`Phase 0 / 技术验证后期`  
> 核心目标：先把“双模式窗口 + 客户端运行时 + 确定性底座”三条链路收成稳定主干，再往完整玩法推进。

| 阶段 | 主题 | 视觉状态 | 目标摘要 |
| --- | --- | --- | --- |
| `Phase 0` | 技术验证 | `🟨 进行中` | 证明窗口体验、Facet 页面运行时与 Lattice 基础能力能在同一项目中稳定共存 |
| `Phase 1` | 核心玩法 | `⬜ 未开始` | 建立真正可循环游玩的地图、战斗、装备、成长与挂机收益链路 |
| `Phase 2` | EA 准备 | `⬜ 未开始` | 把技术原型收成对外可发布版本，包括内容、回放与平台接入 |
| `Phase 3` | 联机扩展 | `⬜ 未开始` | 在已有确定性底座之上追加 Lockstep 与 Steam Relay 能力 |

### Phase 0 · 技术验证

| 状态 | 项目 | 说明 |
| --- | --- | --- |
| `✅` | 无边框窗口原型 | 已验证挂机小窗、置顶、拖拽与大窗切换 |
| `✅` | Facet 页面运行时主链路 | 页面生命周期、Binding、Lua 宿主、诊断链路已接入 |
| `✅` | Lattice 定点数与测试基线 | 定点数与基础运行时验证已具备可持续迭代基础 |
| `🟨` | 最小 ECS 框架实现 | 仍在持续收口，是当前最重要的基础工作之一 |

### Phase 1 · 核心玩法

| 计划能力 | 目标结果 |
| --- | --- |
| 随机地图生成 | 让地下城不再只是演示面板，而是进入可重复刷图阶段 |
| 战斗系统 | 建立角色移动、攻击、受击、技能与敌人行为的最小闭环 |
| 装备系统与存档 | 让刷宝结果能够沉淀，并推动成长驱动 |
| 挂机资源收集完整实现 | 让挂机模式从窗口原型进化成真正的养成入口 |

### Phase 2 · EA 准备

| 计划能力 | 目标结果 |
| --- | --- |
| Steam SDK 接入 | 为成就、排行榜与后续平台能力打基础 |
| 战斗回放 | 利用确定性底座提供可复盘、可验证的回放能力 |
| 内容完善 | 从“技术可行”走向“版本可玩” |

### Phase 3 · 联机扩展

| 计划能力 | 目标结果 |
| --- | --- |
| Lockstep 联机 | 把确定性运行时真正推到多人同步场景 |
| Steam Relay 集成 | 为后续联机发行形态提供网络支撑 |

### 路线理解

这个 `Roadmap` 的关键并不是把事情列得很多，而是强调推进顺序：

1. 先把底层运行时和客户端组织方式做稳。
2. 再把玩法核心闭环真正做出来。
3. 最后再把平台化、回放、联机这些“放大器”接上去。

如果推进顺序反过来，仓库会很快失去现在最有价值的部分：它不仅在做玩法，也在沉淀一套能支撑玩法长期演进的工程结构。

## 相关文档

- 项目网站：https://zgx197.github.io/Sideline/
- 开发文档入口：[docs/docs.md](./docs/docs.md)
- 游戏设计文档：[docs/design/GameDesign.md](./docs/design/GameDesign.md)
- Facet 文档：[godot/scripts/facet/README.md](./godot/scripts/facet/README.md)
- Lattice 文档：[godot/scripts/lattice/README.md](./godot/scripts/lattice/README.md)
- Lattice 运行时架构护栏：[godot/scripts/lattice/ECS/Framework/CoreRuntimeGuardrails.md](./godot/scripts/lattice/ECS/Framework/CoreRuntimeGuardrails.md)

## 许可

本仓库当前采用 [GNU General Public License v3.0](./LICENSE)。

这意味着：

- 你可以在 GPL 约束下使用、修改和分发本项目代码。
- 基于本项目的衍生作品需要继续遵守 GPL 许可证要求。
- 如果你需要闭源或其他商业授权安排，需要单独评估授权策略。

---

Copyright (C) 2026 Sideline Team
