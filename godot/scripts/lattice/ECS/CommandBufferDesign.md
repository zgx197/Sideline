# Lattice ECS CommandBuffer 设计（Phase D4 已完成）

## 文档目的

本文档用于正式定义 Lattice 当前阶段的 `CommandBuffer` 设计。

这里的重点不是描述未来所有网络协议、回滚工具或调试能力，而是先把当前已经进入主线的结构修改闭环写清楚：

- 为什么结构修改必须延迟执行
- `Create / Add / Set / Remove / Destroy` 的正式语义是什么
- 临时实体如何映射到真实实体
- playback 为什么必须按写入顺序执行
- 它与 `Frame / Query / SystemScheduler / SimulationSession` 的依赖关系是什么

本文档是后续 `SystemDesign` 与 `SimulationSessionDesign` 的直接上游文档之一。

---

## 当前定位

在 Lattice 当前底座链路中，`CommandBuffer` 位于：

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
SystemScheduler
  ↓
SimulationSession
```

它的职责很明确：

- 记录当前帧内的延迟结构修改请求
- 在明确时机对 `Frame` 执行统一 playback
- 为系统更新与结构变更之间提供隔离层

它当前不负责：

- 系统调度
- 多帧历史管理
- verified / predicted 协调
- 网络协议定义
- 回滚策略本身

---

## 设计目标

### 1. 把结构修改从查询和系统遍历中分离出去

当前系统遍历主线依赖：

- `frame.Filter<T...>()`
- `frame.GetComponentBlockIterator<T>()`

这些路径都默认建立在“当前存储布局稳定”的前提上。

如果系统在遍历过程中直接做：

- 创建实体
- 销毁实体
- 添加组件
- 删除组件

就会让：

- 当前迭代器状态
- `Storage<T>` 的 packed 布局
- `Frame` 的实体有效性语义

全部进入不稳定状态。

因此当前正式设计要求是：

**系统在更新阶段只读取和修改已拿到的组件值，结构性修改统一经由 `CommandBuffer` 延迟执行。**

### 2. 给单帧结构修改提供稳定的顺序语义

延迟执行本身还不够，结构修改必须具备可预测的顺序。

当前 `CommandBuffer` 的主线语义是：

- 按写入顺序记录命令
- 在 playback 时按同样顺序依次回放

这样做的意义是：

- 行为稳定
- 易于测试
- 易于与确定性回放对齐

### 3. 支持“本帧新建实体，再继续修改它”的闭环

如果命令缓冲只支持已有实体，很多系统写法都会变得别扭。

因此当前设计显式支持：

1. 先 `CreateEntity`
2. 拿到临时实体引用
3. 对这个临时实体继续 `Add / Set / Remove / Destroy`
4. playback 时把它解析成真实实体

这正是当前实现中负索引临时实体模型的作用。

---

## 当前正式模型

当前 `CommandBuffer` 是一个面向单帧结构修改的低层命令缓冲。

它当前正式支持五类命令：

- `CreateEntity`
- `DestroyEntity`
- `AddComponent<T>`
- `RemoveComponent<T>`
- `SetComponent<T>`

命令被顺序写入内部字节缓冲区，必要时自动扩容。

同时，命令缓冲额外维护一份“本缓冲内创建的实体表”，用于把：

- 写入阶段的临时实体引用

映射为：

- playback 阶段真实由 `Frame.CreateEntity()` 返回的实体引用

---

## 为什么结构修改必须延迟执行

### 1. 查询层当前不承担结构修改语义

根据 [QueryDesign.md](/d:/UGit/Sideline/godot/scripts/lattice/ECS/QueryDesign.md)，查询层当前正式职责是：

- 读取组件
- 逐实体遍历
- 修改当前已经拿到的组件值

它不负责：

- 创建实体
- 销毁实体
- 增减组件

如果把结构修改揉进查询层，会直接破坏 `Query` 的职责边界。

### 2. `Storage<T>` 的 packed 布局不适合边遍历边改结构

当前 `Storage<T>` 使用：

- 稀疏映射
- packed block 布局

这意味着添加和移除组件会改变：

- `_sparse`
- packed data
- packed handles
- 存储版本

因此当前正式语义应是：

- 遍历阶段假设存储布局稳定
- 结构修改阶段单独执行

### 3. `Frame` 的实体生命周期需要统一落点

实体创建与销毁会影响：

- 实体版本
- free list
- component mask
- 各组件存储

这些都属于 `Frame` 层面的正式状态。

因此结构修改最终必须落在 `Frame` 上统一执行，而不是让各个系统在不同位置直接改出各自版本的语义。

---

## 核心对象与职责

## 1. `CommandType`

`CommandType` 用于标识命令种类。

当前正式命令种类只有：

- `CreateEntity`
- `DestroyEntity`
- `AddComponent`
- `RemoveComponent`
- `SetComponent`

这五类已经覆盖当前底座需要的最小结构修改闭环。

## 2. `CommandHeader`

`CommandHeader` 是每条命令的前导头。

它当前保存：

- `Type`
- `ComponentTypeId`
- `Entity`

其中：

- 对于实体级命令，`ComponentTypeId` 为 `ushort.MaxValue`
- 对于组件命令，`ComponentTypeId` 由组件注册体系提供

因此 `CommandBuffer` 与组件注册体系并不是松耦合关系，而是正式依赖关系。

## 3. `CommandBuffer`

`CommandBuffer` 当前正式职责包括：

1. 接收结构修改命令写入
2. 维护内部变长字节缓冲
3. 维护本缓冲内创建实体的映射表
4. 在 playback 时按顺序把命令施加到目标 `Frame`
5. 提供基础序列化与反序列化能力

它当前不负责：

- 决定何时 playback
- 自动与系统调度绑定
- 自动做多帧命令合并
- 自动做命令去重或语义压缩

---

## 命令生命周期

当前命令生命周期可以概括为四步：

1. 写入命令
2. 可选序列化 / 反序列化
3. playback 到目标 `Frame`
4. 清空缓冲，等待下一轮记录

### 1. 写入阶段

系统或其他调用方在更新阶段调用：

- `CreateEntity`
- `DestroyEntity`
- `AddComponent<T>`
- `RemoveComponent<T>`
- `SetComponent<T>`

命令会被顺序写入 `_buffer`。

如果是 `CreateEntity`，还会额外生成一个临时 `EntityRef`。

### 2. 暂存阶段

在 playback 之前，命令缓冲只保存“意图”，不直接修改 `Frame`。

因此此时：

- `Frame` 状态还未改变
- 查询层看到的仍是旧结构
- 系统间也不会因为中途增删实体而互相打断

### 3. playback 阶段

调用 `Playback(frame)` 后，命令会按写入顺序逐条执行到目标 `Frame`。

这是当前唯一正式的结构修改落点。

### 4. 清空阶段

playback 结束后，`CommandBuffer` 会执行 `Clear()`。

这意味着当前正式语义是：

- 一轮 playback 对应一轮命令消费
- 命令不会在默认情况下自动保留到下一帧

---

## 五类命令的正式语义

## 1. `CreateEntity`

`CreateEntity` 的写入语义是：

- 向缓冲追加一条创建命令
- 返回一个临时实体引用

它此时不会立即调用 `frame.CreateEntity()`。

它的 playback 语义是：

- 在执行到该命令时调用目标 `Frame.CreateEntity()`
- 将创建结果写回 `_createdEntities[ordinal]`

### 当前关键约束

- 临时实体只在当前命令缓冲内部有效
- 只有 playback 之后它才对应真实实体
- 临时实体不能跨缓冲长期保存并假定始终可解析

## 2. `DestroyEntity`

`DestroyEntity(entity)` 的写入语义是：

- 记录“在 playback 时销毁该实体”

它允许目标是：

- 已存在实体
- 由同一缓冲创建的临时实体

它的 playback 语义是：

- 先通过 `ResolveEntity` 解析实体
- 再调用 `frame.DestroyEntity(entity)`

## 3. `AddComponent<T>`

`AddComponent<T>(entity, component)` 的写入语义是：

- 记录目标实体
- 记录组件类型 ID
- 追加该组件的原始值字节

它的 playback 语义是：

- 解析实体
- 通过 `ComponentCommandRegistry.PlaybackAdd` 调用对应组件命令回放逻辑

## 4. `RemoveComponent<T>`

`RemoveComponent<T>(entity)` 的写入语义是：

- 记录目标实体
- 记录组件类型 ID

它的 playback 语义是：

- 解析实体
- 通过 `ComponentCommandRegistry.PlaybackRemove` 执行组件移除

## 5. `SetComponent<T>`

`SetComponent<T>(entity, component)` 的写入语义与 `AddComponent<T>` 类似：

- 记录目标实体
- 记录组件类型 ID
- 记录组件原始值字节

它的 playback 语义是：

- 解析实体
- 通过 `ComponentCommandRegistry.PlaybackSet` 执行组件覆盖写入

---

## 临时实体模型

## 1. 为什么需要临时实体

当前系统常见的结构修改需求是：

1. 本帧创建实体
2. 立刻给它挂组件
3. 甚至继续设置或销毁它

如果没有临时实体模型，调用方就只能：

- 分成多次 playback
- 或者手动维护“创建结果回填”的上层逻辑

这会让系统层变复杂。

因此当前 `CommandBuffer` 正式支持“临时实体先占位，playback 再解析”的工作流。

## 2. 当前表示方式

当前实现中，临时实体使用：

- `EntityRef.Index < 0`

来表示。

具体规则是：

- 第 1 个临时实体：`Index = -1`
- 第 2 个临时实体：`Index = -2`
- 第 N 个临时实体：`Index = -N`

对应的创建序号为：

- `createdOrdinal = -entity.Index - 1`

### 正式含义

负索引不是 `Frame` 的真实实体索引。

它只是：

- 当前命令缓冲内部
- 当前写入顺序上下文中

的一种占位句柄。

## 3. `ResolveEntity` 的正式语义

当前 playback 时，如果实体索引非负：

- 直接视为真实实体

如果实体索引为负：

- 把它解释为创建序号
- 在 `_createdEntities` 中取出 playback 过程中真实创建出来的实体

如果序号越界，则直接抛错。

这意味着当前正式语义是：

- 临时实体解析失败属于非法命令流，而不是可被静默忽略的情况

---

## playback 顺序语义

当前 `Playback(frame)` 的正式语义是：

- 从 `_buffer` 开始顺序读取命令
- 每读到一条命令就立即执行
- 直到 `_writePosition` 末尾

这里最关键的正式约束有三条。

## 1. 按写入顺序执行

playback 不会重排命令，也不会尝试自动合并相邻命令。

因此下面两段命令流的结果应视为不同：

```text
Create -> Add(Position) -> Set(Position)
```

与：

```text
Create -> Set(Position) -> Add(Position)
```

前者在当前主线中是自然合理的“先添加再设置”。

后者是否有效，取决于组件回放语义本身，但 `CommandBuffer` 不会替调用方修正顺序。

## 2. 创建结果只能影响后续命令

当 playback 执行到 `CreateEntity` 时，才会把该序号对应的真实实体写回 `_createdEntities`。

因此对同一个临时实体的后续命令，只有在创建命令已经回放之后才能成功解析。

这进一步要求：

- 对临时实体的命令必须出现在对应 `CreateEntity` 之后

## 3. playback 是结构修改正式落点

当前主线语义下：

- 写入阶段只是声明意图
- playback 阶段才是真正改变 `Frame` 状态的时刻

因此任何关于“结构什么时候真正发生变化”的判断，都应以 playback 为准，而不是以命令写入为准。

---

## 与 `Frame` 的关系

根据 [FrameStorageDesign.md](/d:/UGit/Sideline/godot/scripts/lattice/ECS/FrameStorageDesign.md)，`Frame` 是当前单帧完整状态容器。

`CommandBuffer` 与 `Frame` 的正式关系是：

- `CommandBuffer` 不拥有 ECS 状态
- `Frame` 才是结构修改的真正承载者
- `CommandBuffer` 只是把结构修改意图延迟并顺序化后，再应用到 `Frame`

因此后续实现必须继续坚持下面这条边界：

- `Frame` 定义状态语义
- `CommandBuffer` 定义延迟修改语义

而不是反过来让 `CommandBuffer` 拥有第二套独立状态模型。

---

## 与组件注册体系的关系

`CommandBuffer` 当前对组件注册体系有正式依赖：

- 写命令时需要 `ComponentTypeId<T>.Id`
- playback 时需要 `ComponentCommandRegistry.GetComponentSize`
- 回放具体组件操作时需要 `ComponentCommandRegistry.PlaybackAdd / PlaybackRemove / PlaybackSet`

这说明当前正式语义是：

- 组件命令回放能力是组件注册模型的一部分
- `CommandBuffer` 不能脱离组件注册体系独立工作

因此后续不应引入“绕过注册体系直接回放任意组件字节”的第二套主线。

---

## 与查询层的关系

根据 [QueryDesign.md](/d:/UGit/Sideline/godot/scripts/lattice/ECS/QueryDesign.md)，查询层只负责读和遍历。

因此当前两者的正式协作关系是：

- 查询层负责读当前稳定结构
- `CommandBuffer` 负责记录下一次结构变更

这两者不是竞争关系，而是互补关系。

如果后续系统需要：

- 遍历一批实体
- 基于条件决定是否创建、销毁或增删组件

推荐写法仍然应是：

1. 用 `Filter<T...>()` 或 `BlockIterator` 读取
2. 把结构修改写入 `CommandBuffer`
3. 在统一时机执行 playback

---

## 与系统层的关系

当前 `CommandBuffer` 并不自动嵌入 `SystemScheduler`，但系统层已经围绕它形成了明确用法：

- 系统更新阶段可以往命令缓冲写命令
- 系统更新结束后，再由上层决定何时 playback

这意味着当前正式边界是：

- `System` 可以依赖 `CommandBuffer`
- 但 `CommandBuffer` 不依赖 `System`

这也是为什么当前测试里，延迟结构修改闭环通常表现为：

1. 调度器先执行系统
2. 缓冲里留下待执行命令
3. 再显式调用 `Playback(frame)`

这种分层是正确方向，因为它避免了把结构修改时机偷偷塞进系统调度器内部。

---

## 与 `SimulationSession` 的关系

当前虽然 `SimulationSession` 文档尚未正式完成，但 `CommandBuffer` 与 session 的依赖方向已经应当明确：

- session 负责决定每个 tick 的推进顺序
- session 负责决定命令何时 playback
- `CommandBuffer` 不负责 session 生命周期

因此后续最小 session 设计应建立在下面这条链路之上：

```text
系统更新
  ↓
命令写入 CommandBuffer
  ↓
session 在明确时机调用 Playback(frame)
  ↓
Frame 完成该 tick 的结构收敛
```

这条边界能避免未来出现：

- scheduler 决定一部分结构修改时机
- session 再决定另一部分结构修改时机

从而形成两套 mutation 语义。

---

## 序列化与反序列化的当前边界

当前 `CommandBuffer` 已经提供：

- `Serialize()`
- `Deserialize(byte[] data)`

同时在反序列化后会通过 `RecountCreatedEntities()`：

- 重新统计创建命令数量
- 重建临时实体占位表

这说明当前实现已经为“命令流可被外部搬运”提供了基础能力。

但当前正式文档边界应保持克制：

- 可以确认它具备基础序列化能力
- 但不把它直接写成完整网络协议或 replay 文件格式

更准确的定位应是：

- `CommandBuffer` 当前具备“命令流字节化”的底层能力
- 上层如何使用这些字节，属于更后面的 session / network / replay 设计问题

---

## 当前一致性约束

后续所有实现都应遵守下面这些约束。

## 1. 结构修改默认应走 `CommandBuffer`

当前正式主线中：

- 系统遍历期间不应直接修改结构
- 默认应通过命令缓冲延迟执行

如果未来确有极少数特殊场景需要直接结构修改，应把它明确标记为例外路径，而不是反向稀释 `CommandBuffer` 的主线地位。

## 2. 命令顺序具有语义

写入顺序就是 playback 顺序。

因此调用方必须自己保证：

- 命令顺序合理
- 对临时实体的后续命令出现在创建命令之后

## 3. 组件命令必须建立在已注册组件之上

如果组件没有注册完成，就不应写入：

- `AddComponent<T>`
- `RemoveComponent<T>`
- `SetComponent<T>`

这是因为 playback 需要依赖组件类型 ID 和组件命令注册表。

## 4. 临时实体只属于当前缓冲上下文

临时实体不是世界级实体句柄。

它只能在：

- 同一个 `CommandBuffer`
- 同一轮命令流

中被可靠解析。

---

## 当前允许的实现方向

后续围绕 `CommandBuffer` 的实现，下面这些方向是允许的：

- 保持当前五类命令作为最小正式主线
- 保持负索引临时实体模型
- 保持按写入顺序 playback
- 继续加强与 `Frame`、组件注册体系的一致性
- 在不破坏主线边界的前提下继续优化缓冲扩容与命令回放性能

---

## 当前不建议的方向

下面这些方向当前不建议进入主线：

- 让查询层直接承担结构修改
- 在没有正式调度语义前，把 playback 时机偷偷绑定到任意系统内部
- 把 `CommandBuffer` 扩展成多帧世界状态容器
- 把当前基础序列化直接宣称为完整网络协议
- 在没有真实收益前引入复杂命令优化器、命令重排器或自动语义压缩

这些做法都会让底座边界重新失焦。

---

## 后续收敛重点

当前 D4 完成后，`CommandBuffer` 层后续最值得继续收敛的方向是：

1. 明确 playback 的统一接入点应由 `SystemScheduler` 还是 `SimulationSession` 最终掌控
2. 继续验证 `DestroyEntity` 与 `Frame` 销毁路径的一致性
3. 继续验证序列化后的命令流在更复杂场景下的稳定性
4. 在不破坏当前顺序语义的前提下，再评估是否需要更细粒度命令统计或调试可视化

---

## 最终结论

Lattice 当前的 `CommandBuffer` 设计可以概括为：

**`CommandBuffer` 是单帧结构修改的延迟执行层。它负责顺序记录 `Create / Destroy / Add / Remove / Set` 命令，通过负索引临时实体模型支持“先创建再继续修改”，并在 playback 时把这些结构变更统一施加到 `Frame`。**

这一定义把查询、系统更新与结构修改正式分层，为后续 `SystemScheduler` 与 `SimulationSession` 提供了稳定上游语义。
