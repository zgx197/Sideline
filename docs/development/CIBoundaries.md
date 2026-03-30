# CI 边界约束

> 本文档用于把 Sideline 当前 CI 中已经确认的职责边界写成明确约束，避免 validation、analysis、site、release 再次混回同一条流程。它描述的是当前仓库应遵守的边界，而不是未来草图。

---

## 结论

当前仓库中的相关职责已经按下面方式固定：

- `[CI] PR Gate` 只负责面向 `main` 的合并门禁。
- `[CI] Common Build`、`[CI] Facet Validation`、`[CI] Lattice Validation` 只负责日常验证层。
- `[Analysis] [Weekly] Full Coverage` 只负责完整覆盖率分析与结果归档。
- `[Analysis] [Weekly] Full Benchmark` 只负责完整性能基准测试与结果归档。
- `[Site] Reports Pages` 只负责把最近一次成功的分析结果发布到 GitHub Pages。
- `[Release] [Manual] Godot Artifact Validate` 与 `[Release] Godot Windows x64 Publish` 只负责发布层。

这意味着：当前 CI 已经形成“验证层、分析层、站点层、发布层”四种明确语义，而不是继续把所有职责塞进同一条 workflow。

---

## Validation 边界

当前验证层文件：

- `.github/workflows/ci-pr.yml`
- `.github/workflows/ci.yml`
- `.github/workflows/ci-facet.yml`
- `.github/workflows/ci-lattice.yml`

它们的职责如下：

- `[CI] PR Gate`
  只负责 `pull_request -> main` 的稳定门禁输出。
- `[CI] Common Build`
  只负责最低成本的 Sideline 主工程健康检查。
- `[CI] Facet Validation`
  只负责 Facet 核心库与主工程集成层验证。
- `[CI] Lattice Validation`
  只负责 Lattice 的格式、构建测试、确定性、分析器与 benchmark smoke。

它们不负责：

- 完整覆盖率报告
- 完整性能基准报告
- GitHub Pages 报告发布
- Godot 导出与正式发布

---

## Analysis 边界

当前分析层文件：

- `.github/workflows/full-coverage.yml`
- `.github/workflows/benchmark-full.yml`

它们的职责如下：

- `[Analysis] [Weekly] Full Coverage`
  运行完整覆盖率分析，生成 HTML 覆盖率报告并上传 artifact。
- `[Analysis] [Weekly] Full Benchmark`
  运行完整 BenchmarkDotNet 基准测试，校验结果后上传 artifact。

它们不负责：

- 触发 PR required checks
- 代偿日常主线验证
- 直接发布 GitHub Pages
- 生成游戏发行包

---

## Site 边界

当前站点层文件：

- `.github/workflows/reports-pages.yml`

它的职责如下：

- 下载最近一次成功的完整 coverage / benchmark artifact
- 组装 `docs/` 站点内容与报告落地目录
- 构建并部署 GitHub Pages

它不负责：

- 自己运行 coverage 或 benchmark
- 修改仓库中的 `docs/reports/**` 提交记录
- 参与 PR 门禁
- 生成 Godot 发布产物

---

## Release 边界

当前发布层文件：

- `.github/workflows/godot-release-artifact.yml`
- `.github/workflows/godot-windows-release.yml`

它们的职责如下：

- `[Release] [Manual] Godot Artifact Validate`
  只负责手动验证 `Windows x64 -> export -> package -> artifact`
- `[Release] Godot Windows x64 Publish`
  只负责 `push tag -> export -> package -> artifact -> GitHub Release`

它们不负责：

- PR 合并门禁
- 日常主干质量验证
- 完整 coverage / benchmark 分析
- 报告站点发布

---

## 当前允许的演进方向

在不打破边界的前提下，当前仍然允许继续演进：

- validation workflow 内部的缓存、摘要和局部触发策略；
- full coverage / full benchmark artifact 的组织结构；
- reports pages 的展示样式、摘要卡片与历史归档方式；
- Godot release workflow 自身的导出缓存、Smoke Test 与 Release notes 能力。

但以下做法当前应避免：

- 把 report 发布逻辑直接塞回 `full-coverage.yml` 或 `benchmark-full.yml`；
- 把完整覆盖率或完整 benchmark 重新塞回日常验证层；
- 把 Godot 导出流程塞进 PR Gate 或主干验证；
- 为了图省事，让一个 workflow 同时承担 analysis、site、release 三类职责。

---

## 收口标准

当看到以下状态时，可以认为当前阶段的边界治理已经完成：

- workflow 分层与文档定义一致；
- validation workflow 不代偿 analysis 或 release 职责；
- analysis workflow 不暗含站点发布职责；
- site workflow 只负责展示层整合与部署；
- release workflow 不承担 PR 门禁或 benchmark / coverage 职责。
