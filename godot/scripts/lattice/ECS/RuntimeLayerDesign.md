# Lattice ECS 上层运行时设计（Phase D8 已完成）

## 文档目的

本文档用于正式定义 Lattice 在底座之上的“上层运行时”设计边界。

这里的重点不是继续扩张底座主线，而是把当前已经明确属于上层运行时、但尚未正式并入底座文档链路的内容收敛出来，包括：

- Engine Bridge
- Runner
- View 同步
- 调试工具 / 观察器
- Replay 与网络层的运行时接入位置

本文档的作用不是宣布这些能力已经全部实现，而是明确：

- 它们在整体架构中的位置
- 它们应依赖谁
- 不应反向污染谁
- 当前已有的原型接口能说明什么

---

## 当前定位

在当前 Lattice 总体分层中，上层运行时位于底座主链路之上：

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
Runtime Layer
```

这里的 `Runtime Layer` 当前应理解为一个上层集合，而不是一个已经存在的统一类型。

它当前包括的概念层主要有：

- Bridge
- Runner
- View Sync
- Debug / Observer
- Replay Runtime
- Network Runtime

---

## 为什么 D8 现在才做

前面 `D0 - D7` 的目标，是先把底座主链路收紧成：

- 明确的状态容器
- 明确的结构修改闭环
- 明确的系统调度
- 明确的最小 session
- 明确的最小确定性循环

只有这些边界稳定后，D8 才有意义。

否则会出现典型问题：

- Bridge 反向定义 frame 语义
- Runner 反向决定 command playback 时机
- Debug Tool 直接读取不稳定内部结构
- View Sync 倒逼系统和组件设计

因此 D8 的正式目标不是“补更多功能”，而是：

- 把运行时层从底座之上单独收敛出来

---

## 当前正式结论

当前 Lattice 的正式架构结论是：

- `D0 - D7` 文档描述的是底座主线
- `D8` 描述的是建立在底座之上的运行时层
- 运行时层当前不是底座的一部分
- 运行时层后续可以逐步实现，但不能反向改写底座职责

更直接地说：

- `SimulationSession` 是当前底座顶层
- `Runtime Layer` 是当前架构中的“下一层”

---

## 上层运行时包含哪些部分

当前建议把上层运行时理解为五类能力。

## 1. Engine Bridge

负责：

- 把 ECS 模拟世界和具体引擎环境连接起来
- 接入时间源、生命周期、日志、资源与平台能力

它不应负责：

- 定义组件存储语义
- 定义系统生命周期
- 定义 snapshot / checksum 规则

## 2. Runner

负责：

- 驱动 session 逐 tick 推进
- 决定一帧里先收输入、再推 verified、再推 predicted 的时机
- 把引擎主循环与 session 调度连接起来

它不应负责：

- 重新实现系统调度器
- 重新定义 command buffer 语义

## 3. View Sync

负责：

- 把 ECS 状态投影到引擎对象或渲染对象
- 创建、更新、销毁实体对应视图

它不应负责：

- 直接修改模拟核心状态
- 让视图层成为权威状态来源

## 4. Debug / Observer

负责：

- 观察 session / frame / system / query / command buffer 的运行态
- 为开发期提供调试、可视化、剖析、诊断入口

它不应负责：

- 重写系统逻辑
- 成为底座正式语义的一部分

## 5. Replay / Network Runtime

负责：

- 消费 snapshot、checksum、输入历史等底座能力
- 构建实际的 replay、回放、联机运行时

它不应负责：

- 重新定义确定性循环底层约束

---

## 与底座的正式依赖方向

运行时层必须遵守下面这条依赖方向：

```text
Math
  ↓
Component
  ↓
Frame / Storage
  ↓
Query / CommandBuffer
  ↓
System
  ↓
SimulationSession
  ↓
Runtime Layer
```

这意味着：

- runtime 可以依赖 session
- runtime 可以读取 system trace、snapshot、checksum
- runtime 可以驱动 command buffer 所在的 session 流程

但反过来不允许：

- `Frame` 依赖具体引擎
- `SystemScheduler` 依赖 Godot / Unity 生命周期
- `SimulationSession` 依赖某个具体 UI 面板或观察器

---

## Engine Bridge 的当前边界

代码库里当前已经存在一份旧原型接口：

- [IEngineAdapter.cs](/d:/UGit/Sideline/godot/scripts/lattice/ECS/Abstractions/IEngineAdapter.cs)

这说明“引擎适配器”方向在项目里早就被识别到了，但要非常明确它当前的真实状态。

## 1. 当前它是什么

它当前更接近：

- 早期运行时抽象草案

它覆盖了：

- 时间管理
- 实体视图同步
- 输入系统
- 资源管理
- 日志调试
- 生命周期钩子

## 2. 当前它不是什么

它当前不是：

- 现行底座主线的一部分
- 已经过当前 `D0 - D7` 文档收敛的正式 API
- 已经围绕当前 `SimulationSession` 实现对齐好的可直接接入层

## 3. 对 D8 的正式结论

因此 D8 对 `IEngineAdapter` 的正式态度应是：

- 可以把它当作上层 bridge 的历史原型证据
- 但不能直接把它认定为当前正式 bridge API

换句话说：

- **当前 bridge 方向已被识别，但正式 bridge API 仍需在后续基于新底座重新收敛。**

---

## Runner 的当前边界

当前代码库里还没有一份正式的 runner 主线实现，但从 `SimulationSession<TInput>` 的语义已经能推导出 runner 应该负责的事。

## 1. Runner 应依赖什么

它至少应依赖：

- `SimulationSession<TInput>`
- 引擎时间源或宿主时间源
- 输入采集来源
- 可能的网络输入来源

## 2. Runner 应组织什么

当前最小 runner 主线至少应组织：

1. 收集本地输入
2. 接收权威输入或回填输入
3. 推进 verified
4. 推进 predicted
5. 触发视图同步
6. 导出调试信息

## 3. Runner 当前不能做什么

Runner 不能：

- 自己决定另一套系统更新顺序
- 自己定义 command playback 时机
- 绕开 session 直接操作 frame 形成第二套主线

因此 D8 的正式边界是：

- runner 是 session 的驱动器，而不是 session 的替代品

---

## View Sync 的当前边界

视图同步是上层运行时中最容易反向污染底座的一层，因此这里必须写得很清楚。

## 1. View Sync 负责什么

它负责：

- 根据 ECS 状态创建、销毁引擎对象
- 把 Position、Rotation、Visible 等投影到引擎对象
- 在不改写底座状态的前提下提供可视化结果

## 2. View Sync 不负责什么

它不负责：

- 作为权威状态来源
- 直接决定实体是否存在
- 在引擎对象上偷偷保存会影响回滚的关键模拟状态

## 3. 当前建议方向

当前最合理的方向是：

- View Sync 始终作为 session 推进之后的投影层
- 只消费 state，不回写 state

这意味着：

- ECS 是 source of truth
- 引擎对象只是 projection

---

## Debug Tool / Observer 的当前边界

当前 `SystemScheduler` 已经有：

- `CurrentSystem`
- `Trace`
- `SystemNodeInfo`

而 `Frame / SimulationSession` 也已经有：

- snapshot
- checksum
- verified / predicted

这说明 D8 的调试工具并不是凭空开始，而是已经有一批很好的上游数据来源。

## 1. 调试工具当前应消费哪些信息

当前最值得消费的上游信息包括：

- system 树与执行顺序
- 当前 trace 事件
- verified / predicted tick
- verified / predicted checksum
- 当前 command buffer 统计信息
- snapshot 恢复与 clone 验证结果

## 2. 当前调试工具不应做什么

调试工具不应：

- 成为底座主线依赖
- 直接侵入 `Frame` 的内部未公开结构
- 为了工具方便而改写底座生命周期

## 3. 观察器的当前正式定位

当前更准确的说法应是：

- 观察器 / 调试工具是对底座公开观察面的消费层

而不是：

- 重新定义一套 runtime state

---

## Replay 与 Network Runtime 的位置

当前 D8 也需要把 replay / network 放到正确位置上，避免它们继续悬空。

## 1. Replay Runtime

Replay runtime 应建立在：

- 输入历史
- snapshot
- checksum
- session 推进能力

之上。

它当前不应直接进入底座主线，因为：

- 文件格式
- 回放控制
- UI 工具

都属于更上层运行时问题。

## 2. Network Runtime

Network runtime 应建立在：

- verified 输入流
- predicted 输入流
- session 回填与重建能力

之上。

它当前也不应回头重写：

- `SimulationSession`
- `SystemScheduler`
- `CommandBuffer`

---

## D8 与旧文档的关系

当前代码库中已有一份旧文档：

- [ENGINE_ADAPTER_DESIGN.md](/d:/UGit/Sideline/godot/scripts/lattice/ECS/Abstractions/ENGINE_ADAPTER_DESIGN.md)

这份文档仍有价值，但它的定位应调整为：

- 历史草案
- 运行时桥接方向的原始设想

而不是当前 D8 的正式主文档。

原因很直接：

- 旧稿里仍混有旧时代命名和过大运行时抽象
- 它没有建立在当前 `D0 - D7` 已收敛好的底座链路上

因此从现在开始，D8 的正式口径应以本文为准。

---

## 当前一致性约束

后续所有上层运行时实现都应遵守下面这些约束。

## 1. 运行时层不得反向污染底座

这是 D8 最重要的总约束。

如果某个 bridge、runner 或 debug tool 需要能力扩展，应优先：

- 消费现有公开接口
- 补充新的公开观察面

而不是直接侵入或改写底座职责。

## 2. 视图层永远不是权威状态

当前权威状态必须始终留在：

- frame
- session
- verified / predicted 模型

引擎对象只能是投影。

## 3. runner 只能驱动 session，不能替代 session

runner 可以：

- 决定什么时候收输入
- 决定什么时候调用 session 接口

但不能：

- 发明另一套 frame 推进主线

## 4. 调试工具只能消费观测面

调试工具可以：

- 订阅 trace
- 读取 checksum
- 展示系统树

但不应成为：

- 修改底座语义的理由

## 5. bridge API 必须基于当前底座重收敛

旧的 `IEngineAdapter` 可以作为参考，但正式 bridge API 应建立在：

- `SimulationSession`
- `SystemScheduler`
- `FrameSnapshot`
- `Trace / Checksum / Input`

这些现有主线语义之上。

---

## 当前允许的实现方向

后续围绕 D8 的实现，下面这些方向是允许的：

- 基于 `SimulationSession<TInput>` 实现最小本地 runner
- 基于 `SystemScheduler.Trace` 和 checksum 实现调试观察器
- 基于 session 推进结果实现纯投影式 view sync
- 基于当前底座重收敛新的 Godot bridge / headless bridge
- 在运行时层上叠加 replay 与 network 适配

---

## 当前不建议的方向

下面这些方向当前不建议进入主线：

- 继续把旧 `IEngineAdapter` 直接当成现行正式 API 扩展
- 在未定义 runner 边界前把引擎主循环逻辑揉进底座
- 让 view sync 直接改写组件与实体生命周期
- 让 debug tool 侵入底座内部布局细节
- 为了 runtime 方便而回头放松 D0 - D7 中已收紧的底座约束

---

## 当前建议的 D8 文档拆分

本文完成后，D8 后续如果继续细化，建议按下面顺序拆文档：

1. `RuntimeLayerDesign.md`
2. `BridgeDesign.md`
3. `RunnerDesign.md`
4. `ViewSyncDesign.md`
5. `DebugObserverDesign.md`
6. `ReplayRuntimeDesign.md`

但在当前阶段，没有必要一次性把这些子文档全都补出来。

当前最合理的策略是：

- 先用本文统一口径
- 等真正开始做对应实现时，再拆出独立子文档

---

## 最终结论

Lattice 当前的 D8 结论可以概括为：

**Bridge、Runner、View Sync、Debug Tool、Replay Runtime、Network Runtime 都属于建立在 `SimulationSession` 之上的上层运行时，而不属于底座主线。它们可以消费底座暴露的 session、trace、snapshot、checksum 与输入能力，但不能反向定义或污染 `Math -> Component -> Frame -> Query -> CommandBuffer -> System -> Session` 这条已经收敛完成的主链路。**

这一定义意味着：底座文档阶段到 D8 为止已经形成完整分层闭环，后续运行时实现可以开始独立推进，而不需要继续推翻底层设计。
