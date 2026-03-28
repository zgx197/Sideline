# CI 架构说明

> 本文档说明 Sideline 当前 GitHub Actions 的职责分层、受保护检查项约束，以及新增 workflow 时应遵守的接入规则。它和 [CIOptimizationPlan.md](./CIOptimizationPlan.md) 的区别是：前者回答“下一步还做什么”，本文回答“现在这套 CI 是怎么组织的”。

---

## 目标

当前 CI 架构围绕三个原则设计：

- 受保护分支依赖的检查项必须稳定出现，不能再出现长期 `Expected` 不落地的情况。
- PR 门禁、主干验证、性能分析、后续发布要分层，不能继续混在一条工作流里。
- 共享的 .NET 执行逻辑必须集中维护，避免 workflow 漂移。

---

## 当前分层

### 1. PR 门禁层

文件：

- `.github/workflows/ci-pr.yml`

职责：

- 仅用于 `pull_request -> main`
- 输出稳定、始终可见的 required checks
- 根据改动范围决定是否真正执行 Sideline / Lattice / 格式校验
- 即使是 docs-only PR，也必须产出最终可完成的 check run

这一层的原则是：宁可多跑一点，也不能不出检查项。

### 2. 主干验证层

文件：

- `.github/workflows/ci.yml`
- `.github/workflows/ci-facet.yml`
- `.github/workflows/ci-lattice.yml`

职责：

- 仅用于 `push -> main` 或人工触发
- 承接更深入、更模块化的验证
- 为合入后的主干健康状态负责，不承担 PR required checks 的稳定命名约束

当前定位如下：

- `Common CI`：最低成本的主工程健康检查，用来快速确认 Sideline 主工程在主干上仍能正常构建。
- `Facet CI`：Facet 核心库与主工程集成层的专项验证。
- `Lattice CI`：Lattice 的格式、跨平台构建测试、确定性校验、分析器校验和 benchmark smoke。

### 3. 分析层

文件：

- `.github/workflows/benchmark-full.yml`

职责：

- 只负责完整性能基准测试
- 通过手动或定时运行产出 benchmark 结果与归档 artifact
- 不参与 PR required checks，也不承担主干最低健康门槛
- 不负责 GitHub Pages 发布、游戏打包或 Godot 导出

### 4. 共享模板层

文件：

- `.github/actions/dotnet-ci/action.yml`
- `.github/filters/ci-pr.yml`

职责：

- `dotnet-ci` 统一承接 .NET SDK 初始化与调用脚本执行
- `ci-pr` 过滤规则负责 PR Gate 的结构化变更分类
- 核心 workflow 的缓存策略和脚本执行入口在这一层集中维护

这一层不直接面向用户，但它决定了各 workflow 的一致性与可维护性。

---

## Required Checks

当前 `main` 分支应只绑定以下稳定检查项：

- `Code Format Check`
- `Build & Test (windows-latest, Debug)`
- `Build & Test (windows-latest, Release)`

这些名称来自 `.github/workflows/ci-pr.yml` 中的 job 名称。它们是对外契约，不应随意调整。

### 变更规则

只有在以下两个条件同时满足时，才允许改动 required checks 的显示名称：

1. workflow 代码已修改完成并验证可用；
2. GitHub Branch Protection 中的 required checks 配置已经同步更新。

如果只改了 workflow 名称但没有同步 branch protection，就可能再次出现 PR 长期卡在 `Expected` 的问题。

---

## 新增 Workflow 的接入原则

新增 workflow 前，先判断它属于哪一层：

- 如果它必须在每个面向 `main` 的 PR 上稳定出现，就属于 PR 门禁层。
- 如果它只需要在合入后验证主干质量，就属于主干验证层。
- 如果它是性能分析、报告生成、发布制品、站点更新等辅助流程，就不应进入 required checks。

### 进入 PR 门禁层的条件

只有同时满足下面条件，才应该进入 PR Gate 或成为 required checks：

- 结果必须影响是否允许合并。
- 运行成本可控。
- 即使在 docs-only 或轻量改动场景下，也能稳定生成最终 check run。

### 不应进入 PR 门禁层的任务

以下任务默认不应进入 required checks：

- 完整 benchmark
- 报告发布
- Godot 导出与打包
- GitHub Pages 更新
- 只服务于观测或分析的辅助任务

---

## 路径分类规则

PR Gate 当前不再使用手写 Bash 逐条遍历文件名，而是使用结构化路径过滤：

- 过滤规则文件：`.github/filters/ci-pr.yml`
- 执行入口：`.github/workflows/ci-pr.yml`

当前分类目标包括：

- `docs_only`
- `needs_format`
- `needs_lattice`
- `needs_sideline`

其中：

- `docs_only` 只允许 `README.md` 与 `docs/**`
- `.github/workflows/**`、`.github/actions/**`、`.github/filters/**` 视为 CI 基础设施变更，默认同时触发 Lattice 与 Sideline 相关验证
- 对于不属于 docs-only、但又未命中显式模块规则的变更，PR Gate 仍会回退到 Sideline 主工程构建，避免漏检

---

## 运行摘要与缓存

当前核心 workflow 已统一具备两个基础能力：

- 使用 `GITHUB_STEP_SUMMARY` 输出分类结果、执行状态与目标项目，减少排障时逐 step 翻日志的成本。
- 使用 `actions/setup-dotnet` 的 NuGet 缓存，并按项目维度传入 `cache-dependency-path`，避免缓存键过粗或依赖隐式推断。

当前已覆盖：

- `ci-pr.yml`
- `ci.yml`
- `ci-facet.yml`
- `ci-lattice.yml`

`benchmark-full.yml` 也已接入相同的缓存入口，但它仍属于分析层，而不是 required checks 的组成部分。

---

## Benchmark / Report / Release 边界

当前边界已经明确固定：

- benchmark workflow 只负责性能测试与结果归档；
- report 代表结果展示层，不等于 benchmark 执行层；
- release 代表可交付制品层，当前尚未进入实现。

更具体的约束见 [CIBoundaries.md](./CIBoundaries.md)。

---

## Common CI 的定位

`Common CI` 当前明确定位为：

- 主干上的最小 Sideline 健康检查
- 成本低于 `Facet CI` 和 `Lattice CI`
- 不负责替代模块专项验证

换句话说，它不是“大而全”的总入口，而是主干上的兜底构建检查。只要这个定位不变，就不应该继续无节制地往里面塞更多专项逻辑。

---

## 维护要求

每次重构 CI 后，至少应验证以下三类场景：

- docs-only PR：required checks 必须全部出现并完成
- 普通代码 PR：应触发对应模块的真实校验
- CI 基础设施改动：PR Gate 与主干验证都应能覆盖共享 action / workflow 的影响面

如果新增了共享 action 或过滤规则，还要同步检查相关 workflow 的 `paths` 触发范围，避免“模板变了，但依赖它的 workflow 没跑”的问题。

---

## 后续边界

后续如果继续推进 Godot 自动发布，建议新增独立的 release / export workflow，并保持以下边界：

- PR Gate 负责合并门禁
- Main Validation 负责合入后的代码质量
- Benchmark 负责性能回归
- Release 负责可交付制品

这四层职责一旦混回一条 workflow，维护成本会很快重新升高。

当前状态补充说明：

- Godot 自动发布流程已明确转入独立议题讨论；
- 它不是当前这轮 CI 优化的未完成残项，也不应由现有 benchmark / validation workflow 临时代偿。
