# Lattice ECS System 设计（Phase D5 已完成）

## 文档目的

本文档用于正式定义 Lattice 当前阶段的 `System` 基座设计。

这里的重点不是继续讨论“未来可能长成什么样的完整 system 框架”，而是先把当前已经进入主线的 system runtime 语义写清楚：

- `ISystem / SystemBase / SystemGroup / SystemScheduler` 各自负责什么
- 系统生命周期与启停规则是什么
- 调度顺序如何稳定确定
- `SystemMetadata / SystemNodeInfo / Trace` 的正式定位是什么
- `System` 与 `Frame / Query / CommandBuffer / SimulationSession` 的依赖边界是什么

本文档建立在前面的底座文档之上，尤其直接依赖：

- [FrameStorageDesign.md](/d:/UGit/Sideline/godot/scripts/lattice/ECS/FrameStorageDesign.md)
- [QueryDesign.md](/d:/UGit/Sideline/godot/scripts/lattice/ECS/QueryDesign.md)
- [CommandBufferDesign.md](/d:/UGit/Sideline/godot/scripts/lattice/ECS/CommandBufferDesign.md)

---

## 当前定位

在 Lattice 当前底座链路中，`System` 位于：

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

它的职责非常明确：

- 定义系统对象的生命周期与元数据
- 定义系统树与系统组层级
- 定义单线程固定顺序调度语义
- 为上层 session 提供稳定的“逐 tick 执行一组系统”能力

它当前不负责：

- 多帧历史管理
- verified / predicted 双帧协调
- 输入缓冲策略
- rollback / resim 策略
- 网络调度
- 引擎层运行时桥接

---

## 设计目标

### 1. 让 system runtime 成为正式可依赖的基础设施

当前 `System` 不再只是一些零散的调用习惯，而是已经有了正式主线：

- `ISystem`
- `SystemBase`
- `SystemGroup`
- `SystemScheduler`

因此文档必须把这条主线收敛成明确规则，而不是继续停留在“建议这样做”的层面。

### 2. 保持系统层只关心“执行”，不反向承载状态

根据当前底座设计，游戏状态应保存在：

- `Frame`
- 组件数据
- 单例组件或其他 frame 内状态

而不是系统对象本身。

因此 `System` 当前正式设计必须保持：

- 系统是行为承载者
- `Frame` 才是状态承载者

### 3. 先稳定单线程固定顺序，再谈并行与更复杂调度

当前 `SystemScheduler` 已正式落地的运行模型是：

- 单线程
- 固定顺序
- 基于元数据 `Order` 和注册顺序的稳定排序

这是当前底座主线，而不是未来并行 system 的过渡占位版本。

### 4. 尽早把可观测能力纳入正式边界

当前实现已经拥有：

- `SystemNodeInfo`
- `CurrentSystem`
- `Trace`
- `SystemSchedulerTraceEvent`

这意味着调度器已经不是黑盒。

因此本文件必须把“系统层可观测性”写成正式能力，而不是可有可无的附属功能。

---

## 当前正式模型

当前 Lattice 的 system 基座由下面几部分组成：

1. `ISystem`
2. `SystemBase`
3. `SystemGroup`
4. `SystemMetadata`
5. `SystemNodeInfo`
6. `SystemSchedulerTraceEvent`
7. `SystemScheduler`

这七者共同构成当前正式主线。

更准确地说：

- `ISystem / SystemBase` 定义系统对象本体
- `SystemGroup` 定义层级容器
- `SystemMetadata` 定义调度与调试元信息
- `SystemNodeInfo / Trace` 定义可观测快照
- `SystemScheduler` 定义实际执行语义

---

## 核心对象与职责

## 1. `ISystem`

`ISystem` 是当前系统对象的正式接口。

它当前要求系统提供：

- `Name`
- `Metadata`
- `EnabledByDefault`
- `OnInit`
- `OnEnabled`
- `OnDisabled`
- `OnUpdate`
- `OnDispose`

这说明当前正式生命周期不是只有“update”，而是已经明确区分为：

1. 初始化
2. 进入有效启用状态
3. 逐帧更新
4. 退出有效启用状态
5. 销毁

## 2. `SystemBase`

`SystemBase` 是当前主线推荐的系统基类。

它提供：

- 默认 `Name = 类型名`
- 默认 `Metadata = SystemMetadata.Default`
- 默认 `EnabledByDefault = true`
- 生命周期空实现
- 强制子类实现 `OnUpdate`

因此它的正式定位是：

- 降低普通系统样板代码
- 统一系统默认行为

而不是增加第二套调度语义。

## 3. `SystemGroup`

`SystemGroup` 当前是正式的层级容器，而不是临时约定。

它当前特点是：

- 自身也实现 `ISystem`
- 保存一组子系统
- 自身 `Metadata.Kind` 会被强制收敛为 `Group`
- 生命周期回调为空实现

这说明当前正式语义是：

- `SystemGroup` 主要用于组织层级、顺序与启停传播
- 它不是在组自身运行一段业务逻辑的 system

## 4. `SystemMetadata`

`SystemMetadata` 是当前正式元数据模型。

它包含：

- `Order`
- `Kind`
- `Category`
- `DebugCategory`
- `AllowRuntimeToggle`

这意味着当前系统层已经正式支持：

- 稳定排序
- 执行类型标签
- 调试分类标签
- 运行时启停约束

## 5. `SystemExecutionKind`

当前正式执行类型包括：

- `MainThread`
- `HotPath`
- `ParallelReserved`
- `Signal`
- `Group`

这里必须特别强调当前真实语义：

- `MainThread`、`HotPath`、`Signal` 都可以被当前调度器接纳
- `Group` 只用于系统组
- `ParallelReserved` 目前是保留值，当前调度器会显式拒绝非组系统以该类型注册

因此它不是“已经支持并行”的标记，而是未来扩展边界的占位。

## 6. `SystemNodeInfo`

`SystemNodeInfo` 是系统节点的只读快照。

它当前提供：

- 系统名称、类型名
- 排序与元数据
- 父节点名称、深度、是否组、是否根
- `EnabledSelf`
- `EnabledInHierarchy`
- `Initialized`
- `Active`

因此它的正式定位是：

- 调度器对外暴露系统树状态的标准快照结构

## 7. `SystemSchedulerTraceEvent`

trace 事件当前保存：

- `Node`
- `Phase`
- `SchedulerState`

并额外把节点的常用字段投影出来。

这说明当前 trace 不是字符串日志，而是结构化调度事件。

## 8. `SystemScheduler`

`SystemScheduler` 是当前 system runtime 的核心执行器。

它当前正式负责：

1. 注册系统与系统组
2. 构建稳定系统树
3. 初始化未初始化系统
4. 维护系统启停状态
5. 执行单线程逐帧更新
6. 反向顺序销毁系统
7. 导出节点快照、执行顺序、trace 事件

它当前不负责：

- 自动 playback `CommandBuffer`
- 持有多帧状态
- 输入推进
- rollback / resim
- 多线程并行调度

---

## 生命周期语义

当前系统生命周期已经有正式语义，顺序如下：

1. 注册
2. 初始化 `OnInit`
3. 进入有效启用状态 `OnEnabled`
4. 若处于激活状态，则参与 `OnUpdate`
5. 失去有效启用状态时调用 `OnDisabled`
6. 调度器销毁时调用 `OnDispose`

### 1. 注册不等于初始化

调用 `scheduler.Add(system)` 只是把系统放入调度器。

此时系统并不会自动触发 `OnInit`，除非：

- 调度器已经处于 `Ready`
- 且有当前 frame 可用于初始化新注册系统

### 2. 初始化不等于激活

一个系统即便已经完成 `OnInit`，也不一定会进入 `Active` 状态。

例如：

- `EnabledByDefault = false`
- 或者父组当前被禁用

在这些情况下，系统会：

- 已初始化
- 但未启用
- 且不会参与 `OnUpdate`

### 3. 激活由层级启用状态决定

当前正式语义不是只看系统自身 `EnabledSelf`，而是看：

- 系统自身是否启用
- 所有父组是否都启用
- 系统是否已经初始化

只有三者同时满足，系统才会进入 `Active`。

### 4. `OnEnabled` / `OnDisabled` 是层级有效启停回调

当前 `OnEnabled` 与 `OnDisabled` 的语义不是“用户调用 Enable / Disable 时直接触发”，而是：

- 当节点从非激活变为激活时触发 `OnEnabled`
- 当节点从激活变为非激活时触发 `OnDisabled`

因此组级启停会向下影响所有子系统。

### 5. `OnDispose` 发生在反向顺序销毁中

调度器 `Dispose(frame)` 时会：

1. 先按逆序遍历已初始化节点
2. 如果节点当前激活，则先触发 `OnDisabled`
3. 再调用 `OnDispose`

这意味着当前正式销毁语义是：

- 先停用，再销毁
- 且销毁顺序与执行顺序相反

---

## 调度器状态机

当前 `SystemSchedulerState` 包含：

- `Uninitialized`
- `Initializing`
- `Ready`
- `Updating`
- `Disposing`

这不是调试字段，而是当前正式状态机。

## 1. `Uninitialized`

此时允许：

- `Add`
- `AddRange`

此时不允许：

- `Update`
- `Dispose`

## 2. `Initializing`

此时调度器正在调用：

- `OnInit`
- `OnEnabled`

当前正式约束是：

- 不允许结构性修改调度器本身

## 3. `Ready`

这是当前稳定运行态。

此时允许：

- `Update`
- `Dispose`
- `Enable / Disable`
- `Add`

并且如果此时新增系统，调度器会立刻对其做补初始化并应用启停状态。

## 4. `Updating`

此时调度器正在执行：

- 非组系统的 `OnUpdate`

当前正式约束是：

- 不允许 `Add`
- 不允许 `Enable / Disable`
- 不允许其他会改动调度器结构或启停拓扑的操作

测试已经把这类行为视为非法调用。

## 5. `Disposing`

此时调度器正在：

- `OnDisabled`
- `OnDispose`

当前同样不允许对调度器做结构修改。

---

## 启停模型

当前系统层正式区分三种状态：

- `EnabledSelf`
- `EnabledInHierarchy`
- `Active`

## 1. `EnabledSelf`

表示节点自身是否被显式设置为启用。

它来源于：

- `EnabledByDefault`
- 运行时 `Enable / Disable`

## 2. `EnabledInHierarchy`

表示从当前节点向上到根的整条链路是否都启用。

这意味着：

- 子系统自己启用，不代表它在层级上有效
- 父组禁用会让所有子节点 `EnabledInHierarchy = false`

## 3. `Active`

表示节点是否真的处于激活运行态。

当前正式条件是：

- 节点已初始化
- 且 `EnabledInHierarchy = true`

## 4. 运行时启停权限

并不是所有系统都允许在 `Ready` 状态下动态切换。

当前正式约束来自：

- `SystemMetadata.AllowRuntimeToggle`

如果某系统禁用了运行时切换，调度器会在 `Enable / Disable` 时直接抛出异常。

这进一步说明：

- 启停权限已经是正式元数据语义
- 不是调用方的自觉约定

---

## 排序与执行顺序语义

当前调度器采用稳定显式排序，而不是自动依赖图求解。

## 1. 根节点排序

根节点先按：

- `Metadata.Order`

升序排序；若相同，则按：

- 注册顺序

作为兜底。

## 2. 组内子节点排序

组内子节点也使用完全相同的规则：

- `Order`
- 注册顺序

## 3. 总体展开方式

调度器当前会先构建系统树，再按稳定顺序做深度优先展开。

因此执行序列可以概括为：

- 先排序根
- 再对每个根递归追加其子树

## 4. 组节点本身不参与 `OnUpdate`

虽然 `SystemGroup` 也在节点树中占一个位置，但当前执行阶段：

- 组节点不会执行 `OnUpdate`
- 只有非组系统会参与更新

因此组的价值主要体现在：

- 层级
- 排序
- 启停传播
- 可观测性

---

## 可观测性模型

当前 `SystemScheduler` 已经正式提供可观测接口。

## 1. `GetNodes()`

导出当前稳定系统树快照。

它包含：

- 组节点
- 非组节点
- 当前层级状态

## 2. `GetExecutionOrder()`

导出当前执行顺序快照。

它只包含：

- 非组系统

因此它是更接近“本轮可能执行哪些系统”的对外视图。

## 3. `TryGetNode / GetNodes(name) / GetNodes<T>()`

这些接口提供：

- 按实例
- 按名称
- 按类型

查询节点快照的能力。

这说明当前调度器不是只能整体观察，也支持局部定位系统节点。

## 4. `CurrentSystem`

当前正在执行生命周期或更新回调的系统，会通过 `CurrentSystem` 暴露。

这让：

- trace
- profiler
- 调试工具

都可以拿到当前上下文。

## 5. `Trace`

`Trace` 当前是一个结构化事件回调：

- 生命周期 enter / exit
- update enter / exit
- 当前调度器状态
- 当前系统节点快照

它的正式定位是：

- system runtime 的低层观察钩子

而不是已经成熟完备的 profiler 系统。

---

## 与 `Frame` 的关系

根据 [FrameStorageDesign.md](/d:/UGit/Sideline/godot/scripts/lattice/ECS/FrameStorageDesign.md)，`Frame` 是当前单帧完整状态容器。

`System` 与 `Frame` 的正式关系是：

- 系统从 `Frame` 读取和修改当前帧状态
- 调度器把 `Frame` 作为生命周期与更新回调的唯一状态入口
- 系统不应维护第二套并行世界状态

这意味着当前 system runtime 的正式工作模式是：

- 调度器驱动系统
- 系统操作 `Frame`
- `Frame` 承载状态

---

## 与查询层的关系

根据 [QueryDesign.md](/d:/UGit/Sideline/godot/scripts/lattice/ECS/QueryDesign.md)，当前系统层的正式读取入口是双路径：

- 普通系统使用 `frame.Filter<T...>()`
- 热点系统可退到 `frame.GetComponentBlockIterator<T>()`

因此 system 文档中的正式口径必须是：

- 当前并不存在统一公共 `Query<T...>` 主线 API
- 普通系统默认写法是 `Filter<T...>()`
- 热点系统默认低层入口是 `BlockIterator`

这也是为什么 `SystemExecutionKind.HotPath` 当前只是系统角色标签，而不意味着调度器会自动为它切换到另一套查询抽象。

---

## 与 `CommandBuffer` 的关系

根据 [CommandBufferDesign.md](/d:/UGit/Sideline/godot/scripts/lattice/ECS/CommandBufferDesign.md)，结构修改当前正式应走 `CommandBuffer`。

因此当前系统层正式语义是：

- 系统在 `OnUpdate` 中负责决定要不要做结构修改
- 具体结构修改请求写入 `CommandBuffer`
- 调度器当前不自动接管 playback

这意味着：

- `System` 可以依赖 `CommandBuffer`
- `SystemScheduler` 当前不等于“系统更新 + 命令自动回放的一体机”

这个边界非常重要，因为它把：

- 系统调度语义
- 结构修改落点语义

保持为两层，而不是混成一层。

---

## 与 `SimulationSession` 的关系

当前虽然 `SimulationSession` 文档尚未完成，但依赖方向已经应当明确：

- `SystemScheduler` 先稳定
- session 再在其之上组织 tick 推进

因此当前正式依赖方向应为：

```text
Frame / Query / CommandBuffer
            ↓
      SystemScheduler
            ↓
   SimulationSession
```

而不是让 session 反过来定义 system 生命周期和调度细节。

后续最小 session 设计应建立在下面这条链路之上：

1. session 准备本 tick 的 `Frame`
2. session 调用 `SystemScheduler.Update(frame, deltaTime)`
3. session 在统一时机处理 `CommandBuffer`
4. session 再决定 snapshot / checksum / rollback 行为

---

## 当前一致性约束

后续所有实现都应遵守下面这些约束。

## 1. 系统对象不应承载可变模拟状态

当前系统对象可以有：

- 只读配置
- 外部服务引用
- 命令缓冲宿主引用

但不应把会随帧推进而变化的核心模拟状态长期保存在系统对象内部。

否则：

- 回滚难以成立
- clone / restore 会出现状态分叉

## 2. 调度顺序必须显式且稳定

当前正式排序依据只有：

- `Metadata.Order`
- 注册顺序

如果后续需要更复杂顺序表达，也必须建立在“显式且稳定”的原则之上，而不是退回隐式依赖推导。

## 3. 更新期不得改动调度器结构

当前调度器已经把下面这些行为视为非法：

- `Update` 期间 `Add`
- `Update` 期间 `Enable / Disable`

后续实现不应破坏这条约束。

## 4. 组节点是层级容器，不是第二种业务系统

当前 `SystemGroup` 的正式作用是：

- 层级组织
- 启停传播
- 排序容器

如果未来要支持“组本身也执行逻辑”，那应是新的明确设计，而不是静默改变当前 `SystemGroup` 语义。

## 5. `ParallelReserved` 当前不是已支持能力

当前调度器会显式拒绝：

- 非组系统声明 `ParallelReserved`

因此任何文档或示例都不应把它写成当前已落地并行主线。

---

## 当前允许的实现方向

后续围绕 `System` 层的实现，下面这些方向是允许的：

- 保持 `ISystem + SystemBase + SystemGroup + SystemScheduler` 作为正式主线
- 保持基于 `Order + 注册顺序` 的稳定排序
- 继续加强节点快照、trace 与调试观察能力
- 继续用 `Filter<T...>()` 和 `BlockIterator` 作为系统读取入口
- 在保持当前边界前提下，为未来 session 接入预留更清晰的调用点

---

## 当前不建议的方向

下面这些方向当前不建议进入主线：

- 把 `SystemScheduler` 扩成 session、runner 和 network 的综合控制器
- 在没有正式并行模型前把 `ParallelReserved` 伪装成已可用能力
- 让系统对象长期保存关键模拟状态
- 在没有清晰顺序语义前引入自动拓扑排序
- 让调度器偷偷自动处理所有 `CommandBuffer` playback，而不在 session 层明确时机

这些都会让 system runtime 的边界重新失焦。

---

## 后续收敛重点

当前 D5 完成后，`System` 层后续最值得继续收敛的方向是：

1. 新建 `SimulationSessionDesign.md`，明确 scheduler 与 session 的最小闭环
2. 继续明确 session 何时接入 `CommandBuffer` playback
3. 在 session 语义稳定后，再决定是否需要更正式的阶段化调度表达
4. 等 session 与确定性循环收敛后，再评估 profiler 聚合、并行调度和配置驱动装配

---

## 最终结论

Lattice 当前的 `System` 设计可以概括为：

**`System` 层当前已经形成正式主线：`ISystem` 定义生命周期接口，`SystemBase` 提供默认基类，`SystemGroup` 提供层级容器，`SystemScheduler` 以单线程固定顺序模型执行系统，并通过 `SystemMetadata`、`SystemNodeInfo` 与 `Trace` 暴露稳定的调度语义和观察能力。**

这一定义把系统执行从 `Frame / Query / CommandBuffer` 之上正式收敛出来，也为后续 `SimulationSession` 文档提供了稳定下游入口。
