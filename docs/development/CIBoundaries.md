# CI 边界约束

> 本文档用于把 Sideline 当前 CI 中已经确认的职责边界写成明确约束，避免 benchmark、report、release 再次混回同一条流程。它描述的是当前仓库应遵守的边界，而不是未来草图。

---

## 结论

当前仓库中的相关职责已经按下面方式固定：

- `PR Gate` 只负责面向 `main` 的合并门禁。
- `Main Validation` 只负责合入后的主干质量验证。
- `Full Benchmark` 只负责性能基准测试和结果归档。
- 报告展示属于站点内容层，不属于 benchmark 执行层。
- Godot 自动发布已经以**独立 release workflow** 的方式落地，不进入 PR Gate、Main Validation 或 Benchmark。

这意味着：当前 CI 已经形成“验证层、分析层、发布层”三种明确语义，而不是继续把所有职责塞进同一条 workflow。

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

---

## Report 边界

报告层当前只表示“结果展示与归档语义”，不等同于 benchmark 执行。

在当前仓库里：

- benchmark workflow 负责产出原始结果和索引素材；
- `docs/` 下的网站内容负责展示；
- 是否将 benchmark 结果自动同步到站点，仍然属于独立能力，而不是 benchmark workflow 的职责扩张。

---

## Release 边界

当前正式发布层已经落地，但它仍然必须与现有验证流程保持严格分离。

当前发布相关文件：

- `.github/workflows/godot-release-artifact.yml`
- `.github/workflows/godot-windows-release.yml`

它们的职责如下：

- `godot-release-artifact.yml`
  只负责手动验证 `Windows x64 -> export -> package -> artifact`
- `godot-windows-release.yml`
  只负责 `push tag -> export -> package -> artifact -> GitHub Release`

它们不负责：

- PR 合并门禁
- main 主干质量验证
- benchmark 结果产出或展示

同样地，现有验证 workflow 也不应代偿 release 职责。

---

## 当前允许的演进方向

在不打破边界的前提下，当前仍然允许继续演进：

- benchmark workflow 内部的性能回归验证方式；
- benchmark artifact 的组织结构；
- report 层未来是否接入 Pages 或其他展示方式；
- Godot release workflow 自身的导出缓存、Smoke Test 与 Release notes 能力。

但以下做法当前应避免：

- 把 report 发布逻辑直接塞回 `benchmark-full.yml`；
- 把 Godot 导出流程塞进 PR Gate 或主干验证；
- 为了图省事，让一个 workflow 同时承担 benchmark、report、release 三类职责。

---

## 收口标准

当看到以下状态时，可以认为当前阶段的边界治理已经完成：

- workflow 分层与文档定义一致；
- benchmark workflow 不暗含发布职责；
- release workflow 不承担 PR 门禁或 benchmark 职责；
- CI 架构文档、优化文档、边界文档与当前实现结论一致。
