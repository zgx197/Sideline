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

关键补充参考：

- `CoreRuntimeGuardrails.md`
  - 用于回答“哪些东西该补，哪些东西不该进入 core-runtime”
  - 用于约束后续中大型项目演进时的架构扩张风险

---

## 实施状态（2026-03-26）

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
- [x] `Session` 非运行态兼容 API 与运行态专属 API 已补更完整的状态行为矩阵测试
- [x] `SessionRunner / SessionRunnerBuilder` 已补零步推进、未启动停止、重复构建独立实例等边界回归
- [x] 稀疏输入 + 空输入区间 + 跨窗口 rollback 组合坏场景已补专门回归
- [x] `Session` 的本地语义 / 预测验证语义边界已完成一轮文档收口
- [x] 已引入很薄的 `SessionRuntimeOptions`，承载稳定公开的运行参数 `DeltaTime / LocalPlayerId`
- [x] `Session.RuntimeBoundary` 已把运行模式边界显式暴露到代码层
- [x] `VerifyFrame / RollbackTo / Rewind` 已补负值与未来 Tick 的参数边界约束
- [x] `README` 与 `SystemDesignNotes` 的历史架构描述已完成第二轮收口
- [x] `FrameSnapshot` 兼容入口已补 `Obsolete` / 隐藏默认曝光标记
- [x] `FrameSnapshot / Filter / FilterOptimized` 的保留范围、禁止新用法与未来收口前置条件已形成兼容清单
- [x] `ECS/Architecture.md` 与 `ECS/PERFORMANCE_OPTIMIZATION.md` 已补历史归档声明，避免继续误导当前主干认知
- [x] 已形成仓内兼容 API usage inventory，并补仓库级测试防止生产代码重新依赖兼容面
- [x] `SystemScheduler` 已补轻量 `SystemPhase`，正式支持 phase 内稳定顺序执行
- [x] 输入主契约已切到 `IPlayerInput + SessionInputSet`，并补正式输入边界与缺失/覆盖/顺序语义
- [x] `Frame` 已补 `Query<T1, T2, T3, T4>()` 与统一 global state API，玩法查询与全局状态入口进一步收口
- [x] Determinism 静态守卫已进入主干构建面，关键风险开始由 analyzer 前置拦截
- [x] 输入 payload / checkpoint / packed snapshot / component schema 已补第一轮正式协议版本治理
- [x] `SessionRunner` 已补正式宿主生命周期模型，包含 lifecycle hooks、failure / shutdown / reset 元信息与 `Faulted` 状态收敛
- [x] `SessionRuntimeBenchmarks` 已补正式治理策略、CSV 校验器与 `--govern` / `--govern-report` 执行入口

当前判断：

**Lattice 已经从“只有不错的 ECS 基座”推进到“最小运行闭环已存在”，并完成了 8.5 分目标中的两轮核心补强、一轮 determinism / 协议治理收口、一轮正式宿主生命周期收口，以及一轮 benchmark 治理收口；下一阶段已不再主要是补主干制度，而更适合转向真实玩法负载验证与更重的产品层建设。**

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
- 还没有 FrameSync 那种更完整的 `Unsafe` 门面、系统启停和更重的高层辅助家族
- 还没有资产、玩家、地图等完整帧级能力；当前只补到了最小正式 global state API

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

当前完成情况：

- 已补 `Session` 主要公开 API 在“未启动 / 运行中 / 停止后 / Dispose 后”四类状态下的系统性断言
- 已明确区分：
  - 非运行态仍允许的 API：如 `SetPlayerInput / GetPlayerInput / GetHistoricalFrame(null path) / RegisterSystem / UnregisterSystem`
  - 运行态专属 API：如 `Update / VerifyFrame / RollbackTo / Rewind`
- `CreateCheckpoint / RestoreFromCheckpoint` 在非运行态下的有效/无效行为也已有显式测试

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

当前完成情况：

- 已覆盖 cache 失效、warm/cold 历史读取一致性、warm read 后回滚重模拟一致性
- 已额外补齐“稀疏输入 + 空输入区间 + 跨窗口 rollback”组合场景，避免测试仅在连续输入下成立

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

当前完成情况：

- `SessionRunner` 已覆盖未启动停止、零步推进、负步数、Dispose 后调用、重复 Dispose
- `SessionRunnerBuilder` 已覆盖 `AddSystem(null)`、`WithSessionFactory(null)`、`WithRuntimeOptions(null)` 与重复构建独立 Session 实例

#### Iteration 3 验收

- 第二轮坏场景测试形成专门回归面
- `Session / Runner / Builder` 的错误路径覆盖明显增强
- 历史 cache / rollback 组合边界有稳定回归测试
- 当前已可按更严格口径视为“第二轮坏场景测试补强”完成

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

当前完成情况：

- 文档层已明确正式支持与不支持的运行模式
- 代码层已新增 `Session.RuntimeBoundary`，把当前正式能力面显式暴露给调用方与测试

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

当前完成情况：

- 已为 `Update / VerifyFrame / RollbackTo / Rewind / CreateCheckpoint / RestoreFromCheckpoint` 补齐更明确的语义说明
- 已为 `VerifyFrame / RollbackTo / Rewind` 增加负值与未来 Tick 的参数边界约束，避免继续依赖模糊隐式行为
- 已补测试区分：
  - `VerifyFrame` 成功更偏“验证/确认”语义
  - `Rewind` 更偏“本地玩法”语义
  - `RestoreFromCheckpoint` 更偏“本地工具/运行时管理”语义，且不会隐式重启 Session

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

当前完成情况：

- `SessionRuntimeOptions` 仍保持很薄，只公开 `DeltaTime / LocalPlayerId`
- 已补默认值、`With...()` 派生行为和 Builder 默认装配路径测试，确保这条配置线稳定可验证

#### Iteration 4 验收

- 当前 `Session` 的正式运行边界写清楚
- 本地语义 / 预测语义的 API 边界更容易理解
- 是否需要更薄运行配置对象有明确结论
- 当前已可按更严格口径视为“运行模式边界”完成：文档、代码显式边界、参数约束与测试三者一致

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
- 已新增 `ECS/CompatibilityInventory.md`，明确仓内生产代码、测试代码与历史归档中的真实使用面

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
- 已补仓库级扫描测试，确保生产代码不会重新消费兼容 API

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
- `Core/README.md`、`ECS/Core/PHASE3_SUMMARY.md` 等仍含旧术语的文档已纳入历史归档校验范围

#### Iteration 5 验收

- 兼容路径保留范围清楚
- 默认主线认知进一步收紧
- 文档中的旧运行时描述继续减少
- 当前已可按更严格口径视为“兼容路径瘦身”完成：兼容清单、仓内 usage inventory、默认曝光约束与仓库级守卫四者齐备

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

## 8.5 分之后的框架设计优化方向（2026-03-26）

基于当前 `main` 分支实现，以及对
`D:\UGit\unity_mini_game\UnityProject\Packages\com.ifreetalk.framesync-framework`
的横向对比，可以补一个更聚焦的判断：

**Lattice 当前的主要设计缺口，已经不在“热点还要不要继续抠”，也不在“系统基类数量够不够多”，而在“最小运行时已经成立之后，如何继续把运行时分层做清楚”。**

这里先明确一个边界：

- 当前这轮设计收口，**先不把回放链路与诊断工具链纳入主目标**
- 当前这轮设计收口，**也不为了表面对齐 FrameSync 而先引入 `SystemBase / SystemGroup / SystemMainThreadFilter` 全家桶**
- 目标不是把 Lattice 做成 FrameSync 的拷贝，而是让 Lattice 的**运行时职责、公开边界、扩展方向**更清晰

### 当前最值得优化的，不是功能缺口，而是分层缺口

横向对比后，当前最需要继续优化的设计问题主要有四类：

1. `Session` 的运行模式虽然已经写清边界，但还没有真正类型化分层
2. `SessionRunner` 仍偏“薄驱动器”，还没有成为稳定的运行产品边界
3. `Frame` 目前仍偏状态容器，缺少一个更清楚的运行时上下文挂载面
4. 系统层当前保持极简是对的，但缺少“未来如何扩而不乱”的明确约束

换句话说：

- **Lattice 已经有了最小运行闭环**
- **但还没有完全形成一个清晰、可持续扩展的模拟运行时骨架**

### 设计优化优先级

| 优先级 | 优化项 | 当前问题 | 建议方向 | 现在是否应做 |
|--------|--------|----------|----------|--------------|
| P1 | `Session` 运行模式分层 | 已完成第一轮类型化分层。共享运行时内核已上提为 `SessionRuntime`，当前正式模式已显式落为 `MinimalPredictionSession`，`Session` 退为兼容入口。 | 后续若补纯本地模式、回放模式或网络模式，应继续复用 `SessionRuntime`，而不是回到单体 `Session`。 | 已完成 |
| P1 | `SessionRunner` 运行边界收口 | 已完成第一轮正式收口。builder 已只负责装配，`SessionRunnerDefinition` 已承载不可变启动定义，runner 已承担 runtime 拥有、状态机与重建职责。 | 后续若补更多运行模式，应继续沿 definition -> runner -> runtime 的边界扩展，而不是把外部语义塞回 `SessionRuntime`。 | 已完成 |
| P2 | 最小运行时上下文 | 已完成第二轮正式收口。当前运行期共享对象已有独立上下文承载面，并新增显式 runtime service 标记与上下文边界约束，`Frame` 继续保持为确定性状态容器。 | 后续若扩更多运行期共享对象，应继续沿 `SessionRuntimeContext` 收口，并保持“显式 shared service + 明确拒绝状态载体”的约束，而不是把附属能力重新塞回 `Frame` 或 `SessionRuntime`。 | 已完成 |
| P2 | 输入/历史策略的职责边界 | 已完成第一轮正式收口。输入 / 历史 / checkpoint 的稳定公开契约已经和内部 sizing 策略分层写清。 | 后续若要补可配置窗口、可插拔 history store 或替代 checkpoint 格式，应作为新正式 API 明确引入，而不是继续泄露内部常量。 | 已完成 |
| P3 | 系统层扩展约束 | 已完成第一轮正式收口。`SystemScheduler` 的平面、有序、静态系统集边界已在代码与测试中显式固化。 | 后续若真实出现跨阶段、显式依赖、启停切换或层级编排需求，应在当前 boundary 基础上增量扩展，而不是直接复制 FrameSync 系统家族。 | 已完成 |
| P3 | 公开 API 面继续收紧 | 已完成第一轮正式收口。运行时主链路、兼容入口与内部策略对象的分层已经在代码、测试与文档中显式写清。 | 后续若再扩运行时能力，应优先补正式 API，再判断是否需要兼容别名，不要把内部策略对象重新暴露为 public surface。 | 已完成 |

### 下一阶段建议拆解

#### Priority 1（已完成）：把 `Session` 从“单体最小实现”继续推进到“清晰的运行时层次”

目标：

让 `Session` 不再只是一个“已经能跑的最小类”，而是成为一个**职责明确、后续可以稳定扩展**的运行时核心。

工作：

- 明确当前 `Session` 中哪些职责属于：
  - 共享运行时内核
  - 本地预测运行模式
  - 输入历史与回滚实现细节
  - 启动与驱动边界
- 评估是否需要把当前单一 `Session` 继续收口为：
  - 一个更稳定的基础运行时主体
  - 若干明确命名的运行模式外壳或派生实现
- 保证后续新增运行模式时，不需要继续修改一个不断膨胀的 `Session`

完成标准：

- “最小预测运行时”不再只靠文档说明，而是能在代码结构上看出边界
- 后续若补纯本地模式、回放模式或网络模式，不需要把所有语义都塞进当前 `Session`

为什么它是第一优先级：

- 这是当前 Lattice 与 FrameSync 的**最大设计差距**
- 这比继续补更多系统基类更影响长期可维护性
- 如果这一步不做，后续所有新能力都容易把 `Session` 再次做成大杂烩

当前完成情况（2026-03-26）：

- 已把原有单体 `Session` 主体上提为共享运行时内核 `SessionRuntime`
- 已新增 `MinimalPredictionSession`，把当前正式支持的最小预测运行模式显式类型化
- 已保留 `Session` 作为兼容入口，避免仓内现有调用面一次性迁移
- `SessionRunner` 与 `SessionRunnerBuilder` 已改为面向 `SessionRuntime` 工作，不再把运行入口绑死在旧 `Session` 名称上
- 已补运行时能力门卫：`Update / VerifyFrame / RollbackTo / Rewind / CreateCheckpoint / RestoreFromCheckpoint` 会按 `RuntimeBoundary` 检查正式能力
- 已补自动化测试验证：
  - `MinimalPredictionSession` 可直接作为正式模式类型使用
  - 派生自 `SessionRuntime` 的自定义运行时可以复用共享生命周期
  - 不支持相应能力的运行时会被 capability guard 正确拒绝

当前判断：

**Priority 1 已可按“严格意义上的 100%”视为完成。**

它的完成标志不是“已经有很多模式”，而是：

- 共享运行时内核已经从旧 `Session` 名称中脱离出来
- 当前正式模式已经有显式类型
- 后续新增模式时，已有明确的继承与能力边界入口
- 仓内主入口与自动化测试已经跟随这一分层完成收口

#### Priority 2（已完成）：把 `SessionRunner` 收口成正式运行边界，而不只是薄包装

目标：

让运行入口真正承担“装配、驱动、模式入口、外部集成边界”的职责，而不是继续由 `Session` 本体吸收外部语义。

工作：

- 明确 `SessionRunner`、`SessionRunnerBuilder`、`Session` 三者的职责分工
- 明确哪些参数属于：
  - 运行时公共启动参数
  - 模式相关参数
  - 内部调优参数
- 避免未来把输入来源、步进策略、模式切换、外部资源装配直接加回 `Session`

完成标准：

- 团队对“如何启动一个运行时实例”有单一清晰入口
- `Session` 不承担过多产品层/装配层语义

当前完成情况（2026-03-26）：

- 已新增不可变 `SessionRunnerDefinition`，作为正式运行定义对象，承载：
  - `RunnerName`
  - `SessionRuntimeOptions`
  - `SessionRuntimeFactory`
  - 系统装配清单
- `SessionRunnerBuilder` 已收口为纯装配入口：
  - `BuildDefinition()` 负责产出稳定定义
  - `BuildRuntime()` / `BuildSession()` 负责按定义创建运行时
  - `Build()` 负责按定义创建 runner
- `SessionRunner` 已不再只是薄包装，当前已承担：
  - 运行器状态机 `Created / Running / Stopped / Disposed`
  - 正式运行元信息暴露：`Name / Runtime / RuntimeOptions / RuntimeKind / RuntimeBoundary`
  - 由 definition 创建时的 runtime 拥有与释放职责
  - `ResetRuntime()` 运行时重建能力
- 兼容构造 `SessionRunner(SessionRuntime)` 仍保留，但仅作为 ad-hoc 入口，不再代表推荐装配路径
- 已补守卫，确保：
  - runtime factory 不能返回 `null`
  - runtime factory 产出的稳定公开参数必须与 definition 一致
  - 没有 definition 的 ad-hoc runner 不能调用 `ResetRuntime()`
- 已补自动化测试验证：
  - definition 的不可变性
  - builder / definition / runner 三段式创建链
  - runner state 转换
  - definition-based runner 的 metadata 暴露
  - `ResetRuntime()` 的重建行为

当前判断：

**Priority 2 已可按“严格意义上的 100%”视为完成。**

它的完成标志不是“`SessionRunner` 代码变多了”，而是：

- 装配、定义、驱动三类职责已经从 `SessionRuntime` 主体中拆开
- 启动参数已经有正式定义对象承载，而不再只是 builder 内部临时状态
- runner 已经成为 runtime 的正式外部边界，而不只是 `Start / Step / Stop` 转发器

#### Priority 3（已完成）：补一个很薄的运行时上下文，而不是过早复制 FrameSync 的 `FrameContext`

目标：

为后续运行时共享对象预留清晰挂载点，但保持当前 Lattice 的克制风格。

工作：

- 评估是否引入一个很薄的 runtime context / session context
- 该上下文只承载**确实属于运行期共享状态**的对象
- 不把物理、导航、资源、事件、任务、Profiler 等 FrameSync 全套能力一次搬进来

完成标准：

- `Frame` 继续主要作为确定性状态容器
- 运行期共享对象有单独归属，不需要继续侵入 `Frame` 或 `Session`

当前完成情况（2026-03-26）：

- 已新增 `SessionRuntimeContext`，作为很薄的运行时上下文对象，正式承载：
  - `Runtime`
  - `RunnerName`
  - `RuntimeOptions`
  - `RuntimeKind`
  - `RuntimeBoundary`
  - 按类型索引的共享对象注册表
- `SessionRuntime` 已正式暴露 `Context`，并在 runtime 生命周期结束时负责释放 context
- `SessionRunner` 已正式暴露 `Context`，外部集成不需要再把共享对象塞回 `SessionRuntime` 或 `Frame`
- `SessionRunnerDefinition` 与 `SessionRunnerBuilder` 已接通 context 配置链：
  - `SessionRunnerBuilder.ConfigureContext(...)`
  - `SessionRunnerDefinition.BuildRuntime()` 中应用 configurator
  - `SessionRunner.ResetRuntime()` 会重新创建 runtime 与 context，并重新应用 configurator
- 已支持按类型写入/读取/移除共享对象：
  - `SetShared<T>()`
  - `TryGetShared<T>()`
  - `GetRequiredShared<T>()`
  - `RemoveShared<T>()`
- 已支持 context 自主托管共享对象生命周期：
  - `disposeWithContext: true` 的对象会在 context / runtime 结束时自动释放
- 已新增 `SessionRuntimeContext.Boundary`，把当前上下文正式语义显式暴露到代码层：
  - 当前模型是 `TypedRuntimeSharedServices`
  - 正式支持：typed lookup、显式 shared service 注册、托管生命周期、运行时元信息暴露
  - 明确不支持：任意对象包、回滚状态承载、gameplay 状态交换、string-keyed service locator
- 已新增 `ISessionRuntimeSharedService` 标记，`SetShared<T>()` 现在只允许注册显式声明为 runtime shared service 的类型
- 已新增对状态载体的硬性拒绝：
  - `Frame`
  - `FrameSnapshot`
  - `PackedFrameSnapshot`
  - `SessionCheckpoint`
  - `SessionRuntime`
  - `SessionRuntimeContext`
- 当前 `Frame` 仍保持为确定性状态容器，没有把 runtime 共享对象侵入到帧状态结构中

已补自动化测试验证：

- 独立 runtime 的默认 context 元信息暴露
- `Context.Boundary` 的正式能力面与非能力面暴露
- context 的共享对象读写与移除
- 未显式声明为 runtime shared service 的对象会被拒绝
- rollback / snapshot 状态载体即便实现 shared service 标记也会被拒绝
- context 托管对象随 runtime 销毁释放
- definition-based runner 的 `Context` 与 `RunnerName` 对齐
- `ConfigureContext(...)` 会在每次 runtime 构建时重新应用
- `ResetRuntime()` 会重建新的 context 而不是复用旧实例

当前判断：

**Priority 3 已可按“严格意义上的 100%”视为完成。**

它的完成标志不是“已经有了一个叫 context 的类”，而是：

- 运行期共享对象已经有正式归属
- 该归属已经接入 runtime / definition / runner 整条启动链
- 共享对象生命周期不再依赖外部手工兜底
- 上下文边界已经被正式写成 `Boundary + ISessionRuntimeSharedService + 状态载体拒绝`
- `Frame` 没有因为补上下文而继续膨胀成运行时大杂烩

#### Priority 4（已完成）：系统层先立规矩，不急着立家族

目标：

避免系统层在未来玩法接入时自然长成一套混乱的“半平面、半树状、半阶段化”结构。

工作：

- 先定义哪些问题出现时，才允许考虑引入：
  - system phase
  - system enable / disable
  - system dependency ordering
  - system group / hierarchy
- 在没有真实需求前，维持当前平面调度模型
- 不提前复制 FrameSync 的 `SystemBase / SystemGroup / SystemMainThreadFilter / SystemThreadedFilter`

完成标准：

- 团队对“为什么现在不做 SystemGroup”有稳定共识
- 将来若真的要扩系统层，有明确触发条件和设计入口，而不是临时加类

当前完成情况（2026-03-26）：

- 已新增 `SystemSchedulerBoundary`、`SystemSchedulerKind`、`SystemSchedulerCapability` 与 `UnsupportedSystemSchedulerCapability`
- `SystemScheduler` 已正式暴露 `Boundary`，把当前系统层支持/不支持的调度能力显式暴露到代码层
- `SystemScheduler.Add()` / `Remove()` / `Clear()` 已统一收口为：
  - 初始化前允许静态装配
  - 初始化后冻结系统集合
  - 若需调整集合，必须先 `Shutdown(frame)`
- `ISystem` 与 `SystemScheduler` 的 XML 注释已明确：
  - 当前正式模型是平面、固定顺序、显式生命周期
  - 不隐含 `SystemBase`、`SystemGroup`、enable / disable、依赖排序、多线程等更重家族语义
  - 当时仍未提前承诺 phase；后续已在 Priority 8 中补为轻量正式能力
- 已补自动化测试验证：
  - `SystemScheduler.Boundary` 的正式能力声明
  - 初始化后动态增删系统会被拒绝
  - 框架程序集未暴露 `SystemBase / SystemGroup / SystemMainThreadFilter / SystemThreadedFilter`
  - `SystemScheduler` 的公开面仍保持最小而平面
- `README` 与主文档已同步更新，避免“代码已经收口，但对外描述仍停留在口头约束”

当前判断：

**Priority 4 已可按“严格意义上的 100%”视为完成。**

它的完成标志不是“系统层已经很复杂”，而是：

- 当前主干已经把“只支持最小平面调度”写成正式公开边界
- 运行中的系统集合不再允许半状态动态变更
- 自动化测试已经能阻止系统层自然长出半套 FrameSync 家族
- 后续若真要扩阶段、依赖或分组，已经有明确的进入点与否定前提

#### Priority 5（已完成）：继续收紧运行时公开 API 面

目标：

把“正式公开 API / 保留兼容 API / 内部运行时 API”三层边界从口头共识收口成可验证事实。

工作：

- 盘点当前运行时主链路真正希望对外承诺的 API
- 把仅用于迁移和旧命名承接的入口明确标成兼容面
- 把不应成为外部设计承诺的内部策略对象收回 internal
- 补测试守住：
  - 非兼容公开面仍保持 runtime-first
  - 兼容入口保持 `Obsolete + EditorBrowsable(Never)`
  - 内部策略对象不会重新回到 public surface

完成标准：

- 新代码默认能清楚看出应使用哪条主链路
- 兼容入口存在，但不会再和正式入口混在同一个默认可见层级
- 内部实现细节不会继续被误读成公开设计承诺

当前完成情况（2026-03-26）：

- 已把当前运行时主链路明确收口为：
  - `SessionRuntime`
  - `MinimalPredictionSession`
  - `SessionRuntimeOptions`
  - `SessionRuntimeInputBoundary`
  - `SessionInputSet`
  - `IPlayerInput`
  - `IInputPayloadCodec<TInput>`
  - `SessionRunnerBuilder`
  - `SessionRunnerDefinition`
  - `SessionRunner`
  - `SessionCheckpoint`
- 已把以下运行时兼容入口显式标记为 `Obsolete + EditorBrowsable(Never)`：
  - `Session`
  - `SessionRunner.Session`
  - `SessionRunnerBuilder.WithSessionFactory(...)`
  - `SessionRunnerBuilder.BuildSession()`
  - `IInputCommand`
- 已把 `InputBuffer` 从 public surface 收回 internal，避免固定窗口 / ring buffer 策略继续被误读成对外正式承诺
- 已把仓内测试与 benchmark 默认迁移到正式 API：
  - `runner.Runtime` 取代 `runner.Session`
  - `WithRuntimeFactory(...)` 取代 `WithSessionFactory(...)`
  - `BuildRuntime()` 取代 `BuildSession()`
  - 新样例 session 默认继承 `MinimalPredictionSession`
- 已补自动化测试验证：
  - 运行时兼容别名仍保持兼容面标记
  - `SessionRunner` 与 `SessionRunnerBuilder` 的非兼容公开面保持 runtime-first
  - `InputBuffer` 不再属于 public runtime surface
- `README` 与主文档已同步新增运行时 API 分层说明，避免“实现已经收口，但外部读者仍按旧命名理解主链路”

当前判断：

**Priority 5 已可按“严格意义上的 100%”视为完成。**

它的完成标志不是“公开 API 数量变少了”，而是：

- 正式主链路已经从兼容别名中明确分离出来
- 兼容入口仍能承接旧代码，但不会继续污染默认公开认知
- 内部输入策略与运行时辅助对象不再被外部误读成稳定承诺
- 仓内自动化测试已经能阻止公开面再次向旧命名和内部细节回摆

#### Priority 6（已完成）：明确输入/历史策略的职责边界

目标：

把输入、历史帧和 checkpoint 的“稳定公开契约”与“当前内部 sizing 实现”明确拆开，避免未来外部代码直接依赖 ring buffer 容量、历史窗口大小和采样间隔。

工作：

- 明确哪些输入 / 历史能力属于稳定公开策略：
  - 输入按 `(playerId, tick)` 读写
  - 历史帧按 tick 查询
  - 更早历史可由 sampled snapshot 按需 materialize
  - checkpoint 支持显式创建与恢复
- 明确哪些仍属于内部实现：
  - 输入窗口大小
  - 历史窗口大小
  - sampled snapshot 间隔
  - materialize cache 大小
  - 当前 ring buffer / hybrid history 的具体 sizing
- 不把这条线过度抽成 pluggable strategy 接口家族，只补最小边界描述与 internal defaults

完成标准：

- 外部代码能明确知道“可以依赖什么语义”，而不是去依赖当前默认常量
- 输入 / 历史 sizing 常量不再挂在 public surface 上
- 自动化测试可以阻止内部策略参数重新泄露成公开设计承诺

当前完成情况（2026-03-26）：

- 已新增 `SessionRuntimeDataBoundary`，并在 `SessionRuntime` 上正式暴露 `DataBoundary`
- 已把当前稳定公开契约显式落到代码层：
  - `SessionInputStorageKind.PlayerTickFixedWindow`
  - `SessionHistoryStorageKind.BoundedLiveFramesWithSampledSnapshots`
  - `SessionCheckpointStorageKind.PackedSnapshot`
  - `SessionRuntimeDataCapability` / `UnsupportedSessionRuntimeDataCapability`
- 已把输入 / 历史 sizing 常量从 `SessionRuntime` 的 public surface 移出，收口到 internal `SessionRuntimeDataDefaults`
- `SetPlayerInput()` / `GetPlayerInput()` / `GetHistoricalFrame()` / `CreateCheckpoint()` / `RestoreFromCheckpoint()` 的 XML 注释已补齐：
  - 哪些是稳定公开语义
  - 哪些仍是内部策略
- 当前 `SessionRuntime` 仍保持“实现内嵌、边界显式”的克制风格，没有为了这条线引入过度抽象接口
- 已补自动化测试验证：
  - `DataBoundary` 的输入 / 历史 / checkpoint 契约
  - `SessionRuntime` public surface 不再暴露 sizing 常量
  - `SessionRuntimeDataDefaults` 与 `InputBuffer` 保持内部实现地位
- `README` 与主文档已同步新增输入/历史策略边界说明

当前判断：

**Priority 6 已可按“严格意义上的 100%”视为完成。**

它的完成标志不是“策略被抽象成一堆接口”，而是：

- 公开代码现在明确承诺的是能力语义，而不是默认 sizing 数值
- 内部 ring buffer / hybrid history / sampled snapshot 仍可继续优化，而不会反向绑死外部调用面
- 输入 / 历史这条线已经做到“边界清楚，但实现仍保持轻量”

### 面向“支撑中大型玩法复杂度且长期不返工框架”的下一轮设计缺口（2026-03-26）

上面的 Priority 1 到 Priority 6，解决的是：

- 让 Lattice 从“最小运行闭环”走到“运行时层次清楚”
- 把 `Session / Runner / Context / Scheduler / DataBoundary` 的边界收口成正式代码事实

但如果目标继续上提为：

**Lattice 不只是能跑最小玩法，而是要长期支撑中大型玩法复杂度，并尽量避免后期因为框架语义不清再次返工。**

那么下一轮真正需要补的，已经不再主要是底层热点，而是**中层运行时约束**。

这里会参考 FrameSync 的成熟经验，但只吸收它在“边界清楚、阶段明确、系统运行语义稳定”上的设计启发，**不直接复制它的产品生态、资源化装配和完整系统家族**。

最新判断（2026-03-26，结合当前代码与 FrameSync 横向对比）：

- Lattice 当前已经满足“可持续承接真实玩法开发”的要求
- 但距离“支撑中大型玩法复杂度且长期不返工框架”仍差最后一层治理能力
- 这层差距已经不再主要体现在 `Frame` 热点或单条 benchmark 曲线，而主要体现在：
  - `SessionRuntime` 内部仍偏单体
  - 系统作者契约还不够正式
  - 运行模式边界只被一个正式 runtime 证明
  - determinism / 协议演进 / benchmark 阈值的制度化治理还没补齐

和 FrameSync 的客观差距，当前主要不在“有没有 `SystemGroup`”这一点，而在：

- 它已经有更完整的 session / runner / context / replay provider / protocol version / determinism analyzer 治理链
- Lattice 当前在公开 API 的克制与干净程度上更好，但在内部模块拆分与长期治理设施上仍更弱

#### 下一阶段总表（滚动更新）

| 方向 | 当前状态 | 与“长期不返工”目标的差距 | 下一步建议 | 优先级 |
|------|----------|--------------------------|------------|--------|
| `SessionRuntime` 内核拆分 | 对外边界已清楚，但内部仍由单一 `SessionRuntime` 承载输入、历史、checkpoint、tick 协调与 rollback/materialize 细节。 | 后续继续长功能时，容易把 runtime 再次做成巨石核心，增加联动修改与回归成本。 | 把输入存储、历史物化、checkpoint/restore、tick pipeline 协调拆成稳定内部模块；对外 API 可以先不变，但内部依赖关系要先收口。 | P1 |
| 系统作者契约正式化 | 已完成第一轮正式收口。当前 `ISystem` 已具备轻量 `Contract` 元数据，`SystemScheduler` 与 `Frame` 会共同校验 global / 结构性修改权限。 | 后续若出现真正需要事件流、显式依赖图或更强静态只读守卫的玩法规模，仍需继续演进；但当前已不再主要依赖文档纪律维持正确性。 | 继续保持轻量 contract 路线；新需求优先在现有 contract / boundary 上增量扩展，而不是直接引入重系统家族。 | 已完成 |
| 第二运行模式验证 | 已完成第一轮正式验证。当前主干除 `MinimalPredictionSession` 外，已新增 `LocalAuthoritativeSession` 作为第二种正式运行模式。 | 后续若增加回放、服务器或更强本地模式，仍需继续验证 runtime family 的可扩展性；但当前已经不再只有一个正式 runtime。 | 后续新增运行模式时，继续复用 `SessionRunner / Definition / Context / Boundary` 主链路，不走旁路实现。 | 已完成 |
| Determinism / 协议治理 | 已完成第一轮正式收口。当前主干已同时具备 determinism analyzer，以及输入 payload / checkpoint / packed snapshot / component schema 的显式协议 metadata 与兼容校验。 | 若未来继续扩展回放文件、网络传输、跨版本迁移工具，还需要在当前协议面之上继续补更完整的升级工具链；但“框架内部格式靠经验演进”这一阶段已经结束。 | 后续继续沿“显式 codec/version + schema/version + 精确兼容校验”路线扩展，不回到隐式 payload 和 ad-hoc checkpoint 演进。 | 已完成 |
| 性能治理制度化 | 当前已有 benchmark runner、成本画像与热点优化。 | 仍缺“真实玩法负载画像 + 长期阈值回归 + 失败守门”，很难防止未来改动把成本慢慢抬高。 | 把 benchmark 从“观察工具”升级成“治理工具”：沉淀关键场景阈值、补玩法负载画像、为重路径建立长期回归门槛。 | P3 |

一句话总结：

**Priority 7 到 Priority 11 解决的是“主干规则要不要写清”；下一阶段要解决的是“这些规则在复杂玩法、多人协作和长期演进下，能不能继续稳定成立”。**

#### Priority 7（已完成）：把 Tick 管线与状态修改纪律正式化

目标：

把“一个 Tick 内到底允许什么修改立即生效、什么修改必须延迟提交”写成正式框架规则，而不是继续依赖系统作者的默契。

为什么它是第一优先级：

- 当前 `Frame` 既允许系统直接 `CreateEntity / DestroyEntity / Add / Set / Remove`
- 同时仓内又已经存在 `CommandBuffer` 与 `CommitDeferredRemovals()` 这类延迟提交路径
- 这意味着现在实际上并存：
  - 立即组件写入
  - 立即结构性修改
  - 延迟删除
  - 命令回放
- 在小型样例里这不是问题，但在中大型玩法中，它会快速演变成系统之间的隐式时序耦合

参考 FrameSync 的启发：

- FrameSync 的强项不只是系统多，而是它把“系统什么时候运行、系统影响何时可见”沉淀成稳定运行语义
- Lattice 不需要复制它完整的 `SystemBase / FrameContext / Event / Signal / Command` 体系，但必须学习它对 Tick 生命周期的明确划分

建议工作：

- 在 `SessionRuntime.Update()` 这一层显式定义 Tick 阶段，例如：
  - `InputApply`
  - `Simulation`
  - `StructuralCommit`
  - `Cleanup`
  - `HistoryCapture`
- 明确哪些修改允许在 `Simulation` 阶段直接发生：
  - 纯数据字段修改
- 明确哪些修改必须延迟到 `StructuralCommit`：
  - 实体创建/销毁
  - 组件增删
  - 其他会影响查询拓扑的结构性写入
- 决定 `CommandBuffer` 是否升级为正式主链路的一部分，还是保留为“结构性修改专用入口”

完成标准：

- 新系统作者可以明确知道：
  - 什么写法是正式推荐
  - 什么写法只适合低层热点或特殊场景
- 回滚、重模拟、历史读取和调试 dump 面对同一 Tick 时，不再因为结构性修改可见性不清而出现语义歧义

当前完成情况（2026-03-26）：

- 已新增 `SessionTickPipelineBoundary`、`SessionTickPipelineKind`、`SessionTickPipelineCapability`、`UnsupportedSessionTickPipelineCapability` 与 `SessionTickStage`
- `SessionRuntime` 已正式暴露：
  - `TickPipeline`
  - `CurrentTickStage`
- 当前正式 Tick 管线已显式落为：
  - `InputApply`
  - `Simulation`
  - `StructuralCommit`
  - `Cleanup`
  - `HistoryCapture`
- `SessionRuntime.Update()`、`RollbackTo()` 的重模拟路径、历史 materialize replay 路径，当前都已统一复用同一套 Tick 管线 helper，不再出现“主更新一套语义、冷路径另一套语义”
- `Frame` 已正式接入结构性修改延迟提交：
  - `CreateEntity / DestroyEntity / Add / Remove` 在 `Simulation` 阶段会先进入 `CommandBuffer`
  - 对缺失组件或临时实体的 `Set` 也会延迟到 `StructuralCommit`
  - 对已存在组件的数据字段写入仍保持立即生效
- `Frame.CommitDeferredRemovals()` 已补守卫，防止系统在 `Simulation` 阶段手工绕开结构提交阶段
- `OnFrameUpdate` 当前看到的是结构提交和清理完成后的帧状态，不再混入“部分系统已看到、部分系统还未看到”的半状态
- 已补自动化测试验证：
  - `TickPipeline` 正式能力边界
  - `CurrentTickStage` 的阶段暴露与空闲态回归
  - `Frame` 级结构性修改延迟提交
  - 同 Tick 内结构性修改对后续系统不可立即见
  - rollback / resimulate 复用相同阶段语义
  - `SpawnerSystem` 端到端样例已切到新的结构提交语义

当前判断：

**Priority 7 已可按“严格意义上的 100%”视为完成。**

它的完成标志不是“又多了一个 boundary 类”，而是：

- Tick 生命周期现在已经有正式阶段语义
- 结构性修改的可见性已经从隐式约定收口成显式规则
- 主更新、回滚重模拟和历史重建不再各跑一套修改纪律
- 样例系统和自动化测试已经跟随这一语义完成收口

#### Priority 8（已完成）：给系统层补轻量 phase，而不是回到“纯注册顺序”

目标：

在不引入 `SystemGroup` 家族的前提下，让系统编排从“完全靠 Add 顺序”升级为“平面但有正式阶段语义”。

为什么它重要：

- 当前 `SystemScheduler` 只承诺平面、固定顺序、显式生命周期，这对当前阶段是正确的
- 但一旦系统数量增长到十几个甚至几十个，仅靠注册顺序维护：
  - 输入系统先后
  - 战斗结算时机
  - 死亡/清理时机
  - 生成/销毁与后续消费系统的可见性
  会越来越脆弱

参考 FrameSync 的启发：

- FrameSync 的系统家族之所以复杂，本质上是在解决“系统阶段和调度语义稳定”的问题
- Lattice 当前不需要复制 `SystemGroup / SystemMainThreadFilter / SystemThreadedFilter`
- 但应该吸收“系统运行位置不能只靠外部顺序猜”的经验

建议工作：

- 为 `ISystem` 或调度元数据补一个很薄的 phase 描述
- phase 先控制在最小集合，例如：
  - `Input`
  - `PreSimulation`
  - `Simulation`
  - `Resolve`
  - `Cleanup`
- 保持调度器仍然是平面列表：
  - 先按 phase 排
  - phase 内仍按注册顺序排
- 不引入树状层级、运行时 enable / disable、依赖图求解

完成标准：

- 中大型玩法系统不会继续因为“注册顺序变化”而产生大面积隐式行为变化
- 仍然保持当前 Lattice 的轻量和平面风格

当前完成情况（2026-03-26）：

- 已新增正式 `SystemPhase`：
  - `Input`
  - `PreSimulation`
  - `Simulation`
  - `Resolve`
  - `Cleanup`
- `ISystem` 已正式暴露 `Phase` 契约：
  - 默认值为 `SystemPhase.Simulation`
  - 新系统不需要派生 `SystemBase` 或引入额外元数据对象即可声明阶段
- `SystemScheduler` 已从“纯注册顺序”升级为：
  - 先按 `SystemPhase` 排序
  - phase 内保持注册顺序稳定
  - `Initialize / Update / Shutdown` 统一复用同一套 phase 执行计划
- `SystemSchedulerBoundary` 已同步更新为 phase-aware 正式边界：
  - `SystemSchedulerKind.FlatPhasedOrdered`
  - `SystemSchedulerCapability.PhasedExecution`
  - 不再把 `SystemPhase` 标记为当前调度器明确不支持的能力
- 当前实现仍保持克制：
  - 没有引入 `SystemGroup`
  - 没有引入 enable / disable
  - 没有引入依赖图排序
  - 没有引入多线程系统家族
- 已补自动化测试验证：
  - phase 排序优先于原始注册顺序
  - 同 phase 内注册顺序稳定
  - `Shutdown` 生命周期也遵循相同 phase 顺序
  - `ISystem` 默认 phase 保持 `Simulation`
- `README` 与系统层边界说明已同步更新，避免“代码已支持轻量 phase，但文档仍声称完全不支持 phase”的版本差

当前判断：

**Priority 8 已可按“严格意义上的 100%”视为完成。**

它的完成标志不是“系统层开始长出半套 FrameSync 家族”，而是：

- 系统编排已经从“只能靠 Add 顺序维持”提升到“有正式阶段语义的平面调度”
- 系统阶段现在已经是代码层正式契约，而不是外部约定
- 调度器公开 API 仍保持最小，没有为了 phase 能力把系统层重新做重
- `SystemGroup` 这条线仍然被明确压住，没有借 phase 之名提前把系统家族一并引进来

#### Priority 9（已完成）：把输入契约从“最小可跑”推进到“正式游戏开发契约”

目标：

让输入层不再只是“按 `(playerId, tick)` 存一个 `IInputCommand`”，而是成为可长期承载本地玩法、预测/验证、未来多玩家扩展的稳定契约。

当前问题：

- `ApplyInputs(Frame frame)` 仍是派生类自定义 hook
- `IInputCommand` 直接携带 `Serialize()/Deserialize()`，把游戏输入语义与传输/序列化语义绑在了一起
- 当前尚未明确：
  - 一个 Tick 内如何聚合多玩家输入
  - 缺失输入如何处理
  - 默认输入和重复输入的正式语义
  - 本地玩法输入与未来验证输入之间的边界

参考 FrameSync 的启发：

- FrameSync 把输入、会话配置、输入偏移、重发和回放放在更清楚的契约层中
- Lattice 目前不需要引入它完整的网络/重发/时钟校正体系
- 但应该学习“输入数据模型”和“输入传输/序列化”不要混成同一个接口

建议工作：

- 重新定义稳定公开的输入模型：
  - 更偏 typed input state / input set
  - 序列化作为外层 adapter，而不是核心输入对象职责
- 明确 `SessionRuntime` 对输入应用的正式扩展点：
  - 输入来源
  - 输入聚合
  - 输入缺失策略
- 继续保持当前 `DataBoundary` 的轻量设计，不把输入线过度抽象成大量策略接口

完成标准：

- 输入层既能支撑当前单线程纯 C# 玩法开发
- 未来若增加更正式的预测/验证模式，也不需要推翻输入主契约

当前完成情况（2026-03-26）：

- 已新增正式输入主模型：
  - `IPlayerInput`
  - `SessionPlayerInput`
  - `SessionInputSet`
- 已新增输入 payload 外层 adapter：
  - `IInputPayloadCodec<TInput>`
  - 使 payload 序列化不再属于核心输入对象职责
- 旧 `IInputCommand` 已降为兼容面：
  - `Obsolete + EditorBrowsable(Never)`
  - 仅用于承接旧“输入对象自带 Serialize/Deserialize”写法
- `SessionRuntime` 已正式暴露 `InputBoundary`，明确写清：
  - 输入契约模型是 `TickScopedPlayerInputSet`
  - 缺失输入采用 `OmitMissingPlayers`
  - 重复写入采用 `LatestWriteWins`
  - tick 内玩家输入遍历顺序为 `PlayerIdAscending`
- `SetPlayerInput()` 现在会校验：
  - 传入参数 `(playerId, tick)`
  - 与输入对象自身的 `PlayerId / Tick`
  - 二者不一致时直接拒绝，而不是继续容忍模糊状态
- `SessionRuntime` 已补正式输入扩展点：
  - `CollectInputSet(int tick)`
  - `ApplyInputSet(Frame frame, in SessionInputSet inputSet)`
  - 旧 `ApplyInputs(Frame frame)` 保留为兼容 hook，但默认实现已统一走 `SessionInputSet`
- 当前正式输入语义已经落到代码层：
  - 一个 tick 内只聚合实际写入的玩家输入
  - 不会自动补默认输入
  - 不会隐式沿用上一帧输入
  - 重复写入按最后一次为准
  - 遍历顺序稳定，不再依赖字典枚举顺序
- 仓内样例、联调测试与 benchmark 已迁移到新的输入扩展点：
  - `ApplyInputSet(...)` 取代直接重写 `ApplyInputs(...)`
  - 样例输入类型默认改为实现 `IPlayerInput`
- 已补自动化测试验证：
  - `InputBoundary` 的正式能力描述
  - 输入身份不一致时的参数守卫
  - 稀疏输入、稳定排序、重复写入覆盖语义
  - 兼容 `IInputCommand` 仍保持隐藏兼容面
  - 生产代码默认不再重新消费 `IInputCommand`
- `README` 与主文档已同步更新，避免“代码已经切到正式输入契约，但文档仍把 `IInputCommand` 当主入口”的版本差

当前判断：

**Priority 9 已可按“严格意义上的 100%”视为完成。**

它的完成标志不是“又多了几个输入接口名”，而是：

- 输入主模型已经和传输序列化职责分离
- `SessionRuntime` 已经有正式的输入聚合与应用扩展点
- 缺失输入、重复写入和遍历顺序都已变成显式框架规则
- 仓内样例、测试和 benchmark 已跟随新契约收口
- 旧 `IInputCommand` 没有继续污染主链路，而是被明确压回兼容层

#### Priority 10（已完成）：补查询表达力与全局状态 API，避免玩法代码风格分裂

目标：

让业务系统在复杂度上升后，仍然能主要通过正式 API 写逻辑，而不是越来越多地掉回底层存储指针和 ad-hoc 模式。

当前问题：

- 强类型 `Query` 目前主要停留在 1/2/3 组件组合
- `OwningGroup` 也主要停留在 2/3 组件热点场景
- `Storage<T>` 底层已有 singleton 能力，但 `Frame` 层还没有一组正式、统一的 singleton / global state API

这会带来的风险：

- 玩法系统越来越多后，查询写法和风格会开始分裂
- 部分系统会为了表达更复杂条件，直接绕回底层 `Storage<T>*`
- 全局状态可能被团队以多种方式实现：
  - singleton 组件
  - 特殊实体
  - context 共享对象
  - 手工静态变量

参考 FrameSync 的启发：

- FrameSync 的 `Frame` 在高层访问体验上更完整，尤其是全局状态、系统访问和若干辅助门面
- Lattice 不需要把 `Frame` 长成 FrameSync 那样重
- 但必须提供足够清楚的高层玩法入口，避免玩法代码长期漂移

建议工作：

- 评估是否补 4 组件及以上的正式查询入口，或补更清楚的动态过滤组合模式
- 为 `Frame` 补统一的 singleton / global state 访问 API
- 明确：
  - 哪些全局状态应该进帧状态
  - 哪些对象应该留在 runtime context

完成标准：

- 中大型玩法系统仍以正式 `Frame` API 为主，而不是越来越依赖内部细节
- 全局状态的归属和写法不再分裂

当前完成情况（2026-03-26）：

- 已把 `Frame.Query` 的正式强类型公开上限补到 4 组件：
  - `Query<T1, T2, T3, T4>`
  - `Frame.Query<T1, T2, T3, T4>()`
  - `FrameReadOnly.Query<T1, T2, T3, T4>()`
- 四组件查询当前仍保持与现有 Query 家族一致的克制风格：
  - 先选最稀疏的主遍历存储
  - 其余组件按位图和存储指针校验命中
  - 不引入额外的动态 query builder 或系统家族
- 已明确这条线的正式设计结论：
  - 1 到 4 组件是当前主干正式支持的强类型查询范围
  - 更高维组合当前不继续扩成任意 N 组件 Query 家族
  - 若真的出现更复杂条件，优先拆系统职责，或结合 `MatchesFilter(...)` 做显式条件匹配
- 已为 `Frame` 补统一的 singleton / global state API：
  - `HasGlobal<T>()`
  - `GetGlobal<T>()`
  - `TryGetGlobal<T>(out T)`
  - `SetGlobal<T>(in T)`
  - `RemoveGlobal<T>()`
  - `GetGlobalEntity<T>() / TryGetGlobalEntity<T>(...)`
- global state API 当前正式语义已经落到代码层：
  - 仅允许已注册为 `ComponentFlags.Singleton` 的组件使用这组 API
  - 首次 `SetGlobal<T>()` 会自动创建承载实体
  - 后续 `SetGlobal<T>()` 会复用同一 global 实体
  - `RemoveGlobal<T>()` 在该实体只承载该 global 组件时，会一并销毁空壳实体
- `FrameReadOnly` 已同步暴露只读 global state 入口，避免玩法代码在只读视图和可写帧之间分裂出两套全局状态读法
- 已补自动化测试验证：
  - 四组件查询的枚举与 `ForEach` 路径
  - `FrameReadOnly` 对四组件查询与 global state 的转发
  - global state API 的创建、复用、读取、移除语义
  - 非 singleton 组件误用 global state API 会被明确拒绝
  - global 组件与普通组件共存时，移除 global 不会误删剩余组件
- `README` 与主文档已同步更新，避免“代码已支持正式 global/query 入口，但外部仍按旧风格理解 Frame”的版本差

当前判断：

**Priority 10 已可按“严格意义上的 100%”视为完成。**

它的完成标志不是“`Frame` 又长出一层厚门面”，而是：

- 常见多组件玩法查询现在仍能主要停留在正式 `Query` API 内
- 全局状态现在有统一正式入口，不再逼团队在 singleton / 特殊实体 / context / 静态变量之间各自发明写法
- `OwningGroup` 仍被压在“少数热点专用”位置，没有趁机重新长成默认玩法入口
- 查询表达力与全局状态这两条线已经形成清晰、可验证、不会反复回摆的主链路设计

#### Priority 11（已完成）：继续治理 `SessionRuntimeContext`，避免它退化成万能包

目标：

保持 `SessionRuntimeContext` 的轻量与清晰，让它成为正式 runtime service 挂载点，而不是逐渐膨胀成 service locator。

最终完成判断：

- `SessionRuntimeContext` 已不再只是一个“可以塞对象的地方”
- 它现在具备正式边界、显式 shared service 标记和明确误用拒绝
- 这意味着它已经从“方向正确但容易滥用”的最小实现，推进到了“职责可以在代码层被直接约束”的稳定实现

参考 FrameSync 的启发：

- FrameSync 的 `FrameContext` 和周边 runtime/game 对象很完整，但也明显更重
- Lattice 需要学习的是“共享服务要有正式归属”
- 不该学习的是“把越来越多 gameplay 语义都挂到 context 上”

本轮实际完成内容：

- 已为 `SessionRuntimeContext` 增加正式 `Boundary`
- 已明确当前上下文模型是 `TypedRuntimeSharedServices`
- 已要求所有共享对象必须显式实现 `ISessionRuntimeSharedService`
- 已明确拒绝把以下对象注册进 `Context`：
  - `Frame` 及其派生状态载体
  - `FrameSnapshot / PackedFrameSnapshot`
  - `SessionCheckpoint`
  - `SessionRuntime`
  - `SessionRuntimeContext`
- 已补自动化测试与公开面测试，保证该边界不是文档约定，而是可持续回归验证的代码契约

完成标准：

- `Frame` 继续作为确定性状态容器
- `Context` 继续作为非状态型 runtime service 容器
- 两者之间的职责不再模糊

当前判断：

**Priority 11 现在可以按“严格意义上的 100%”视为完成。**

它完成的关键不是“又新增了一个 Context API”，而是：

- `Context` 的用途已经被正式收口为 runtime shared service 挂载点
- 误把 gameplay / rollback / snapshot 状态塞进 `Context` 会被代码层直接拒绝
- 新共享对象若要进入 `Context`，必须先显式声明自己的 runtime service 身份
- 这条边界已经有测试和公开面约束，不再只是团队约定

#### Priority 12（已完成）：拆分 `SessionRuntime` 内部内核，结束单体 runtime 持续膨胀

目标：

把当前 `SessionRuntime` 从“对外边界清楚、对内仍承载过多职责”的状态，推进到“内部模块稳定分层”的状态。

为什么它重要：

- 当前 `SessionRuntime` 已经成功收口了运行边界
- 但它内部仍同时承载：
  - 输入存储
  - 历史保留
  - sampled snapshot / materialize
  - checkpoint / restore
  - rollback / resimulate
  - tick pipeline 协调
- 这意味着后续如果继续补运行能力，很容易重新长回“单体最小实现”

参考 FrameSync 的启发：

- FrameSync 的强项不只是功能多，而是 session / game / frame context / replay provider / runner 等职责分层更完整
- Lattice 不需要复制它的完整产品层，但需要学习“核心运行时不能长期挂成一个总控类”

建议工作：

- 先把 `SessionRuntime` 内部拆成稳定的内部模块，而不是先改公开 API
- 优先拆出的方向应包括：
  - 输入保留与读取
  - 历史 / snapshot / materialize
  - checkpoint / restore
  - tick pipeline 协调
- 对外仍允许暂时保持 `SessionRuntime` 作为总入口，但内部依赖关系要先清晰

完成标准：

- 后续新增运行能力时，不再默认修改一个不断膨胀的 `SessionRuntime`
- 输入 / 历史 / checkpoint / replay helper 不再深度缠绕在同一实现段落中
- runtime 核心可以继续演化，而不必每次都冒着大面积回归风险

当前完成情况（2026-03-26）：

- 已把 `SessionRuntime` 内部最重的几类职责拆成独立 internal helper，而不是继续都塞在 runtime 主类中：
  - `SessionInputStore`
  - `SessionFrameHistory`
  - `SessionTickProcessor`
  - `SessionCheckpointFactory`
- `SessionRuntime` 当前已从“自己直接维护输入 buffer、历史字典、snapshot 索引、materialize cache、recycled frame stack”的状态，收口成“协调这些内部模块工作的总入口”
- 输入相关职责已从 runtime 主类中拆出：
  - `(playerId, tick)` 输入保留
  - `SessionInputSet` 聚合 scratch
  - `InputBuffer` ring buffer
- 历史相关职责已从 runtime 主类中拆出：
  - live history
  - sampled snapshot 索引
  - materialize cache
  - scratch frame
  - recycled frame 池
- Tick 管线执行已从 runtime 主类中拆出：
  - `InputApply`
  - `Simulation`
  - `StructuralCommit`
  - `Cleanup`
  - `HistoryCapture`
- checkpoint 捕获/恢复已从 runtime 主类中拆出为专门 helper，不再与主运行逻辑混在一起
- 当前公开 API 保持不变：
  - `SessionRuntime`
  - `MinimalPredictionSession`
  - `SessionRunner / Definition / Builder`
  - 输入 / 历史 / checkpoint 的正式对外契约没有因为内部拆分而重新膨胀

已补自动化测试验证：

- 原有 `Session / Runner / Builder / Runtime85` 运行时回归全部通过，确保行为未因内部拆分回退
- 已新增结构性回归，明确 `SessionRuntime` 当前内部内核已分离出 dedicated helper，而不是继续保留旧的私有字段堆积形态
- 新 helper 当前仍保持 internal，不会把内部重构结果错误扩散成新的 public surface

当前判断：

**Priority 12 现在可以按“严格意义上的 100%”视为完成。**

它完成的关键不是“把一个大文件拆成几个文件”，而是：

- `SessionRuntime` 内部职责已经从“单体实现细节堆积”推进到“稳定 internal 模块协作”
- 输入、历史/materialize、tick pipeline、checkpoint/restore 已经有明确内部归属
- 公开 API 没有因为内部重构而重新变厚
- 这条拆分已经有测试守卫，不是一次性的代码整理

#### Priority 13（已完成）：把系统作者契约继续正式化，而不回到重系统家族

目标：

在保持当前 `ISystem + SystemPhase + SystemScheduler` 轻量风格的前提下，把系统作者真正需要遵守的运行契约继续写清。

为什么它重要：

- 当前已经有：
  - Tick 管线
  - 结构性修改纪律
  - 轻量 system phase
- 但系统作者侧仍然容易出现以下隐式约定：
  - 哪些阶段可以写 global state
  - 哪些系统允许发起结构性修改
  - 哪些系统只该读而不该写
  - 哪些运行期共享能力应走 `Context`，哪些必须留在 `Frame`

参考 FrameSync 的启发：

- FrameSync 的系统层之所以厚，不只是为了功能堆叠，而是在解决“系统运行语义可否长期稳定”
- Lattice 仍然不该直接复制 `SystemBase / SystemGroup` 家族
- 但必须继续提升“系统作者不靠默契写代码”的能力

建议工作：

- 为系统层补更正式的作者契约说明与代码约束
- 重点明确：
  - 读写阶段纪律
  - 结构性修改权限
  - global state 使用边界
  - `Context` 与 `Frame` 的职责分界
- 继续优先选择轻量 contract，而不是先长系统树和依赖图

完成标准：

- 中大型玩法系统数量增长后，不再主要依赖注册顺序和团队默契维持行为正确
- 新系统作者可以较低成本判断“这类逻辑该写在哪里、何时生效、该改什么不该改什么”

当前完成情况（2026-03-26）：

- 已新增轻量系统作者契约：
  - `SystemAuthoringContract`
  - `SystemFrameAccess`
  - `SystemGlobalAccess`
  - `SystemStructuralChangeAccess`
- `ISystem` 已正式暴露 `Contract`：
  - 默认契约允许读写普通组件数据
  - 默认不开放 global state
  - 默认不开放结构性修改
- `SystemScheduler` 已正式把作者契约纳入系统层能力面：
  - `SystemSchedulerCapability.AuthoringContractValidation`
  - 注册系统时会校验 phase 与 contract 是否兼容
- 当前已把 global state 写阶段正式收口成代码约束：
  - 允许写 global state 的 phase 为 `Input / Resolve / Cleanup`
  - `PreSimulation / Simulation` 若声明 `SystemGlobalAccess.ReadWrite` 会在注册阶段被直接拒绝
- `Frame` 已新增系统执行守卫作用域，`SystemScheduler.Update(...)` 会在每个系统 `OnUpdate(...)` 前后自动进入/退出该守卫
- 当前已把以下 API 纳入执行期约束：
  - `CreateEntity / DestroyEntity / Add / Remove`
  - 会触发结构性补写的 `Set`
  - `HasGlobal / TryGetGlobal`
  - `GetGlobal / GetGlobalEntity / TryGetGlobalEntity / SetGlobal / RemoveGlobal`
- 当前正式 global state 约束已明确分层：
  - 只读 global 访问推荐使用 `HasGlobal / TryGetGlobal`
  - `GetGlobal / GetGlobalEntity` 视为可写入口，不再默认对所有系统开放
- 样例系统与测试系统已同步迁移到显式 contract：
  - `SpawnerSystem`
  - `LifetimeSystem`
  - `DeferredSpawnSystem`
- `Context` 与 `Frame` 的职责分界在当前系统层也已收口成正式结论：
  - 系统运行时仍不直接拿到 `SessionRuntimeContext`
  - runtime shared service 继续由宿主 / 构造注入负责
  - `Frame` 继续只承载确定性状态，不回退成 runtime service 入口
- 已补自动化测试验证：
  - 默认 `ISystem.Contract` 的最小能力面
  - global 写 phase 约束
  - 未声明结构性修改权限时的执行期拒绝
  - 显式声明结构性修改权限后的正常执行
  - 未声明 global 读写权限时的执行期拒绝
  - 只读 global access 的合法读取路径
  - 仓内相关 session / scheduler / integration 测试已全部通过

当前判断：

**Priority 13 现在可以按“严格意义上的 100%”视为完成。**

它完成的关键不是“系统层又多了几个枚举和结构体”，而是：

- 系统作者需要声明的关键权限已经进入正式代码契约，而不再主要停留在口头约定
- global state 与结构性修改已经有注册期 + 执行期双层守卫
- `Context` 与 `Frame` 的边界没有因为补契约而重新混回系统接口
- 主干仍保持 `ISystem + SystemPhase + SystemScheduler` 的轻量风格，没有借机长回重系统家族

#### Priority 14（已完成）：补第二种正式运行模式，验证 runtime family 不是单例抽象

目标：

让 `SessionRuntime` 的运行模式抽象不再只服务于 `MinimalPredictionSession` 一个实现，而是真正被第二种正式模式验证。

为什么它重要：

- 当前 `MinimalPredictionSession` 已是正式模式
- 但只有一个正式 runtime 落地时，很多 boundary / capability 设计仍可能只是“对当前实现的包装”
- 如果没有第二模式验证，就很难判断：
  - 哪些抽象真的是稳定层
  - 哪些只是当前实现细节换了个名字

参考 FrameSync 的启发：

- FrameSync 的 session / game / runner 能承载本地、联机、回放、服务器等不同使用场景
- Lattice 当前不需要一次做到那么重
- 但至少要有第二条真实 runtime 路径，证明现在的 runtime family 不是纸面抽象

建议工作：

- 先补一个比 `MinimalPredictionSession` 更窄或更清楚的第二模式
- 候选方向例如：
  - `ReplayRuntime`
  - `LocalAuthoritativeRuntime`
  - 纯 verified-only 的只前推模式
- 新模式应尽量复用现有 `SessionRunner / Definition / Context / Boundary` 链路，而不是绕开主干另起炉灶

完成标准：

- `SessionRuntimeBoundary`、capability guard、runner / definition / context 链路都被第二种正式模式证明可复用
- 当前运行时分层不再只是围绕一个 runtime 的单例设计

当前完成情况（2026-03-26）：

- 已新增第二种正式运行模式：
  - `LocalAuthoritativeSession`
  - `SessionRuntimeKind.LocalAuthoritative`
- `LocalAuthoritativeSession` 当前正式定位为：
  - 纯 C#、单线程、固定顺序的本地权威运行时
  - 保留 `LocalPredictionStep`
  - 保留 `CheckpointRestore`
  - 不承诺 `PredictionVerification`
  - 不承诺 `LocalRewind`
- 第二模式没有绕开现有主干，而是正式复用：
  - `SessionRuntime`
  - `SessionRunnerBuilder.WithRuntimeFactory(...)`
  - `SessionRunnerDefinition.BuildRuntime()`
  - `SessionRunner.CreateRunner()`
  - `SessionRuntimeContext`
  - `SessionRuntimeBoundary`
- 当前已经通过第二模式证明：
  - `RuntimeKind` 不是只为 `MinimalPredictionSession` 预留的单值包装
  - capability guard 可以按运行模式选择性开放 / 拒绝 API
  - `Context.RuntimeKind / RuntimeBoundary / RunnerName` 元信息不会绑定到单一 runtime
  - builder / definition / runner 的创建链路可以稳定承载非默认 runtime
- 当前主链路行为已补自动化测试验证：
  - `LocalAuthoritativeSession` 的正式 boundary 能力面
  - 第二模式的 `Context` 元信息暴露
  - 第二模式对 `VerifyFrame / RollbackTo / Rewind` 的拒绝
  - 第二模式对 `CreateCheckpoint / RestoreFromCheckpoint` 的保留
  - `SessionRunnerBuilder` 通过正式 factory 创建第二模式 runtime
  - `SessionRunner` 通过 definition 承载第二模式 runtime
- 当前默认运行模式仍保持克制：
  - `SessionRunnerBuilder` 默认仍构建 `MinimalPredictionSession`
  - 第二模式通过显式 factory 选择，不会把默认主链路重新做模糊

当前判断：

**Priority 14 现在可以按“严格意义上的 100%”视为完成。**

它完成的关键不是“又新增了一个派生类”，而是：

- 第二种正式 runtime 已进入主干编译面，而不再只是测试里的临时派生类型
- `SessionRuntimeBoundary`、capability guard、runner / definition / context 链路都已经被第二模式真实复用
- 当前 runtime family 不再只是围绕 `MinimalPredictionSession` 的单例抽象
- 新模式仍复用主干，不需要回到旁路装配或 ad-hoc 宿主代码
#### Priority 15（已完成）：补 determinism 静态守卫，降低“靠纪律防失控”的风险

目标：

把当前主要依赖代码纪律和测试兜底的 determinism 约束，逐步前置到更正式的静态守卫或 tooling 上。

为什么它重要：

- 当前 Lattice 在 determinism 上的主要优势来自：
  - 固定点数学
  - 运行时边界
  - 测试覆盖
- 但对中大型项目来说，这仍然不够
- 因为团队规模一大，以下问题会更频繁出现：
  - 不当静态字段
  - 非法状态缓存
  - 非确定性调用混入系统逻辑
  - 本地方便写法慢慢侵入正式模拟代码

参考 FrameSync 的启发：

- FrameSync 已有 `CanMutateAttribute`、`StaticFieldAttribute` 等 analyzer / 标记治理思路
- Lattice 不需要一开始就补一整套重 analyzer 生态
- 但必须开始建设最小可行的 determinism 静态守卫

建议工作：

- 先明确最值得被 tooling 守住的几类风险：
  - 静态字段
  - 不允许的运行时 API 使用
  - 系统中的非确定性依赖
- 再决定是通过 analyzer、测试扫描还是 build-time 守卫先落最小闭环

完成标准：

- determinism 约束不再主要靠“团队记得”
- 关键非确定性风险能够在联调前被更早发现

当前完成情况（2026-03-26）：

- 已新增 `Tools/Lattice.Analyzers/DeterminismGuardAnalyzer.cs`，并形成三条正式诊断：
  - `LATTICE100`：禁止 `ISystem` / `SessionRuntime` 相关类型引入可变静态字段
  - `LATTICE101`：禁止在正式模拟入口中调用典型非确定性 API
  - `LATTICE102`：系统在 `OnUpdate(...)` 中使用受守卫 `Frame` API 时，必须显式声明兼容的 `SystemAuthoringContract`
- 当前已正式守住的高风险项包括：
  - `Guid.NewGuid`
  - `DateTime.Now / UtcNow`
  - `DateTimeOffset.Now / UtcNow`
  - `Environment.TickCount / TickCount64`
  - `Random` 构造、实例调用与 `Random.Shared`
  - `Task.Run / Delay`
  - `Thread.Sleep / Yield`
  - `SpinWait.SpinUntil`
  - `Stopwatch.StartNew / GetTimestamp`
- 受守卫的 `Frame` API 已覆盖第一轮最关键入口：
  - 结构性修改：`CreateEntity / DestroyEntity / Add / Remove`
  - global 读取：`HasGlobal / TryGetGlobal`
  - global 写入：`GetGlobal / GetGlobalEntity / TryGetGlobalEntity / SetGlobal / RemoveGlobal`
- analyzer 已接入 `Lattice.csproj` 的正式构建链路；主干编译时会先构建 analyzer，再把产物作为 `<Analyzer>` 引入，避免继续依赖文档或人工检查
- 已补 `DeterminismAnalyzerTests` 自动化回归，覆盖：
  - system / runtime 的可变静态字段拒绝
  - `OnInit(...)` / `OnUpdate(...)` / `ApplyInputSet(...)` 中的典型非确定性 API 拒绝
  - 缺失 contract 的结构性修改与 global 访问拒绝
  - 显式兼容 contract 的合法路径放行

当前判断：

**Priority 15 现在可以按“严格意义上的 100%”视为完成。**

它完成的关键不是“多了一个 analyzer 文件”，而是：

- determinism 风险已经开始前置到主干编译期，而不再主要依赖评审与联调阶段兜底
- 系统作者契约与 `Frame` 运行时边界，已经有了对应的静态守卫，而不是只靠运行期异常提示
- 当前最容易把正式模拟拖回“靠纪律维护”的几类问题，已经有了仓内自动化回归保护

#### Priority 16（已完成）：补协议 / 版本演进策略，避免输入与状态格式长期失控

目标：

在输入、checkpoint、component schema 等格式继续演进之前，先把版本治理策略补出来。

为什么它重要：

- 当前 Lattice 已经有：
  - `IPlayerInput`
  - packed checkpoint
  - sampled snapshot
  - 组件序列化路径
- 但还没有一套正式的版本演进策略去回答：
  - 输入格式变更怎么办
  - checkpoint 格式升级怎么办
  - component schema 扩展或裁剪怎么办
  - 哪些兼容是正式承诺，哪些不是

参考 FrameSync 的启发：

- FrameSync 有更明确的 protocol version / compatibility 思路
- Lattice 当前不需要先补完整联机协议产品层
- 但必须先补“框架内部格式如何演进”的治理方式

建议工作：

- 明确 Lattice 当前需要治理的版本面：
  - 输入 payload / codec
  - checkpoint / packed snapshot
  - component serialization schema
- 再决定哪些版本字段属于正式公开承诺，哪些只属于内部实现
- 优先做最小但清晰的版本策略，而不是先做沉重兼容层

完成标准：

- 未来输入、checkpoint、component schema 继续演进时，不会只能靠临时 patch 和经验判断
- 哪些格式可以变、怎么变、变更后如何兼容，已经有正式框架规则

当前完成情况（2026-03-26）：

- 已新增正式输入协议治理面：
  - `SessionInputProtocol.CurrentContractVersion`
  - `SessionInputContractStamp`
  - `SessionInputPayloadFormat`
  - `IVersionedInputPayloadCodec<TInput>`
- 当前规则已经明确为：
  - runtime 输入契约通过 `SessionRuntimeInputBoundary` 生成正式 stamp
  - payload codec 若要形成稳定对外载荷协议，必须显式声明 `codecId + version`
  - 第一轮兼容策略采用“精确匹配优先”，不再默认接受隐式兼容
- 已新增正式 checkpoint 协议面：
  - `SessionCheckpointProtocol`
  - `SessionCheckpoint.Protocol`
  - `SessionCheckpointFactory` 的 capture / restore 协议校验
- 当前 checkpoint 恢复会在 restore 前显式校验：
  - checkpoint 协议版本
  - 输入契约 stamp
  - checkpoint 存储类型
  - packed snapshot 格式版本
  - checkpoint 协议与 snapshot metadata 是否自洽
- 已新增 packed snapshot / component schema 治理面：
  - `PackedFrameSnapshot.CurrentFormatVersion`
  - `PackedFrameSnapshot.SchemaManifest`
  - `ComponentSchemaManifest`
  - `ComponentCallbacks.SerializationVersion`
- 当前 `Frame.CapturePackedSnapshot(...)` / `RestoreFromPackedSnapshot(...)` 已正式具备：
  - 写出时记录 packed snapshot 格式版本
  - 按当前实际写出的组件集合生成 schema 指纹
  - 恢复时显式校验格式版本、序列化模式与组件 schema 指纹
- 当前组件序列化协议的正式规则已写进代码：
  - 若组件稳定序列化布局发生兼容性变更，应显式递增 `ComponentCallbacks.SerializationVersion`
  - 不再把组件 schema 变化完全留给“回头查 diff”或“作者记得”
- 已补自动化测试验证：
  - checkpoint 协议 metadata 捕获
  - 输入契约版本不匹配时的 restore 拒绝
  - packed snapshot 格式版本不匹配时的 restore 拒绝
  - component schema 指纹不匹配时的 restore 拒绝
  - versioned input payload codec 的精确兼容校验
  - 全量 `Lattice.Tests` 回归通过

当前判断：

**Priority 16 现在可以按“严格意义上的 100%”视为完成。**

它完成的关键不是“多了几个 version 字段”，而是：

- 输入、checkpoint、packed snapshot、component schema 这几类长期最容易失控的格式面，已经有了正式协议对象，而不是继续散落在注释和实现细节里
- restore 路径已经会主动拒绝不兼容协议，而不是把问题拖到更晚的联调或线上
- 组件序列化与输入 payload 的演进规则已经变成仓内可执行约束，而不是默认靠团队记忆维护

#### Priority 17（已完成）：把 `SessionRunner` 推进到更正式的宿主生命周期模型

目标：

让 `SessionRunner` 从“很好用的最小驱动器”继续推进到“正式宿主生命周期边界”，但仍保持轻量，不复制 FrameSync 的完整产品壳。

为什么它重要：

- 当前 `SessionRunner` 已经很好地解决了：
  - start / step / stop
  - definition-based runtime 重建
  - runner 名称与 context 对齐
- 但它仍然是薄 runner
- 对于中大型项目来说，还缺一层更正式的宿主语义去回答：
  - runtime 创建、启动、停止、重建的宿主责任边界
  - 外部服务与 runtime service 的挂接时机
  - 错误、失败、关闭原因、状态机扩展如何承载

参考 FrameSync 的启发：

- FrameSync 的 `SessionRunner` 已经能承载更完整的宿主生命周期、上下文初始化与不同宿主场景
- Lattice 当前不该照搬它的异步/网络/平台壳
- 但应该吸收“runner 不是只负责转发 `Start/Step/Stop`”这一点

建议工作：

- 继续明确 `SessionRunner / Definition / Runtime / Context` 的宿主边界
- 评估是否需要补：
  - 更明确的 runner state 扩展
  - 失败 / shutdown cause
  - 宿主级生命周期钩子
  - runtime service 挂接与释放时机
- 保持当前纯 C#、最小宿主风格，不提前引入完整联机/平台壳

完成标准：

- `SessionRunner` 不再只是薄包装，而是正式的 runtime 宿主生命周期边界
- 后续要接更多运行模式或宿主场景时，不需要回头改写整个启动链

当前完成情况（2026-03-26）：

- 已为 `SessionRunner` 补正式生命周期状态与元信息：
  - `SessionRunnerState.Faulted`
  - `SessionRunnerShutdownCause`
  - `SessionRunnerResetReason`
  - `SessionRunnerFailureKind`
  - `SessionRunnerFailureInfo`
- `SessionRunner` 现在已正式暴露最近一次宿主生命周期结果：
  - `LastShutdownCause`
  - `LastResetReason`
  - `LastFailure`
- 已新增轻量宿主生命周期钩子面：
  - `SessionRunnerLifecycleHooks`
  - `SessionRunnerBuilder.ConfigureLifecycle(...)`
- 当前 definition-based runner 已具备清晰的宿主时序：
  - runtime 创建完成后先经过 `RuntimeCreated`
  - 启动走 `BeforeStart / AfterStart`
  - 停止走 `BeforeStop / AfterStop`
  - 重建走 `BeforeReset / AfterReset`
- `RuntimeCreated` 的调用时机已经明确为：
  - runtime factory 创建完成
  - context configurator 与 system 注册完成之后
  - runner 正式开始驱动之前
- 这意味着宿主级 shared service 现在可以在正式、稳定的时机挂接到 `SessionRuntimeContext`，而不必继续依赖 ad-hoc 初始化顺序
- `Start / Step / Stop / ResetRuntime / Dispose` 的主要失败路径已经进入正式收敛：
  - 失败会记录 `LastFailure`
  - runner 状态会进入 `Faulted`
  - 启动失败与步进失败会记录明确 shutdown cause
  - 已运行 runtime 会尝试做故障后的停止收敛
- 已补自动化回归覆盖：
  - 宿主钩子顺序与 runtime service 挂接时机
  - `Start` 失败收敛
  - `Step` 失败收敛
  - 生命周期钩子失败收敛
  - builder 生命周期配置的定义式不可变性
  - compatibility surface 对新增公开 API 的锁定
- 当前验证结果：
  - `dotnet build godot/scripts/lattice/Tests/Lattice.Tests.csproj -nologo --no-restore`
  - `dotnet test godot/scripts/lattice/Tests/Lattice.Tests.csproj -nologo --no-build`
  - 883 个测试已全部通过

当前判断：

**Priority 17 现在可以按“严格意义上的 100%”视为完成。**

它完成的关键不是“多了几个状态字段和 hook”，而是：

- `SessionRunner` 已经不再只是 runtime 的薄转发器，而是正式承载宿主创建、启动、停止、重建、失败收敛的生命周期边界
- runtime shared service 的宿主挂接时机已经被写进正式代码路径，而不是继续依赖调用顺序默契
- 失败、关闭原因和重建原因已经进入稳定公开面，后续接更多宿主场景时不需要再重写整个启动链

#### Priority 18（已完成）：把 benchmark 从“观察项”升级成“治理项”

目标：

让当前已经存在的 benchmark 与成本画像，从“看一眼趋势”升级为“长期防退化的治理工具”。

为什么它重要：

- 当前 benchmark 已经能较好描述热点与坏场景
- 但它还没有真正成为长期门槛
- 这意味着后续改动很容易让性能慢慢回退，而没有明确的失败信号

建议工作：

- 为关键路径沉淀更稳定的场景与阈值
- 优先补：
  - 更贴近真实玩法的负载画像
  - MixedSerialized 坏场景基线
  - rollback / restore 的长期守门
- 再决定是否引入自动化阈值守卫

完成标准：

- benchmark 不再只是“优化时跑一下”
- 关键路径成本已有稳定基线，长期回退会更早暴露

当前完成情况（2026-03-26）：

- 已新增正式 benchmark 治理面：
  - `SessionRuntimeBenchmarkGovernancePolicy`
  - `SessionRuntimeBenchmarkGovernanceScenario`
  - `SessionRuntimeBenchmarkBudget`
  - `SessionRuntimeBenchmarkMeasurement`
  - `SessionRuntimeBenchmarkGovernanceResult`
- 当前正式治理策略已扩到 8 个场景、49 条预算：
  - `gameplay_256_mixed`
  - `stress_1024_mixed`
  - `stress_1024_raw_history`
- 已进一步补入更贴近真实玩法复杂度的 CombatMixed 治理画像：
  - `gameplay_256_combat_mixed`
  - `gameplay_1024_combat_mixed`
  - `checkpoint_heavy_1024_combat_save_like`
  - `history_read_1024_combat_replay`
  - `rollback_storm_256_combat_window`
- 上述场景已经把 Priority 18 最关键的三类诉求都落到了正式预算里，而且补齐了第二优先希望验证的三类更重画像：
  - 更贴近真实玩法的负载画像
  - `MixedSerialized` 坏场景基线
  - `restore / rollback / history` 的长期守门
- 第二优先的更重治理画像也已进入正式预算：
  - `1024 CombatMixed` 主玩法负载
  - `checkpoint-heavy` 的 save-like 压力
  - `history-read` 的 replay-like 压力
- 本轮还把其中两类重画像补成了专项 benchmark 方法，而不再只靠通用方法侧面观察：
  - `Checkpoint Save-Like Chain x16`
  - `Historical Replay Scrub 9-24 x16`
- benchmark 治理正式入口也已补自动化测试覆盖：
  - `BenchmarkGovernanceCli.ParseArguments(...)`
  - `--govern-report` 的成功 / 失败 / 缺参路径
- 已新增 BenchmarkDotNet CSV 报告校验器：
  - 能解析 `Mean / Allocated / EntityCount / PayloadProfile`
  - 能识别缺失场景、耗时超线、分配超线
  - 校验结果已具备正式 pass/fail 文本输出
- 已把 benchmark runner 升级成正式治理入口：
  - `--govern`
  - `--govern-report <csv-path>`
- 当前仓内已经可以直接执行：
  - 先跑 benchmark，再自动做门槛校验
  - 或只对已有 CSV 报告做治理校验
- 已补自动化测试覆盖：
  - policy 场景覆盖校验
  - BenchmarkDotNet CSV 解析
  - 预算内通过
  - 超预算失败
  - 缺失场景失败
- 本轮治理面已继续演进到 policy `v4`：
  - 第一轮 Mixed/Raw 基线、256 CombatMixed、rollback storm 与第二优先的 1024 combat / checkpoint-heavy / replay-like history 都已纳入
  - 并补了专项 save-like / replay-like benchmark surface 与 policy 一致性守卫
  - 当前 policy 元数据为 `policy v4 | scenarios=8 | budgets=49`
- 相关文档已经同步：
  - `Benchmarks/README.md`
  - `README.md`
  - `SystemDesignNotes.md`

当前判断：

**Priority 18 现在可以按“严格意义上的 100%”视为完成。**

它完成的关键不是“又多了一份 benchmark 报告”，而是：

- benchmark 已经从“人工看趋势”升级为“仓内可执行治理规则”
- 当前关键路径已有正式预算与失败信号，而不是继续靠经验判断是否变慢
- 更贴近真实玩法的 MixedSerialized 画像、坏场景和 restore / rollback 重路径，已经进入长期守门范围

### 面向中大型玩法复杂度的建议推进顺序

如果新的阶段目标是：

**让 Lattice 从“8.5 分、边界清楚的最小运行时”继续推进到“能长期承载中大型玩法复杂度的稳定框架”。**

那么建议顺序应改为：

1. 当前这一轮优先级清单已全部完成
2. 如果继续推进，更适合转向真实玩法负载验证、兼容面持续瘦身与更重的产品层能力建设

一句话总结：

**这一轮真正需要制度化的主干项已经全部收口；下一阶段更适合把这些制度化边界拿到更真实、更复杂的玩法与产品场景里继续验证。**

### 当前明确暂不作为主目标的设计项

在“先把框架设计做清楚”这个阶段，以下内容**不是当前主目标**：

- 回放文件、Instant Replay、Checksum 录制、Frame Differ 等回放/诊断工具链
- `SystemsConfig` 风格的资源化系统装配
- FrameSync 的完整 `SystemBase / SystemGroup / Filter System` 家族
- 物理、导航、动态资源、视图同步等完整 simulation ecosystem
- 以 Unity/编辑器为中心的配置资源与运行时胶水层

原因：

- 这些内容大多属于**产品化能力扩展**
- 它们确实是 FrameSync 的优势，但不是当前 Lattice 设计层面最急迫的短板
- 如果在运行时分层还不够清楚时就先补这些，很容易把主干结构再次做乱

### 更新后的建议推进顺序

如果下一阶段的目标仍然只是“先把框架设计做到清晰合理”，建议顺序改为：

1. 保持当前宿主生命周期、协议治理、determinism 守卫与 benchmark 治理继续稳定
2. 在此基础上，再评估真实玩法负载、兼容面瘦身与更重的产品层建设

一句话总结：

**Lattice 当前最该做的已经不再是继续补同类主干制度，而是把已经建立好的制度拿去承载更真实的复杂度，并继续防止主干结构回退。**

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
- 不把回放链路与诊断工具链纳入当前这轮框架设计主目标
- 不追求完整玩家/网络会话层
- 不追求资产与原型系统
- 不追求物理、导航、View 生态
- 不追求“所有系统都支持并行”
- 不追求为了理论性能而过早改写所有系统为 `OwningGroup`

---

## 一句话原则

当前阶段的 Lattice 设计应遵循：

**先把 `System + Session + 输入 + 历史帧 + 回滚` 接成真实运行闭环，再谈热点优化和外围生态。**
