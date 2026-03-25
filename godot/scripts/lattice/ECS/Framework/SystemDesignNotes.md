# Lattice 运行闭环实施文档

## 目标

本文档替代旧的 `SystemDesignNotes`，用于回答一个更直接的问题：

**Lattice 现在离“真正可落地的运行闭环”还有多远，以及我们应该如何把它补到至少 7 分。**

这里的“7 分”不是宣传口径，而是工程口径：

- 能编译进主干产物，不依赖文档脑补
- 能稳定创建帧、注册组件、创建实体、执行系统
- 有固定帧顺序和确定性的主更新循环
- 有最小可用的输入、历史帧、检查点、回滚/重模拟能力
- 能用 1 到 2 个真实样例系统跑通完整链路
- 文档描述与真实代码一致

本阶段不追求 FrameSync 的完整产品面，不追求一上来做到 9 分或 10 分。

---

## 实施状态（2026-03-25）

截至当前主干，Phase 1 到 Phase 3 的最小骨架已经落地：

- [x] `Session` 已重新进入正式编译面
- [x] `ISystem` 已进入正式编译面
- [x] `SystemScheduler` 已进入正式编译面
- [x] `Session` 已通过 `SystemScheduler` 驱动系统
- [x] `AdvanceFrame()` 已切到 `Frame.CopyStateFrom()` 的 direct copy 推进
- [x] `Frame.CalculateChecksum()` 已切到流式 checksum writer，不再走完整 `FrameSnapshot` 哈希
- [x] `Checkpoint` 与 sampled history 已切到 `PackedFrameSnapshot`
- [x] `InputBuffer` 已切到固定窗口 ring buffer，并具备按 Tick 存取与容量淘汰能力
- [x] `Checkpoint / Restore / Rollback / Resimulate` 已接通最小闭环
- [x] `SystemScheduler` 与 `Session` 均已有独立自动化测试
- [x] `MovementSystem + LifetimeSystem` 真实样例已进入主干
- [x] 端到端“创建实体 -> 输入 -> 系统更新 -> 回滚”最小样例已补齐
- [x] `SessionRunner` 已进入正式编译面
- [x] `Session` 状态边界与错误用法边界已进一步收紧
- [x] 运行时坏场景测试已形成第一轮回归集
- [x] `SessionRunnerBuilder` 已进入正式编译面
- [x] 第二条更接近玩法的样例链路（`SpawnerSystem`）已进入主干
- [x] 8.5 分固定回归测试入口已建立
- [x] `Session` 历史帧已改为有界 O(1) 按 Tick 访问
- [x] 历史帧替换与淘汰时的 `Frame` 释放路径已补齐
- [x] `Frame` 的 snapshot restore 双链路已完成第一轮公共 helper 收口
- [x] `CountSerializableStorages()` 已从 packed writer 的双遍重复工作中拆出
- [x] `SessionRuntimeBenchmarks` 已进入性能观察面
- [x] 已补充独立的 `Lattice.RuntimeBenchmarks` runner 项目，用于执行 Session 基线 benchmark
- [x] 历史读取 benchmark 已拆分为 hot rebuild / cold materialize / sequential read 三类画像
- [x] sampled history 的 snapshot anchor 查找已切到有序索引，不再全表扫描
- [x] 历史 materialize 已补小范围 cache，用于连续前向读取复用
- [x] `TryMaterializeHistoricalFrame()` 冷 replay 已切到原地前推快路径，去掉每 Tick 的整帧 copy/recycle；若派生类 override `AdvanceFrame()` 则自动回退旧语义
- [x] `PackedFrameSnapshot` 写出已去掉最终 `ToArray()` 整包复制
- [x] `MixedSerialized` 已补固定 payload 直写/直读路径，避免每条目临时 `BitStream`/`byte[]` 中转
- [x] sampled history materialize 已补同 Tick scratch 命中，避免重复历史重建
- [x] 第二轮坏场景测试已补 `Session / SessionRunner / SessionRunnerBuilder` 的主要生命周期误用与组合边界
- [x] `Session` 的本地语义 / 预测验证语义边界已完成一轮文档收口
- [x] 已引入很薄的 `SessionRuntimeOptions`，承载稳定公开的运行参数 `DeltaTime / LocalPlayerId`
- [x] `README` 与 `SystemDesignNotes` 的历史架构描述已完成第二轮收口
- [x] `FrameSnapshot` 兼容入口已补 `Obsolete` / 隐藏默认曝光标记
- [x] `FrameSnapshot / Filter / FilterOptimized` 的保留范围、禁止新用法与未来收口前置条件已形成兼容清单
- [x] `ECS/Architecture.md` 与 `ECS/PERFORMANCE_OPTIMIZATION.md` 已补历史归档声明，避免继续误导当前主干认知

当前判断：

**Lattice 已经从“只有不错的 ECS 基座”推进到“最小运行闭环已存在”，并完成了 8.5 分目标中的两轮核心补强以及一轮运行时热点收口；接下来更适合进入真实玩法承接与有证据的 benchmark 驱动优化。**

---

## 当前判断

基于当前 `main` 分支代码，以及对 FrameSync 源码目录
`D:\UGit\unity_mini_game\UnityProject\Packages\com.ifreetalk.framesync-framework`
的横向对比，可以先给出一个整体结论：

**Lattice 已经具备不错的“确定性内核”，但还没有形成真正的“模拟运行时”。**

更具体地说：

- `FP / Entity / Component / Storage / Query / FrameSnapshot` 已经达到较高完成度
- `System / Session` 已补出最小运行骨架，但 `Runner / 配置驱动装配 / 真实业务样例系统` 仍明显落后于 FrameSync
- 当前最大的短板已经从“运行层完全没接起来”转为“运行层虽能工作，但还缺真实业务链路验证”

---

## 模块评分

以下评分用于帮助我们判断优先级，不代表对代码质量的绝对评价。

| 模块 | Lattice | FrameSync | 判断 |
|------|---------|-----------|------|
| `FP` | 7.5/10 | 9.5/10 | 核心可用，但数学族不完整 |
| `Entity` | 8/10 | 8.5/10 | 已接近生产可用 |
| `Component` | 8/10 | 9/10 | 元数据与回调设计不错，生态面不足 |
| `Frame` | 7/10 | 9.5/10 | 强在数据容器，弱在完整运行时能力 |
| `System` | 6/10 | 9/10 | 最小调度层已落地，缺系统家族与真实业务样例 |
| `Session` | 5.5-6/10 | 9/10 | 最小闭环已落地，缺 Runner 与更完整的会话产品面 |

### 评分说明

#### `FP`

Lattice 的优点：

- 严格限制 `float/double` 入口，确定性纪律很强
- `FP / FPVector2 / FPVector3 / LUT / SIMD` 已具备可用性
- 测试覆盖明显优于很多同阶段自研框架

Lattice 的缺口：

- 缺 `FPQuaternion`
- 缺 `FPBounds2/3`
- 缺 `FPMatrix2x2/3x3/4x4`
- 缺 `FPCollision`
- `Log2/Exp` 等扩展数学仍未补齐

结论：

`FP` 不是当前闭环工作的主阻塞项。

#### `Entity`

Lattice 的优点：

- `EntityRef` 已与 FrameSync 风格基本对齐
- `Raw` 快速比较、8 字节布局、确定性哈希都已落地

Lattice 的缺口：

- 调试辅助、命名、运行时生态扩展还不如 FrameSync 丰富

结论：

`Entity` 已足够支撑 7 分闭环。

#### `Component`

Lattice 的优点：

- `ComponentRegistry / ComponentTypeId<T>` 已形成较完整的运行时元数据系统
- 已支持回调、序列化模式、快照捕获、延迟删除分发
- `StorageFlags.Singleton / DeferredRemoval` 已有明确设计

Lattice 的缺口：

- 当前主干没有正式保留 `ComponentSet64 / 256`
- 还没有 FrameSync 那样完整的内建组件生态
- 还没有 `ComponentTypeRef`、原型、资产引用这类外围配件

结论：

`Component` 已足够支撑闭环第一阶段，不应继续横向铺生态。

#### `Frame`

Lattice 的优点：

- 已具备实体和组件的核心增删改查
- 已具备 `Query<T...>()`
- 已具备 `OwningGroup` 热路径机制
- 已具备 `FrameSnapshot / PackedFrameSnapshot`、恢复、克隆、流式校验和

Lattice 的缺口：

- 还没有真正把系统状态与帧状态打通
- 还没有 FrameSync 那种完整的 `Unsafe` 门面、Singleton API、系统启停、运行时上下文
- 还没有资产、玩家、地图、全局状态等完整帧级能力

结论：

`Frame` 已经是强基座，但还不是完整运行时容器。

#### `System`

Lattice 当前已具备：

- 正式编译中的 `ISystem`
- 最小可用的 `SystemScheduler`
- 固定顺序 `Initialize / Update / Shutdown`
- 空值、重复注册、重复初始化等最小防呆
- 独立的调度器自动化测试

Lattice 仍然缺少：

- `SystemBase`
- `SystemGroup`
- 系统树状层级
- 依赖图排序
- 多线程系统家族
- 真实业务系统样例沉淀

而 FrameSync 已经具备：

- `SystemBase`
- `SystemGroup`
- `SystemMainThreadFilter`
- `SystemThreadedFilter`
- `SystemArrayComponent`
- `SystemArrayFilter`
- 系统启停和层级状态管理

结论：

`System` 已经从头号缺口下降为“骨架已成、业务验证不足”。

#### `Session`

Lattice 当前已具备：

- `Session` 已重新纳入 `Lattice.csproj`
- `CreateFrame()` 与当前 `Frame` API 已对齐
- `AdvanceFrame()` 已采用基于 `Frame.CopyStateFrom()` 的 direct copy 推进
- 已有正式的 `SessionRunner` 与 `SessionRunnerBuilder`
- `InputBuffer` 已切到固定窗口 ring buffer
- `Checkpoint / Restore / Rollback / Resimulate` 已打通最小实现
- `Checkpoint` 与 sampled history 已完成 packed snapshot 化
- `CalculateChecksum()` 已切到流式状态遍历写入
- `PackedFrameSnapshot` 写出已去掉最终整包复制，checkpoint 分配进一步下降
- 自定义序列化组件已具备固定 payload 直写/直读快路径，`MixedSerialized` 的 checkpoint/checksum/restore 成本已完成第二轮收口
- sampled history materialize 已具备同 Tick scratch 复用，重复读取同一历史 Tick 不再重复 restore + replay
- Session 层已有独立自动化测试

Lattice 仍然缺少：

- 配置驱动装配
- 更完整的本地运行 / 网络运行边界
- benchmark 驱动的成本画像与基线已经建立，但尚未沉淀到长期阈值回归
- 真实业务系统参与下的端到端回滚样例

而 FrameSync 已经具备：

- `SessionRunner`
- 启动 / 服务 / 关闭流程
- `SystemsConfig` 配置驱动装配
- 真实的多模式启动入口

结论：

`Session` 已经进入“可运行 MVP”阶段，但距离可扩展产品面还有明显差距。

---

## 7 分闭环的定义

“7 分运行闭环”并不要求 Lattice 在功能面上追平 FrameSync。

本阶段的目标定义如下：

### 必须具备

1. `System` 进入正式编译面
2. `Session` 进入正式编译面
3. 可以在纯 C# 环境中创建一个会话并驱动固定帧更新
4. 会话内部至少维护：
   - 当前帧
   - 历史帧
   - 检查点
   - 最小回滚/重模拟
5. 系统执行顺序固定、确定、可预测
6. 至少有两个真实系统示例：
   - `MovementSystem`
   - `LifetimeSystem` 或 `DecaySystem`
7. 至少有一条完整链路：
   - 创建实体
   - 添加组件
   - 输入进入
   - 系统更新
   - 产生新帧
   - 创建检查点
   - 恢复或回滚
8. 有自动化测试覆盖该链路

### 可以暂时不具备

- 多线程系统家族
- `SystemGroup` 的复杂树状调度
- 依赖图自动排序
- 资产数据库
- 原型实例化
- 物理、导航、玩家映射
- Unity/Godot 编辑器层工具
- 完整网络会话

---

## 与 FrameSync 的对标边界

本阶段我们不直接对标 FrameSync 的完整形态，而是对标它的**最小模拟骨架**。

### 需要学习的点

- `Frame` 必须是状态容器，而不是单纯的数据仓库
- `System` 必须是运行时正式抽象，而不是文档示例
- `Session` 或 `Runner` 必须负责真正驱动模拟循环
- 系统顺序、启停、调度边界必须明确

### 暂不追的点

- `SystemMainThreadFilter / SystemThreadedFilter / SystemArrayFilter` 这一整套系统家族
- `SystemsConfig` 的资源化配置
- 资产、原型、玩家、地图、View 等业务生态
- FrameSync 完整的序列化产品面

---

## 当前主干中的剩余缺口

这里列的是当前代码里仍然存在、下一步必须直面的工程问题。

### 1. `Session` 当前仍是保守实现

表现：

- 当前帧推进、回滚起点与历史补帧已改为 direct state copy
- 历史帧查找与替换已经收口为有界 O(1)
- `CalculateChecksum()` 已切到流式 checksum writer
- 检查点与历史采样快照已切到 `PackedFrameSnapshot`
- `InputBuffer` 已切到固定窗口 ring buffer
- `CountSerializableStorages()` 的双遍重复工作已清理
- `PackedFrameSnapshot` 写出已去掉最终整包复制
- 固定 payload 自定义序列化已具备直写/直读快路径
- sampled history materialize 已补 scratch 命中缓存
- 公开 `FrameSnapshot` 兼容路径仍保留
- 还没有更明确的本地运行 / 网络运行边界

影响：

- 正确性和热点路径的第一轮成本收口已经完成
- 下一步更适合转向 benchmark 证据、兼容路径瘦身与玩法侧真实负载验证

### 2. 装配层仍是最小实现

表现：

- 已有 `SessionRunner`
- 已有 `SessionRunnerBuilder`
- 但仍没有更丰富的配置驱动装配与资源化入口

影响：

- 代码侧装配已经够用，但距离更完整的工程接入方式还有距离

### 3. 坏场景测试已完成第二轮系统补强

表现：

- 当前测试已经覆盖最小成功路径
- 已建立 8.5 分固定回归测试入口
- 已补充连续恢复、重复回滚、空输入区间、超窗口输入、错误生命周期调用等第一轮边界测试
- 已补充 `VerifyFrame / RollbackTo / Rewind / Stop / Dispose` 的主要生命周期误用矩阵
- 已补 history cache 失效、warm/cold 历史读取一致性、历史读取后回滚重模拟等组合坏场景测试

影响：

- 运行层的主要错误路径不再主要依赖人工推断
- 后续更适合把重点转向运行模式边界与兼容路径收口

### 4. `Session` 运行模式边界已有最小实现，但尚未抽象清楚

表现：

- 已有 `Session`
- 已有 `SessionRunner`
- 已有 `SessionRunnerBuilder`
- 当前已经形成“纯 C#、单线程、固定顺序、可回滚”的最小正式运行入口
- 当前已明确“最小模拟运行时”边界，并补充 `Update / VerifyFrame / RollbackTo / Rewind / CreateCheckpoint / RestoreFromCheckpoint` 的语义说明
- 当前已引入很薄的 `SessionRuntimeOptions`，承载 `DeltaTime / LocalPlayerId`
- 但仍未拆分为独立的 `LocalSession / NetworkSession` 类型体系

影响：

- 当前代码足够支撑玩法开发和基准优化
- 运行模式边界已经比前一轮清楚很多
- 后续若继续扩展网络产品面，仍需避免把 `Session` 继续长成“什么都管”的大类

### 5. 兼容路径已经与热路径脱钩，但还没有继续系统瘦身

表现：

- `Session` 热路径已经切离 `FrameSnapshot`
- 公开 `FrameSnapshot` 保留给兼容、调试与显式恢复测试
- 旧 `Filter / FilterOptimized` 已经标记为兼容层并隐藏默认曝光
- 但这些兼容 API 仍然保留在正式编译面中，尚未继续清点使用面与收口策略

影响：

- 当前不会再明显拖累运行时主路径
- 但如果长期不继续清理，后续会持续增加认知负担与维护成本

### 6. 更大范围文档仍需继续收口

表现：

- 本文档已更新到当前实现
- README 已完成第二轮同步
- 但其他运行层相关文档仍需要后续继续排查与收口

影响：

- 如果不继续收口，后续仍可能出现认知版本差

---

## 本阶段的核心原则

### 1. 先补运行层，再补生态层

在 7 分闭环完成前，不再优先扩展：

- 新数学大件
- 新资产系统
- 新编辑器工具
- 新并行系统族

### 2. `Query` 继续作为默认系统入口

本阶段默认所有新系统先用：

```csharp
frame.Query<T...>()
```

原因：

- 可读性高
- 迁移成本低
- 足够支撑闭环验证

### 3. `OwningGroup` 只保留为显式热点路径

本阶段不做“所有系统自动热点化”。

原因：

- 当前真正缺的是运行层，不是热路径接口种类
- 没有真实系统之前，过早做热点迁移容易失焦

### 4. 单线程优先

先保证：

- 顺序稳定
- 行为确定
- 易于调试

再考虑并行。

### 5. Session 必须是编译中的真实能力

本阶段结束时，`Session` 不应再是“文档层 API”，而必须是主干中可编译、可测试、可驱动的真实模块。

---

## 目标架构

7 分闭环建议采用如下最小结构：

### `Frame`

职责：

- 仅负责保存当前帧 ECS 状态
- 提供组件增删改查、查询、快照、恢复、校验和
- 不直接承担复杂会话驱动职责

### `ISystem`

职责：

- 定义系统生命周期
- 约束系统为无状态或近无状态对象

建议最小接口：

```csharp
public interface ISystem
{
    void OnInit(Frame frame);
    void OnUpdate(Frame frame, FP deltaTime);
    void OnDestroy(Frame frame);
}
```

### `SystemScheduler`

职责：

- 维护固定顺序系统列表
- 统一执行 `OnInit / OnUpdate / OnDestroy`
- 暂不负责复杂依赖图和并行任务

### `Session`

职责：

- 持有 `Verified / Predicted / History`
- 接收输入
- 推进 Tick
- 管理回滚与重模拟
- 协调 `SystemScheduler`

### `InputBuffer`

职责：

- 提供按 Tick 存取输入的确定性容器
- 至少支持当前阶段的单玩家或本地输入场景

---

## 最小闭环范围

本阶段结束时，我们至少要能跑通如下场景：

1. 初始化组件类型
2. 创建 `Session`
3. 注册 `MovementSystem`
4. 注册 `LifetimeSystem`
5. 创建实体并挂上：
   - `Position`
   - `Velocity`
   - `Lifetime`
6. 连续推进若干 Tick
7. 验证：
   - 位置发生变化
   - 生命周期递减
   - 生命周期归零后实体被移除或标记失效
8. 创建检查点
9. 恢复检查点
10. 进行一次最小回滚并重模拟

如果这条链路稳定跑通，就说明 Lattice 已经跨过“只是一个好看的 ECS 基座”阶段。

---

## 实施顺序

### Phase 1: 修正编译边界

状态：

已完成（2026-03-25）

目标：

- 把 `System` 和 `Session` 从文档层搬回主干编译面

工作：

- 移除 `Lattice.csproj` 中对 `ECS\Session\**` 的排除
- 正式新增或整理 `ISystem`
- 明确 `System` 所在命名空间
- 清理 `Session` 与 `Frame` 的 API 失配

验收：

- `Lattice.csproj` 可编译通过
- `Session` 与 `ISystem` 成为正式主干代码

### Phase 2: System MVP

状态：

已完成（2026-03-25）

目标：

- 建立最小系统运行层

工作：

- 实现 `ISystem`
- 实现 `SystemScheduler`
- 先不实现复杂 `SystemGroup`
- 先不做多线程

验收：

- 可以注册多个系统
- 系统按固定顺序执行
- 有生命周期回调

### Phase 3: Session MVP

状态：

已完成（2026-03-25）

目标：

- 建立最小会话驱动层

工作：

- 修复 `CreateFrame / AdvanceFrame`
- 完成输入缓冲最小实现
- 打通 `Start / Update / Verify / Rollback / Checkpoint`
- 让 `Session` 通过 `SystemScheduler` 驱动系统

验收：

- 可连续推进 Tick
- 可保存历史帧
- 可检查点恢复
- 可最小回滚重模拟

## 第一阶段详细清单

本节将 `Phase 1 + Phase 2 + Phase 3` 进一步压缩为真正可执行的最小任务。

原则：

- 先让主干可编译
- 再让 `System` 可运行
- 再让 `Session` 可驱动
- 每一小步都必须有明确验收点

### 一、建议文件落点

建议优先使用以下文件组织，不额外引入新的大目录：

| 类型 | 建议文件 |
|------|----------|
| `ISystem` | `godot/scripts/lattice/ECS/Framework/ISystem.cs` |
| `SystemScheduler` | `godot/scripts/lattice/ECS/Framework/SystemScheduler.cs` |
| 样例系统 | `godot/scripts/lattice/ECS/Framework/Systems/` |
| `Session` | 保留在 `godot/scripts/lattice/ECS/Session/Session.cs` |
| 输入缓冲 | 可先保留在 `Session.cs`，稳定后再拆文件 |
| 运行层测试 | `godot/scripts/lattice/Tests/ECS/` |

说明：

- `ISystem` 和 `SystemScheduler` 建议放在 `ECS/Framework`，与当前语义最一致
- 当前阶段不强行引入 `SystemBase` 和 `SystemGroup`
- 当前阶段不再创建新的 “Runtime” 大目录，先让现有结构收口

### 二、Phase 1: 修正编译边界

这一阶段的目标只有一个：

**让 `System` 和 `Session` 重新进入正式主干编译面，并与当前 `Frame` API 对齐。**

#### Task 1.1 修正项目编译包含关系

工作：

- 从 `Lattice.csproj` 中移除 `ECS\Session\**` 的 `Compile Remove`
- 保持 `ECS\Abstractions\**` 继续排除，避免桥接层提前混入

完成标准：

- `Session.cs` 进入编译
- 不因 Session 引入新的循环依赖

#### Task 1.2 正式引入 `ISystem`

工作：

- 新增正式的 `ISystem` 定义
- 统一生命周期接口：
  - `OnInit(Frame frame)`
  - `OnUpdate(Frame frame, FP deltaTime)`
  - `OnDestroy(Frame frame)`

完成标准：

- `Session` 不再依赖“隐含存在”的 `ISystem`
- `ISystem` 可被测试项目直接引用

#### Task 1.3 清理 `Session` 与 `Frame` 的 API 失配

当前问题：

- `Session.CreateFrame()` 仍使用旧的 `Frame` 构造方式

工作：

- 统一 `Session.CreateFrame()` 的真实创建路径
- 至少保证新帧创建后能够正确设置：
  - `Tick`
  - `DeltaTime`
- 如有必要，在不破坏现有 `Frame` 语义的前提下增加最小构造辅助

完成标准：

- `Session` 可以创建真实的 `Frame`
- `AdvanceFrame()` 不再依赖不存在的构造签名

#### Task 1.4 明确命名空间边界

工作：

- `ISystem` 与 `SystemScheduler` 使用统一命名空间
- `Session` 中引用方式与该命名空间保持一致
- 避免把运行时接口散落在多个命名空间中

建议：

- `ISystem` 和 `SystemScheduler` 使用 `Lattice.ECS.Framework`

完成标准：

- `System` 相关类型的命名空间一眼可读
- `Session`、测试代码、样例系统都能稳定引用

### 三、Phase 2: System MVP 最小任务

这一阶段的目标是：

**用最少的正式代码，建立一个真正可运行的系统调度层。**

#### Task 2.1 实现 `SystemScheduler`

建议职责：

- 维护系统列表
- 按注册顺序固定执行
- 分别负责：
  - `Initialize(Frame frame)`
  - `Update(Frame frame, FP deltaTime)`
  - `Shutdown(Frame frame)`

建议最小 API：

```csharp
public sealed class SystemScheduler
{
    public int Count { get; }
    public void Add(ISystem system);
    public bool Remove(ISystem system);
    public void Clear();
    public void Initialize(Frame frame);
    public void Update(Frame frame, FP deltaTime);
    public void Shutdown(Frame frame);
}
```

完成标准：

- 可注册多个系统
- 系统总是按稳定顺序执行
- 生命周期调用顺序可预测

#### Task 2.2 增加最小防呆行为

建议最小防呆：

- 不允许添加 `null`
- 初始化后再次 `Initialize()` 不应重复触发系统初始化
- `Shutdown()` 后可安全重复调用
- 同一实例重复注册时应有明确策略

推荐策略：

- 当前阶段先禁止重复注册同一实例

完成标准：

- 调度器不会因为重复调用生命周期而产生明显错误

#### Task 2.3 不实现的内容

当前阶段明确不做：

- `SystemBase`
- `SystemGroup`
- 系统树状层级
- 依赖图排序
- 多线程 `Schedule`
- 主线程/线程安全双 API

原因：

- 这些都属于 8 分以上的运行层能力
- 7 分闭环的关键是“跑起来”，不是“把未来所有扩展点一次设计完”

#### Task 2.4 System 层测试清单

至少应补以下测试：

1. 调度顺序测试
   - 注册 `A/B/C` 系统后，执行顺序固定为 `A -> B -> C`
2. 生命周期测试
   - `Initialize -> Update -> Shutdown` 顺序正确
3. 防重复初始化测试
4. 移除系统测试

完成标准：

- `SystemScheduler` 有独立测试，不依赖 `Session` 才能验证

### 四、Phase 3: Session MVP 最小任务

这一阶段的目标是：

**让 Session 不再只是“带回滚概念的草稿类”，而是可以真实驱动系统和帧推进的最小运行器。**

#### Task 3.1 用 `SystemScheduler` 取代 `Session` 内部裸列表

当前状态：

- `Session` 直接维护 `List<ISystem>`

工作：

- 改为持有一个正式的 `SystemScheduler`
- `RegisterSystem / UnregisterSystem` 仅作为调度器外层包装

完成标准：

- `Session` 不再自己手写生命周期循环
- 系统执行统一通过调度器完成

#### Task 3.2 修正新帧推进模型

当前状态：

- 已完成（2026-03-25）

需要明确：

- `Start()` 如何创建初始帧
- `Update()` 如何从当前帧推进到下一帧
- `AdvanceFrame()` 到底是“空帧推进”还是“复制状态后推进”

当前阶段建议：

- 当前主干已采用基于 `Frame.CopyStateFrom()` 的 direct copy 推进
- `CloneState()` / `CopyStateFrom()` 已承担预测推进、回滚起点和历史补帧的热路径复制
- 公开 `Clone(mode)` / `CopyFrom(mode)` 仍保留快照语义，但已切到 packed snapshot 而非完整 `FrameSnapshot`

完成标准：

- 帧推进后，状态能连续保留
- 不会出现每帧状态被清空的问题

#### Task 3.3 完成最小 `InputBuffer`

当前状态：

- 已完成（2026-03-25）

当前问题：

- 早期占位实现已替换

当前阶段建议：

- 当前主干已采用固定窗口 ring buffer
- 支持窗口内按 Tick 存取、容量淘汰与窗口内乱序写入

可接受实现：

- 当前主干实现为固定窗口 `RingBuffer`

优先建议：

- 后续若继续优化，重点应放在窗口尺寸策略与真实输入负载验证，而不是回退到字典结构

完成标准：

- `SetInput(tick, input)` 后能稳定 `GetInput(tick)`
- 超出窗口或不存在输入时返回明确空值

#### Task 3.4 打通最小检查点

当前状态：

- 已完成（2026-03-25）

工作：

- `SessionCheckpoint` 至少保存：
  - `Tick`
  - `VerifiedFrame`
  - `PredictedFrame`
- 恢复时正确重建历史最小状态

建议：

- 当前主干已切到 `PackedFrameSnapshot` checkpoint
- 公开 `FrameSnapshot` 保留给兼容、调试与显式对象图恢复场景，不再作为 `Session` 主链路 checkpoint 方案

完成标准：

- `CreateCheckpoint()` 后可以恢复到相同状态
- 恢复后系统仍可继续推进

#### Task 3.5 打通最小回滚与重模拟

当前状态：

- 已完成（2026-03-25）

工作：

- `VerifyFrame()` 能比较历史帧校验和
- 不一致时触发 `RollbackTo()`
- `RollbackTo()` 后能从历史帧重新模拟到目标 Tick

当前阶段建议：

- 当前主干已保证最小成功路径，并具备回滚后继续推进的自动化验证
- 仍未在这一轮引入复杂网络输入修补逻辑

完成标准：

- 一次最小回滚可成功执行
- 回滚后 `PredictedFrame` 与 `CurrentTick` 状态自洽

#### Task 3.6 Session 层测试清单

当前状态：

- 已完成（2026-03-25）

至少应补以下测试：

1. `Start()` 后初始帧创建成功
2. `Update()` 后 Tick 递增
3. 输入可在目标 Tick 被读取
4. 检查点创建与恢复正确
5. 校验和不匹配时能触发一次最小回滚
6. 回滚后可继续推进

完成标准：

- `Session` 的核心行为可在测试中独立验证

### 五、System + Session 的最小联调清单

状态：

已完成（2026-03-25）

当前主干已满足以下项目：

- [x] `ISystem` 已存在于正式编译面
- [x] `SystemScheduler` 已存在于正式编译面
- [x] `Session` 已重新进入正式编译面
- [x] `Session` 可使用当前真实 `Frame` 创建帧
- [x] `Session` 通过 `SystemScheduler` 驱动系统
- [x] 输入缓冲可按 Tick 存取
- [x] 历史帧、检查点、恢复可工作
- [x] 最小回滚/重模拟可工作
- [x] `System` 与 `Session` 均有单独测试

补充说明：

- 主干已新增最小样例组件：`Position2D / Velocity2D / Lifetime`
- 主干已新增真实样例系统：`MovementSystem / LifetimeSystem`
- 当前 `Session` 在一次运行期间固定系统集合，不支持运行中动态增删系统
- 当前 `SystemScheduler.Clear()` 仅允许在未初始化状态调用；若已初始化，必须先 `Shutdown(frame)`
- 已有端到端联调测试覆盖：
  - 创建实体
  - 添加组件
  - 输入进入
  - 系统更新
  - 检查点恢复
  - 回滚重模拟
  - 生命周期耗尽后实体销毁

### 七、当前历史帧与检查点语义

本节用于明确当前主干中 `Verified / Predicted / History / Checkpoint` 的真实含义，避免后续优化时误伤语义。

#### `Verified`

当前语义：

- 表示最近一次已确认的基线帧
- `Start()` 时先创建 `VerifiedFrame`，并在其上执行系统初始化
- `VerifyFrame()` 成功后会将目标历史帧克隆为新的 `VerifiedFrame`

当前特点：

- `Verified` 更偏“权威基线”
- 当前实现优先保证语义正确，不追求最小复制成本

#### `Predicted`

当前语义：

- 表示当前本地模拟使用的工作帧
- `Start()` 时由 `VerifiedFrame.CloneState()` 得到
- `Update()` 与 `RollbackTo()` 都围绕 `PredictedFrame` 推进

当前特点：

- 所有系统更新都作用在 `PredictedFrame`
- 回滚不会重新初始化系统，只会重建帧状态

#### `History`

当前语义：

- 保存最近一段 Tick 的帧状态
- 当前使用“热窗口 `Frame` + sampled `PackedFrameSnapshot`”的组合保存
- 写入方式以“保守正确”为主：
  - 初始基线帧进入历史
  - 新预测帧进入或替换历史

当前特点：

- 目标是保证验证、恢复、回滚有稳定基线
- 热窗口内优先保留可直接读取的帧对象，窗口外通过 sampled snapshot 进行重建

#### `Checkpoint`

当前语义：

- `SessionCheckpoint` 保存：
  - `Tick`
  - `VerifiedFrame`
  - `PredictedFrame`
- 当前通过 `Frame.CapturePackedSnapshot(ComponentSerializationMode.Checkpoint)` 捕获检查点
- `RestoreFromCheckpoint()` 只恢复帧状态，不重建系统集合

当前特点：

- 检查点当前是面向 `Session` 内部使用的 packed binary snapshot
- 优点是减少 managed allocation、避免完整对象图与重复 `byte[]` 构造
- 缺点是公开调试可读性不如 `FrameSnapshot`

#### 为什么当前仍保留 `FrameSnapshot`

原因：

- 公开 API、对象图断言和显式恢复测试仍然需要稳定兼容面
- `FrameSnapshot` 对排查单个组件存储恢复问题仍然更直观
- 当前已经把 Session 热路径切离 `FrameSnapshot`，因此兼容保留不再阻塞主线优化

#### 明确的未来替换点

后续若要继续优化，优先考虑以下替换点：

- 历史帧保留策略
- checkpoint 轻量化
- `AdvanceFrame()` 的复制成本
- `Verified / Predicted` 的共享或差量策略

原则：

- 先保语义，再做成本优化
- 后续优化必须以真实基准和回归测试为前提

### 六、第一阶段完成后的直接下一步

第一阶段完成后，不要立刻转去做并行或热点优化。

应立即进入：

- `MovementSystem`
- `LifetimeSystem` 或 `DecaySystem`
- 真实组件定义
- 端到端运行测试

原因：

- 只有真实系统跑起来，才能验证 `System + Session` 的抽象是否顺手
- 只有真实系统跑起来，才能知道 `OwningGroup` 是否真的值得提前介入

### Phase 4: 真实系统样例

目标：

- 用真实逻辑验证运行层设计

工作：

- 实现 `MovementSystem`
- 实现 `LifetimeSystem` 或 `DecaySystem`
- 使用 `Query` 作为默认入口

验收：

- 两个真实系统可跑通
- 自动化测试覆盖完整链路

### Phase 5: 热点验证与文档收口

目标：

- 只在有证据的前提下考虑热点路径

工作：

- 检查 `MovementSystem` 是否已成为显著热点
- 如有必要，再评估迁移到 `OwningGroup`
- 更新 README 与相关文档，消除版本差

验收：

- 文档描述与真实代码一致
- 热点优化建立在真实基准上，而非预设想象上

---

## 从 7 分到 8.5 分

本节的目标不是继续扩系统家族，而是把当前已经可运行的闭环补到更稳、更清晰、更适合继续承接玩法开发。

### 8.5 分目标定义

达到 8.5 分时，我们希望 Lattice 具备以下特征：

- 有正式的 `SessionRunner` 或等价启动入口
- `Session` 的状态边界、生命周期边界、错误用法边界都更明确
- 历史帧 / 检查点 / 回滚语义清晰，文档与实现一致
- 有一组专门覆盖坏场景的自动化测试
- README 与运行层文档能反映真实主干能力

明确不包含：

- `SystemGroup`
- 依赖图排序
- 多线程系统家族
- 资源化 `SystemsConfig`
- 完整网络产品面

### Iteration 1：运行时可靠性补强

状态：

已完成（2026-03-25）

目标：

把当前 7 分闭环从“能跑”补到“边界更稳、误用更少、测试更全”。

#### Task 1.1 引入最小 `SessionRunner`

工作：

- 新增一个最小 `SessionRunner`
- 负责统一封装：
  - `Start()`
  - `Step()` 或 `Update()`
  - `Stop()`
- Runner 本身不引入线程，不引入复杂时钟系统
- 保持为纯 C# 层，先不绑定 Godot/Unity

完成标准：

- 不依赖测试代码，也能用正式入口驱动一个 `Session`
- Runner 的生命周期与 `Session` 生命周期语义一致

#### Task 1.2 收紧 Session 状态边界

工作：

- 明确哪些 API 只能在未运行状态调用
- 明确哪些 API 只能在运行状态调用
- 对错误用法统一抛出清晰异常，而不是静默接受
- 补充 XML 文档注释，说明：
  - 运行期间系统集合固定
  - 检查点恢复不会自动重置系统集合
  - 回滚只影响帧状态，不重新初始化系统

完成标准：

- `Session` 的主要公开 API 都有明确的状态语义
- 错误用法在测试中可验证

#### Task 1.3 补强坏场景测试

工作：

- 补以下测试：
  - 连续两次 `RestoreFromCheckpoint()`
  - 连续两次 `RollbackTo()`
  - 没有输入的 Tick 区间推进
  - 输入超过窗口后的读取行为
  - `SessionRunner` 的重复启动 / 重复停止
  - `SystemScheduler` 的错误生命周期调用

完成标准：

- 运行层的“错误路径”不再主要靠人工推断
- 新测试进入 `Lattice.Tests`

#### Task 1.4 文档收口到当前真实实现

工作：

- 更新 `README`
- 更新与 `Session / System / Runner` 相关的说明文档
- 删除或标记已过时的运行时描述

完成标准：

- 新同学仅看主文档和 README，不会得到明显错误的运行层认知

#### Iteration 1 验收

- 有正式的最小 `SessionRunner`
- `Session` 生命周期和错误边界清晰
- 坏场景测试明显增强
- README 与主文档基本一致

### Iteration 2：装配与成本控制补强

状态：

已完成（2026-03-25）

目标：

把运行闭环从“稳定 MVP”推进到“可持续承接玩法开发”的状态。

#### Task 2.1 增加最小系统装配入口

工作：

- 提供最小装配方式，例如：
  - `SessionBuilder`
  - 或 `SessionRunnerBuilder`
- 支持清晰注册：
  - `Session`
  - 系统列表
  - `DeltaTime`
  - 本地玩家 ID
- 暂不做资源化配置，只做代码侧可读装配

完成标准：

- 创建一条完整运行链路时，不再需要在测试里手工拼太多样板
- 样例代码比当前更接近真实项目接入方式

#### Task 2.2 明确历史帧与检查点策略

工作：

- 补充 `History / Checkpoint / Verified / Predicted` 的语义说明
- 明确哪些路径保留 `FrameSnapshot` 兼容 API，哪些路径已经切到 packed snapshot
- 明确未来可替换点：
  - 历史帧保留策略
  - checkpoint 轻量化
  - clone 成本优化
- 当前迭代先不强行重构实现，但要把边界写清楚

完成标准：

- 团队后续再优化历史帧/检查点时，有稳定的语义锚点
- 文档与测试能解释当前保守实现为什么可接受

#### Task 2.3 增强端到端样例的业务可信度

工作：

- 在现有 `MovementSystem + LifetimeSystem` 之外，再补一个更接近玩法的最小样例
- 优先建议：
  - `SpawnerSystem`
  - 或 `Decay/Hit` 一类更贴近战斗循环的系统
- 继续使用 `Query`，不提前切 `OwningGroup`

完成标准：

- 不再只有“位置变化 + 生命周期结束”这一条极简样例
- 至少有两条风格不同的端到端链路能跑通

#### Task 2.4 设立 8.5 分验收回归集

工作：

- 为以下能力建立固定回归集：
  - `SessionRunner`
  - 系统装配
  - 成功路径
  - 错误路径
  - 回滚路径
  - 检查点恢复路径
- 后续每次改 `Session / SystemScheduler / Runner` 都先跑这组测试

完成标准：

- 8.5 分目标有一组明确、固定、可重复执行的回归入口

#### Iteration 2 验收

- 有最小但正式的装配入口
- 历史帧 / 检查点 / 回滚语义更清晰
- 端到端样例从 1 条提升到 2 条左右
- 已有一组可单独执行的 8.5 分回归测试集

#### Iteration 2 完成情况拆分

为了避免把“Iteration 2 主交付已完成”和“后续仍需继续收口”混在一起，这里额外做一张拆分表。

| 主题 | Iteration 2 已完成 | 本质上延期到 Iteration 3/4/5 |
|------|-------------------|-------------------------------|
| 最小装配入口 | 已完成。`SessionRunner` 与 `SessionRunnerBuilder` 已进入正式编译面，并有独立测试。 | 是否需要进一步引入更薄的运行配置对象，例如 `SessionRuntimeOptions`，延期到 `Iteration 4` 评估。 |
| 历史帧 / checkpoint / rollback 语义说明 | 已完成第一版。当前已明确 `Verified / Predicted / History / Checkpoint` 的主链路语义，并写清 `PackedFrameSnapshot` 已替代 Session 热路径中的对象图快照。 | “哪些能力更偏本地玩法语义，哪些更偏预测 / 验证语义”尚未抽清，延期到 `Iteration 4`。 |
| 端到端样例可信度 | 已完成。除 `MovementSystem + LifetimeSystem` 外，已补 `SpawnerSystem` 链路。 | 更复杂的组合玩法样例与多边界联动样例不属于 Iteration 2 最小范围，后续按真实玩法需要继续补。 |
| 8.5 分固定回归入口 | 已完成。`Runtime85RegressionTests` 已建立，覆盖 Builder / Runner / checkpoint / rollback / gameplay path。 | 当前仍偏第一轮 smoke/regression 入口，不是完整坏场景矩阵；第二轮系统化坏场景回归延期到 `Iteration 3`。 |
| benchmark 与成本画像 | 已完成，而且超出原始 Iteration 2 范围。`SessionRuntimeBenchmarks` 与独立 benchmark runner 已建立。 | 长期阈值回归、稳定成本门槛和失败守卫还未建立，这属于后续治理工作。 |
| 坏场景测试 | 已完成第一轮。连续恢复、重复回滚、空输入区间、超窗口输入、Runner/Scheduler 的部分错误路径已进入测试。 | `VerifyFrame / RollbackTo / Rewind / Dispose` 生命周期误用矩阵、history cache 失效、跨 anchor 组合坏场景尚未系统补齐，延期到 `Iteration 3`。 |
| Session 状态边界 | 已完成第一轮。运行中禁止改系统集合，主要错误用法已开始统一抛异常。 | 本地语义与网络预测/验证语义尚未显式抽开，延期到 `Iteration 4`。 |
| 兼容路径处理 | 已完成第一步。Session 热路径已切离 `FrameSnapshot`，旧 `Filter / FilterOptimized` 已标记为兼容层并降低默认曝光。 | 兼容 API 的保留范围、使用面盘点与继续收口策略尚未完成，延期到 `Iteration 5`。 |
| 文档与实现一致性 | 主设计文档与 README 已完成两轮同步，足以支撑当前主线认知。 | 其他历史文档、阶段总结与旧架构表述仍需继续排查和收口，延期到 `Iteration 5`。 |

结论：

- `Iteration 2` 作为阶段性交付，可以认为已经完成。
- 但它不是“后续完全不用再碰”的最终收口。
- 当前更准确的判断是：**Iteration 2 主交付完成，后续收口任务已顺延到 Iteration 3 / 4 / 5。**

### Iteration 3：第二轮坏场景测试补强

状态：

已完成（2026-03-25）

目标：

把当前运行闭环从“主路径稳定”推进到“组合边界也更稳”，避免后续玩法接入时靠人工猜测错误语义。

#### Task 3.1 补全 `Session` 生命周期误用矩阵

工作：

- 补以下测试：
  - `VerifyFrame()` 在未运行状态调用
  - `RollbackTo()` 在未运行状态调用
  - `Rewind()` 在未运行状态调用
  - `Stop()` 后再次执行 `Update / VerifyFrame / RollbackTo / Rewind`
  - `Dispose()` 后主要公开 API 的调用行为
- 为 `SetPlayerInput(null)`、`RestoreFromCheckpoint(null)` 等参数错误补显式测试

完成标准：

- `Session` 主要公开 API 在“未启动 / 运行中 / 停止后 / Dispose 后”四类状态下都有可验证行为
- 错误用法不再主要靠读代码判断

#### Task 3.2 补 history / rollback 的组合坏场景回归

工作：

- 补以下测试：
  - 历史 materialize cache 在 `UpdateHistory()` 后失效，不会读取到陈旧历史帧
  - sampled history 命中后，再执行回滚 / 重模拟，历史重建结果仍正确
  - 连续前向读取后再跨 anchor 读取，结果与冷启动一致
  - 空输入区间与跨窗口输入混合出现时，回滚 / 重模拟结果仍稳定

完成标准：

- 历史读取、cache 失效、回滚重模拟三者之间的边界关系有专门测试兜底
- 这部分回归不再仅依赖 benchmark 侧观察

#### Task 3.3 补 `SessionRunner / SessionRunnerBuilder` 的错误路径

工作：

- 补以下测试：
  - `SessionRunner.Dispose()` 后再次 `Start / Step / Stop`
  - `SessionRunner.Step(stepCount < 0)`
  - `SessionRunnerBuilder.AddSystem(null)`
  - `SessionRunnerBuilder.WithSessionFactory(null)`
  - Builder 构建出的 Session 在重复运行后仍保持基本生命周期一致性

完成标准：

- 装配入口与驱动入口不再只有成功路径测试
- Builder / Runner 的错误语义与 `Session` 主体一致

#### Iteration 3 验收

- 第二轮坏场景测试形成专门回归面
- `Session / Runner / Builder` 的错误路径覆盖明显增强
- 历史 cache / rollback 组合边界有稳定回归测试

### Iteration 4：明确 `Session` 运行模式边界

状态：

已完成（2026-03-25）

目标：

把当前 `Session` 真正支持什么、不支持什么写清楚，并在 API 层收紧最容易混淆的边界。

#### Task 4.1 写清当前正式支持的运行模式

工作：

- 在文档中明确当前正式支持的是：
  - 纯 C# 驱动
  - 单线程固定顺序
  - 本地预测推进
  - 带验证 / 回滚能力的最小模拟运行时
- 明确当前暂不正式支持的是：
  - 多线程系统调度
  - 资源化 / 配置化多模式启动
  - 完整网络会话管理
  - 玩家映射 / 房间态 / 传输层

完成标准：

- 新同学只看主文档，就不会把当前 `Session` 误读成完整联机框架
- 团队内部讨论“要不要继续往 `Session` 塞能力”时有稳定边界

#### Task 4.2 收紧本地语义与网络语义的注释边界

工作：

- 梳理并补充以下 API 的语义说明：
  - `Update()`
  - `VerifyFrame()`
  - `RollbackTo()`
  - `Rewind()`
  - `CreateCheckpoint()`
  - `RestoreFromCheckpoint()`
- 明确哪些更偏“本地玩法能力”
- 明确哪些更偏“预测 / 验证能力”
- 先不拆类型，但先把语义写清楚

完成标准：

- `Session` 中最容易混淆的 API 不再只有实现语义，没有命名与文档语义
- 后续如果真的要拆 `LocalSession / NetworkSession`，也有清晰锚点

#### Task 4.3 评估是否需要最小运行配置对象

工作：

- 评估是否需要引入一个很薄的 `SessionRuntimeOptions` 或等价配置对象
- 只考虑承载：
  - `DeltaTime`
  - `LocalPlayerId`
  - 历史窗口 / snapshot 间隔等运行时参数
- 不提前引入 FrameSync 风格的大型配置系统

当前结论：

- 已引入一个很薄的 `SessionRuntimeOptions`
- 当前只公开 `DeltaTime / LocalPlayerId`
- 暂不公开历史窗口、snapshot 间隔等内部运行策略参数，避免把尚未稳定的实现细节过早固化为 API

完成标准：

- 当前已经有一个很薄、不会扩大设计面的运行配置入口

#### Iteration 4 验收

- 当前 `Session` 的正式运行边界写清楚
- 本地语义 / 预测语义的 API 边界更容易理解
- 是否需要更薄运行配置对象有明确结论

### Iteration 5：继续瘦身兼容路径，但不做大拆

状态：

已完成（2026-03-25）

目标：

继续降低兼容路径带来的认知负担与维护成本，但不在当前阶段做激进删除。

#### Task 5.1 清点兼容 API 的保留范围

工作：

- 盘点当前仍保留的主要兼容面：
  - `FrameSnapshot`
  - `Filter<T...>`
  - `FilterOptimized<T...>`
- 为每类兼容 API 明确：
  - 仍保留的原因
  - 不应进入的新用法
  - 未来移除前需要具备的前置条件

完成标准：

- 团队对“哪些兼容 API 只是历史包袱，哪些仍有现实用途”有统一认知

当前完成情况：

- 已在 README 与本文档中明确盘点 `FrameSnapshot`、`Filter<T...>`、`FilterOptimized<T...>` 三类兼容面
- 已写清保留原因、禁止新用法和未来继续收口前置条件

#### Task 5.2 继续压低兼容层默认曝光

工作：

- 检查兼容 API 是否都已：
  - `Obsolete`
  - 隐藏默认编辑器曝光
  - 在 README / 主文档中标明“非主链路”
- 对仍未标记清楚的兼容面继续补标记

完成标准：

- 新代码路径默认不会再走到兼容 API
- 兼容面仍保留，但不再干扰主线认知

当前完成情况：

- `Filter<T...>` 与 `FilterOptimized<T...>` 保持 `Obsolete + EditorBrowsable(Never)`
- `FrameSnapshot` 类型以及 `Frame.CreateSnapshot() / RestoreFromSnapshot()` 已补同级别兼容标记
- 已补测试锁定这些兼容标记，避免后续回退

#### Task 5.3 继续收口文档中的旧运行时表述

工作：

- 继续排查 README 之外的运行层文档
- 标记或修正仍然使用：
  - `World`
  - `SystemBase`
  - `SystemGroup`
  - `StateSnapshot`
  - “Session 仍基于 FrameSnapshot 热路径”等过时说法的内容

完成标准：

- 运行层核心文档基本不再与当前编译面冲突
- 兼容说明与主链路说明不会混在一起

当前完成情况：

- `README` 已新增兼容 API 边界章节
- `ECS/Architecture.md` 已改成历史归档口径，明确其中 `SystemGroup`、多线程、旧快照表述不代表当前主干
- `ECS/PERFORMANCE_OPTIMIZATION.md` 已改成历史归档口径，明确其中 `FrameBase`、旧序列化适配待办不再代表当前实现

#### Iteration 5 验收

- 兼容路径保留范围清楚
- 默认主线认知进一步收紧
- 文档中的旧运行时描述继续减少

### 当前推荐推进顺序

如果后续继续按“第二、第三、第四阶段”推进，建议顺序如下：

1. `Iteration 3：第二轮坏场景测试补强`
2. `Iteration 4：明确 Session 运行模式边界`
3. `Iteration 5：继续瘦身兼容路径，但不做大拆`

原因：

- 第二轮坏场景测试最能直接降低玩法接入阶段的踩坑概率
- 运行模式边界写清楚后，后续扩展才不容易把 `Session` 继续做成大杂烩
- 兼容路径现在已经不阻塞热路径，适合按证据逐步瘦身，而不是抢在前面做大清理

### 优先级结论

如果当前只能继续做一个迭代，优先做 `Iteration 3`。

原因：

- 它最直接提升运行时边界可靠性
- 它最能降低后续玩法开发和继续优化时的误判成本
- 它比继续深挖热点更接近 8.5 分目标的短板

如果可以做两个迭代，则按以下顺序推进：

1. `Iteration 3：第二轮坏场景测试补强`
2. `Iteration 4：明确 Session 运行模式边界`

如果可以继续做三个迭代，则再追加：

3. `Iteration 5：继续瘦身兼容路径，但不做大拆`

---

## 7 分验收标准

当以下条件全部满足时，可认为 Lattice 已达到本阶段目标：

1. `System` 与 `Session` 都已进入正式编译面
2. 可以用纯 C# 测试创建一个可运行 Session
3. 系统按固定顺序驱动帧更新
4. 输入可以按 Tick 存取
5. 历史帧、检查点、恢复可以工作
6. 最小回滚与重模拟可以工作
7. `MovementSystem + LifetimeSystem` 样例跑通
8. 相关测试进入 CI
9. README 与实现描述一致

---

## 本阶段明确不做的事

为了防止范围失控，本阶段明确不做：

- 不追求复刻 FrameSync 全套系统家族
- 不追求 `SystemsConfig` 资源化装配
- 不追求完整玩家/网络会话层
- 不追求资产与原型系统
- 不追求物理、导航、View 生态
- 不追求“所有系统都支持并行”
- 不追求为了理论性能而过早改写所有系统为 `OwningGroup`

---

## 一句话原则

当前阶段的 Lattice 设计应遵循：

**先把 `System + Session + 输入 + 历史帧 + 回滚` 接成真实运行闭环，再谈热点优化和外围生态。**
