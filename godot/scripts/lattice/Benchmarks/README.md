# Lattice Benchmarks

用于运行 Lattice 的 BenchmarkDotNet 基线套件。

当前已接入：

- `SessionRuntimeBenchmarks`
- `SessionRuntimeBenchmarkGovernance`

推荐命令：

```bash
dotnet run --configuration Release --project godot/scripts/lattice/Benchmarks/Lattice.RuntimeBenchmarks.csproj -- --filter "*SessionRuntimeBenchmarks*"
```

运行正式治理校验：

```bash
dotnet run --configuration Release --project godot/scripts/lattice/Benchmarks/Lattice.RuntimeBenchmarks.csproj -- --govern
```

如果已经有 BenchmarkDotNet 导出的 CSV 报告，也可以只做门槛校验：

```bash
dotnet run --configuration Release --project godot/scripts/lattice/Benchmarks/Lattice.RuntimeBenchmarks.csproj -- --govern-report BenchmarkDotNet.Artifacts/results/Lattice.Tests.Performance.SessionRuntimeBenchmarks-report.csv
```

当前 `SessionRuntimeBenchmarks` 的参数维度：

- `EntityCount = 64 / 256 / 1024`
- `PayloadProfile = RawDense / MixedSerialized / CombatMixed`

基线关注项：

- `Update`
- `CreateCheckpoint`
- `RestoreCheckpoint`
- `Rollback + Resimulate`
- `Frame Checksum`
- `Historical Rebuild From Sampled Snapshot`
- `Historical Cold Materialize`
- `Historical Sequential Read 13-15`
- `Historical Cross-Anchor Read 11-14`
- `Rollback Storm Window x4`
- `Checkpoint Save-Like Chain x16`
- `Historical Replay Scrub 9-24 x16`

当前正式纳入治理的场景：

- `gameplay_256_mixed`
  - `EntityCount = 256`
  - `PayloadProfile = MixedSerialized`
  - 作为更贴近真实玩法的中等负载画像，守住 update / checkpoint / restore / rollback / history
- `stress_1024_mixed`
  - `EntityCount = 1024`
  - `PayloadProfile = MixedSerialized`
  - 作为 MixedSerialized 坏场景长期基线，重点防止 checkpoint / rollback / restore / history 重路径慢慢回退
- `stress_1024_raw_history`
  - `EntityCount = 1024`
  - `PayloadProfile = RawDense`
  - 用于守住非序列化主路径的 restore / rollback / history 基线，避免性能治理只盯 MixedSerialized
- `gameplay_256_combat_mixed`
  - `EntityCount = 256`
  - `PayloadProfile = CombatMixed`
  - 用于守住更贴近真实玩法的 combat update / rollback / history 成本
- `gameplay_1024_combat_mixed`
  - `EntityCount = 1024`
  - `PayloadProfile = CombatMixed`
  - 用于守住中大型战斗密度下的主玩法成本基线
- `checkpoint_heavy_1024_combat_save_like`
  - `EntityCount = 1024`
  - `PayloadProfile = CombatMixed`
  - 用于守住高频 checkpoint / restore 的 save-like 压力路径
- `history_read_1024_combat_replay`
  - `EntityCount = 1024`
  - `PayloadProfile = CombatMixed`
  - 用于守住 replay-like 的历史读取 / scrub 成本
- `rollback_storm_256_combat_window`
  - `EntityCount = 256`
  - `PayloadProfile = CombatMixed`
  - 用于守住连续迟到输入窗口下的 rollback storm 路径

治理策略来源：

- 正式预算定义在 `Tests/Performance/SessionRuntimeBenchmarkGovernance.cs`
- 当前 policy 版本：`v4`
- 最近更新时间：`2026-03-26`

2026-03-25 最新观察：

- `TryMaterializeHistoricalFrame()` 已补“原地前推 replay”快路径；在 `AdvanceFrame()` 未被 override 的 Session 上，冷历史回放不再每 Tick 做整帧 `CopyStateFrom()` + recycle。
- 历史路径新基线（`x32`）大致落到：
  - `Historical Cold Materialize`: `225-717 us`
  - `Historical Sequential Read 13-15`: `844-1333 us`
  - `Historical Cross-Anchor Read 11-14`: `1164-1564 us`
- 当前历史路径的框架性额外成本已经明显下降，下一阶段更值得关注 `Rollback + Resimulate` 与 `Restore Checkpoint` 这两条重路径，而不是继续抠 snapshot anchor 查找。

2026-03-26 治理更新：

- benchmark 已不再只是手工观察入口，当前已有正式预算与 pass/fail 校验
- 当前治理重点已扩到：
  - 更贴近真实玩法的 `CombatMixed`
  - MixedSerialized / RawDense 的长期坏场景基线
  - `restore / rollback / history` 的长期守门
  - save-like 的 checkpoint 链
  - replay-like 的历史 scrub
- `BenchmarkGovernanceCli` 的参数解析与 `--govern-report` 成功/失败路径现在也已有自动化测试，不再只靠手工命令冒烟验证
