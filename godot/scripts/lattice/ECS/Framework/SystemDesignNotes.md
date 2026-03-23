# Lattice System 设计备忘

## 目的

本文档用于记录当前 Lattice ECS 在 `System` 层的真实落地状态，以及后续实现 `System` 运行时框架时的设计参考。

重点回答三个问题：

1. 当前仓库里到底有没有真正的 `System` 框架
2. 未来 `System` 层最小可用闭环应该怎么补
3. `Query` 与 `OwningGroup` 在系统实现里分别适合什么场景

---

## 当前状态

### 已实现

当前已经具备以下 ECS 基座能力：

- `Frame`
  - 负责实体与组件存储管理
  - 提供 `Query<T...>()`
  - 提供 `RegisterOwningGroup<T...>()`
- `Storage<T>`
  - 作为通用组件存储
- `Query<T...>`
  - 作为默认的多组件查询入口
- `OwningGroup<T...>`
  - 作为显式 opt-in 的热点组件打包迭代入口
- `FullOwningGroup<T...>`
  - 作为更底层的连续打包存储能力
- `CommandBuffer`
  - 作为框架层命令缓冲工具
- `CullingSystem`
  - 名称上带 `System`，但本质是裁剪位图工具，不是通用系统调度框架

### 未实现

当前仓库里还没有真正成型的 `System` 运行时层，至少以下抽象尚未正式落地：

- `ISystem`
- `SystemBase`
- `SystemGroup`
- `SystemScheduler`
- 固定帧顺序调度
- 系统依赖声明与排序
- 主线程 / 并行线程分阶段执行边界
- 游戏层真实的 `MovementSystem` / `CombatSystem` / `AISystem`

### 结论

当前 Lattice 已经具备了较强的 `Component / Storage / Query` 基座，但还不能算“System 框架已完成”。

更准确地说，现在的状态是：

- 已完成“数据层”和“迭代层”
- 尚未完成“逻辑运行层”和“系统调度层”

---

## 对“最热系统入口”的定义

此前讨论中的“最热系统入口”，不是指仓库里已经存在的某个 `System` 类，而是指后续真正开始实现 `System` 层后，那些**每帧都要执行、遍历规模大、CPU 占比高**的系统主更新入口。

典型形式如下：

```csharp
public interface ISystem
{
    void Update(Frame frame);
}
```

或者：

```csharp
public interface ISystem
{
    void OnUpdate(Frame frame, FP deltaTime);
}
```

“系统入口”通常就是这些函数内部最核心的遍历段，例如：

```csharp
var query = frame.Query<Position, Velocity>();
```

或者：

```csharp
var group = frame.GetOwningGroup<Position, Velocity>();
```

---

## 什么样的系统算“热点系统”

满足越多，就越值得视为热点：

- 每帧都执行
- 遍历实体数量大
- 组件组合稳定且固定
- 系统逻辑简单，时间主要花在扫数据上
- 对缓存命中率和顺序迭代吞吐量敏感

### 典型热点候选

未来在游戏逻辑中，通常以下系统最有可能成为热点：

- `MovementSystem`
  - 常见组件组合：`Position + Velocity`
- `PhysicsIntegrateSystem`
  - 常见组件组合：`Transform + Velocity (+ Acceleration)`
- `ProjectileUpdateSystem`
  - 子弹数量大，通常每帧更新
- `LifetimeSystem`
  - 常见组件组合：`Lifetime + Active`
- `AnimationAdvanceSystem`
  - 仅当实体数足够大时才可能成为明显热点

### 非热点或不适合 OwningGroup 的系统

以下系统一般不建议优先做专用热点路径：

- 技能释放系统
- 剧情 / 任务系统
- UI 状态同步
- 低频 AI 决策
- 强依赖复杂条件分支的系统
- 组件组合经常变化的系统

这类系统通常更适合继续使用 `Query`。

---

## Query 与 OwningGroup 的职责边界

### Query 的定位

`Query` 应该继续作为 **默认系统入口**。

适用场景：

- 普通系统
- 组件组合较稳定，但不是极限热点
- 需要较高可读性与可维护性
- 开发初期快速落地系统逻辑
- 系统逻辑复杂，时间不主要耗在迭代本身

建议原则：

- 新系统默认先用 `Query`
- 只有在性能证据明确时，再考虑迁移到 `OwningGroup`

### OwningGroup 的定位

`OwningGroup` 应该作为 **显式 opt-in 的热点路径**，而不是默认方案。

适用场景：

- 少量固定热点系统
- 组件组合非常稳定
- 数据量足够大
- 系统执行频率高
- Benchmark 已证明确实值得切换

建议原则：

- 不要把所有系统都改成 `OwningGroup`
- 只挑最重的 1 到 3 个系统先收敛
- 让 `Query` 保持通用默认入口

---

## 为什么不能把所有 Query 都自动变成 OwningGroup

虽然 `OwningGroup` 热路径更快，但它不应该替代所有通用查询，原因如下：

1. `Query` 是通用的
   - 不要求额外注册
   - 适合大多数系统

2. `OwningGroup` 是专用的
   - 需要显式注册与维护
   - 更适合少量固定热点组合

3. 并不是所有系统都值得承担额外维护成本
   - 很多系统并不在迭代热路径上

4. 数据布局与同步约束更强
   - `OwningGroup` 的收益来自更激进的数据组织方式
   - 也意味着更适合固定、高频、可预期的系统

因此应坚持以下策略：

- `Query` 负责广覆盖
- `OwningGroup` 负责极少数高收益系统

---

## System 层最小可用闭环建议

后续真正实现 `System` 层时，建议按最小闭环推进，而不是一开始就做完整调度器。

### 第一步：定义最小接口

建议先实现：

- `ISystem`
- `SystemScheduler`

例如：

```csharp
public interface ISystem
{
    void Update(Frame frame);
}
```

```csharp
public sealed class SystemScheduler
{
    public void Add(ISystem system);
    public void Update(Frame frame);
}
```

### 第二步：先做单线程固定顺序执行

先不要立刻做：

- 并行调度
- 依赖图自动排序
- Job 化系统拆分

先保证：

- 执行顺序稳定
- 行为确定
- 易于调试

### 第三步：补 1 到 2 个真实样例系统

建议最先补：

- `MovementSystem`
- `LifetimeSystem` 或 `DecaySystem`

原因：

- 逻辑简单
- 便于验证系统调度闭环
- 同时可以验证 `Query` 默认路径是否顺手

### 第四步：有基准后再选择热点迁移

只有当真实系统跑起来并出现明显热路径后，再考虑：

- 哪些系统继续保留 `Query`
- 哪些系统改用 `OwningGroup`

不建议在 `System` 层尚未完成前，就过早把所有入口都设计成热点专用接口。

---

## 后续迁移策略建议

当 `System` 层开始落地后，建议采用如下准则：

### 默认策略

- 绝大多数系统使用 `frame.Query<T...>()`

### 热点策略

- 只有极少数稳定热点系统，才使用：
  - `frame.RegisterOwningGroup<T...>()`
  - `frame.GetOwningGroup<T...>()`
  - `NextBlock(...)` 之类的块迭代方式

### 推荐迁移顺序

1. 先实现真实 `ISystem` / `SystemScheduler`
2. 先让系统都能用 `Query` 跑起来
3. 用 benchmark 或 profile 确认热点系统
4. 仅将最热的 1 到 3 个系统切到 `OwningGroup`

---

## 当前阶段的工程判断

基于当前代码现状，可以得出以下工程结论：

- `Component` 基座已经达到较高完成度
- `Query` 已足以作为未来大多数系统的默认实现基础
- `OwningGroup` 已具备作为热点专用路径的工程价值
- 真正缺的不是更多查询花样，而是 `System` 运行层本身

因此后续优先级应为：

1. 补齐最小可用 `System` 层
2. 用真实系统验证 `Query` 默认路径
3. 再针对少量热点系统引入 `OwningGroup`

---

## 一句话原则

未来的 Lattice System 设计应遵循：

**Query 作为默认系统入口，OwningGroup 作为少数热点系统的显式优化入口。**
