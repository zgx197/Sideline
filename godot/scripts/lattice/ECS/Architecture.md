# Lattice ECS 架构设计（历史归档）

> 历史归档文档：
> 本文反映的是较早阶段对“完整游戏框架版 ECS”的设想，其中大量 `SystemGroup`、多线程、旧快照与完整产品层能力描述不等同于当前主干。
> 当前实现状态请优先以 `godot/scripts/lattice/README.md` 和 `godot/scripts/lattice/ECS/Framework/SystemDesignNotes.md` 为准。

## 文档版本
- **Version**: 1.0（历史归档）
- **Last Updated**: 2026-03-18
- **Status**: Archived / 非当前主干实现说明

---

## 当前定位

Lattice 当前阶段的目标，不是复制一个完整版 FrameSync，也不是立刻覆盖运行时、回放、联机、调试工具的全部能力。

当前真正要先稳定的是底座主链路：

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

这条链路一旦边界清晰，后续无论是：

- Godot Bridge
- 回滚 / 重模拟
- 本地 Runner
- Replay
- 调试工具

都可以建立在它之上，而不需要反复推翻底层设计。

---

## 设计目标

### 1. 确定性优先

相同输入必须得到相同输出。

这意味着：

- 数值计算必须走确定性数学
- 组件必须是纯值类型
- 系统执行顺序必须稳定
- 结构修改必须有明确时机
- 快照、恢复、校验和必须建立在正式状态模型上

### 2. 先收敛底座，再向上扩展

Lattice 当前不追求一次性做完所有层。

当前阶段优先稳定：

- 数据模型
- 状态容器
- 查询路径
- 结构修改闭环
- 系统调度
- 最小模拟闭环

### 3. 单线程主线优先

当前底座以单线程确定性模拟为主线。

并行、Job 化、线程化 System 目前只做边界预留，不作为当前主线设计前提。

### 4. 引擎无关

底座必须保持纯 C#、纯模拟层语义。

Godot 只应通过桥接层接入，不应反向污染底座抽象。

---

## 模块范围

## 属于当前底座的模块

当前 Lattice ECS 底座只包含下面这些模块：

- `Math / FP`
- `Component` 模型与类型注册
- `EntityRef / Frame / Storage`
- `Filter / BlockIterator`
- `CommandBuffer`
- `ISystem / SystemBase / SystemGroup / SystemScheduler`
- `SimulationSession`
- `FrameSnapshot / Checksum / 输入缓冲` 的最小闭环

## 明确不属于当前底座主线的模块

下面这些内容当前不属于底座主线：

- Godot Bridge
- EntityView / Node 同步
- 运行时面板
- 交互式调试工具
- Replay 文件工具链
- 网络同步
- 地图、物理、导航等游戏能力
- 编辑器资产配置体系

这些能力后续仍然会做，但必须建立在底座稳定之后。

---

## 分层模型

当前建议把 Lattice ECS 底座理解为下面四层：

```text
┌─────────────────────────────────────────────┐
│ Session Layer                               │
│ - SimulationSession                         │
│ - verified / predicted 最小双帧闭环          │
├─────────────────────────────────────────────┤
│ System Layer                                │
│ - ISystem / SystemBase / SystemGroup        │
│ - SystemScheduler                           │
│ - 生命周期、顺序、trace                     │
├─────────────────────────────────────────────┤
│ State Layer                                 │
│ - Frame                                     │
│ - Storage<T>                                │
│ - Filter / BlockIterator                    │
│ - CommandBuffer                             │
│ - FrameSnapshot / Checksum                  │
├─────────────────────────────────────────────┤
│ Deterministic Core                          │
│ - FP / FPMath / FPVector                    │
│ - DeterministicHash                         │
│ - Allocator / BitStream 等基础设施          │
└─────────────────────────────────────────────┘
```

需要注意：

- `System` 层不拥有游戏状态
- `Session` 层不重新定义 system 语义
- `Frame` 是单帧状态容器，不是完整运行时
- `Runtime / Bridge` 在这四层之外

---

## 依赖方向

底座内部只允许下面这种单向依赖：

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

这条依赖方向的含义是：

- `Component` 可以依赖 `Math`
- `Frame` 可以依赖 `Component`
- `Query` 和 `CommandBuffer` 建立在 `Frame` 之上
- `SystemScheduler` 只协调 `Frame`、`Query`、`CommandBuffer`
- `SimulationSession` 只负责组织 `Frame + Scheduler + Input`

反过来不允许：

- `Frame` 去依赖 `SystemScheduler`
- `Storage<T>` 去依赖 `Session`
- `System` 去定义新的 snapshot 规则
- `Session` 去重新发明 system 生命周期

---

## 核心职责划分

## 1. Math

职责：

- 提供确定性数值基础
- 提供所有模拟层共用的数学类型

当前主线：

- `FP`
- `FPVector2`
- `FPVector3`
- `FPMath`

不负责：

- ECS 状态管理
- 游戏对象生命周期
- 系统调度

## 2. Component

职责：

- 定义组件必须满足的约束
- 维护组件类型身份
- 为命令回放与快照恢复提供类型入口

当前主线：

- `ComponentTypeId<T>`
- `ComponentRegistry`
- `ComponentCommandRegistry`

不负责：

- 存储组件实例数据
- 调度系统
- 管理模拟帧

## 3. Frame / Storage

职责：

- 表示某一时刻的完整 ECS 状态
- 管理实体、组件、位图、存储与快照

当前主线：

- `Frame`
- `Storage<T>`
- `FrameSnapshot`

不负责：

- 多帧历史策略
- 系统装配
- 网络输入同步

## 4. Query

职责：

- 为系统提供稳定的数据读取入口

当前主线：

- `frame.Filter<T...>()`
- `frame.GetComponentBlockIterator<T>()`

分层约定：

- `Filter<T...>()` 是普通系统的正式入口
- `BlockIterator` 是热点路径入口
- 更激进的 owning group / zipped fast path 暂不属于默认主线

## 5. CommandBuffer

职责：

- 承接系统中的结构修改请求
- 在稳定时机回放结构变更

当前主线：

- `CreateEntity`
- `AddComponent`
- `SetComponent`
- `RemoveComponent`
- `DestroyEntity`

不负责：

- 系统调度
- 历史输入管理
- 网络序列化协议设计

## 6. SystemScheduler

职责：

- 管理系统注册、初始化、启停、顺序和更新
- 为 system runtime 提供正式生命周期

当前主线：

- `ISystem`
- `SystemBase`
- `SystemGroup`
- `SystemMetadata`
- `SystemScheduler`

关键约束：

- system 不应持有可变模拟状态
- system 不能把具体 `Frame` 的内部指针缓存成长期状态
- 顺序必须显式且稳定

## 7. SimulationSession

职责：

- 组织最小模拟闭环
- 管理 `verified / predicted` 双帧
- 管理最小输入缓冲
- 用 `SystemScheduler` 推进模拟

当前主线：

- `SimulationSession<TInput>`
- `SimulationSessionOptions`
- `SimulationCommandBufferHost`

不负责：

- 完整 replay 文件体系
- 网络层
- 引擎驱动主循环

---

## 当前实现状态

## 已基本落地

下面这些能力已经形成当前底座主线：

- 确定性数学底座
- `ComponentTypeId / ComponentRegistry`
- `Frame / Storage<T>`
- `Filter<T...>()`
- `CommandBuffer`
- `ISystem / SystemBase / SystemGroup / SystemScheduler`
- 最小 `SimulationSession`
- `FrameSnapshot / checksum` 的第一版能力

## 已经明确但仍需继续收紧

下面这些点方向已经确定，但实现与文档还要继续硬化：

- 组件类型注册的稳定性约束
- `Frame` 与 `Storage<T>` 的生命周期一致性
- 实体销毁后的真实存储清理语义
- snapshot / restore 的长期运行边界
- system 无状态契约
- query 默认路径与热点路径的正式分层

## 当前明确不做

当前阶段底座主线明确不做：

- 线程化 system runtime
- 复杂 runner 层
- 网络同步实现
- 上层调试 UI
- Godot 视图同步

---

## 与 FrameSync 的关系

Lattice 当前的设计思路明确参考了 FrameSync，但不直接复制其完整规模。

当前主要借鉴的是下面几类结构性经验：

- 底层确定性数学必须先稳定
- `Frame` 应作为正式状态容器存在
- `System` 应有明确生命周期、层级和调度器
- `Session / SimulationCore` 应建立在稳定 system runtime 之上
- Runtime / Bridge / Replay / Tooling 应放在更上层

Lattice 当前明确不照搬的部分包括：

- 资产化系统配置体系
- 大量派生 system 基类
- 过早引入完整 runner / replay / network 体系
- 引擎运行时细节先行

---

## 当前架构约束

后续所有底座演进都应遵守下面几条约束。

### 1. 底层模块不能反向依赖上层模块

例如：

- `Frame` 不能依赖 `SimulationSession`
- `Storage<T>` 不能依赖 `SystemScheduler`

### 2. Session 不能重新定义 System 语义

`SimulationSession` 只能组织：

- frame
- scheduler
- input
- command buffer

而不能重新发明 system 生命周期与调度规则。

### 3. System 不能拥有模拟状态

所有可回滚状态必须放在：

- `Frame`
- 组件
- 全局组件或单例组件

而不是放在 `ISystem` 实例字段里。

### 4. 查询路径必须分层，而不是混用

后续默认推荐路径应始终清晰：

- 普通路径：`Filter<T...>()`
- 热点路径：`BlockIterator`

不要让所有系统直接下沉到最底层遍历 API。

### 5. 上层运行时不能倒逼底座抽象

如果未来 Bridge、Runner、调试工具需要额外能力，应优先评估是否能在不破坏底座职责的前提下扩展。

只有在底层职责本身真的缺失时，才回到底座文档修正。

---

## 后续文档顺序

当前底座文档化与架构收敛，统一按下面顺序推进：

1. `Architecture.md`
2. `ComponentDesign.md`
3. `FrameStorageDesign.md`
4. `QueryDesign.md`
5. `CommandBufferDesign.md`
6. `SystemDesign.md`
7. `SimulationSessionDesign.md`
8. `DeterministicLoopDesign.md`

详细迭代顺序见 [FoundationDocumentationRoadmap.md](/d:/UGit/Sideline/godot/scripts/lattice/ECS/FoundationDocumentationRoadmap.md)。

---

## 最终结论

Lattice 当前阶段最重要的事情，不是扩展更多运行时能力，而是先稳定下面这条底座主链路：

**`Math -> Component -> Frame / Storage -> Query -> CommandBuffer -> SystemScheduler -> SimulationSession`**

只要这条链路的边界和依赖方向持续保持稳定，后续无论是回滚、Runner、Bridge、Replay 还是调试工具，都可以在不推翻底座的前提下继续生长。
