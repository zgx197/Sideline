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
  <a href="https://img.shields.io/badge/Status-Tech%20Validation-F39C12"><img alt="Tech Validation" src="https://img.shields.io/badge/Status-Tech%20Validation-F39C12" /></a>
  <a href="https://img.shields.io/badge/License-GPLv3-blue.svg"><img alt="GPLv3" src="https://img.shields.io/badge/License-GPLv3-blue.svg" /></a>
</p>

| 维度 | 说明 |
| --- | --- |
| 核心定位 | 用一个真正可玩的游戏项目，验证“双模式窗口 + 确定性运行时 + 客户端页面框架”的组合 |
| 玩法体验 | 挂机模式负责资源积累，地下城模式负责战斗与刷宝，两种体验在同一个进程内切换 |
| 技术策略 | Godot 4.6.1 Mono + C# 作为表现层，自研 `Lattice` 负责确定性运行时，自研 `Facet` 负责客户端页面运行时 |
| 当前阶段 | 技术验证为主，窗口切换、Facet 页面运行时与部分调试链路已接入，核心 ECS 仍在持续收口 |

> 本页用于概览项目定位、当前阶段与文档入口；细节实现请继续查看下方对应模块文档。

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

## 项目定位

`Sideline` 当前首先是一个技术验证项目，同时也是游戏内容原型。

项目现阶段不以尽快堆出完整商业版本为目标，而是借由一个真实可运行、可迭代的游戏载体，持续验证下面这些方向：

| 技术方向 | 当前关注点 |
| --- | --- |
| AI 游戏应用 | 探索 AI 在客户端开发、工具链、内容生产与工作流组织中的实际落点 |
| 系统架构 | 验证双模式窗口、客户端页面运行时、确定性底座能否在同一仓库中长期共存 |
| 性能优化 | 持续收口定点数、运行时调度、测试基线与 benchmark 治理，避免原型阶段就积累失控成本 |
| 程序化生成 | 为后续地图生成、内容扩展与可重复游玩能力预留稳定接口和实现空间 |

项目的建设重点不是单独追求玩法堆量，而是同时推进体验表达与工程底座：

- 在体验层面，项目强调“工作间隙经营地下世界”的双重生活感。
- 在系统层面，挂机积累与地下城战斗共享同一套状态与运行时，而不是拆成两套彼此独立的程序。
- 在工程层面，项目希望逐步沉淀出可回放、可联机、可诊断、可持续迭代的底座。

当前仓库围绕三条主线展开：

| 问题 | 当前答案 |
| --- | --- |
| 游戏想提供什么体验 | 一个可以挂在角落经营、也可以放大进入战斗的双模式客户端 |
| 客户端想怎么组织自己 | 用 `Facet` 把页面、Binding、Lua 控制器、诊断工具收进统一运行时 |
| 底层状态想如何长期演进 | 用 `Lattice` 走确定性运行时路线，为回放、长时模拟和未来联机预留空间 |

## 当前状态

当前版本更接近“可验证的高保真原型”，而不是“已经完成完整玩法闭环的 EA 版本”。仓库中已经跑通的重点包括：

| 模块 | 当前已具备 |
| --- | --- |
| 主客户端 | `Main` 已负责窗口模式切换、页面打开、Projection 发布与基本协调 |
| 窗口体验 | `WindowManager` 已实现挂机小窗 / 地下城大窗切换、置顶、拖拽与居中 |
| 挂机 / 地下城面板 | `IdlePanel` 与 `DungeonPanel` 已接入页面生命周期、Projection 消费和运行时验证内容 |
| Facet | 页面运行时、Binding、Lua 宿主、日志、红点、布局实验与诊断快照已形成主链路 |
| Lattice | 定点数、会话运行时、系统调度、历史帧、测试与 benchmark 治理持续收口中 |

当前项目形态如下：

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

从代码库组织上看，`Sideline` 目前分为四层：

| 层级 | 主要职责 | 当前落点 |
| --- | --- | --- |
| 表现层 | 场景、窗口、UI 面板、Godot 输入与渲染 | `godot/scenes`、`godot/scripts/main`、`godot/scripts/ui`、`godot/scripts/window` |
| 客户端运行时 | 页面注册、生命周期、Binding、Lua 控制器、日志、红点、诊断 | `godot/scripts/facet` |
| 确定性运行时 | 定点数、会话、系统调度、历史帧、checkpoint、测试与 benchmark | `godot/scripts/lattice` |
| 文档与治理 | 设计文档、开发流程、装配说明、报告与站点内容 | `docs` |

这些模块按照下面的职责链路衔接：

```text
Godot Window / Scene / Input
    -> Main / Panels
    -> Facet Runtime
    -> Projection / Binding / Lua
    -> Lattice Runtime
    -> future replay / lockstep / long-running simulation
```

按职责划分：

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

> 当前重点是把“双模式窗口 + 客户端运行时 + 确定性底座”收成稳定主干，再逐步把原型推进到完整可玩的版本。

| 方向 | 当前重点 | 后续目标 |
| --- | --- | --- |
| 客户端体验 | 打磨挂机小窗与地下城大窗之间的切换体验，保持窗口模式、页面状态和交互链路一致 | 让双模式窗口成为项目最鲜明的产品特征，而不是单次演示功能 |
| 核心玩法 | 在现有原型之上补全地图、战斗、装备、成长与挂机收益闭环 | 形成真正可重复游玩的刷图与养成循环 |
| 工程底座 | 持续收口 `Facet` 与 `Lattice`，稳住页面运行时、确定性会话、测试、诊断与性能基线 | 为回放、长时模拟、平台化接入与未来联机能力留下可靠基础 |
| 技术探索 | 推进 AI 游戏应用、系统架构、性能优化与程序化生成相关验证 | 把原型阶段沉淀下来的方法和能力转成可复用的长期资产 |
| 对外发布 | 先把项目说明、结构和技术方向表达清楚 | 后续逐步推进到内容完善、平台接入与版本发布准备 |

### 路线说明

项目按下面的顺序推进：

1. 先把底层运行时和客户端组织方式做稳。
2. 再把玩法核心闭环真正做出来。
3. 最后再把平台化、回放、联机这些“放大器”接上去。

这一顺序对应项目的建设原则：先稳住底座和客户端组织，再扩展玩法，再推进 AI 应用、程序化生成和性能相关能力的深度落地，最后接入平台化与联机能力。后续迭代都会建立在这套工程结构之上。

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
