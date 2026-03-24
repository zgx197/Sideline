# Lattice 底座模块文档路线图（Phase D0 - D1 已完成）

## 文档目的

本文档用于梳理 Lattice 在当前阶段真正需要优先沉淀的“底座模块”文档，并明确后续迭代顺序。

这里的“底座模块”特指：

- 会被所有上层能力反复依赖
- 一旦边界不清，后续容易反复返工
- 应优先稳定抽象与约束，再继续向上扩展

本文档的作用不是替代实现设计，而是作为后续文档化与架构收敛的统一迭代依据。

---

## 参考基线

本路线图参考了 FrameSync 框架的整体分层：

- `Core`
- `Simulation`
- `Runtime`

但 **Lattice 不会按 FrameSync 的目录与功能规模一比一复制**。

Lattice 当前阶段只抽取其中最适合作为“底座”的部分，优先沉淀下面几层：

1. 确定性数据模型
2. 帧状态容器
3. 查询与结构修改
4. 系统调度
5. 最小模拟闭环

而下面这些内容当前明确不作为底座优先项：

- 引擎桥接细节
- 运行时 UI / 调试面板
- Replay 文件格式细节
- 网络层实现
- 复杂 Runner 体系
- Physics / NavMesh / Prototype 等高层能力

---

## 当前判断

结合当前 Lattice 实现状态，底座整体方向已经基本正确：

- `FP / Math` 已经相对成熟
- `Component / Storage / Frame / Filter / CommandBuffer / SystemScheduler / SimulationSession` 已形成最小主链路

但当前文档仍然存在几个问题：

- 文档分散，层次不统一
- 一部分旧文档带有“完整框架版”预设，超出了当前底座范围
- 模块之间的依赖方向没有在一份统一文档中收紧
- 某些关键约束还停留在实现习惯层，而不是正式设计层

因此，下一阶段不是继续堆更多上层文档，而是先把底座文档体系收敛稳定。

---

## 文档化原则

后续所有底座文档都应遵循下面 6 条原则。

### 1. 先写稳定边界，再写扩展方向

优先回答：

- 这个模块负责什么
- 不负责什么
- 它依赖谁
- 谁依赖它

不要一开始把未来所有扩展能力都写进来。

### 2. 先写当前主线，再写未来预留

文档主体必须优先描述当前已经落地、或者近期确定要落地的实现路线。

对于未来能力，应统一放在“后续扩展”章节中，避免把预留能力写成既成事实。

### 3. 先底层，后上层

只有当下层模块的边界已经清晰后，才继续文档化它的上层模块。

例如：

- 先写 `Frame / Storage`
- 再写 `Query`
- 再写 `System`
- 再写 `Session`

而不是反过来。

### 4. 先约束，后示例

每份设计文档都应先明确：

- 数据约束
- 生命周期约束
- 一致性约束
- 确定性约束

然后再提供示例代码。

### 5. 不把引擎层混入底座文档

Godot 适配、UI、View、Node 同步、Editor 工具等内容，只能在上层文档出现，不能污染底座模块定义。

### 6. 文档要能直接指导后续实现

每一份底座文档最终都必须能回答：

- 下一步应该先改哪里
- 哪些实现是允许的
- 哪些实现属于越界或过度设计

---

## 模块范围

当前阶段 Lattice 底座文档范围定义如下：

### 底座内

- `Math / FP`
- `Component` 模型
- `EntityRef / Frame / Storage`
- `Filter / Query / BlockIterator`
- `CommandBuffer`
- `SystemScheduler`
- `SimulationSession`
- `Snapshot / Checksum / InputBuffer / Rollback` 的最小闭环约束

### 底座外

- Godot / Unity Bridge
- 实体视图同步
- 运行时面板与交互式调试工具
- Replay 文件工具链
- 网络接入
- 地图、物理、导航等游戏能力
- 编辑器资产配置体系

---

## 优先级总览

后续文档化与架构收敛，统一按下面顺序推进。

| 优先级 | 模块 | 目标文档 | 当前状态 | 为什么先做 |
|------|------|----------|----------|-----------|
| P0 | 总体分层与依赖方向 | `ECS/Architecture.md` | 已完成 | 先统一边界，避免后续每份文档口径不一致 |
| P1 | Component 模型与注册体系 | `ECS/ComponentDesign.md` | 已完成 | 所有存储、查询、回放、快照都依赖组件身份 |
| P2 | Entity / Frame / Storage | `ECS/FrameStorageDesign.md` | 缺失 | 这是整个 ECS 状态容器的核心 |
| P3 | Query / Filter / BlockIterator | `ECS/QueryDesign.md` | 缺失 | 这是系统读取数据的正式入口 |
| P4 | CommandBuffer / 结构修改闭环 | `ECS/CommandBufferDesign.md` | 缺失 | 不先定清楚，系统运行语义会漂 |
| P5 | System 基座 | `ECS/SystemDesign.md` | 已存在，需要继续收敛 | 依赖前面所有数据访问与结构变更约束 |
| P6 | 最小 SimulationSession 闭环 | `ECS/SimulationSessionDesign.md` | 缺失 | 用于稳定 `verified / predicted` 最小模型 |
| P7 | Snapshot / Checksum / Input / Rollback 约束 | `ECS/DeterministicLoopDesign.md` | 缺失 | 这是帧同步闭环，但必须晚于 session 主体 |
| P8 | Runtime Bridge / Runner / Debug Tool | 后续单独文档 | 暂不进入底座主线 | 这是上层运行时，不应倒逼底座设计 |

---

## 逐步实施顺序

### Phase D0：统一总览入口（已完成）

**目标**

把现有 [Architecture.md](/d:/UGit/Sideline/godot/scripts/lattice/ECS/Architecture.md) 收敛为真正的底座总览入口。

**应完成的内容**

- 明确 Lattice 当前底座的模块边界
- 明确 `Math -> Component -> Frame -> Query -> CommandBuffer -> System -> Session` 的依赖方向
- 删除或下沉当前与高层运行时混杂的描述
- 明确哪些能力属于“当前已落地”
- 明确哪些能力属于“后续预留”

**完成标准**

- 新人只看这一份总览文档，就能知道当前 Lattice 的底座范围
- 不再把完整游戏框架、引擎桥接、调试工具混进底座架构图

---

### Phase D1：Component 模型文档（已完成）

**目标文档**

- `ECS/ComponentDesign.md`

**应完成的内容**

- 组件的定义约束：`unmanaged`、禁止引用类型、确定性要求
- 组件类型注册模型
- `ComponentTypeId`
- `ComponentRegistry`
- `ComponentCommandRegistry`
- singleton / deferred removal / flags 的语义边界
- 组件注册与命令回放、快照恢复的关系

**必须回答的问题**

- 什么样的类型允许成为组件
- 组件类型 ID 是否允许纯运行时递增
- 组件注册失败、重复注册、跨模块注册的边界是什么
- 为什么命令回放和组件注册不能分离

**完成标准**

- `Component` 的身份模型和注册模型被正式写清楚
- 后续实现不再依赖隐含约定

---

### Phase D2：Frame / Storage 文档

**目标文档**

- `ECS/FrameStorageDesign.md`

**应完成的内容**

- `EntityRef` 生命周期
- `Frame` 的职责边界
- `Storage<T>` 的稀疏集 + Block 布局
- `Frame` 与 `Storage<T>` 的所有权关系
- 添加、移除、销毁实体时的一致性要求
- `FrameSnapshot` 的职责边界
- restore / clone / checksum 的正式语义

**必须回答的问题**

- `Frame` 持有哪些状态
- `Storage<T>` 持有哪些状态
- 实体销毁时 mask、version、free list、storage 如何保持一致
- snapshot 恢复时哪些数据必须严格一致

**完成标准**

- `Frame` 和 `Storage<T>` 的边界不再模糊
- 后续 rollback 与 session 可以稳定建立在这层之上

---

### Phase D3：Query 文档

**目标文档**

- `ECS/QueryDesign.md`

**应完成的内容**

- `Filter<T...>()` 的定位
- `ComponentBlockIterator<T>` 的定位
- 默认路径与热点路径的分层
- 多组件查询如何选择主遍历源
- 哪些优化属于“当前主线”，哪些属于未来 fast path

**必须回答的问题**

- 当前正式推荐的查询入口是什么
- 热点系统什么时候可以退回 block 级遍历
- owning group / zipped fast path 当前是否属于正式主线

**完成标准**

- 系统层有稳定的数据读取入口
- 文档不会把未来 Query 抽象写成当前事实

---

### Phase D4：CommandBuffer 文档

**目标文档**

- `ECS/CommandBufferDesign.md`

**应完成的内容**

- 为什么结构修改必须延迟执行
- `Create / Add / Set / Remove / Destroy` 的生命周期
- 临时实体映射模型
- playback 的顺序语义
- 它与系统更新、session 推进、回滚重演的关系

**必须回答的问题**

- 为什么系统遍历中不应该直接改结构
- 命令缓冲和 `Frame` 的关系是什么
- playback 时如何解析临时实体

**完成标准**

- 结构修改闭环被正式定义
- 后续系统和 session 不会再各自定义一套 mutation 语义

---

### Phase D5：System 基座文档

**目标文档**

- [SystemDesign.md](/d:/UGit/Sideline/godot/scripts/lattice/ECS/SystemDesign.md)

**应完成的内容**

- `ISystem / SystemBase / SystemGroup / SystemScheduler`
- 生命周期与启停
- 顺序与元数据
- trace hook
- system 无状态契约
- system 对 `Frame / Query / CommandBuffer` 的依赖方式

**必须回答的问题**

- system 能持有哪些状态，不能持有哪些状态
- system 调度与 session 的依赖方向是什么
- 当前为什么只支持单线程调度

**完成标准**

- System 文档只描述系统自身与其直接依赖，不再承担 session 或 runner 的职责

---

### Phase D6：SimulationSession 文档

**目标文档**

- `ECS/SimulationSessionDesign.md`

**应完成的内容**

- 最小 `verified / predicted` 双帧模型
- 输入写入
- 基于最新 verified 重建 predicted
- session 与 scheduler 的关系
- session 与 command buffer 的关系

**必须回答的问题**

- 为什么当前只做最小闭环
- session 负责什么，不负责什么
- 为什么不继续在旧 `Session` 设计上叠加

**完成标准**

- Lattice 当前的最小模拟闭环被正式定义
- 后续 runner / replay / network 可以建立在它之上

---

### Phase D7：确定性循环文档

**目标文档**

- `ECS/DeterministicLoopDesign.md`

**应完成的内容**

- snapshot
- checksum
- verified / predicted input buffer
- rollback / resim 的最小约束
- 哪些内容属于当前实现，哪些属于后续扩展

**必须回答的问题**

- 如何验证同输入同结果
- 什么时候需要 rollback
- snapshot 与 replay 的关系是什么

**完成标准**

- 帧同步闭环的核心约束被写清
- 但不把网络层、文件格式、运行时工具提前拉进来

---

### Phase D8：上层运行时文档

**目标**

在底座稳定后，再逐步文档化以下上层能力：

- Bridge
- Runner
- View 同步
- 调试工具
- 交互式观察器

**说明**

这一阶段不属于当前底座主线，因此不纳入本路线图的优先推进目标。

---

## 当前建议的文档文件布局

建议在 `godot/scripts/lattice/ECS/` 下逐步形成下面的文档结构：

```text
ECS/
├── Architecture.md
├── FoundationDocumentationRoadmap.md
├── ComponentDesign.md
├── FrameStorageDesign.md
├── QueryDesign.md
├── CommandBufferDesign.md
├── SystemDesign.md
├── SimulationSessionDesign.md
└── DeterministicLoopDesign.md
```

其中：

- `Architecture.md` 负责总览
- `FoundationDocumentationRoadmap.md` 负责迭代顺序
- 其余文档各自只负责一个模块

---

## 文档之间的依赖顺序

后续编写时必须遵守下面的依赖方向：

```text
Architecture
    ↓
ComponentDesign
    ↓
FrameStorageDesign
    ↓
QueryDesign
    ↓
CommandBufferDesign
    ↓
SystemDesign
    ↓
SimulationSessionDesign
    ↓
DeterministicLoopDesign
```

约束如下：

- 后面的文档不能反过来定义前面模块的核心语义
- 上层文档只能引用下层文档，不能修改下层职责
- 如果出现边界冲突，应优先回到更底层文档修正

---

## 近期迭代建议

按照当前实现状态，建议后续按下面顺序推进：

1. 新建 `FrameStorageDesign.md`
2. 新建 `QueryDesign.md`
3. 新建 `CommandBufferDesign.md`
4. 回收并继续整理 `SystemDesign.md`
5. 新建 `SimulationSessionDesign.md`
6. 新建 `DeterministicLoopDesign.md`

---

## 最终结论

Lattice 当前最需要的不是继续快速扩展更多运行时能力，而是先把底座模块的文档边界稳定下来。

后续应始终坚持下面这条主线：

**先稳定 `Component / Frame / Query / CommandBuffer / System / Session`，再继续向 Runner、Bridge、Replay、Debug Tool 扩展。**

本路线图即为后续底座文档化与架构收敛的统一迭代依据。
