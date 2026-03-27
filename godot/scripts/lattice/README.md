# Lattice

Lattice 是 Sideline 项目中孵化的自研确定性 ECS 运行时，目标是为独立游戏提供一套可测试、可回滚、可逐步扩展的纯 C# 模拟底座。

当前主干已经不再是“只有 ECS 基座”，而是具备了一个最小可运行闭环：

- `FP`、`Entity`、`Component`、`Frame` 已进入稳定可用阶段
- `ISystem`、`SystemScheduler`、`SessionRuntime / MinimalPredictionSession / LocalAuthoritativeSession` 已进入正式编译面
- `SessionRunner`、`SessionRunnerBuilder` 已提供最小装配与驱动入口
- 已支持输入、历史帧、检查点、最小回滚与重模拟
- 已有 `MovementSystem`、`LifetimeSystem`、`SpawnerSystem` 三条真实样例链路
- 已有 `System + SessionRuntime` 端到端联调测试与一组固定回归入口

权威设计说明文档：

- `godot/scripts/lattice/ECS/Framework/SystemDesignNotes.md`
- `godot/scripts/lattice/ECS/Framework/CoreRuntimeGuardrails.md`

如果需要了解当前真实能力，请优先看上面的设计文档和 `Tests/ECS` 下的测试，而不是参考历史草图。

## 当前状态（2026-03-27）

当前实现已经不再处于“刚跨过 7 分”的阶段，而是完成了 8.5 分目标中的主干制度化收口，重点已经从“能不能跑起来”转向：

- 运行时可靠性
- 装配成本
- 历史帧与回滚成本
- 文档与实现一致性
- determinism / 协议 / benchmark 治理长期稳定性

本轮已完成的关键收口：

- `Session` 历史帧管理已改为有界 O(1) 按 Tick 访问
- 历史帧替换与淘汰时会及时释放脱离引用的 `Frame`
- 已完成 `SessionRuntime / MinimalPredictionSession / LocalAuthoritativeSession` 三层运行时分层
- 已完成 `SystemAuthoringContract`、轻量 `SystemPhase` 与 determinism analyzer 主干收口
- 已完成输入 payload / checkpoint / packed snapshot / component schema 的第一轮正式版本治理
- 已完成 `SessionRunner` 生命周期模型、失败收敛与 runtime shared service 宿主挂接
- `SessionRuntimeBenchmarks` 已从“观察项”升级为“治理项”，当前 policy 为 `v4`
- benchmark 已补正式 CLI 入口 `--govern / --govern-report` 与自动化测试覆盖
- README 已移除旧 `World / SystemBase / SystemGroup / StateSnapshot` 架构描述，并同步当前 benchmark / runtime 边界

## 最小运行时用法

```csharp
using Lattice.ECS.Framework.Systems;
using Lattice.ECS.Session;
using Lattice.Math;

var options = new SessionRuntimeOptions(FP.One, localPlayerId: 0);

using var runner = new SessionRunnerBuilder()
    .WithRuntimeOptions(options)
    .AddSystem(new MovementSystem())
    .AddSystem(new LifetimeSystem())
    .Build();

runner.Start();
runner.Step();
runner.Stop();
```

如果你需要更贴近玩法的链路，可以参考：

- `Tests/ECS/SystemSessionIntegrationTests.cs`
- `Tests/ECS/SpawnerSystemIntegrationTests.cs`

## 当前主干的真实模块划分

```text
lattice/
├── Core/                   # 基础设施与通用类型
├── Math/                   # 定点数与确定性数学
├── Collections/            # 确定性集合
├── ECS/
│   ├── Core/               # Frame / Entity / Storage / Query / Snapshot
│   ├── Framework/          # ISystem / SystemScheduler / Gameplay sample systems
│   └── Session/            # SessionRuntime / MinimalPredictionSession / LocalAuthoritativeSession / SessionRunner / SessionRunnerBuilder
└── Tests/
    ├── ECS/                # 运行时与联调测试
    └── Performance/        # BenchmarkDotNet 与性能烟雾测试
```

当前主干中正式存在且可用的运行时骨架是：

- `Frame`
- `ISystem`
- `SystemScheduler`
- `SessionRuntime`
- `MinimalPredictionSession`
- `LocalAuthoritativeSession`
- `SessionRuntimeDataBoundary`
- `SessionRuntimeOptions`
- `SessionRunnerDefinition`
- `SessionRunner`
- `SessionRunnerBuilder`

当前主干中保留但不推荐作为新代码默认入口的兼容 API 是：

- `Session`
- `SessionRunner.Session`
- `SessionRunnerBuilder.WithSessionFactory(...)`
- `SessionRunnerBuilder.BuildSession()`

当前主干中没有正式实现，不应再按“现成能力”理解的内容是：

- `World`
- `SystemBase`
- `SystemGroup`
- `StateSnapshot`

这些名词只属于历史设计阶段，不代表当前编译中的主干实现。

代码层面上，系统层当前的正式边界也有显式入口：

- `SystemScheduler.Boundary`
- `SystemSchedulerKind.FlatPhasedOrdered`
- `SystemSchedulerCapability`
- `UnsupportedSystemSchedulerCapability`

也就是说，当前系统层支持什么和不支持什么，不再只停留在 README 和设计文档里。

## 当前正式支持的运行模式边界

当前主干正式支持的是两种 runtime 模式：

- `MinimalPredictionSession`
  - 纯 C# 驱动的固定帧模拟
  - 单线程、固定顺序系统调度
  - 基于 `PredictedFrame` 的本地预测推进
  - 基于 `VerifyFrame()` / `RollbackTo()` 的最小验证与回滚修正
  - 基于 `CreateCheckpoint()` / `RestoreFromCheckpoint()` 的显式状态保存与恢复
- `LocalAuthoritativeSession`
  - 纯 C# 驱动的固定帧模拟
  - 单线程、固定顺序系统调度
  - 本地权威前推
  - 显式 checkpoint 保存与恢复
  - 不承诺预测验证与 rewind

当前主干明确不作为正式能力提供的是：

- 完整网络会话管理
- 房间态、玩家映射、传输层
- 多线程系统调度
- 资源化 / 配置化多模式启动
- FrameSync 风格的大型系统家族与产品层会话入口

因此，当前正式运行时应优先理解为 `SessionRuntime / MinimalPredictionSession / LocalAuthoritativeSession`，而不是完整联机框架。

代码层面上，这个边界现在也有显式描述入口：

- `Session.RuntimeBoundary`
- `SessionRuntimeKind.MinimalPrediction`
- `SessionRuntimeKind.LocalAuthoritative`
- `SessionRuntimeCapability`
- `UnsupportedSessionRuntimeCapability`

也就是说，“当前支持什么、不支持什么”不再只停留在 README 和设计文档里。

## 运行时 API 分层

当前主干对运行时 API 的推荐分层是：

- 正式公开 API：
  - `SessionRuntime`
  - `MinimalPredictionSession`
  - `LocalAuthoritativeSession`
  - `SessionRuntimeOptions`
  - `SessionRuntimeContext`
  - `SessionRuntimeContextBoundary`
  - `ISessionRuntimeSharedService`
  - `SessionRuntimeInputBoundary`
  - `SessionInputSet`
  - `IPlayerInput`
  - `IInputPayloadCodec<TInput>`
  - `SessionTickPipelineBoundary`
  - `SessionTickStage`
  - `SessionRunnerBuilder`
  - `SessionRunnerDefinition`
  - `SessionRunner`
  - `SessionCheckpoint`
- 保留兼容 API：
  - `Session`
  - `SessionRunner.Session`
  - `SessionRunnerBuilder.WithSessionFactory(...)`
  - `SessionRunnerBuilder.BuildSession()`
  - `IInputCommand`
- 内部运行时 API：
  - `InputBuffer`
  - 运行时内部历史 / checkpoint / materialize 辅助结构
  - context 绑定、历史 cache 失效等 internal 协调入口

这意味着：

- 新代码默认应面向 `SessionRuntime / MinimalPredictionSession / LocalAuthoritativeSession / SessionRunnerBuilder`
- 运行期共享对象若要进入 `Context`，应显式实现 `ISessionRuntimeSharedService`，并保持为非状态型 runtime service
- 兼容 API 可以继续用来承接旧调用面，但不应再作为新样例、新 benchmark、新玩法入口
- 内部策略对象不再作为对外设计承诺的一部分

## 输入/历史策略边界

当前主干对输入、历史帧和 checkpoint 的稳定公开契约，已通过：

- `SessionRuntime.InputBoundary`
- `SessionRuntime.DataBoundary`

显式暴露到代码层。

当前正式输入语义是：

- 输入主模型是 `IPlayerInput`，而不是把 payload 序列化绑进核心输入对象
- 输入按 `(playerId, tick)` 写入，并在每个 tick 聚合成 `SessionInputSet`
- `SessionInputSet` 只包含实际写入的玩家输入，不会自动补默认输入或沿用上一帧输入
- 同一 `(playerId, tick)` 的重复写入采用 `LatestWriteWins`
- tick 内输入遍历顺序按玩家 ID 升序稳定排列
- 若需要网络 / 文件 / 回放 payload，对接点应放在外层 `IInputPayloadCodec<TInput>`

当前正式数据语义是：

- 输入策略：按 `(playerId, tick)` 读写，属于固定窗口保留
- 历史策略：按 tick 读取，live history 不足时可由 sampled snapshot 按需重建
- checkpoint 策略：显式创建与恢复，主路径格式是 packed snapshot

当前明确不作为稳定公开 API 承诺的是：

- 缺失输入自动沿用上一帧
- 内建默认输入合成
- 内建网络传输序列化
- 输入窗口大小
- 历史窗口大小
- sampled snapshot 采样间隔
- materialize cache 大小
- 可插拔 history store 或替代 checkpoint 格式

这些参数现在属于内部实现调优，而不是对外设计承诺。也就是说，外部代码应依赖“能做什么”，而不是依赖当前默认 sizing 数值。

## Tick 管线与结构提交边界

当前主干对 Tick 生命周期与结构性修改可见性的稳定公开契约，已通过：

- `SessionRuntime.TickPipeline`
- `SessionRuntime.CurrentTickStage`

显式暴露到代码层。

当前正式语义是：

- Tick 按 `InputApply -> Simulation -> StructuralCommit -> Cleanup -> HistoryCapture` 顺序推进
- `Simulation` 阶段允许直接修改已存在组件的数据字段
- 实体创建/销毁、组件增删等结构性修改会先进入延迟提交缓冲
- 结构性修改在 `StructuralCommit` 阶段统一生效，而不是在后续系统中立即可见

当前明确不作为稳定公开 API 承诺的是：

- 在 `Simulation` 阶段立即看到同 Tick 的结构性修改
- 运行期自由重排 Tick 阶段
- 每个系统拥有独立结构提交阶段

这意味着：Lattice 当前已经把“结构性修改何时生效”和“系统按什么轻量 phase 运行”写成正式运行时规则，但仍没有继续扩成更重的 `SystemGroup` / 依赖图 / 系统家族。

## 兼容 API 边界

当前仍保留、但不属于主链路的兼容面主要有：

- `FrameSnapshot`
- `Frame.CreateSnapshot() / RestoreFromSnapshot()`
- `Filter<T...>`
- `FilterOptimized<T...>`

这些 API 仍保留的原因：

- `FrameSnapshot` 仍适合对象图断言、显式恢复测试、单个组件存储恢复问题排查
- `Filter<T...>` 与 `FilterOptimized<T...>` 仍给旧调用方留出迁移窗口，避免一次性大拆

这些 API 不应进入的新用法：

- 新的 checkpoint、history、rollback 路径不要再基于 `FrameSnapshot`
- 新系统默认不要再写成 `Filter<T...>` / `FilterOptimized<T...>`，统一改走 `Frame.Query<T...>()`
- 新的性能优化与 benchmark 不再围绕兼容 API 做主路径结论

未来继续收口前，需要先满足的前置条件：

- 兼容 API 在主仓库和玩法层的实际使用面已清点清楚
- `PackedFrameSnapshot` 和 `Query<T...>()` 已覆盖现存主用途
- 显式恢复测试、调试断言与迁移样例已有稳定替代说明

当前实现中，上述兼容 API 都应视为“可用但非推荐”，不会作为后续主线能力继续扩展。

带 usage inventory 的兼容面清单见：

- `godot/scripts/lattice/ECS/CompatibilityInventory.md`

## 已完成的最小闭环能力

- 创建 `SessionRuntime` / `MinimalPredictionSession` 并驱动固定帧更新
- 注册多个系统并按固定顺序执行
- 创建实体、添加组件、执行查询与样例系统逻辑
- 使用 `Frame.Query<T1, T2, T3, T4>()` 表达常见多组件玩法查询
- 使用 `Frame.SetGlobal<T>() / GetGlobal<T>() / RemoveGlobal<T>()` 访问正式全局状态
- 保存历史帧并按 Tick 读取
- 创建检查点并恢复
- 校验失败后回滚并重模拟
- 用 `SessionRunnerBuilder` 进行代码侧最小装配

## 当前仍然保守的地方

Lattice 现在可以跑，但还没有进入“极限优化”阶段。当前仍然保守的点主要有：

- 帧推进、回滚起点和历史补帧已切到 direct state copy
- checkpoint 与 sampled history 已切到 packed snapshot，但公开 `FrameSnapshot` 兼容 API 仍然保留
- 运行配置对象当前只公开 `DeltaTime / LocalPlayerId`，不提前暴露历史窗口与采样策略
- 运行时仍以单线程和固定顺序为主
- 系统层当前只支持轻量 phase，不包含系统树、依赖排序、多线程系统族
- `Frame.Query` 当前正式强类型上限收口到 4 组件，更高维组合仍建议显式拆系统或配合 `MatchesFilter(...)`
- 装配层仍是代码驱动，尚未引入更完整的资源化配置

这不是 bug，而是当前阶段的刻意选择：先把正确性、边界和可维护性收紧，再继续往玩法和更深优化推进。

## 主要测试入口

功能与回归测试：

- `Tests/ECS/SessionTests.cs`
- `Tests/ECS/SystemSchedulerTests.cs`
- `Tests/ECS/SessionRunnerTests.cs`
- `Tests/ECS/SessionRunnerBuilderTests.cs`
- `Tests/ECS/SystemSessionIntegrationTests.cs`
- `Tests/ECS/SpawnerSystemIntegrationTests.cs`
- `Tests/ECS/Runtime85RegressionTests.cs`

性能与治理入口：

- `Tests/Performance/SessionRuntimeBenchmarks.cs`
- `Tests/Performance/SessionRuntimeBenchmarkGovernance.cs`
- `Tests/Performance/BenchmarkGovernanceCliTests.cs`
- `Tests/Performance/FPBenchmarks.cs`
- `Tests/Performance/StorageBenchmark.cs`
- `Benchmarks/Lattice.RuntimeBenchmarks.csproj`

运行 Session 基线 benchmark：

```bash
dotnet run --configuration Release --project godot/scripts/lattice/Benchmarks/Lattice.RuntimeBenchmarks.csproj -- --filter "*SessionRuntimeBenchmarks*"
```

运行 Session benchmark 治理校验：

```bash
dotnet run --configuration Release --project godot/scripts/lattice/Benchmarks/Lattice.RuntimeBenchmarks.csproj -- --govern
```

如果已经有 BenchmarkDotNet CSV 报告，也可以直接校验：

```bash
dotnet run --configuration Release --project godot/scripts/lattice/Benchmarks/Lattice.RuntimeBenchmarks.csproj -- --govern-report BenchmarkDotNet.Artifacts/results/Lattice.Tests.Performance.SessionRuntimeBenchmarks-report.csv
```

## 构建与验证

```bash
dotnet build godot/Sideline.sln -nologo
dotnet test godot/scripts/lattice/Tests/Lattice.Tests.csproj -nologo
```

## 下一步关注点

如果目标是让 Lattice 继续支撑更真实、更复杂的玩法负载，而不是继续修改底层设计，下一阶段最值得投入的方向是：

- 继续清点兼容 API 的真实使用面，并决定下一轮是否可以继续缩小保留范围
- 在正式 benchmark 治理已经到位的前提下，继续补真实玩法级 soak / stress / replay-like 验证
- 持续压缩文档与实现之间的版本差
- 把当前制度化边界继续带到更复杂的产品层场景里验证
- 用真实玩法链路继续验证回滚和装配边界
