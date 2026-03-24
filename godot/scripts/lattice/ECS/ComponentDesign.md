# Lattice ECS Component 设计（Phase D1 已完成）

## 文档目的

本文档用于正式定义 Lattice 当前阶段的 `Component` 模型。

这里的重点不是讨论具体业务组件怎么写，而是收敛下面这些底座约束：

- 什么样的类型允许成为组件
- 组件类型身份如何建立
- 组件注册模型是什么
- 为什么命令回放、快照恢复都依赖组件注册
- 当前哪些能力已经正式进入主线，哪些还只是预留

本文档是后续 `Frame / Storage / Query / CommandBuffer / Session` 文档的上游依赖。

---

## 当前定位

在 Lattice 的底座主链路里，`Component` 位于：

```text
Math
  ↓
Component
  ↓
Frame / Storage
  ↓
Query / CommandBuffer
  ↓
SystemScheduler
  ↓
SimulationSession
```

这意味着 `Component` 这一层必须先稳定两件事：

1. 组件的**数据约束**
2. 组件的**类型身份**

如果这两件事不稳定，后面所有内容都会一起漂移：

- `Storage<T>` 不知道如何为组件建表
- `CommandBuffer` 不知道如何按类型回放
- `FrameSnapshot` 不知道如何恢复具体存储
- `SimulationSession` 无法保证回滚后还是同一套组件语义

---

## 设计目标

### 1. 组件必须是纯确定性数据

组件本质上是帧状态的一部分，因此必须满足：

- 可直接存入 `Frame`
- 可被快照复制和恢复
- 可被命令回放按字节解释
- 不引入引用语义和 GC 不确定性

### 2. 组件必须有稳定的类型身份

Lattice 当前用“组件类型 ID”把下面几件事串起来：

- `Frame` 中的 component mask
- `Storage<T>` 的定位
- `CommandBuffer` 的 Add / Set / Remove 回放
- `FrameSnapshot` 中的存储枚举与恢复

因此组件身份不能只停留在 `typeof(T)` 这个反射层，而必须落成底层运行时可用的 ID。

### 3. 组件注册必须同时完成运行时接线

组件注册不只是“拿一个 ID”，还必须同时注册：

- 组件字节大小
- Add 回放入口
- Set 回放入口
- Remove 回放入口
- Storage 快照大小
- Storage 快照写入
- Storage 快照读取
- Storage 创建入口

这就是为什么当前 `ComponentRegistry` 和 `ComponentCommandRegistry` 不能完全分离。

---

## 当前主线模型

当前组件模型由下面三部分组成：

- `ComponentTypeId<T>`
- `ComponentRegistry`
- `ComponentCommandRegistry`

它们分别负责不同层面的事情。

## 1. `ComponentTypeId<T>`

职责：

- 为每个组件类型 `T` 绑定一个运行时唯一的类型 ID
- 作为后续所有组件相关能力的统一索引入口

当前语义：

- `Id == 0` 表示未注册
- `Id > 0` 表示已注册
- 有效范围是 `1 .. Frame.MaxComponentTypes - 1`

当前实现要求：

- 同一组件类型只能注册一次
- 不能重复覆盖已有 ID
- 不能分配到非法范围

## 2. `ComponentRegistry`

职责：

- 负责为组件分配类型 ID
- 负责驱动命令回放注册表同步建立组件元信息

当前主线提供：

- `Register<T>()`
- `EnsureRegistered<T>()`

当前语义：

- `Register<T>()` 用于首次注册
- `EnsureRegistered<T>()` 用于“如果没注册就补注册”

## 3. `ComponentCommandRegistry`

职责：

- 建立组件类型 ID 到底层操作处理器之间的映射
- 让 `CommandBuffer`、`FrameSnapshot`、`Storage<T>` 能按类型 ID 分发

当前主线注册的内容包括：

- 组件大小
- Add handler
- Set handler
- Remove handler
- Storage snapshot size handler
- Storage snapshot write handler
- Storage snapshot read handler
- Storage create handler

这使得 Lattice 当前组件注册，本质上是“类型身份 + 运行时接线”一起完成。

---

## 组件定义约束

## 1. 当前正式要求：组件必须是 `unmanaged`

当前 Lattice 主线把组件约束为：

- `where T : unmanaged`

这是当前最重要、也是最不应该放松的底座约束。

原因如下：

### 原因 A：快照与恢复要求组件可直接复制

当前 `Storage<T>` 和 `FrameSnapshot` 的设计建立在下面这个前提上：

- 组件数据可以安全地按字节写入和恢复

如果组件中包含引用类型，这个前提就会失效。

### 原因 B：命令回放直接按字节解释组件

当前 `CommandBuffer` 在回放组件时，直接按组件大小读取数据并交给对应的 handler。

这要求组件：

- 字节布局可直接搬运
- 不依赖 GC 对象图
- 不包含运行时引用语义

### 原因 C：确定性要求值语义优先

帧同步底座里，组件应只表达状态，而不表达外部对象关系。

因此当前明确禁止：

- `string`
- `class`
- `List<T>`
- `Dictionary<TKey, TValue>`
- 任意托管引用字段

## 2. 当前允许的组件内容

当前主线允许的组件字段类型应以“确定性值类型”为准，包括但不限于：

- 基础整数类型
- `bool`
- 枚举
- `FP`
- `FPVector2`
- `FPVector3`
- 其他满足 `unmanaged` 的确定性结构体

## 3. 当前不建议在底座层直接承诺的内容

下面这些能力虽然未来可能支持，但当前不应在组件主线文档中写成既成事实：

- 自动代码生成组件元数据
- 组件序列化属性系统
- 组件生命周期回调
- 复杂反射扫描注册
- 引擎对象字段桥接

这些都属于更上层或更后期能力。

---

## 类型身份模型

## 1. 为什么需要组件类型 ID

当前 Lattice 不是只靠 `typeof(T)` 运行，而是把组件类型映射为整型 ID。

这样做的核心原因是：

- `Frame` 的 component mask 需要位索引
- `CommandBuffer` 需要定长 header 中的类型标识
- `ComponentCommandRegistry` 需要数组索引分发
- `FrameSnapshot` 恢复时需要按 ID 找回存储创建器

因此，组件类型 ID 不是附加优化，而是当前底座的正式组成部分。

## 2. 当前 ID 分配方式

当前 `ComponentRegistry` 使用全局递增方式分配类型 ID。

也就是说：

- 第一次注册的组件获得较小 ID
- 后续组件继续递增

这在当前阶段可以支撑：

- 单元测试
- 本地模拟
- 当前底座闭环

但这里需要明确说明：

**“运行时按注册顺序递增”是当前实现，不是长期最终形态。**

## 3. 当前已知风险

如果组件 ID 长期依赖“谁先调用谁先注册”，后面会出现几个风险：

- 不同启动路径导致注册顺序变化
- 测试和正式运行环境的类型 ID 不一致
- 快照、命令、回放、存档难以长期稳定

因此，当前文档对这个问题的正式结论是：

- 现阶段允许使用全局递增注册
- 但后续必须收敛到更稳定的注册顺序模型

可选方向包括：

- 显式组件注册清单
- 启动期固定顺序注册
- 代码生成注册表

这属于后续实现收敛项，不在当前 D1 阶段展开。

---

## 注册模型

## 1. 注册不只是分配 ID

当前 Lattice 中，“组件注册”应理解为一次完整接线：

1. 分配类型 ID
2. 绑定 `ComponentTypeId<T>.Id`
3. 绑定 `ComponentCommandRegistry` 的所有处理器

这意味着：

- 组件一旦能被 `Frame.Add<T>()`、`CommandBuffer.AddComponent<T>()`、`FrameSnapshot` 等路径使用
- 它就必须已经完成注册

## 2. `Register<T>()` 的语义

适用场景：

- 启动时显式注册组件
- 测试初始化时一次性建立组件表

当前要求：

- 未注册时才能调用
- 重复调用应失败
- 注册失败不能留下半初始化状态

## 3. `EnsureRegistered<T>()` 的语义

适用场景：

- 单元测试
- 文档示例
- 当前阶段的小规模模块接入

它的优点是：

- 使用简单
- 可以减少样板注册代码

它的局限是：

- 容易把“注册顺序”隐藏起来
- 未来不适合作为完整工程的长期唯一入口

因此当前建议是：

- 测试和底座示例可以继续使用 `EnsureRegistered<T>()`
- 长期应向“明确且稳定的注册入口”收敛

---

## 组件注册与命令回放的关系

这是当前 D1 阶段最需要明确的一点。

## 为什么两者不能分开定义

在 Lattice 当前设计中，`CommandBuffer` 并不知道泛型 `T` 的真实类型，只知道：

- header 中的 `componentTypeId`

回放时它需要根据这个 `componentTypeId` 完成：

- 组件大小查询
- Add / Set / Remove 分发

这意味着：

- 如果一个组件拿到了类型 ID，但没有注册命令回放处理器
- 那么它只能“看起来已注册”，但实际上无法参与完整主线运行

同样，`FrameSnapshot` 在恢复存储时也依赖：

- storage size handler
- storage create handler
- storage read/write handler

所以当前正式结论是：

**组件注册必须同时完成命令回放与存储快照接线。**

这不是实现巧合，而是底座约束。

---

## 组件注册与快照恢复的关系

当前 `FrameSnapshot` 的恢复过程依赖组件类型 ID 找到对应存储能力。

恢复所需的关键信息包括：

- 该组件的存储如何创建
- 该存储如何读取快照
- 该存储的快照大小如何计算

因此，对当前主线来说，一个“真正可用的组件类型”必须同时满足：

- 能在 `Frame` 中创建存储
- 能被 `CommandBuffer` 回放
- 能被 `FrameSnapshot` 恢复

如果缺失其中任一项，它都不是一个完整接入底座的组件。

---

## Flags 与特殊语义

当前代码库中存在两类相关标记：

- `ComponentFlags`
- `StorageFlags`

这两者虽然都和组件有关，但职责不同，必须明确区分。

## 1. `ComponentFlags`

`ComponentFlags` 更接近“组件级语义标记”的概念。

当前定义里包含了类似下面的预留语义：

- `DontSerialize`
- `Singleton`
- `ExcludeFromPrediction`
- `ExcludeFromCheckpoints`
- `SignalOnChanged`
- `DontClearOnRollback`
- `HideInDump`

当前正式结论：

- 这些标记目前主要属于**能力预留**
- 当前底座主线并没有围绕它们建立完整规则
- 因此它们当前不应被写成“已全面接入的正式组件语义系统”

## 2. `StorageFlags`

`StorageFlags` 更接近“存储行为标记”。

当前 `Storage<T>` 主线实际接入的是：

- `Singleton`
- `DeferredRemoval`

这两个标记与存储行为直接相关，因此属于当前更接近正式主线的部分。

## 3. 当前文档结论

当前组件层正式承认两类事实：

- 组件级语义标记体系还没有完全落地
- 存储级行为标记已经开始进入正式主线

因此后续不应混淆：

- “某个组件声明了一个 flag”
- “这个 flag 已经在底座所有路径里生效”

---

## 当前允许的实现方式

后续实现 `Component` 相关能力时，下面这些方向是允许的：

- 继续保持 `unmanaged` 组件约束
- 保持 `ComponentTypeId<T>` 作为底层运行时身份入口
- 保持注册时同步绑定命令回放与快照处理器
- 将注册顺序逐步收敛到稳定模型
- 将 `StorageFlags` 继续作为存储行为层语义推进

---

## 当前不允许或不建议的方向

下面这些方向当前不建议进入底座主线：

- 放宽到允许托管引用组件
- 让组件注册只分配 ID，不注册命令回放
- 让不同模块自行维护各自的组件 ID
- 直接把 `ComponentFlags` 视为全部已经落地
- 依赖引擎层对象作为组件字段的一部分

这些做法都会破坏当前底座的一致性。

---

## 与后续模块的关系

`ComponentDesign` 对后续模块的影响如下：

- `FrameStorageDesign` 将建立在本文件定义的组件身份模型之上
- `QueryDesign` 将建立在组件已注册、可建存储的前提之上
- `CommandBufferDesign` 将直接依赖本文件定义的回放注册模型
- `SimulationSessionDesign` 将假设组件可以参与快照、恢复和回放

因此，如果后续这些文档与本文件冲突，应优先回到本文件和更底层实现确认边界。

---

## 后续收敛重点

当前 D1 完成后，组件层后续最值得继续收敛的方向是：

1. 固定组件注册顺序，避免长期依赖运行时递增
2. 梳理 `ComponentFlags` 与 `StorageFlags` 的正式边界
3. 明确测试、启动期、正式运行时的注册入口规范
4. 为未来代码生成注册表预留稳定接口

---

## 最终结论

Lattice 当前的 `Component` 设计可以概括为：

**组件是满足 `unmanaged` 约束的确定性值类型；组件注册不仅建立类型 ID，也同时完成命令回放与快照恢复所需的运行时接线。**

这一定义是当前底座主链路成立的前提，后续 `Frame / Query / CommandBuffer / Session` 都应建立在这套组件身份模型之上。
