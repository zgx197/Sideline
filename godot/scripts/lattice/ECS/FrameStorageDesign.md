# Lattice ECS Frame / Storage 设计（Phase D2 已完成）

## 文档目的

本文档用于正式定义 Lattice 当前阶段的：

- `EntityRef`
- `Frame`
- `Storage<T>`
- `FrameSnapshot`

这几者组成的单帧状态容器模型。

本文档的重点不是描述所有性能技巧，而是收敛下面几类底座边界：

- `Frame` 负责什么，不负责什么
- `Storage<T>` 负责什么，不负责什么
- 实体生命周期如何与组件存储保持一致
- 快照、恢复、克隆、校验和的正式语义是什么

本文档是后续 `Query / CommandBuffer / SimulationSession` 的直接上游文档。

---

## 当前定位

在 Lattice 当前底座链路中，`Frame / Storage` 位于：

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

这层的核心作用是：

- 把某一时刻的 ECS 状态以确定性方式保存下来
- 为查询、结构修改、系统执行和快照恢复提供统一状态容器

如果 `Frame / Storage` 边界不稳，后面所有层都会失去可靠前提：

- `Query` 不知道自己遍历的是不是完整且一致的实体集合
- `CommandBuffer` 不知道 playback 后的结构状态是否可靠
- `SystemScheduler` 不知道系统读取到的状态是否自洽
- `SimulationSession` 不知道 snapshot / clone / rollback 是否可信

---

## 设计目标

### 1. `Frame` 表示单帧完整状态

`Frame` 不是世界管理器，也不是 Runner，更不是系统调度器。

当前正式定位是：

- 表示某一个 tick 下的 ECS 完整状态
- 提供实体与组件的直接访问入口
- 提供快照、恢复、克隆和校验和能力

### 2. `Storage<T>` 表示单一组件类型的存储后端

`Storage<T>` 的职责是：

- 管理某一组件类型的所有实例
- 提供 O(1) 的增删查改
- 提供可快照、可恢复的底层字节表示

它不负责：

- 多帧历史
- 系统调度
- 网络同步

### 3. 实体生命周期必须和组件状态保持一致

一个实体在 `Frame` 内部的有效性，必须与：

- 实体版本
- 空闲链表
- 组件 mask
- 对应 `Storage<T>` 中的存在性

尽可能保持一致。

当前实现已经具备这套一致性模型的主体，但仍有一部分销毁路径需要继续收紧。

### 4. 快照语义必须建立在正式状态模型上

`FrameSnapshot` 不是调试日志，也不是差量包。

当前正式定位是：

- 某一帧状态的完整、可恢复表示
- 用于 clone、rollback、checksum 的基础载体

---

## 核心对象与职责

## 1. `EntityRef`

`EntityRef` 是实体身份句柄。

它当前承担两层语义：

- `Index`：实体槽位
- `Version`：实体版本

这意味着实体是否有效，不取决于“是否存在某个索引”，而取决于：

- 槽位索引合法
- 当前版本与句柄版本一致

因此，实体复用不是问题本身，**旧句柄在版本变化后失效** 才是正式语义。

## 2. `Frame`

`Frame` 是当前单帧状态容器。

它当前直接持有：

- 实体数组
- 实体版本数组
- 空闲链表数组
- 实体组件位图
- 组件存储表
- 分配器
- `Tick`
- `DeltaTime`

它当前对外暴露的正式能力包括：

- `CreateEntity`
- `DestroyEntity`
- `Add<T> / Remove<T>`
- `Get<T> / GetPointer<T> / TryGet<T> / Has<T>`
- `Filter<T...>()`
- `GetComponentBlockIterator<T>()`
- `CreateSnapshot / RestoreFromSnapshot / Clone / CalculateChecksum`

## 3. `Storage<T>`

`Storage<T>` 是单组件类型存储。

它当前采用：

- 稀疏映射：`Entity.Index -> GlobalIndex`
- Block 存储：`PackedHandles + PackedData`
- 1-based 索引，`0` 作为 tombstone

它当前对外暴露的正式能力包括：

- `Add`
- `Remove`
- `Get / GetPointer / TryGet / Has`
- block 级访问
- `WriteSnapshot / ReadSnapshot`
- singleton / deferred removal 行为

## 4. `FrameSnapshot`

`FrameSnapshot` 是 `Frame` 的完整快照载体。

它当前保存：

- `Tick`
- `DeltaTime`
- `EntityCapacity`
- 原始快照字节
- 对应 checksum

它不负责解释具体字段，只负责作为“可恢复的完整快照对象”存在。

---

## `Frame` 的正式职责边界

## `Frame` 负责的事情

当前 `Frame` 负责：

1. 实体槽位管理
2. 实体有效性判定
3. 组件 mask 管理
4. 组件存储表持有
5. 组件操作入口协调
6. 查询入口暴露
7. 快照、恢复、克隆、校验和

## `Frame` 不负责的事情

当前 `Frame` 不负责：

- 多帧历史管理
- verified / predicted 协调
- 输入缓冲策略
- system 生命周期
- runner 主循环
- 网络协议

如果后续这些能力需要使用 `Frame`，应建立在它之上，而不是把它们塞回 `Frame` 内部。

---

## `Storage<T>` 的正式职责边界

## `Storage<T>` 负责的事情

当前 `Storage<T>` 负责：

1. 管理某个组件类型的实例数据
2. 通过稀疏映射实现 O(1) 查询
3. 通过 block 数据布局提升遍历效率
4. 维护组件级版本、自身 flags、singleton 状态、deferred removal 状态
5. 提供本组件类型的快照读写能力

## `Storage<T>` 不负责的事情

当前 `Storage<T>` 不负责：

- 实体是否整体有效
- system 遍历顺序
- session 级回滚策略
- 组件类型注册
- 命令缓冲排队

`Storage<T>` 只知道“这个实体在当前组件存储里是否存在”，不知道整个 `Frame` 层面的实体是否仍然是有效活动实体。

---

## 当前内存与数据模型

## 1. `Frame` 级数据

`Frame` 当前内部维护四类实体级数据：

- `_entities`
- `_entityVersions`
- `_entityNextFree`
- `_entityComponentMasks`

这些数据共同定义了：

- 哪些槽位已分配
- 哪些句柄仍有效
- 哪些槽位已回收到 free list
- 每个实体当前拥有哪些组件类型

## 2. `Storage<T>` 级数据

`Storage<T>` 当前内部维护：

- `_sparse`
- `_blocks`
- `_count`
- `_version`
- `_flags`
- `_singletonSparse`
- `_pendingRemoval`

这套模型让它既能：

- 做随机访问
- 做 block 遍历
- 做快照恢复

又能在一定程度上支持 singleton 与 deferred removal 语义。

## 3. `Frame` 与 `Storage<T>` 的关系

当前正式关系是：

- `Frame` 持有所有已初始化组件存储的入口
- 每个 `Storage<T>` 只管理某一个组件类型
- `Frame` 通过 component type id 找到对应存储
- `Storage<T>` 不反向知道 `SystemScheduler`、`Session` 等更上层对象

这是一种“单帧状态容器持有多组件存储后端”的关系，而不是每个组件自己独立存在于世界之外。

---

## 实体生命周期模型

## 1. 创建实体

当前 `CreateEntity` 的正式语义是：

- 优先复用 free list 槽位
- 否则分配新槽位
- 清空对应的组件 mask
- 返回新的 `EntityRef(index, version)`

实体是否“新建”并不重要，重要的是：

- 返回的句柄版本必须与当前槽位版本一致

## 2. 实体有效性

当前 `IsValid` 的正式判断标准是：

- `Index < _entityCount`
- `_entityVersions[index] == entity.Version`

这意味着：

- 索引合法但版本不匹配的句柄，也是无效实体
- 实体复用后，旧句柄必须失效

## 3. 销毁实体

当前设计意图非常明确：

- 销毁实体时，应删除其所有组件
- 版本递增，使旧句柄失效
- 将槽位放回空闲链表

但这里要明确记录一个当前实现现状：

**当前 `DestroyEntity` 的设计方向是正确的，但其内部销毁路径还没有完全实现到“同步清理所有 `Storage<T>` 数据”的严格程度。**

也就是说：

- 当前语义已经明确要求实体销毁应与组件状态保持一致
- 但实现层仍需继续收紧，避免只清 mask 而未完全移除实际存储项

这不是架构方向问题，而是当前实现需要继续补齐的地方。

---

## `Frame` 与 `Storage<T>` 的一致性要求

后续所有实现都必须遵守下面这些一致性要求。

## 1. `Has<T>` 与组件 mask 语义一致

从 `Frame` 视角看，一个实体是否拥有某组件，当前正式入口是：

- `Frame.Has<T>(entity)`

这层语义以 `Frame` 的 component mask 为准。

## 2. `Storage<T>.Has(entity)` 与真实存储存在性一致

从 `Storage<T>` 视角看，一个实体是否存在于该存储中，以 `_sparse` 为准。

## 3. 理想状态下两者应保持一致

也就是说，在正式语义上：

- 如果 `Frame` 认为实体有该组件
- 那么对应 `Storage<T>` 里也应存在该组件实例

反之亦然。

当前需要继续收敛的重点，就是把这层一致性从“多数路径成立”进一步收紧到“销毁与恢复路径也完全成立”。

---

## `Storage<T>` 的核心行为语义

## 1. Add

当前 `Storage<T>.Add` 的正式语义是：

- 实体索引必须合法
- 组件不得已存在
- 必要时扩容 block
- 把实体句柄与组件数据写入 packed block
- 更新 `_sparse`
- 更新 `_version`

如果是 singleton 存储，还必须额外保证：

- 同一时刻最多只有一个实例

## 2. Remove

当前 `Storage<T>.Remove` 有两种语义：

- 普通模式：立即删除
- deferred removal 模式：标记为待删除

普通模式下：

- 通过与末尾交换保持 packed 布局
- 更新被交换实体的 `_sparse`
- 清空被删实体索引

这决定了：

- `Storage<T>` 不是稳定顺序存储
- 它更强调稠密布局和查询效率

## 3. Get / GetPointer / TryGet / Has

这几类 API 的正式语义是：

- 查询的是当前组件存储中的存在性与数据
- 不自动代表实体在整个 `Frame` 层级仍有效

因此上层查询和系统遍历仍然应结合 `Frame.IsValid(entity)` 使用。

## 4. DeferredRemoval

`StorageFlags.DeferredRemoval` 当前已经进入主线设计，但仍属于“局部能力已落地、整帧提交策略尚未完全统一”的状态。

当前正式语义是：

- `Remove` 可以只标记待删除
- `CommitRemovals` 再真正删除

但它还需要上层 `Frame` 或其他协调者提供明确提交时机。

因此它当前属于：

- 已存在的底层行为能力
- 但仍需进一步纳入完整 frame-level 生命周期协调

---

## 快照模型

## 1. `FrameSnapshot` 的定位

`FrameSnapshot` 当前正式定位是：

- `Frame` 在某个时刻的完整字节快照

它不是：

- 差量快照
- 网络包
- 持久化文件格式
- 调试 dump 文本

## 2. `CreateSnapshot`

当前 `Frame.CreateSnapshot()` 会序列化：

- 快照格式版本
- `Tick`
- `DeltaTime`
- `EntityCapacity`
- `_entityCount`
- `_freeListHead`
- 实体数组
- 实体版本数组
- 空闲链表数组
- 组件 mask
- 已初始化存储数量
- 每个存储的类型 ID、大小与原始快照数据

因此当前语义非常明确：

**这是完整单帧快照，而不是“按需字段快照”。**

## 3. `RestoreFromSnapshot`

当前 `Frame.RestoreFromSnapshot(snapshot)` 的正式语义是：

- 快照容量必须匹配当前 `Frame`
- 恢复 tick 与 deltaTime
- 恢复实体级数组和 component mask
- 按快照重建或恢复所有已初始化组件存储

这套模型已经足以支撑：

- clone
- checksum 验证
- 最小 session 重建 predicted frame

## 4. 当前实现风险记录

这里要明确记下一条非常重要的当前现状：

**当前快照恢复模型方向是正确的，但“长期高频 restore 下的资源与生命周期硬化”仍需后续继续加强。**

也就是说，当前它已经是正式主线能力，但仍不是“已经完全打磨到最终形态”的结束状态。

---

## Clone 与 Checksum 语义

## 1. Clone

当前 `Frame.Clone()` 的正式语义是：

- 先创建完整快照
- 再用相同容量新建一个 `Frame`
- 再从快照恢复该 `Frame`

因此 clone 当前不是共享存储，也不是写时复制，而是：

- 基于完整快照恢复出的独立帧对象

## 2. Checksum

当前 `Frame.CalculateChecksum()` 的正式语义是：

- 对当前完整快照字节执行确定性 hash

因此 checksum 当前绑定的是：

- 当前单帧完整状态

而不是某个组件子集、某个系统子集或某个网络层视图。

---

## 所有权与生命周期

## 1. `Frame` 拥有分配器

当前 `Frame` 在构造时创建内部 `Allocator`，并通过它分配：

- 实体数组
- mask
- 存储表
- 组件存储

这意味着当前所有权模型是：

- `Frame` 拥有分配器
- 分配器再拥有该帧内所有底层内存

## 2. `Storage<T>` 通常不独立拥有分配器

在当前 `Frame` 主线下，`Storage<T>` 一般通过 `Frame` 的分配器创建。

因此在这个模式下：

- `Storage<T>` 生命周期受 `Frame` 管理

虽然 `Storage<T>` 本身也支持内部 allocator 初始化，但那更接近低层独立使用能力，而不是当前 `Frame` 主线的默认模式。

## 3. `Frame.Dispose()`

当前 `Frame.Dispose()` 的正式语义是：

- 结束整个单帧状态容器生命周期
- 释放其 allocator 及由其分配的内存

当前实现中，组件存储释放主要依赖 allocator 统一回收，这符合当前所有权模型。

---

## 当前允许的实现方向

后续围绕 `Frame / Storage` 的实现，下面这些方向是允许的：

- 保持 `Frame` 作为单帧完整状态容器
- 保持 `Storage<T>` 作为单组件类型存储后端
- 继续沿用稀疏集 + packed block 布局
- 继续沿用 `FrameSnapshot` 作为完整快照载体
- 继续用 checksum 绑定完整单帧状态
- 继续强化实体销毁与存储清理的一致性

---

## 当前不建议的方向

下面这些方向当前不建议进入主线：

- 让 `Frame` 承担多帧历史与 session 策略
- 让 `Storage<T>` 反向依赖 system 或 session
- 把快照语义拆成未经正式设计的多套局部格式
- 绕过 `Frame` 直接让上层长期持有内部存储指针
- 在还未明确一致性规则前继续扩展更多特殊存储路径

这些都会破坏当前底座边界。

---

## 与后续模块的关系

`FrameStorageDesign` 对后续文档的影响如下：

- `QueryDesign` 将建立在 `Frame` 作为统一查询入口的前提上
- `CommandBufferDesign` 将建立在 `Frame` 提供结构修改落点的前提上
- `SystemDesign` 将建立在 `Frame` 提供系统读取与写入状态的前提上
- `SimulationSessionDesign` 将建立在 clone / snapshot / checksum 正式语义之上

因此，如果后续文档试图重新定义 `Frame` 的角色，应优先回到本文件确认边界。

---

## 后续收敛重点

当前 D2 完成后，`Frame / Storage` 层后续最值得继续收敛的方向是：

1. 补齐实体销毁后的真实存储清理闭环
2. 收紧 `Frame` 与 `Storage<T>` 在 restore 路径上的生命周期一致性
3. 明确 deferred removal 的 frame-level 提交时机
4. 继续验证长期高频 snapshot / restore 的稳定性

---

## 最终结论

Lattice 当前的 `Frame / Storage` 设计可以概括为：

**`Frame` 是单帧完整状态容器，负责实体、组件 mask、组件存储表和快照语义；`Storage<T>` 是单组件类型的稠密存储后端，负责实例数据、稀疏映射和组件级快照读写。**

这一定义为当前 `Query / CommandBuffer / System / SimulationSession` 提供了正式的状态基础。
