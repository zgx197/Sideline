# Lattice

Lattice 是 Sideline 项目中孵化的自研确定性 ECS 运行时，目标是为独立游戏提供一套可测试、可回滚、可逐步扩展的纯 C# 模拟底座。

当前主干已经不再是“只有 ECS 基座”，而是具备了一个最小可运行闭环：

- `FP`、`Entity`、`Component`、`Frame` 已进入稳定可用阶段
- `ISystem`、`SystemScheduler`、`Session` 已进入正式编译面
- `SessionRunner`、`SessionRunnerBuilder` 已提供最小装配与驱动入口
- 已支持输入、历史帧、检查点、最小回滚与重模拟
- 已有 `MovementSystem`、`LifetimeSystem`、`SpawnerSystem` 三条真实样例链路
- 已有 `System + Session` 端到端联调测试与一组固定回归入口

权威设计说明文档：

- `godot/scripts/lattice/ECS/Framework/SystemDesignNotes.md`

如果需要了解当前真实能力，请优先看上面的设计文档和 `Tests/ECS` 下的测试，而不是参考历史草图。

## 当前状态（2026-03-25）

当前实现更接近“7 分以上、正在向 8.5 分补强”的阶段，重点已经从“能不能跑起来”转向：

- 运行时可靠性
- 装配成本
- 历史帧与回滚成本
- 文档与实现一致性

本轮已完成的关键收口：

- `Session` 历史帧管理已改为有界 O(1) 按 Tick 访问
- 历史帧替换与淘汰时会及时释放脱离引用的 `Frame`
- 已新增 `SessionRuntimeBenchmarks` 用于观察 `Update / Checkpoint / Rollback` 成本
- README 已移除旧 `World / SystemBase / SystemGroup / StateSnapshot` 架构描述

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
│   └── Session/            # Session / SessionRunner / SessionRunnerBuilder
└── Tests/
    ├── ECS/                # 运行时与联调测试
    └── Performance/        # BenchmarkDotNet 与性能烟雾测试
```

当前主干中正式存在且可用的运行时骨架是：

- `Frame`
- `ISystem`
- `SystemScheduler`
- `Session`
- `SessionRuntimeOptions`
- `SessionRunner`
- `SessionRunnerBuilder`

当前主干中没有正式实现，不应再按“现成能力”理解的内容是：

- `World`
- `SystemBase`
- `SystemGroup`
- `StateSnapshot`

这些名词只属于历史设计阶段，不代表当前编译中的主干实现。

## 当前正式支持的运行模式边界

当前主干正式支持的是：

- 纯 C# 驱动的固定帧模拟
- 单线程、固定顺序系统调度
- 基于 `PredictedFrame` 的本地预测推进
- 基于 `VerifyFrame()` / `RollbackTo()` 的最小验证与回滚修正
- 基于 `CreateCheckpoint()` / `RestoreFromCheckpoint()` 的显式状态保存与恢复

当前主干明确不作为正式能力提供的是：

- 完整网络会话管理
- 房间态、玩家映射、传输层
- 多线程系统调度
- 资源化 / 配置化多模式启动
- FrameSync 风格的大型系统家族与产品层会话入口

因此，当前 `Session` 应理解为“最小模拟运行时”，而不是完整联机框架。

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

## 已完成的最小闭环能力

- 创建 `Session` 并驱动固定帧更新
- 注册多个系统并按固定顺序执行
- 创建实体、添加组件、执行查询与样例系统逻辑
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
- 系统层仍是 MVP，不包含系统树、依赖排序、多线程系统族
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

性能观察入口：

- `Tests/Performance/SessionRuntimeBenchmarks.cs`
- `Tests/Performance/FPBenchmarks.cs`
- `Tests/Performance/StorageBenchmark.cs`
- `Benchmarks/Lattice.RuntimeBenchmarks.csproj`

运行 Session 基线 benchmark：

```bash
dotnet run --configuration Release --project godot/scripts/lattice/Benchmarks/Lattice.RuntimeBenchmarks.csproj -- --filter "*SessionRuntimeBenchmarks*"
```

## 构建与验证

```bash
dotnet build godot/Sideline.sln -nologo
dotnet test godot/scripts/lattice/Tests/Lattice.Tests.csproj -nologo
```

## 下一步关注点

如果目标是把运行闭环从当前水平继续推向更稳的 8.5 分，下一阶段最值得投入的方向仍然是：

- 继续清点兼容 API 的真实使用面，并决定下一轮是否可以继续缩小保留范围
- 建立更明确的运行时成本基线
- 持续压缩文档与实现之间的版本差
- 用真实玩法链路继续验证回滚和装配边界
