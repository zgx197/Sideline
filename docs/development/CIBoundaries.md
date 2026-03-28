# CI 边界约束

> 本文档用于把 Sideline 当前 CI 中已经确认的职责边界写成明确约束，避免 benchmark、report、release 再次混回同一条流程。它不是未来方案草图，而是当前仓库应遵守的边界定义。

---

## 结论

当前仓库中的相关职责已经按下面方式固定：

- `PR Gate` 只负责面向 `main` 的合并门禁。
- `Main Validation` 只负责合入后的主干质量验证。
- `Full Benchmark` 只负责性能基准测试和结果归档。
- 报告展示属于站点内容层，不属于 benchmark 执行层。
- Godot 自动发布流程暂缓实现，转入单独议题讨论，不纳入当前 CI 优化收口范围。

这意味着：当前 CI 优化阶段已经完成了“职责拆分”，但没有承诺“立即上线发布流水线”。

---

## Benchmark 边界

当前文件：

- `.github/workflows/benchmark-full.yml`

它的职责只有三件事：

- 运行完整 BenchmarkDotNet 基准测试。
- 校验结果产物是否完整、有效。
- 上传 benchmark artifact，并生成供后续查看的索引文件。

它不负责：

- 触发 GitHub Pages 部署。
- 发布线上报告站点。
- 生成游戏发行包。
- 导出 Godot 构建。
- 作为 PR required checks 的一部分参与门禁。

如果后续需要报告上线或站点发布，那应该由单独的 report / pages 流程接管，而不是继续堆到 `benchmark-full.yml` 里。

---

## Report 边界

报告层当前只表示“结果展示与归档语义”，不等同于 benchmark 执行。

在当前仓库里：

- benchmark workflow 负责产出原始结果和索引素材；
- `docs/` 下的网站内容负责展示；
- 是否将 benchmark 结果自动同步到站点，属于未来可选能力，不是当前 CI 的既有职责。

因此，report 不是 benchmark job 里的一个 step 细节，而是后续可以独立演进的一层能力。

---

## Release 边界

Godot 自动发布流程当前明确处于“暂缓设计”状态。

这意味着：

- 仓库当前没有正式的 Godot release workflow；
- benchmark、主干验证、PR Gate 都不应代替 release 流程承担导出或打包职责；
- 任何与 Godot 导出、版本标签、Release asset、分发渠道相关的设计，都应在单独讨论后再进入实现。

这是一个明确决策，不是遗留空白。

---

## 当前允许的演进方向

在不打破边界的前提下，当前仍然允许继续演进：

- benchmark workflow 内部的性能回归验证方式；
- benchmark artifact 的组织结构；
- report 层未来是否接入 Pages 或其他展示方式；
- release 设计的前期方案讨论。

但以下做法当前应避免：

- 把 report 发布逻辑直接塞回 `benchmark-full.yml`；
- 把 Godot 导出流程塞进 PR Gate 或主干验证；
- 为了图省事，让一个 workflow 同时承担 benchmark、report、release 三类职责。

---

## 收口标准

当看到以下状态时，可以认为当前阶段的边界治理已经完成：

- workflow 分层与文档定义一致；
- benchmark workflow 不再暗含发布职责；
- CI 架构文档、优化文档、边界文档三者结论一致；
- Godot 自动发布被明确记录为独立议题，而不是当前 CI 优化中的未完成项。
