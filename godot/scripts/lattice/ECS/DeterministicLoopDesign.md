# Lattice ECS 确定性循环设计（Phase D7 已完成）

## 文档目的

本文档用于正式定义 Lattice 当前阶段的“确定性循环”设计边界。

这里的重点不是把完整联机回滚框架、完整 replay 体系或复杂历史缓存一次性写完，而是先把当前代码库里已经成立的最小确定性闭环写清楚：

- `FrameSnapshot` 的正式定位是什么
- `CalculateChecksum` 与 `DeterministicHash` 当前如何参与一致性验证
- verified / predicted 输入分层的正式语义是什么
- 当前已经具备的最小 resim 能力是什么
- 当前 rollback 还没有做到哪一步

本文档直接建立在下面几份底座文档之上：

- [FrameStorageDesign.md](/d:/UGit/Sideline/godot/scripts/lattice/ECS/FrameStorageDesign.md)
- [CommandBufferDesign.md](/d:/UGit/Sideline/godot/scripts/lattice/ECS/CommandBufferDesign.md)
- [SimulationSessionDesign.md](/d:/UGit/Sideline/godot/scripts/lattice/ECS/SimulationSessionDesign.md)

---

## 当前定位

在 Lattice 当前底座链路中，确定性循环位于：

```text
Math
  ↓
Component
  ↓
Frame / Storage
  ↓
Query
  ↓
CommandBuffer
  ↓
System
  ↓
SimulationSession
  ↓
Deterministic Loop
```

这里的“Deterministic Loop”不是一个单独类型，而是由下面几层共同构成的正式约束集合：

- 快照
- 克隆
- 校验和
- 输入分层
- verified / predicted 推进规则
- 最小重建预测语义

它当前不负责：

- 网络消息格式
- 多 peer 校验协议
- 完整 rollback 历史窗口
- replay 文件读写
- 运行时可视化诊断工具

---

## 设计目标

### 1. 先验证“同输入同结果”

当前确定性循环最核心的目标不是功能规模，而是：

- 在相同初始状态、相同系统、相同输入序列下
- 得到相同的帧状态与相同校验和

如果这点不成立，后续：

- rollback
- replay
- lockstep

都没有可靠基础。

### 2. 先稳定当前最小 resim 路径

当前代码库已经有一个明确的最小重建预测路径：

- verified 向前推进
- predicted 从最新 verified clone
- 再按可用输入重推到目标 tick

这已经是一种受控的最小重模拟能力。

当前 D7 的任务不是把它夸大成完整 rollback，而是把它正式定义清楚。

### 3. 把“已实现能力”和“未来预留能力”切开

当前已经具备：

- `FrameSnapshot`
- `Frame.Clone()`
- `CalculateChecksum()`
- `SimulationSession<TInput>` 的 verified / predicted 双帧
- predicted 从 verified 重建

当前尚未具备：

- 按 tick 保存多份快照历史
- 在 verified 输入回填后自动回滚并重模拟
- 正式 rollback 窗口管理
- 远端输入冲突处理

因此本文档必须非常克制，只写当前成立的主线。

---

## 当前正式模型

当前最小确定性循环由下面几部分组成：

1. `FrameSnapshot`
2. `Frame.CreateSnapshot()`
3. `Frame.RestoreFromSnapshot()`
4. `Frame.Clone()`
5. `Frame.CalculateChecksum()`
6. `DeterministicHash`
7. `SimulationSession<TInput>` 的 verified / predicted 双帧推进
8. `SimulationSession<TInput>` 的输入缓冲分层

这意味着当前“确定性循环”不是独立 runtime，而是建立在底层快照和 session 主线之上的组合语义。

---

## 快照模型

## 1. `FrameSnapshot` 的正式定位

当前 `FrameSnapshot` 保存：

- `Tick`
- `DeltaTime`
- `EntityCapacity`
- `Data`
- `Checksum`

它的正式定位是：

- 某一时刻单帧状态的完整可恢复表示

它当前不是：

- 差量快照
- 网络包
- replay 文件块
- 调试文本 dump

## 2. `CreateSnapshot()` 的正式语义

当前 `Frame.CreateSnapshot()` 会：

1. 序列化当前帧完整字节数据
2. 用 `DeterministicHash.Fnv1a64` 计算这份字节的 64 位哈希
3. 返回带 `Checksum` 的 `FrameSnapshot`

因此当前快照模型的正式结论是：

- 快照本身就绑定一份对应状态的校验和

## 3. `RestoreFromSnapshot()` 的正式语义

当前 `Frame.RestoreFromSnapshot(snapshot)` 会：

- 校验实体容量一致
- 用快照原始字节完全恢复当前 frame

这意味着当前恢复语义是：

- 全量恢复
- 强一致恢复

而不是“尽量恢复部分字段”。

## 4. `Clone()` 的正式语义

当前 `Frame.Clone()` 的实现就是：

- `CreateSnapshot()`
- 新建同容量 frame
- `RestoreFromSnapshot(snapshot)`

因此 clone 当前不是共享内存，也不是写时复制，而是：

- 基于完整快照恢复出的独立帧副本

---

## 校验和模型

## 1. `CalculateChecksum()` 的正式定位

当前 `Frame.CalculateChecksum()` 会：

1. 再次序列化当前 frame 的完整快照字节
2. 对字节执行 `DeterministicHash.Fnv1a64`
3. 返回 `ulong` 结果

因此当前 checksum 的正式含义是：

- 当前整帧完整状态的确定性摘要

## 2. `FrameSnapshot.Checksum` 与 `CalculateChecksum()` 的关系

`FrameSnapshot.Checksum` 表示：

- 创建该快照时，对快照字节计算出的哈希

`frame.CalculateChecksum()` 表示：

- 对当前 frame 当前状态重新计算出的哈希

如果 restore 语义正确，这两者在快照恢复后应当一致。

现有测试已经把这件事作为正式证据验证。

## 3. `DeterministicHash` 的当前边界

当前 `DeterministicHash` 提供：

- `Fnv1a32`
- `Fnv1a64`
- `EntityRef` 哈希
- 简单组合哈希

在确定性循环主线中，当前真正正式使用的是：

- `Fnv1a64`

因此当前文档结论应当是：

- FNV-1a 64 位是当前整帧状态 checksum 的正式哈希算法

而不是“未来可能随时替换的不重要实现细节”。

---

## 输入分层模型

当前最小确定性循环显式区分两类输入：

- verified 输入
- predicted 输入

这是当前 loop 语义的核心之一。

## 1. verified 输入

verified 输入的正式含义是：

- 已确认、可用于推进权威状态的输入

当前 verified 推进规则是：

- 目标 tick 必须存在 verified 输入
- 缺失则直接失败

因此 verified 主线是严格的、非推测性的。

## 2. predicted 输入

predicted 输入的正式含义是：

- 尚未成为权威输入，但可用于本地继续预测未来 tick 的输入

当前 predicted 推进规则是：

1. 优先使用 verified 输入
2. 其次使用 predicted 输入
3. 再次退回 `default(TInput)`

因此 predicted 主线是允许推测与补默认值的。

## 3. `TickBuffer` 的当前正式边界

当前 session 内部输入缓存是固定容量 ring buffer。

它的正式边界是：

- 提供最小 tick 到输入的存取
- 不保证长期历史保留
- 不提供复杂冲突分析

这意味着当前输入缓存是：

- 最小闭环设施

而不是：

- 已完整实现的 rollback 输入历史系统

---

## verified / predicted 双帧循环

根据 [SimulationSessionDesign.md](/d:/UGit/Sideline/godot/scripts/lattice/ECS/SimulationSessionDesign.md)，当前 session 已形成最小双帧模型：

- `VerifiedFrame`
- `PredictedFrame`

## 1. verified 的正式语义

verified frame 表示：

- 当前权威状态

它只允许：

- 基于 verified 输入向前推进

它当前不允许：

- 回退到更早 tick

## 2. predicted 的正式语义

predicted frame 表示：

- 以最新 verified 为基线推演得到的未来状态

它当前不是：

- 独立于 verified 长期演化的另一棵状态树

## 3. predicted 重建就是当前最小 resim

当前 predicted 推进的正式步骤是：

1. 从最新 verified clone
2. 依次应用后续 tick 输入
3. 重新执行 scheduler 与 command playback
4. 生成新的 predicted frame

这实际上已经构成：

- 最小重模拟语义

只是它当前还没有被扩展为“带历史回退的正式 rollback 系统”。

---

## 单步确定性推进语义

当前无论推进 verified 还是 predicted，单步推进都通过相同流程完成：

1. clone 当前基线 frame
2. 写入目标 tick
3. 写入固定 `DeltaTime`
4. 准备该步的 `CommandBuffer`
5. 应用当前 tick 输入
6. 执行 `SystemScheduler.Update`
7. 执行 `CommandBuffer.Playback`
8. 返回下一帧

这说明当前最小确定性循环的正式链路是：

```text
上一帧状态
  ↓
clone
  ↓
apply input
  ↓
system update
  ↓
command playback
  ↓
下一帧状态
  ↓
checksum / snapshot validation
```

如果这条链路在相同输入下给出不同结果，就说明底座某层失去了确定性。

---

## 当前“rollback / resim”到什么程度

这是 D7 最需要明确写清的一点。

## 1. 当前已经具备的能力

当前已经具备：

- 快照创建
- 快照恢复
- frame clone
- verified / predicted 双帧
- 从最新 verified 重建 predicted
- 用 checksum 验证相同输入序列得到相同结果

因此当前代码库已经拥有：

- 最小 resim 基础设施

## 2. 当前还没有正式具备的能力

当前还没有正式具备：

- 多 tick 快照历史缓存
- 输入回填后自动回退到某个历史 tick
- 从历史 tick 重建 verified / predicted 的统一流程
- rollback 窗口容量与清理策略
- 冲突输入检测策略

因此当前不能把“predicted 从最新 verified 重建”写成完整 rollback。

更准确的表述应是：

- **当前已具备最小重建预测能力，但还没有正式 rollback 历史管理与自动回退机制。**

## 3. 为什么当前这样定义是合理的

因为当前阶段最重要的是先验证：

- snapshot / restore 是否可靠
- clone 是否可靠
- checksum 是否稳定
- session 重建 predicted 是否稳定

这些都是完整 rollback 的前置条件。

如果这些底层都还没有稳定，就提前做大而全的 rollback 框架，只会放大问题。

---

## 当前测试证据

当前确定性循环设计已经有直接测试证据支撑。

## 1. 快照恢复与 checksum 一致性

`FrameSnapshot_RestoreRecoversChecksumAndEntities` 证明了：

- frame 在发生修改后可以通过 snapshot 完整恢复
- 恢复后 `CalculateChecksum()` 与快照 checksum 一致
- clone 后 checksum 仍一致

因此当前可以正式确认：

- snapshot / restore / clone / checksum 这一组基础设施已经进入主线

## 2. verified / predicted 推进正确性

`SimulationSession_AdvancesVerifiedAndPredictedFrames` 证明了：

- verified 输入推进后，权威状态按预期变化
- predicted 输入推进后，预测状态按预期变化

## 3. predicted 从 verified 重建

`SimulationSession_RebuildsPredictionFromLatestVerifiedFrame` 证明了：

- 当 verified 前进后
- predicted 会基于最新 verified 重建并重新推导目标 tick 状态

这正是当前最小 resim 语义的直接证据。

## 4. 同输入同 checksum

`SimulationSession_SameInputsProduceSameChecksums` 证明了：

- 在相同输入序列下
- verified 与 predicted 的最终 checksum 可重复一致

这为当前最小确定性循环提供了直接验证。

---

## 与 `SimulationSession` 的关系

当前确定性循环不是绕开 session 存在的。

当前正式关系是：

- `SimulationSession` 负责组织 verified / predicted 推进
- `Deterministic Loop` 文档定义这些推进必须满足的快照、输入、checksum 与重建约束

因此后续如果 session 设计发生变化，也必须继续满足本文中的确定性约束，而不是反过来破坏它们。

---

## 与 `CommandBuffer` 的关系

当前系统更新并不会立刻完成全部结构变化，结构修改要通过 `CommandBuffer` playback 收敛。

因此在当前确定性循环中：

- command playback 是单步推进的一部分
- checksum 必须在 playback 之后的完整帧状态上计算才有意义

这意味着后续任何“验证同输入同结果”的流程，都必须建立在：

- 输入应用后
- 系统更新后
- 命令已 playback 后

的完整帧状态之上。

---

## 当前一致性约束

后续所有实现都应遵守下面这些约束。

## 1. checksum 必须绑定完整帧状态

当前正式校验对象是：

- 序列化后的完整 frame 数据

不能随意退化成：

- 只校验部分组件
- 只校验部分系统结果

否则 checksum 将失去作为确定性证据的意义。

## 2. verified 输入必须严格，predicted 输入可以宽松

这条分层是当前最小闭环的基础。

不要把 verified 也改成可默认为零的宽松模型，否则权威状态会变得模糊。

## 3. predicted 必须以最新 verified 为基线

这是当前最小 resim 的正式前提。

如果 predicted 脱离 verified 独立长期演化，就不再属于当前正式主线。

## 4. rollback 不能被提前写成既成事实

当前可以说：

- 已有最小 resim 能力

但不能说：

- 已有完整 rollback 系统

文档与实现都必须保持这个边界。

## 5. 快照恢复必须与 checksum 验证一起看

只说“能 restore”还不够。

当前正式验证标准应同时包含：

- restore 后状态一致
- restore 后 checksum 一致

---

## 当前允许的实现方向

后续围绕确定性循环的实现，下面这些方向是允许的：

- 保持 `FrameSnapshot + Clone + Checksum` 作为底座主线
- 保持 verified / predicted 输入分层
- 保持 predicted 从最新 verified 重建的最小 resim 语义
- 在 session 之上逐步补齐历史快照与 rollback 窗口
- 继续用测试与 checksum 验证“同输入同结果”

---

## 当前不建议的方向

下面这些方向当前不建议进入主线：

- 在没有正式历史模型前宣称已经支持完整 rollback
- 让 checksum 脱离完整帧字节序列
- 在 verified 输入缺失时静默用默认值推进权威状态
- 让 predicted 不再依赖 verified 基线
- 把 replay、network、debug tool 一次性并入当前 D7 文档主线

这些都会让当前确定性循环边界失真。

---

## 后续收敛重点

当前 D7 完成后，确定性循环层后续最值得继续收敛的方向是：

1. 为 session 增加正式的历史快照保存策略
2. 定义 verified 输入回填后的 rollback 起点选择规则
3. 定义 resim 窗口的容量与淘汰规则
4. 定义更高层 replay / network 如何消费 snapshot 与 checksum 能力

---

## 最终结论

Lattice 当前的确定性循环设计可以概括为：

**当前主线已经形成最小确定性闭环：`FrameSnapshot` 提供完整快照，`Clone` 与 `RestoreFromSnapshot` 提供状态复制与恢复，`CalculateChecksum` 通过 `DeterministicHash.Fnv1a64` 对完整帧状态做一致性摘要，`SimulationSession<TInput>` 则通过 verified / predicted 双帧与从最新 verified 重建 predicted 的方式提供最小 resim 能力。**

这一定义足以支撑当前“同输入同结果”的底座验证，同时也明确了：完整 rollback 历史与自动回退机制仍是后续扩展，而不是当前既成事实。
