# Lattice 兼容面清单

> 当前主干中的兼容 API 不再承担运行时热路径职责。
> 本文用于把“哪些兼容面仍保留、仓内哪里还在用、下一轮继续收口前提是什么”写成可核对清单。

---

## 目标

本清单只回答 3 个问题：

1. 当前仍保留了哪些兼容 API
2. 这些兼容 API 在仓内还有哪些真实使用面
3. 未来若继续缩减公开面，需要先满足什么条件

---

## 当前保留的兼容 API

### 1. `FrameSnapshot`

当前入口：

- `FrameSnapshot`
- `Frame.CreateSnapshot()`
- `Frame.RestoreFromSnapshot()`

当前状态：

- 已标记 `Obsolete`
- 已标记 `EditorBrowsable(Never)`
- 不再作为 `Session` checkpoint / sampled history / checksum 主路径

仍保留原因：

- 对象图断言依然直观
- 显式恢复测试依然需要
- 排查单个组件存储的 snapshot restore 问题时更容易定位

### 2. `Filter<T...>`

当前入口：

- `Filter<T>`
- `Filter<T1, T2>`
- `Filter<T1, T2, T3>`

当前状态：

- 已标记 `Obsolete`
- 已标记 `EditorBrowsable(Never)`
- 新系统默认入口已经统一为 `Frame.Query<T...>()`

仍保留原因：

- 给旧调用方留迁移窗口
- 避免一次性破坏仍依赖旧遍历接口的外部代码

### 3. `FilterOptimized<T...>`

当前入口：

- `FilterOptimized<T>`
- `FilterOptimized<T1, T2>`

当前状态：

- 已标记 `Obsolete`
- 已标记 `EditorBrowsable(Never)`
- 新系统默认不再使用该接口承载主路径优化

仍保留原因：

- 给旧裁剪遍历代码留迁移窗口
- 避免在当前阶段做不必要的大范围破坏式重构

### 4. 运行时兼容别名

当前入口：

- `Session`
- `SessionRunner.Session`
- `SessionRunnerBuilder.WithSessionFactory(...)`
- `SessionRunnerBuilder.BuildSession()`

当前状态：

- 已标记 `Obsolete`
- 已标记 `EditorBrowsable(Never)`
- 新代码默认入口已经统一为：
  - `MinimalPredictionSession`
  - `SessionRunner.Runtime`
  - `SessionRunnerBuilder.WithRuntimeFactory(...)`
  - `SessionRunnerBuilder.BuildRuntime()`

仍保留原因：

- 给仓内外旧调用面保留迁移窗口
- 避免在当前阶段因命名收口引入不必要的大范围破坏

---

## 仓内真实使用面（2026-03-26）

### 生产代码

`FrameSnapshot`：

- 仅保留定义与兼容入口实现：
  - `ECS/Core/FrameSnapshot.cs`
  - `ECS/Core/Frame.cs`

`Filter<T...>`：

- 仅保留定义文件：
  - `ECS/Core/Filter.cs`

`FilterOptimized<T...>`：

- 仅保留定义文件：
  - `ECS/Framework/FilterOptimized.cs`

结论：

- 当前仓内生产代码已经不再依赖这些兼容 API 参与主链路运行。
- 它们的存在形态已经从“仍被主线使用”收缩为“仅保留定义与兼容实现”。

### 测试代码

`FrameSnapshot` 当前仍在以下测试中被显式使用：

- `Tests/ECS/ComponentIntegrationTests.cs`
- `Tests/ECS/SessionTests.cs`
- `Tests/ECS/CompatibilitySurfaceTests.cs`

用途：

- 显式恢复测试
- checkpoint / checksum 对照测试
- 兼容面标记测试

`Filter<T...>` / `FilterOptimized<T...>` 当前仅在以下测试中被反射引用：

- `Tests/ECS/CompatibilitySurfaceTests.cs`

用途：

- 校验兼容面仍保持 `Obsolete + EditorBrowsable(Never)`

运行时兼容别名当前仍在以下位置出现：

- `Tests/ECS/SessionRunnerBuilderTests.cs`
- `Tests/ECS/CompatibilitySurfaceTests.cs`

用途：

- `Session` 兼容别名回归
- 校验运行时兼容入口仍保持 `Obsolete + EditorBrowsable(Never)`

### 文档代码块与历史归档

仍然出现旧术语或旧架构口径的文档：

- `ECS/Architecture.md`
- `ECS/Core/README.md`
- `ECS/PERFORMANCE_OPTIMIZATION.md`
- `ECS/Core/PHASE3_SUMMARY.md`

当前处理方式：

- 全部已改成“历史归档文档”口径
- 不再作为当前主干实现说明

---

## 明确禁止的新用法

- 新的 checkpoint / history / rollback 路径不得重新引入 `FrameSnapshot`
- 新系统不得再以 `Filter<T...>` / `FilterOptimized<T...>` 作为默认入口
- 新 benchmark 结论不得建立在兼容 API 主路径上
- 新运行时代码不得再默认使用 `Session`、`runner.Session`、`WithSessionFactory(...)`、`BuildSession()`
- 新文档不得把 `World / SystemBase / SystemGroup / StateSnapshot` 写成当前现成能力

---

## 下一轮继续收口前的前置条件

- 已清点仓外调用面，确认外部项目是否仍依赖这些兼容 API
- `PackedFrameSnapshot` 已覆盖对象图以外的实际主用途
- `Frame.Query<T...>()` 已覆盖现存系统默认遍历入口
- 调试断言和显式恢复测试若要迁移，已有等价替代方案

---

## 当前结论

截至 2026-03-26：

- 兼容 API 已经从主链路中退出
- 仓内生产代码已无新的兼容 API 消费点
- 兼容面当前主要只剩定义保留、测试对照与历史归档说明

这意味着 Iteration 5 已经不只是“写清楚边界”，而是形成了：

- 兼容清单
- 仓内 usage inventory
- 可回归验证的仓库级约束
