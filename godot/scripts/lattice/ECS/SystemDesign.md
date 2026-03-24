# Lattice ECS System 设计

## 文档目的

本文档用于基于 FrameSync 的 `System` 设计经验，重新梳理 Lattice 的 `System` 运行层设计。

目标不是照搬 FrameSync，而是提取其中真正适合 Lattice 当前阶段的部分，建立一套：

- 与现有 `Frame / Storage / Query / OwningGroup` 基座一致
- 支持确定性与回滚
- 先单线程闭环，再逐步扩展到并行
- 对未来热重载、调试工具、系统分组具备扩展空间

---

## 当前现状

### 已具备

Lattice 当前主线已经拥有一批可直接支撑系统调度的数据层能力：

- `Frame`
- `Storage<T>`
- `Filter<T...>`
- `ComponentBlockIterator<T>`
- `FrameSnapshot`
- `CommandBuffer`

这些能力已经足以支撑 `System` 主线运行。

### 当前完成度判断

如果只看 `System` 自身，不考虑它和 `Session / Snapshot / Rollback / Network` 的整合：

- 作为“单线程确定性主线程 system 运行层”，当前完成度约为 `70 / 100`
- 作为“完整的 ECS system 框架”，当前完成度约为 `40 / 100`

这意味着：

**Lattice 的 `System` 已经从“待设计概念”进入“正式可用的第一版 runtime”，但距离成熟 system 框架还有明显差距。**

### 已经落地的能力

当前已经具备：

- 正式主线接口：`ISystem`
- 基础基类：`SystemBase`
- 层级容器：`SystemGroup`
- 单线程固定顺序调度器：`SystemScheduler`
- `OnEnabled / OnDisabled` 生命周期回调
- 调度器状态机约束与更新期变更保护
- 系统树 / 执行顺序 / 节点查询快照接口
- 正式 `SystemMetadata` 元数据：`Order / Kind / Category / DebugCategory / AllowRuntimeToggle`
- 基于 `Order` 的稳定排序，注册顺序作为并列兜底
- `SystemScheduler` trace hook：`CurrentSystem / Trace / SystemSchedulerTraceEvent`
- 生命周期与逐帧更新的 enter / exit trace 事件
- `ParallelReserved` 执行类型的显式边界校验
- 普通系统默认入口：`frame.Filter<T...>()`
- 热点系统低层入口：`GetComponentBlockIterator<T>()`
- 基本延迟结构修改闭环：`CommandBuffer`
- 调度测试与系统集成测试

### 当前还缺什么

下面这些能力，仍然是 `System` 自身没有做完的，而不是外部模块问题：

- system 分类边界仍未正式分层
- 调度顺序还没有扩展到 before / after 与阶段化表达
- trace hook 还没有进一步扩展为正式 profiler / timeline / 统计采样接口
- 并行 system 目前只有显式拒绝边界，还没有真正的并行调度抽象

---

## FrameSync 的可借鉴设计

参考 FrameSync 的系统层，最有价值的不是“类名”，而是下面几条结构性经验。

### 1. System 不是单一接口，而是分层体系

FrameSync 的系统层不是只有一个 `ISystem`，而是：

- `SystemBase`
- `SystemMainThread`
- `SystemMainThreadFilter<T>`
- `SystemThreadedFilter<T>`
- `SystemSignalsOnly`
- `SystemGroup`
- `SystemsConfig`
- `DeterministicSystemSetup`

这说明一个成熟的系统层需要同时解决：

- 生命周期
- 调度入口
- 分组层级
- 启停控制
- 数据驱动装配
- 主线程与并行线程的职责分离

### 2. System 生命周期必须和启停控制绑定

FrameSync 的 `SystemBase` 支持：

- `OnInit`
- `OnEnabled`
- `OnDisabled`
- `OnSchedule`

并且系统本身具备：

- 运行时索引
- 父子层级
- 启用 / 禁用状态

这意味着：

- “系统是否存在”
- “系统是否启用”
- “系统何时参与调度”

应该是三件不同的事情。

### 3. 系统分组是正式概念，不是临时约定

FrameSync 中系统可以形成层级：

- 根系统
- 子系统
- 组系统

这带来的价值是：

- 可以整体开关一组系统
- 可以保证局部执行顺序
- 可以给调试与 profiler 提供层级上下文

### 4. 系统装配要有“代码入口”与“配置入口”

FrameSync 不是直接把系统写死，而是通过：

- `SystemsConfig`
- `SystemSetup`

来实例化系统列表。

这意味着系统层最好分成：

- 运行时系统定义
- 系统注册 / 装配入口

### 5. 并行系统不该和普通系统混成一种抽象

FrameSync 将：

- 主线程系统
- 线程化 Filter 系统

明确区分开来。

这个拆分对 Lattice 也非常重要，因为：

- 当前 `Query` 默认是普通路径
- `OwningGroup` 是热点路径
- `JobSystem / ParallelFilter` 已经存在

如果未来要支持并行系统，必须从抽象层面把“主线程系统”和“并行系统”分开。

---

## Lattice 不应直接照搬的部分

虽然 FrameSync 的方向值得参考，但 Lattice 不应该原样复制。

### 1. 不要先做过重的配置资产体系

FrameSync 有 Unity 资产化的 `SystemsConfig`，这是它生态的一部分。

Lattice 当前更适合：

- 先以纯 C# 代码方式注册系统
- 等 System 主线稳定后，再补配置驱动层

### 2. 不要先做过多派生类

FrameSync 的系统基类很多，是因为它已经成熟。

Lattice 当前阶段如果直接引入：

- `SystemMainThread`
- `SystemThreadedFilter<T>`
- `SystemSignalsOnly`
- `SystemGroup`
- `SystemArrayComponent<T>`

会导致在真正闭环前就过度设计。

### 3. 不要让 Session 先于 Scheduler 落地

FrameSync 的会话层建立在成熟系统层之上。

Lattice 当前更合理的顺序应该反过来：

1. 先做 `ISystem`
2. 再做 `SystemScheduler`
3. 再把 Session 接回去

而不是继续在旧 `Session.cs` 上堆功能。

---

## Lattice 的目标设计

## 设计原则

Lattice 的 System 设计应遵循下面 6 条原则。

### 1. System 无状态

系统对象不持有可变模拟状态。

所有游戏状态都应存于：

- `Frame`
- 组件
- 全局组件 / 单例组件

这样做的好处：

- 天然适配回滚
- 适配快照恢复
- 更容易支持热重载

### 2. 第二阶段默认入口是 `frame.Filter<T...>()`

普通系统应以：

- `frame.Filter<T...>()`
- `frame.GetComponentBlockIterator<T>()`

作为默认数据访问入口。

其中：

- `frame.Filter<T...>()` 用于大多数普通系统
- `GetComponentBlockIterator<T>()` 仍作为热点系统的低层入口

当前主线尚未提供正式 `Query<T...>` API，因此文档与示例不应把 `Query` 写成既成事实。

### 3. 热点专用入口留待第二阶段评估

只有极少数高频固定系统，未来才考虑使用更激进的热点路径，例如：

- 更窄的专用遍历器
- 显式 owning group / hot bundle
- 特定系统的 block zipped fast path

### 4. 调度顺序必须显式且稳定

System 层首先要保证：

- 顺序明确
- 可预测
- 可测试

而不是优先追求自动推导依赖或隐式排序。

### 5. 先单线程，再并行

第一版 System 运行层只做：

- 单线程
- 固定顺序

第二阶段再引入：

- 并行系统
- Job 化系统

### 6. 分组与启停要早做，但做轻量版本

系统分组对后续非常重要，应该尽早设计，但不需要一开始做复杂资产配置。

---

## 建议的最小系统分层

### 第一层：基础接口

建议先定义一个最小接口：

```csharp
public interface ISystem
{
    string Name { get; }
    bool EnabledByDefault { get; }

    void OnInit(Frame frame);
    void OnEnabled(Frame frame);
    void OnDisabled(Frame frame);
    void OnUpdate(Frame frame, FP deltaTime);
    void OnDispose(Frame frame);
}
```

说明：

- `Name` 用于调试、日志、Profiler
- `EnabledByDefault` 用于系统启动时的初始状态
- 生命周期已经扩展到“初始化 + 启停 + 销毁”的最小正式集合

### 第二层：基础基类

建议提供一个轻量 `SystemBase`：

```csharp
public abstract class SystemBase : ISystem
{
    public virtual string Name => GetType().Name;
    public virtual bool EnabledByDefault => true;

    public virtual void OnInit(Frame frame) { }
    public virtual void OnEnabled(Frame frame) { }
    public virtual void OnDisabled(Frame frame) { }
    public abstract void OnUpdate(Frame frame, FP deltaTime);
    public virtual void OnDispose(Frame frame) { }
}
```

这层的目标是：

- 统一系统命名
- 提供基础生命周期空实现
- 减少样板代码

### 第三层：系统组

建议引入轻量 `SystemGroup`：

```csharp
public sealed class SystemGroup : ISystem
{
    public string Name { get; }
    public IReadOnlyList<ISystem> Systems { get; }
}
```

作用：

- 表达顺序结构
- 表达局部层级
- 未来可扩展为组级启停与 profiler 域

这里不要一开始就做复杂树状配置资产，先支持：

- 代码组装
- 嵌套组
- 深度优先顺序展开

### 第四层：调度器

建议单独实现 `SystemScheduler`，负责：

- 系统注册
- 初始化
- 逐帧调度
- 销毁
- 启停状态维护

建议接口如下：

```csharp
public sealed class SystemScheduler
{
    public void Add(ISystem system);
    public void AddRange(IEnumerable<ISystem> systems);
    public void Initialize(Frame frame);
    public void Update(Frame frame, FP deltaTime);
    public void Dispose(Frame frame);

    public void Enable<T>() where T : ISystem;
    public void Disable<T>() where T : ISystem;
    public bool IsEnabled<T>() where T : ISystem;
}
```

---

## 推荐的系统分类

为了吸收 FrameSync 的优点，但不过度复杂化，Lattice 建议分三类。

### 1. MainThreadSystem

普通主线程系统，默认类型。

特点：

- 能访问完整 `Frame`
- 能使用 `Filter / BlockIterator`
- 能发命令
- 适合大多数游戏逻辑

例如：

- `MovementSystem`
- `LifetimeSystem`
- `CombatResolveSystem`

### 2. QuerySystem

不是必须一开始实现为单独基类，但设计上要承认这是一种主要模式。

这类系统特点：

- 主逻辑是扫描一组固定组件
- 在当前主线中，推荐以 `Filter<T...>` 或 `BlockIterator` 编写
- 未来可以进一步提供便捷基类

未来可以考虑：

```csharp
public abstract class QuerySystem<T1, T2> : SystemBase
```

但第一阶段不必立即引入。

### 3. HotPathSystem

这是 Lattice 特有的重点，不是 FrameSync 原样照搬。

这类系统直接使用：

- `ComponentBlockIterator<T>`
- 经过显式注册和维护的热点布局

用于极少数固定高频系统。

例如：

- `MovementHotSystem`
- `ProjectileHotSystem`

注意：

- 这不是默认系统类型
- 只服务极少数热点系统

---

## 不建议第一阶段实现的类型

下面这些能力很有价值，但不建议放到第一阶段：

- 线程化系统基类
- 复杂依赖图排序
- 数据驱动 `SystemsConfig` 资产
- 自动根据查询类型生成系统特化类
- 信号系统专用基类
- 完整 Session 回滚框架

原因很简单：

当前真正缺的是最小运行闭环，而不是高级装配能力。

---

## 建议的第一阶段 API

Lattice 第一阶段 System 设计建议只保留这些正式 API：

- `ISystem`
- `SystemBase`
- `SystemGroup`
- `SystemScheduler`

系统编写方式建议如下：

```csharp
public sealed class MovementSystem : SystemBase
{
    public override void OnUpdate(Frame frame, FP deltaTime)
    {
        var filter = frame.Filter<Position, Velocity>();
        var enumerator = filter.GetEnumerator();

        while (enumerator.MoveNext())
        {
            enumerator.Component1.Value += enumerator.Component2.Value * deltaTime;
        }
    }
}
```

更保守、完全贴合当前主线的热点遍历建议如下：

```csharp
public unsafe sealed class MovementHotSystem : SystemBase
{
    public override void OnUpdate(Frame frame, FP deltaTime)
    {
        var iterator = frame.GetComponentBlockIterator<Position>();

        while (iterator.Next(out var entity, out Position* position))
        {
            Velocity* velocity = frame.GetPointer<Velocity>(entity);
            if (velocity == null)
            {
                continue;
            }

            position->Value += velocity->Value * deltaTime;
        }
    }
}
```

---

## 调度顺序建议

第一阶段建议采用显式顺序，不做自动拓扑排序。

例如：

```text
BootstrapGroup
InputGroup
SimulationGroup
LateSimulationGroup
PresentationBridgeGroup
```

每组内部顺序固定：

```text
InputCollectSystem
MovementSystem
LifetimeSystem
CombatSystem
CleanupSystem
```

这样做的优点：

- 易理解
- 易测试
- 易调试
- 与回滚重模拟兼容

---

## 与 Session 的关系

Lattice 后续仍然需要会话层，但应重建，而不是继续直接使用旧 `Session.cs`。

正确依赖方向应为：

```text
Frame / Filter / BlockIterator / CommandBuffer
                    ↓
             SystemScheduler
                    ↓
       SimulationWorld / SimulationLoop
                    ↓
PredictedSession / ReplaySession / LocalSession
```

而不是：

```text
Session 先定义完
  ↓
再倒推需要什么 System
```

### Session 第二阶段应负责的事

- 管理当前 `Frame`
- 管理历史快照
- 执行回滚与重模拟
- 持有 `SystemScheduler`
- 协调输入与校验

### Session 不应负责的事

- 直接定义系统接口
- 直接承载系统生命周期语义
- 把系统装配和网络逻辑混在一起

---

## 分阶段落地建议

## Phase S1：最小可用运行层

实现：

- `ISystem`
- `SystemBase`
- `SystemGroup`
- `SystemScheduler`

验证：

- 可以跑 2 到 3 个真实系统
- 可以验证执行顺序
- 可以验证启停逻辑

## Phase S2：和现有 ECS 基座对齐

实现：

- 系统级单测
- 系统与 `Filter / BlockIterator` 的推荐写法
- `CommandBuffer` 与系统延迟结构修改闭环
- 热点系统与显式专用遍历路径的协作方式

验证：

- 普通系统默认走 `Filter / BlockIterator`
- `CommandBuffer` 能对已注册组件执行 Create / Add / Set / Remove / Destroy 回放
- 少量热点系统再评估是否值得引入专用布局

## Phase S3：重建 Session

实现：

- 新版 `SimulationSession`
- 绑定 `SystemScheduler`
- 接入 `FrameSnapshot`
- 回滚 / 重模拟闭环

## Phase S4：并行与高级调度

实现：

- 主线程系统与并行系统拆分
- JobSystem 对接
- 组级 profiler / 调试入口

---

## 后续优化优先级

为了避免 `System` 设计再次失焦，建议把后续工作拆成 `P0 / P1 / P2` 三层。

## P0：必须优先补齐的内核能力

> 当前状态：本轮已完成。

### 1. 生命周期语义补全

- `OnEnabled`
- `OnDisabled`
- 组级启停传播语义
- 重复初始化 / 重复销毁 / 已销毁后再次更新的严格约束

这是最高优先级，因为它决定了 system runtime 的基本语义是否清晰。

### 2. 调度器可观测能力

- 导出当前系统树
- 导出稳定执行顺序
- 查询系统是否已初始化
- 查询系统当前是否被层级禁用
- 按类型 / 名称 / 实例查询系统节点

这类能力是后续调试、验证和工具化的基础。

### 3. 调度器状态机收紧

- 未初始化前能做什么
- 初始化后还能做什么
- Dispose 后是否允许再次 Initialize
- Update 过程中是否允许 Add / Enable / Disable
- 组级结构变更的时机限制

这决定了 `SystemScheduler` 能否成为可靠的基础设施。

## P1：内核稳定后应推进的能力

### 4. system 元数据

> 当前状态：本轮已完成第一版正式落地。

当前 system 元数据只有：

- `Name`
- `EnabledByDefault`

后续建议补：

- 阶段或分类名
- 可选的 order / priority
- 调试标签或 profiler 分类名
- 是否允许热切换或动态启停的元数据

当前已经落地：

- `SystemMetadata`
- `SystemExecutionKind`
- `Order / Kind / Category / DebugCategory / AllowRuntimeToggle`

### 5. system 分类边界

长期看，至少应在设计层承认下面几类 system：

- 普通主线程系统
- 热点系统
- 未来的并行系统
- 可能的信号 / 事件型系统

当前不急着一次全部落代码，但应先把分类边界说清楚。

### 6. 更明确的顺序表达

> 当前状态：本轮已完成第一版稳定 `Order` 排序。

现在只有：

- `Order`
- 注册顺序兜底
- 分组深度优先

后续可能需要：

- 组内显式 order
- 组间阶段顺序
- before / after 约束
- 受控的局部排序

这里不建议直接上自动拓扑排序，应先补“显式且稳定”的顺序表达。

## P2：可以延后，但要提前留余量的能力

> 当前状态：本轮已完成第一版 trace hook 与并行边界预留；配置驱动装配仍未开始。

### 7. profiler / debug hook

后续 `System` 层需要能暴露：

- 当前执行中的 system 名称
- 每帧执行顺序
- system 层级路径
- system 级耗时采样入口

当前第一版已经落地：

- `SystemScheduler.CurrentSystem`
- `SystemScheduler.Trace`
- `SystemSchedulerTracePhase / SystemSchedulerTraceEvent`
- `OnInit / OnEnabled / OnUpdate / OnDisabled / OnDispose` 的 enter / exit trace

后续仍可继续补：

- 真正的 profiler 采样聚合
- 每帧 trace 缓冲与 timeline 导出
- 面向调试 UI 的订阅与筛选接口

### 8. 并行 system 抽象预留

虽然当前明确只做单线程，但设计上要避免把未来并行支持堵死：

- 主线程系统和并行系统不要共用完全相同的执行假设
- 不要让 `SystemScheduler` 默认承担多线程调度职责
- system 数据访问模式要区分读写约束

当前第一版已经落地：

- 保留了 `SystemExecutionKind.ParallelReserved`
- `SystemScheduler` 在注册时显式拒绝 `ParallelReserved` 非组系统
- 错误信息会明确提示当前调度器只支持单线程执行类型

### 9. 配置驱动装配

这属于较后阶段工作。

当前应避免过早引入：

- 资产化系统配置
- 复杂 DI 容器
- 编辑器驱动的 system graph

但可以在 API 设计上为未来装配层预留余量。

---

## 推荐的下一步实现顺序

如果继续只优化 `System` 本身，建议按下面顺序推进：

1. 生命周期补全：`OnEnabled / OnDisabled` 与组级传播规则
2. 调度器状态机收紧：非法调用、结构变更时机、重复初始化 / 销毁语义
3. 可观测接口：导出系统树、执行顺序、节点状态
4. system 元数据：阶段、顺序、调试标签
5. system 分类边界：普通系统 / 热点系统 / 并行系统预留
6. profiler / debug hook

这个顺序的核心原因是：

**先补运行语义，再补观察能力，再补扩展能力。**

---

## 当前明确不做的事

为了避免 system 设计再次失焦，下面这些内容暂时不纳入当前阶段：

- 新版 `Session`
- 回滚 / 重模拟主循环
- 网络同步
- 完整事件系统
- 自动拓扑排序
- 线程化调度器
- 配置资产化装配

这些都重要，但都不应先于 system 内核语义稳定。

---

## 最终结论

参考 FrameSync 后，Lattice 的 System 设计不应复制它的全部复杂度，而应提取其中四个核心思想：

1. System 是一个分层体系，不只是一个接口
2. 生命周期、分组和启停控制要作为正式概念存在
3. 调度器要独立于 Session
4. 并行系统必须和普通系统在抽象层面分离

结合 Lattice 当前阶段，最合理的 System 设计路线是：

**先落一个轻量但正式的 `ISystem + SystemBase + SystemGroup + SystemScheduler` 主线闭环，再逐步向 Session、并行调度和配置驱动扩展。**
