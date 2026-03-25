# Lattice Benchmarks

用于运行 Lattice 的 BenchmarkDotNet 基线套件。

当前已接入：

- `SessionRuntimeBenchmarks`

推荐命令：

```bash
dotnet run --configuration Release --project godot/scripts/lattice/Benchmarks/Lattice.RuntimeBenchmarks.csproj -- --filter "*SessionRuntimeBenchmarks*"
```

当前 `SessionRuntimeBenchmarks` 的参数维度：

- `EntityCount = 64 / 256 / 1024`
- `PayloadProfile = RawDense / MixedSerialized`

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

2026-03-25 最新观察：

- `TryMaterializeHistoricalFrame()` 已补“原地前推 replay”快路径；在 `AdvanceFrame()` 未被 override 的 Session 上，冷历史回放不再每 Tick 做整帧 `CopyStateFrom()` + recycle。
- 历史路径新基线（`x32`）大致落到：
  - `Historical Cold Materialize`: `225-717 us`
  - `Historical Sequential Read 13-15`: `844-1333 us`
  - `Historical Cross-Anchor Read 11-14`: `1164-1564 us`
- 当前历史路径的框架性额外成本已经明显下降，下一阶段更值得关注 `Rollback + Resimulate` 与 `Restore Checkpoint` 这两条重路径，而不是继续抠 snapshot anchor 查找。
