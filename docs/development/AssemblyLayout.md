# 程序集划分说明

> 本文档用于记录 Sideline 当前代码库中的程序集划分、依赖方向、设计意图与后续扩展规则。
> 当项目继续拆分新的客户端层、工具链或测试工程时，应优先回看并更新本文档。

---

## 文档目标

当前项目已经从“主项目误吞整个仓库源码”的状态，调整为“按程序集边界显式组织”。
这份文档的目的有四个：

1. 说明当前每个 `.csproj` 的职责边界。
2. 说明程序集之间允许的依赖方向。
3. 说明为什么 Godot / C# 项目中要用 `.csproj + ProjectReference` 替代 Unity 风格的 `asmdef` 心智模型。
4. 为后续继续拆分 `Facet`、接入更多客户端层模块时提供统一回顾基线。

---

## 当前程序集总览

当前 `godot/Sideline.sln` 中已纳入以下程序集：

| 程序集 | 路径 | 角色 |
|------|------|------|
| `Sideline` | `godot/Sideline.csproj` | Godot 客户端主程序集，负责场景脚本、窗口管理、当前 Godot 宿主接入 |
| `Lattice` | `godot/scripts/lattice/Lattice.csproj` | 逻辑层核心程序集，纯 .NET 逻辑库 |
| `Facet.Core` | `godot/scripts/facet/Facet.Core.csproj` | Facet 的运行时核心基础设施，不依赖 Godot |
| `Lattice.Tests` | `godot/scripts/lattice/Tests/Lattice.Tests.csproj` | Lattice 单元测试程序集 |
| `Lattice.Benchmarks` | `godot/Lattice.Benchmarks/Lattice.Benchmarks.csproj` | Lattice 基准测试程序集 |
| `SwizzleGenerator` | `godot/scripts/lattice/Tools/SwizzleGenerator/SwizzleGenerator.csproj` | Lattice 数学代码生成器 |
| `LutGenerator` | `godot/scripts/lattice/Tools/LutGenerator/LutGenerator.csproj` | LUT 生成工具 |
| `Lattice.Analyzers` | `godot/scripts/lattice/Tools/Lattice.Analyzers/Lattice.Analyzers.csproj` | Roslyn 分析器程序集 |

---

## 当前依赖关系

当前依赖关系可概括为：

```text
Sideline
  -> Lattice
  -> Facet.Core

Lattice.Tests
  -> Lattice

Lattice.Benchmarks
  -> Lattice

Lattice
  -> SwizzleGenerator (Analyzer 方式接入)

Facet.Core
  -> 无项目内依赖
```

### 说明

- `Sideline` 是 Godot 运行时真正加载的主程序集。
- `Lattice` 作为逻辑层核心，已经通过 `ProjectReference` 正式接回 `Sideline`。
- `Facet.Core` 当前只承载 Facet 的无引擎运行时基础能力，故意不依赖 Godot。
- `Facet` 中依赖 Godot 的部分目前仍留在 `Sideline` 中，主要是 `FacetHost` 与 `FacetLogger`。
- `Tests`、`Benchmarks`、`Tools` 已经从主程序集编译范围中隔离出去，避免再次污染 Godot `Problems`。

---

## 当前代码归属

### 1. Sideline

`Sideline` 当前只应承载以下代码：

- `scripts/main/**`
- `scripts/ui/**`
- `scripts/window/**`
- `scripts/facet/Runtime/FacetHost.cs`
- `scripts/facet/Runtime/FacetLogger.cs`

这意味着 `Sideline` 的职责是：

- Godot 场景脚本入口
- Godot 节点生命周期接入
- 窗口与界面表现层宿主
- 对 `Lattice` 与 `Facet.Core` 的运行时拼装

### 2. Lattice

`Lattice` 的职责是纯逻辑层：

- 定点数与数学库
- ECS 核心实现
- 逻辑框架基础设施
- 与客户端引擎无关的确定性能力

`Lattice` 不应反向依赖 `Sideline`。

### 3. Facet.Core

`Facet.Core` 当前承载的是 Facet 最基础的运行时核心：

- `FacetConfig`
- `FacetLogLevel`
- `FacetRuntimeContext`
- `FacetServices`
- `IFacetLogger`

这些类型被抽出来的原因是：

- 它们本质上属于客户端表现层运行时的核心概念。
- 它们不需要直接依赖 Godot API。
- 它们应该能被后续不同宿主层复用，而不是被绑死在 `Sideline` 主程序集里。

### 4. Facet 的 Godot 宿主适配层

当前尚未单独拆成 `Facet.Godot`，但已经形成逻辑边界：

- `FacetHost`
- `FacetLogger`

它们依赖 `Godot.Node`、`Godot.GD` 等 API，因此暂时保留在 `Sideline` 中。
后续如果需要进一步降低 `Sideline` 主程序集体积，或将 Facet 提炼为更独立的客户端运行时，可再拆出 `Facet.Godot.csproj`。

---

## 为什么这套方案等价于 Unity 的 asmdef

在 Unity 中，程序集隔离主要通过 `asmdef` 完成；在 Godot + C# / .NET 中，对应物是：

- `.csproj`：定义一个程序集
- `ProjectReference`：定义程序集依赖
- `.sln`：组织多个程序集为同一个开发工作区
- `Compile Include / Remove`：控制源码属于哪个程序集

也就是说，Godot 里并不存在一个专门等价于 `asmdef` 的单独资源文件，程序集边界本身就是标准 .NET 工程边界。

因此，在本项目里应建立如下认知：

- “新增一个独立客户端模块” 的正确动作是“新增一个 `.csproj`”。
- “让主项目使用该模块” 的正确动作是“新增 `ProjectReference`”。
- “防止测试代码污染主项目” 的正确动作是“让测试拥有自己的 `.csproj`，并从主项目编译白名单中排除”。

---

## 当前拆分原则

当前代码库后续继续拆分程序集时，应遵守以下原则：

### 原则 1：依赖方向必须单向

推荐方向：

```text
Godot Host / Sideline
    -> Facet.Godot（未来可选）
    -> Facet.Core
    -> Lattice
```

不允许：

- `Lattice` 依赖 `Sideline`
- `Lattice` 依赖 `Facet.Core`
- `Facet.Core` 依赖 `Sideline`
- 测试程序集反向成为生产程序集的依赖

### 原则 2：主程序集只保留真正需要被 Godot 直接加载的代码

只要一段代码不需要直接挂载为 Godot 脚本、不需要直接访问 `Node` / `GD` / 场景资源，就应该优先考虑放到独立程序集，而不是继续堆在 `Sideline` 中。

### 原则 3：先拆无引擎核心，再拆宿主适配层

拆分顺序应优先是：

1. 抽无引擎依赖的核心类型
2. 稳定依赖方向
3. 最后再拆 Godot 宿主适配层

原因是无引擎核心更容易稳定，也更容易测试。

### 原则 4：测试、工具、基准必须天然隔离

以下代码永远不应进入 Godot 主程序集：

- `Tests`
- `Benchmarks`
- `Tools`
- `.godot/mono/temp/obj`

这条原则已经通过当前的项目结构重新落实。

---

## 当前阶段结论

截至当前状态：

- `Sideline.csproj` 已切换为白名单编译。
- `Lattice` 已通过 `ProjectReference` 正式接回 `Sideline`。
- `Facet` 已完成第一步拆分，形成 `Facet.Core` 与 Godot 宿主层的边界。
- `Sideline.sln` 已纳入主程序集、逻辑程序集、测试程序集、基准程序集与工具程序集。
- 当前整套 solution 已可成功构建。
- 当前 `Sideline` 对应的 Godot `Problems` 抓取结果为 `0 error / 0 warning`。

---

## 后续建议

### 建议 1：后续再拆 `Facet.Godot`

当 Facet 在 Godot 侧的适配代码开始增多时，可以把以下内容进一步抽成独立程序集：

- `FacetHost`
- `FacetLogger`
- 后续 Binding / Layout / Godot Node Resolver
- Facet 的 Godot UI 宿主实现

### 建议 2：明确客户端层的最终结构

后续理想结构可演进为：

```text
Sideline
  -> Facet.Godot
  -> Facet.Core
  -> Lattice
```

其中：

- `Sideline` 只负责项目入口与场景装配
- `Facet.Godot` 负责表现层宿主适配
- `Facet.Core` 负责表现层运行时核心
- `Lattice` 负责逻辑层

### 建议 3：每次拆分程序集时同步更新本文档

后续若出现以下动作，应同步更新本文档：

- 新增 `.csproj`
- 调整 `ProjectReference`
- 改动某段代码的程序集归属
- 将某个模块从 `Sideline` 中抽离

否则几轮迭代后，团队会重新失去对程序集边界的共同认知。
