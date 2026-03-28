# CI 优化规划

> 本文档用于整理 Sideline 当前 CI 基线、后续优化项的优先级，以及推荐实施顺序。目标不是一次性做“大而全”的重构，而是在不破坏现有分支保护和 PR 体验的前提下，逐步把验证、发布和报告流程拆清楚。

---

## 当前基线

截至本轮调整完成后，仓库的 CI 结构已经具备一个比较清晰的基础层次：

- `PR Gate` 负责 `pull_request -> main` 的门禁校验，并保证受保护分支所依赖的检查项会稳定出现。
- `Common CI`、`Facet CI`、`Lattice CI` 负责 `push -> main` 的主干验证，用于承接更深入、耗时更高的校验。
- `Full Benchmark` 已经独立为手动 / 定时运行任务，不再与常规 PR 门禁强绑定。
- 当前分支保护应只绑定稳定、始终会出现的检查项，避免再次出现 “Expected” 长期不落地的问题。
- `ci-pr.yml` 的变更分类已经切换为结构化路径过滤，不再依赖手写 Bash 遍历规则。
- 共享的 .NET CI 模板已经抽取到本地 composite action，核心 workflow 不再各自维护重复的 setup / restore / build / test 骨架。
- CI 架构与 required checks 约束已经形成正式文档，后续维护有明确边界。

P0、P1 与当前范围内的 P2 已经完成。

补充说明：

- benchmark、report、release 的边界已经正式定型并写入文档。
- Godot 自动发布流程已被明确转入独立议题，不再作为本轮 CI 优化的待办项。

---

## 优先级排序

下表按“收益 / 风险 / 对当前流程的影响面”综合排序，优先级从高到低分为 `P0`、`P1`、`P2`。

| 优先级 | 优化项 | 主要收益 | 当前判断 | 当前状态 |
|------|------|------|------|------|
| P0 | 替换 PR 变更分类实现 | 提高门禁正确性，降低漏判风险 | 最值得先做 | 已完成 |
| P0 | 补齐 CI 架构与受保护检查文档 | 降低维护成本，避免后续误改分支保护 | 应尽快落文档 | 已完成 |
| P0 | 提取可复用的 .NET CI 模板 | 降低重复配置，减少工作流漂移 | 第二轮代码层优化重点 | 已完成 |
| P1 | 明确 `Common CI` 的长期角色 | 避免主干验证职责重叠 | 需要结合后续发布设计一起定 | 已完成 |
| P1 | 为核心 workflow 增加 `GITHUB_STEP_SUMMARY` | 提高排障效率和 PR 可读性 | 收益稳定，实施成本低 | 已完成 |
| P1 | 优化 NuGet / restore / test 缓存策略 | 缩短运行时间，降低 CI 成本 | 在结构稳定后推进更合适 | 已完成 |
| P2 | 将 Godot 自动发布流程转入独立议题 | 避免未定方案污染当前 CI 结构 | 需要与发布策略单独讨论 | 已完成 |
| P2 | 继续拆分 benchmark / report / release 职责 | 让验证链路更纯粹 | 适合在发布流程明确前先固化边界 | 已完成 |

---

## 详细优化项

### P0. 替换 PR 变更分类实现

#### 背景

当前 `ci-pr.yml` 使用 Bash `case` 语句手工判断：

- 是否为 docs-only
- 是否需要 Lattice 校验
- 是否需要 Sideline 构建
- 是否需要格式检查

这个方案已经能工作，但有两个明显问题：

- 路径规则一旦继续增长，脚本会越来越脆弱。
- 如果新增目录或重构路径后忘记同步脚本，就会出现“该跑的没跑”的漏检风险。

#### 建议方案

优先评估将变更分类改为成熟的路径过滤方案，例如：

- `dorny/paths-filter`
- 或仓库内自定义但有测试覆盖的分类脚本

建议优先采用现成 Action，而不是继续扩展手写 Bash。

#### 预期收益

- 让 PR Gate 的触发判定更可靠。
- 把“路径规则”从命令脚本转为结构化配置，更容易审阅。
- 后续新增模块时，只需维护过滤规则，而不是重写判断逻辑。

#### 完成标准

- `docs-only`、`lattice`、`facet/sideline`、`format` 的判定规则可读、可扩展。
- 至少覆盖当前 `.github/workflows/`、`godot/scripts/lattice/`、`godot/scripts/facet/`、`README.md`、`docs/` 等核心路径。
- 替换后仍然保持受保护检查名称不变。

---

### P0. 补齐 CI 架构与受保护检查文档

#### 背景

现在 CI 已经从“单一工作流混合承担所有职责”切分成多层结构，但仓库里还没有一份正式文档说明：

- 哪些 workflow 属于 PR 门禁
- 哪些 workflow 属于主干验证
- 哪些检查项允许作为 branch protection 的 required checks
- 哪些任务只适合作为手动、定时或发布后验证

如果没有这份文档，后续维护者非常容易再次把 path-filtered 工作流直接绑进 required checks，从而复现此前的卡住问题。

#### 建议方案

新增一份专门的 CI 架构说明文档，建议内容至少包括：

- 当前 workflow 清单与职责划分
- 受保护检查项名单
- 新增 workflow 时的接入原则
- “何时放到 PR Gate，何时放到 main 验证，何时放到 release” 的判断标准

#### 预期收益

- 把这次经验固化下来，避免后续反复踩坑。
- 降低分支保护配置与 workflow 设计之间的认知偏差。
- 让后续 Godot 发布流程设计有统一的接入边界。

#### 完成标准

- 文档可直接指导后续新增或调整 workflow。
- 文档中明确列出 required checks 的稳定命名约束。
- 文档与实际 workflow 命名保持一致。

---

### P0. 提取可复用的 .NET CI 模板

#### 背景

当前多个工作流都重复包含类似步骤：

- `actions/checkout`
- `actions/setup-dotnet`
- `dotnet restore`
- `dotnet build`
- `dotnet test`

短期内这种重复是可接受的，但随着后续加入 Godot 自动导出、发布、测试矩阵或更多子模块，重复逻辑会迅速膨胀。

#### 建议方案

优先考虑以下两种方案之一：

- 使用 `workflow_call` 提取复用工作流
- 使用 composite action 提取通用 .NET 构建 / 测试步骤

如果后续需要保留不同工作流的独立 job 名称，通常 `workflow_call` 更适合；如果更关注步骤复用且希望保留各工作流的主结构，则 composite action 也可行。

#### 预期收益

- 减少重复 YAML。
- 修改 .NET 版本、缓存策略、公共参数时只需改一处。
- 降低不同 workflow 行为逐渐不一致的概率。

#### 完成标准

- 至少抽取出一层公共的 .NET setup / restore / build / test 逻辑。
- `ci-pr.yml`、`ci.yml`、`ci-facet.yml`、`ci-lattice.yml` 不再各自维护完全重复的实现。
- 不影响现有 required checks 的名称与展示。

---

### P1. 明确 `Common CI` 的长期角色

#### 背景

当前 `Common CI` 仍然有价值，但它和 `Facet CI` 在 Sideline 主工程构建上存在一定重叠。随着后续模块继续拆分，如果不尽早定义边界，会出现：

- 同一类构建被多个 workflow 重复执行
- 某些失败不知道应归属哪个工作流负责
- 主干验证越来越重，但职责却越来越模糊

#### 建议方向

建议在后续迭代中二选一：

- 将 `Common CI` 定位为“最低成本的主工程健康检查”
- 或者逐步弱化 `Common CI`，把验证职责分别沉到 `Facet CI`、`Lattice CI`、未来的 `Release CI`

短期更推荐第一种，因为它能保留一个成本较低、语义明确的主干兜底检查。

#### 完成标准

- `Common CI` 在文档中有明确定位。
- 与 `Facet CI`、`Lattice CI` 的职责边界可以一句话解释清楚。
- 避免同一份主工程构建在多个 workflow 中无意义重复。

---

### P1. 为核心工作流增加运行摘要

#### 背景

现在工作流已经能跑，但当 PR 或 main 校验失败时，阅读成本仍然偏高。尤其是 PR Gate 这种汇总性质较强的 workflow，如果没有摘要，排障要逐个 step 点进去看。

#### 建议方案

为以下 workflow 增加 `GITHUB_STEP_SUMMARY`：

- `ci-pr.yml`
- `ci.yml`
- `ci-facet.yml`
- `ci-lattice.yml`

摘要建议至少包含：

- 本次变更分类结果
- 实际执行了哪些 job / 哪些 job 被跳过
- 关键 restore / build / test 结果
- 如果失败，给出定位入口或下一步建议

#### 预期收益

- PR 页面可读性明显提升。
- Review 时更容易理解本次代码改动为什么触发某些检查。
- 排障路径更短，尤其适合后续多人协作。

#### 当前结果

- `ci-pr.yml`、`ci.yml`、`ci-facet.yml`、`ci-lattice.yml` 已全部接入 `GITHUB_STEP_SUMMARY`。
- PR Gate 会输出变更分类摘要，主干验证 workflow 会输出各 job 的执行结果、目标项目与缓存状态。

---

### P1. 优化缓存与恢复策略

#### 背景

当前工作流以“先确保正确”为主，缓存策略还比较保守。后续如果 workflow 数量继续增加，restore 和 build 的重复耗时会越来越明显。

#### 建议方案

在结构稳定后再引入缓存优化，避免一边重构一边调缓存导致定位复杂化。优先关注：

- `actions/setup-dotnet` 自带缓存能力是否足够
- NuGet 包缓存键是否能按项目维度拆分
- Lattice 测试与 Sideline 主工程的 restore 是否能减少重复

#### 预期收益

- 缩短单次 CI 运行时长。
- 降低并发执行时的资源消耗。
- 为未来 Godot 导出和发布任务预留更多预算空间。

#### 完成标准

- PR Gate 和 main 验证的总体耗时有可见下降。
- 缓存策略不会引入隐蔽的脏缓存问题。

#### 当前结果

- 共享 composite action 已支持 `actions/setup-dotnet` 的 NuGet 缓存配置。
- 核心 workflow 已按项目维度传入 `cache-dependency-path`，避免继续依赖隐式缓存或粗粒度键。
- 缓存能力已经覆盖 PR Gate、Common CI、Facet CI、Lattice CI，以及完整 benchmark workflow。

---

### P2. 将 Godot 自动发布流程转入独立议题

#### 背景

当前 CI 已经有较清晰的“验证”层，但“发布”层仍未正式建立。由于 Godot 自动发布会直接影响导出平台、制品结构、版本策略和后续渠道分发，如果在方案未定时仓促落地，很容易污染现有 CI 分层。

#### 建议方案

本轮不直接实现 Godot release workflow，而是先把它明确转入独立议题，并固定两条约束：

- 现有 PR Gate、Main Validation、Benchmark workflow 不代偿 release 职责；
- 后续要做 Godot 自动发布时，必须以单独讨论和单独 workflow 的方式进入仓库。

#### 预期收益

- 先把当前 CI 结构稳定住，避免“边讨论边塞实现”。
- 避免让未定的发布策略反向绑架现有验证流程。
- 为后续单独讨论 release 方案保留清晰边界。

#### 完成标准

- 文档明确记录 Godot 自动发布已转入独立议题。
- 当前 workflow 中不存在伪装成验证或 benchmark 的 release 逻辑。
- 后续如需实现 release，必须新开议题推进。

#### 当前结果

- Godot 自动发布已从当前 CI 优化范围中正式剥离。
- `CIArchitecture.md` 与 `CIBoundaries.md` 已明确声明 release 不是现有 workflow 的职责。

---

### P2. 继续拆分 benchmark / report / release 职责

#### 背景

`Full Benchmark` 已经独立出来，这是正确方向。但从长期看，benchmark、报告发布、正式发行仍然是三类不同职责：

- benchmark 关注性能回归
- report 关注结果展示
- release 关注可交付制品

未来如果把这些职责继续混在一个 workflow 中，维护成本还是会上升。

#### 建议方案

继续保持以下边界：

- benchmark 只负责跑测试和产出结果
- report 只负责整理和发布展示材料
- release 只负责导出与发布制品

#### 预期收益

- 每个 workflow 的失败原因更单一。
- 便于按需触发，而不是一条流水线承载所有工作。
- 后续可以分别设置不同的运行频率和保留策略。

#### 完成标准

- `benchmark-full.yml` 只承载 benchmark 与结果归档职责。
- 文档明确区分 benchmark、report、release 三层语义。
- 站点展示、游戏发布均不再被描述为 benchmark workflow 的隐含职责。

#### 当前结果

- `benchmark-full.yml` 已明确标注自己属于 analysis 层，只负责 benchmark 与 artifact。
- CI 架构文档和边界文档已统一说明 report / release 不属于 benchmark workflow。
- 当前仓库内关于这三层职责的表述已经完成收口。

---

## 推荐实施顺序

建议按下面顺序推进，而不是并行大改：

1. 替换 PR Gate 的变更分类实现。
2. 新增 CI 架构说明文档，并同步分支保护约束。
3. 抽取公共 .NET CI 模板，收敛重复 YAML。
4. 明确 `Common CI` 的长期职责，必要时做精简或重命名。
5. 为核心 workflow 增加运行摘要。
6. 在结构稳定后，再做缓存优化。
7. 固化 benchmark / report / release 的职责边界。
8. 将 Godot 自动发布流程转入独立议题。

这个顺序的核心原则是：先稳住门禁正确性，再降低维护成本，最后打通发布链路。

---

## 执行原则

后续每轮优化都建议遵守以下原则：

- 不随意变更已有 required checks 的显示名称。
- 新增 workflow 前，先判断它属于门禁、主干验证还是发布层。
- 优先保证“会不会误放过问题”，再考虑“能不能更快”。
- 每次重构 workflow 后，都要验证 docs-only PR、代码 PR、工作流自身变更这三类场景。
- 如果某个 workflow 只是辅助分析用途，不要直接接入分支保护。

---

## 下一步建议

P0、P1 与当前范围内的 P2 已全部完成。下一步不再继续扩展现有 CI，而是单独讨论 Godot 自动发布议题，确认以下内容后再决定是否进入实现：

1. 发布目标平台与导出矩阵。
2. 制品结构、版本号策略和 artifact 归档方式。
3. GitHub Release、Steam 或其他渠道的接入边界。

在这些问题明确之前，现有 CI 结构应保持稳定，不再把 release 能力临时塞入 benchmark 或 validation workflow。
