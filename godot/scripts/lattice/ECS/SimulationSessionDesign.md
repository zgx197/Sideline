# Lattice ECS SimulationSession 设计（Phase D6 已完成）

## 文档目的

本文档用于正式定义 Lattice 当前阶段的 `SimulationSession` 设计。

这里的重点不是描述完整联机、完整回滚框架或复杂 runner 体系，而是先把当前代码库里已经落地的最小 session 闭环写清楚：

- 为什么当前采用 `verified / predicted` 双帧模型
- `SimulationSession<TInput>` 当前负责什么，不负责什么
- 输入如何写入与读取
- verified 与 predicted 分别如何推进
- session 如何与 `SystemScheduler`、`CommandBuffer`、`FrameSnapshot` 协作

本文档直接建立在下面几份底座文档之上：

- [FrameStorageDesign.md](/d:/UGit/Sideline/godot/scripts/lattice/ECS/FrameStorageDesign.md)
- [CommandBufferDesign.md](/d:/UGit/Sideline/godot/scripts/lattice/ECS/CommandBufferDesign.md)
- [SystemDesign.md](/d:/UGit/Sideline/godot/scripts/lattice/ECS/SystemDesign.md)

---

## 当前定位

在 Lattice 当前底座链路中，`SimulationSession` 位于：

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
```

它的职责很明确：

- 组织单局模拟的启动与结束
- 持有当前 `verified frame` 与 `predicted frame`
- 持有输入缓冲
- 驱动 scheduler 在目标 tick 上推进帧状态
- 在每个模拟步中接入 `CommandBuffer` playback

它当前不负责：

- 网络层
- 多 peer 协调
- 输入确认协议
- 快照历史缓存池
- 广义 rollback 窗口管理
- replay 文件格式
- 引擎层主循环与时间源

---

## 设计目标

### 1. 先建立最小可运行的确定性闭环

当前 session 设计优先解决的是：

- 可以从一个初始帧启动
- 可以写入 verified 输入推进权威帧
- 可以写入 predicted 输入推进预测帧
- 当 verified 前进后，predicted 能基于最新 verified 重新构建

这已经足以支撑当前底座验证：

- 系统调度
- 输入驱动
- frame clone
- checksum 一致性

### 2. 保持 session 自身足够轻量

当前 `SimulationSession<TInput>` 没有承担一个“全能 runtime 容器”的角色。

它只持有：

- 一份 `SystemScheduler`
- 一份 `SimulationCommandBufferHost`
- 两个输入缓冲
- 一份 verified frame
- 一份 predicted frame

这样做的目的很明确：

- 先稳定最小主线
- 不把 runner、network、replay、tooling 都提前塞进 session

### 3. 用 clone + 重推的方式先稳定 predicted 语义

当前 predicted 的正式语义不是：

- 长期积累一条独立不可回溯分支

而是：

- 每次从最新 verified frame clone 出一份当前基线
- 再按输入把 predicted 推到目标 tick

这条路径实现简单，但语义稳定，非常适合当前阶段。

---

## 当前正式模型

当前 session 主线由下面几部分组成：

1. `SimulationSessionOptions`
2. `SimulationInputApplier<TInput>`
3. `SimulationCommandBufferHost`
4. `SimulationSession<TInput>`
5. 内部 `TickBuffer`

其中：

- `Options` 定义容量与固定步长
- `InputApplier` 定义如何把输入写入 frame
- `CommandBufferHost` 为系统提供当前 tick 的结构修改缓冲
- `SimulationSession<TInput>` 负责 verified / predicted 主流程
- `TickBuffer` 负责最小输入缓存

---

## 核心对象与职责

## 1. `SimulationSessionOptions`

当前 `SimulationSessionOptions` 包含：

- `InitialTick`
- `DeltaTime`
- `MaxEntities`
- `InputCapacity`
- `CommandBufferCapacity`

这说明当前 session 的正式配置边界是：

- 初始逻辑 tick
- 固定时间步长
- 单帧最大实体容量
- 输入缓冲容量
- 每步命令缓冲初始容量

它当前不包含：

- 网络延迟窗口
- 快照历史上限
- rollback 深度
- 多 peer 输入路由

## 2. `SimulationInputApplier<TInput>`

这是一个最小委托：

- 接收 `Frame`
- 接收当前 tick 的输入
- 把输入写入 frame 状态

它的正式定位是：

- 把“输入如何作用到帧”从 session 核心流程中解耦出来

因此当前 session 不理解具体输入语义，只负责：

- 找到某 tick 应该使用的输入
- 在模拟前把它交给 input applier

## 3. `SimulationCommandBufferHost`

当前 host 只有一个字段：

- `CommandBuffer Buffer`

它的正式定位是：

- 为当前模拟步的系统执行暴露一份命令缓冲

这意味着 session 当前不直接把 `CommandBuffer` 作为参数传给 scheduler，而是通过共享宿主对象把它暴露给系统或外部协作对象。

## 4. `SimulationSession<TInput>`

这是当前正式 session 主体。

它当前持有：

- `_options`
- `_applyInput`
- `_predictedInputs`
- `_verifiedInputs`
- `_scheduler`
- `Commands`
- `_verifiedFrame`
- `_predictedFrame`

它当前正式能力包括：

- 注册系统
- 启动 session
- 写 verified 输入
- 写 predicted 输入
- 推进 verified
- 推进 predicted
- 计算 verified / predicted checksum
- 释放所有资源

## 5. `TickBuffer`

`TickBuffer` 当前是 session 内部最小输入缓冲。

它的实现特点是：

- 固定容量
- 通过 `tick % capacity` 取槽位
- 同槽位被新 tick 覆盖时，旧值直接失效

因此它的正式定位是：

- 最小 ring buffer 级别的输入缓存

它当前不是：

- 通用历史输入数据库
- 可枚举的回放日志
- 可追踪冲突的复杂缓存层

---

## 为什么当前采用 verified / predicted 双帧

当前 session 要解决的是一个很实际的问题：

- 既需要有一份“已经确认的权威状态”
- 又需要有一份“可以继续往未来推演的预测状态”

因此当前最小闭环自然收敛为双帧模型：

- `VerifiedFrame`
- `PredictedFrame`

### 1. verified 的职责

verified frame 表示：

- 已被权威输入推进到当前 tick 的正式状态

它是：

- 后续预测的基线
- checksum 验证的正式对象

### 2. predicted 的职责

predicted frame 表示：

- 基于最新 verified frame，再叠加本地可用输入继续推演得到的状态

它的目标是：

- 给未来 tick 一个可先行运行的状态视图

### 3. 为什么当前不维护更多层次

当前还没有正式引入：

- verified 历史帧池
- rollback checkpoint ring
- replay timeline

这不是缺漏，而是当前阶段的明确克制：

- 先把一份当前 verified
- 一份当前 predicted

的最小模型稳定下来。

---

## 启动语义

当前 `SimulationSession<TInput>.Start` 的正式流程如下：

1. 校验当前 session 未启动且未释放
2. 创建初始 verified frame
3. 执行可选 bootstrap
4. 用 verified frame 初始化 `SystemScheduler`
5. 令 `VerifiedFrame = verified`
6. 用 `verified.Clone()` 生成初始 predicted frame
7. 标记 `IsRunning = true`

这里有几个当前正式语义需要明确记录。

## 1. bootstrap 只作用于初始 verified frame

bootstrap 回调的正式作用是：

- 在系统初始化前，搭建初始世界状态

例如：

- 创建初始实体
- 添加基础组件
- 写入初始单例状态

由于 predicted frame 直接来自 verified clone，因此 bootstrap 结果会自然进入 predicted 基线。

## 2. scheduler 只初始化一次

当前实现只会对 scheduler 调用一次：

- `_scheduler.Initialize(verified)`

之后 predicted frame 的推进不会再次初始化 scheduler。

这背后的正式前提是：

- 系统对象本身不承载 frame 状态
- scheduler 生命周期独立于具体某一帧实例

这也正是为什么前面的 `SystemDesign` 强调 system 应保持无状态。

## 3. 初始 predicted 等于 verified clone

当前启动后：

- `PredictedFrame` 不是空
- 它一开始就是 `VerifiedFrame` 的完整克隆

因此 session 启动后，verified 与 predicted 的起点是完全对齐的。

---

## 输入模型

当前 session 显式区分两类输入：

- `verified input`
- `predicted input`

## 1. verified 输入

通过：

- `SetVerifiedInput(int tick, in TInput input)`

写入。

它的正式含义是：

- 某个 tick 的权威输入已经可用

verified 推进时只能使用 verified 输入。

如果某个目标 tick 缺少 verified 输入，当前实现会直接抛错。

这说明当前正式语义是：

- verified 推进不允许猜测输入

## 2. predicted 输入

通过：

- `SetPredictedInput(int tick, in TInput input)`

写入。

它的正式含义是：

- 某个未来 tick 的本地预测输入可用

predicted 推进时会优先使用 verified 输入；如果没有 verified，再退回 predicted 输入；如果两者都没有，则使用默认值。

因此当前 predicted 输入解析优先级是：

1. verified input
2. predicted input
3. `default(TInput)`

## 3. `TickBuffer` 的当前边界

输入缓冲当前通过固定容量 ring buffer 实现。

这意味着：

- 超出容量后，旧 tick 的输入会被新 tick 覆盖
- 它只适合作为当前阶段的最小输入窗口

因此后续若需要长窗口 rollback 或 replay，必须在更高层重新定义输入历史策略，而不是默认依赖当前 `TickBuffer`。

---

## verified 推进语义

当前 verified 推进通过：

- `AdvanceVerifiedTo(int targetTick)`

完成。

其正式流程为：

1. 校验 session 正在运行
2. 禁止 `targetTick < VerifiedTick`
3. 逐 tick 从 `VerifiedTick + 1` 推进到目标 tick
4. 每一 tick 都必须取到 verified 输入
5. 用 `SimulateNext` 生成下一帧
6. 用新帧替换旧 verified frame

这里最关键的正式点有三条。

## 1. verified 只能向前推进

当前实现明确禁止：

- 让 verified tick 回退

这说明 rollback 当前还不是 session 主线语义的一部分。

## 2. verified 缺输入即失败

如果某 tick 没有 verified 输入，当前实现会抛出：

- `缺少 tick=... 的 verified 输入`

因此 verified 推进是严格的，不做任何推测性补值。

## 3. 每一步都是“上一 verified frame clone 后再模拟”

当前每个 `next verified frame` 都来自：

- 当前 verified frame clone
- 应用该 tick 输入
- 执行 scheduler
- playback 命令缓冲

因此 verified 推进本质上是：

- 基于 frame clone 的纯前推链路

---

## predicted 推进语义

当前 predicted 推进通过：

- `AdvancePredictedTo(int targetTick)`

完成。

其正式流程为：

1. 校验 session 正在运行
2. 若目标 tick 小于当前 verified，则把目标 tick 收敛到 verified tick
3. 从最新 verified frame clone 一份 `current`
4. 从 `current.Tick + 1` 一直推进到目标 tick
5. 每个 tick 用 `ResolveBestInput` 选输入
6. 最终用得到的帧替换当前 predicted frame

这里有几个必须明确写清的当前正式语义。

## 1. predicted 每次都从最新 verified 重建

这意味着当前 predicted 不是“在旧 predicted 上继续叠加到天荒地老”，而是：

- 以最新 verified 为基线
- 每次完整重推到目标 tick

这正是当前最小 resim 模型。

## 2. predicted 不会落后于 verified

如果调用方传入的目标 tick 小于当前 verified tick，session 会把它自动提升到 verified tick。

因此当前正式语义是：

- predicted 至少与 verified 对齐

## 3. predicted 允许默认输入

如果某 tick 既没有 verified 输入，也没有 predicted 输入，当前会使用：

- `default(TInput)`

这使得 predicted 推进在输入不完整时仍然可继续运行。

---

## 单步模拟语义

无论是 verified 还是 predicted，它们当前都通过同一个内部入口：

- `SimulateNext(Frame currentFrame, int nextTick, TInput input)`

来推进下一帧。

其正式流程如下：

1. `next = currentFrame.Clone()`
2. 更新 `next.Tick`
3. 更新 `next.DeltaTime`
4. 为该帧准备 `CommandBuffer`
5. 调用 `_applyInput?.Invoke(next, input)`
6. 调用 `_scheduler.Update(next, _options.DeltaTime)`
7. 调用 `Commands.Buffer.Playback(next)`
8. 释放本步命令缓冲
9. 返回 `next`

这条流程非常关键，因为它把底座主线完全串起来了：

- `Frame` clone 提供状态基线
- input applier 注入当前 tick 输入
- `SystemScheduler` 推进行为逻辑
- `CommandBuffer` 完成结构修改收敛

---

## 与 `SystemScheduler` 的关系

根据 [SystemDesign.md](/d:/UGit/Sideline/godot/scripts/lattice/ECS/SystemDesign.md)，scheduler 当前只负责系统执行，不负责 session、多帧与命令时机。

当前 session 与 scheduler 的正式关系是：

- session 持有唯一一份 `SystemScheduler`
- session 负责 `Initialize`
- session 在每个模拟步调用 `Update`
- session 在释放时调用 `Dispose`

这意味着当前正式依赖方向是：

- `SimulationSession -> SystemScheduler`

而不是让 scheduler 反过来持有 session。

同时，这也说明当前 session 没有为 verified 和 predicted 分别维护两套 scheduler。

这是刻意的：

- scheduler 是行为调度器
- frame 才是状态载体

---

## 与 `CommandBuffer` 的关系

根据 [CommandBufferDesign.md](/d:/UGit/Sideline/godot/scripts/lattice/ECS/CommandBufferDesign.md)，结构修改要通过命令缓冲延迟执行。

当前 session 正式把命令缓冲接入到“每个模拟步”中：

1. 在该步开始前 `PrepareCommandBuffer`
2. 让系统更新期间可写入 `Commands.Buffer`
3. 在 scheduler update 结束后立即 `Playback(next)`
4. 本步结束后释放缓冲

这说明当前正式语义是：

- 命令缓冲的 playback 时机由 session 掌控
- 每个模拟步使用一份新的本步缓冲

因此 D4 中遗留的关键问题，在当前代码里已经有了实际答案：

- 当前主线是由 `SimulationSession`，而不是 `SystemScheduler`，来统一掌控 command buffer 的 playback 时机

---

## 与 `FrameSnapshot` / clone / checksum 的关系

根据 [FrameStorageDesign.md](/d:/UGit/Sideline/godot/scripts/lattice/ECS/FrameStorageDesign.md)，当前 `Frame.Clone()` 建立在完整快照与恢复之上。

session 当前明确依赖这些能力：

- verified 推进时每步 clone
- predicted 重建时从 verified clone
- checksum 用于验证同输入同结果

### 1. clone 是当前 session 的核心基础设施

当前 session 并没有直接维护“差量更新帧”。

它选择的是：

- 直接 clone 一份完整帧
- 在 clone 上推进下一 tick

因此如果 frame clone 语义不稳定，session 主线也不会稳定。

### 2. checksum 当前是结果一致性验证入口

当前 session 公开：

- `CalculateVerifiedChecksum()`
- `CalculatePredictedChecksum()`

这说明 checksum 当前正式定位是：

- 对当前 verified / predicted 状态做确定性一致性验证

它当前不是：

- 网络包字段
- 自动断线回滚协议的一部分

---

## 资源与生命周期

当前 session 的生命周期大致分为：

1. 构造
2. 注册系统
3. `Start`
4. 若干次 verified / predicted 推进
5. `Dispose`

## 1. 构造阶段

构造时会完成：

- options 参数校验
- 输入缓冲创建
- 命令缓冲宿主创建

但此时：

- session 还未运行
- frame 尚未创建
- scheduler 尚未初始化

## 2. 运行阶段

`Start` 之后：

- `IsRunning = true`
- `VerifiedFrame` 与 `PredictedFrame` 均可用

## 3. 释放阶段

`Dispose` 时当前正式流程为：

1. 如果正在运行且 verified frame 存在，则先 `scheduler.Dispose(_verifiedFrame)`
2. 释放 command buffer
3. 清空 verified / predicted 引用
4. 释放 frame 内存
5. 标记 `_disposed = true`

这里要特别强调一个当前正式现实：

- scheduler 的 `Dispose` 只会对 verified frame 调用一次
- predicted frame 不会单独再跑一轮 scheduler dispose 生命周期

这与当前 system 无状态契约是一致的。

---

## 当前一致性约束

后续所有实现都应遵守下面这些约束。

## 1. session 只组织模拟，不定义系统语义

当前系统生命周期、排序、trace 语义由 `SystemScheduler` 定义。

session 不应重新定义第二套 system 生命周期。

## 2. verified 是唯一权威基线

当前 predicted 必须从最新 verified 重建。

不应让 predicted 反过来污染 verified。

## 3. verified 推进必须使用 verified 输入

当前 verified 缺输入即失败，这是正确边界。

如果未来要支持“缺权威输入时等待”或“输入回填后 rollback”，也应在更高层明确建模，而不是改写当前 verified 主线。

## 4. command buffer playback 时机由 session 统一掌控

当前系统不应自己偷偷选择不同 playback 时机。

这一点必须继续保持，否则 session 闭环会失去统一语义。

## 5. 系统对象必须保持无状态前提

当前 session 之所以能用“一份 scheduler 跑 verified 与 predicted 两类 frame”，成立前提就是：

- 系统不把核心模拟状态保存在对象内部

这条约束如果被破坏，session 设计也会随之失效。

---

## 当前允许的实现方向

后续围绕 `SimulationSession` 的实现，下面这些方向是允许的：

- 保持 `verified / predicted` 双帧最小模型
- 保持 verified 严格输入、predicted 宽松输入的分层
- 保持 predicted 从最新 verified 重建的最小 resim 路径
- 继续以 session 作为 command buffer playback 的统一接入点
- 在不破坏当前边界前提下逐步补齐 snapshot 历史、rollback 窗口与输入确认能力

---

## 当前不建议的方向

下面这些方向当前不建议进入主线：

- 把 session 直接扩成 network runner、replay runner 和 engine runner 的综合容器
- 在还没有正式历史模型前把当前双帧 session 伪装成完整 rollback 框架
- 让 scheduler 反向接管 session 的输入与 playback 时机
- 让 predicted 成为长期独立状态树，而不再以 verified 为基线
- 把当前 `TickBuffer` 当作完整历史输入系统

这些都会让当前最小闭环重新失焦。

---

## 后续收敛重点

当前 D6 完成后，`SimulationSession` 层后续最值得继续收敛的方向是：

1. 在 D7 中正式定义 snapshot / checksum / input / rollback 的最小确定性循环约束
2. 明确 verified 输入回填后如何触发 rollback / resim
3. 定义 session 需要保存哪些历史快照、保存多久
4. 明确更高层 runner 如何驱动 `Start / AdvanceVerifiedTo / AdvancePredictedTo`

---

## 最终结论

Lattice 当前的 `SimulationSession` 设计可以概括为：

**`SimulationSession<TInput>` 是当前最小确定性模拟闭环。它持有一份 `SystemScheduler`、一份命令缓冲宿主、两份输入缓冲以及 `verified / predicted` 双帧状态；verified 只使用权威输入向前推进，predicted 则每次从最新 verified clone 后按可用输入重建，并由 session 在每个模拟步统一执行 `CommandBuffer` playback。**

这一定义把 session 从 system、frame 和 command buffer 之上正式收敛出来，也为下一步 `DeterministicLoopDesign` 提供了稳定上游语义。
