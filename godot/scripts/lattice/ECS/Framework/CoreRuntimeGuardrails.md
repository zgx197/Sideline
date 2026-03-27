# Lattice Core Runtime 架构护栏

## 目的

本文档用于约束后续 `core-runtime` 演进，避免在“底座已经足够好用”之后进入无边界扩张。

这不是宣传文档，而是一份偏保守、偏否定式的工程参考：

- Lattice 现在最危险的短板是什么
- 哪些方向可以补
- 哪些方向千万不要直接学 FrameSync
- 什么样的东西才允许进入 `core-runtime`

目标非常明确：

**让 Lattice 在支撑中大型游戏开发时，尽量通过上层扩展解决问题，而不是频繁回改底层设计。**

---

## 当前判断

截至当前主干，Lattice 已经不是“只有一点 ECS 雏形”的状态。

它已经具备：

- 确定性 `Math / FP`
- `Entity / Component / Frame / Storage / Query`
- `CommandBuffer` 与延迟结构修改
- `ISystem / SystemScheduler / SystemAuthoringContract`
- `SessionRuntime / MinimalPredictionSession / LocalAuthoritativeSession`
- 输入、历史帧、checkpoint、rollback、resimulate 最小闭环
- 兼容面治理、determinism analyzer、benchmark governance

但必须保持清醒：

**Lattice 当前更像“高质量的 deterministic runtime kernel”，而不是“完整产品级帧同步框架”。**

对中大型项目来说，真正的风险已经不再是“它能不能跑”，而是：

**团队会不会因为它现在已经足够顺手，就开始把越来越多不该属于底层的复杂度塞回底层。**

---

## 五个最危险的短板

### 1. 边界定义已经很成熟，但真实生产闭环还不够厚

现在 `SessionRuntime`、`SystemScheduler`、`SystemAuthoringContract` 的边界表达已经很强，正式支持与明确不支持的能力也开始代码化。

这很好，但它不等于：

- 存档工作流已经完全闭环
- 回放工具链已经完全闭环
- 多入口装配已经完全闭环
- 玩法项目接入后的长期演化已经闭环

危险在于团队容易把“边界写清楚了”误判成“底层未来不再需要补关键能力”。

**护栏：**

- 任何“底层已稳定”的判断，必须至少同时满足：
  - 真实玩法链路已接入
  - 持久化链路已接入
  - 调试观测链路已接入
  - 至少一轮多人协作开发已验证

### 2. 系统层过于轻量，复杂度会倒灌到业务代码

`ISystem + SystemScheduler + Contract` 的最小模型是对的，但它天然会把很多“组织复杂度”推迟到上层。

中大型项目里，最常见的后果不是框架报错，而是业务代码开始出现：

- 隐式顺序依赖
- 伪分组
- 伪阶段
- 伪只读系统
- 通过命名和注释维持的系统约束

如果这些约束不被上层正式化，复杂度最终会绕过框架设计，长在业务层阴影里。

**护栏：**

- `core-runtime` 继续保持轻量
- 但必须在 `core-runtime` 之外补一层项目级 system assembly / system policy 组织层
- 不允许把“业务顺序约定”长期停留在口头约定和注释里

### 3. 缺少正式 authoring / 装配层，项目越大越容易出现多套运行时事实

当前主干更偏代码侧装配，`SessionRunnerBuilder` 很适合程序员，但还不够像“项目基础设施”。

中大型项目真正危险的不是“不方便”，而是：

- A 场景一套系统装配
- B 场景一套系统装配
- 调试入口一套装配
- 测试入口再来一套装配

最后所有入口都能跑，但它们并不是同一个正式 runtime 事实。

**护栏：**

- 必须补项目级正式装配层
- 但装配层应放在 `core-runtime` 之外
- `core-runtime` 只提供稳定装配原语，不负责承载整个项目的内容编排

### 4. 调试与观测能力仍不足，后续会成为主程第一瓶颈

当前已经有测试、benchmark 和治理策略，这是非常好的起点。

但中大型项目真正会卡住主程的，往往是这些问题：

- 某次 rollback 为什么发生
- 某个实体在第几 tick 被谁删掉
- 哪个系统引入了结构修改
- 历史重建为何和实时帧不一致
- 哪类 checkpoint 成本突然上升

如果没有足够好的运行时 trace、状态观测、差异定位能力，团队后续会不断诉诸“进底层加更多特判”，这是很危险的。

**护栏：**

- 优先补 runtime diagnostics，而不是优先补更多 runtime feature
- 任何新进入 `core-runtime` 的复杂能力，都必须同时回答“以后出了问题怎么查”

### 5. 全局静态注册链路仍偏脆，说明初始化治理还未完全封顶

当前 `ComponentRegistry / ComponentTypeId<T> / ComponentCommandRegistry` 已经能工作，但它仍然带有明显的全局静态注册特征。

这类机制在中大型项目中最容易引发：

- 测试顺序依赖
- 模块接入顺序依赖
- 初始化时机不一致
- 工具态与运行态表现不同
- 某些链路忘记同步注册

最近 CI 里暴露的组件命令注册问题，本质上就是这类风险的真实信号，而不是偶发小 bug。

**护栏：**

- 继续收口到统一注册入口
- 任何新增注册链路都必须能被统一自检
- 必须逐步补齐 boot-time validation / registration sanity check

---

## 千万不要学 FrameSync 去补的方向

FrameSync 很强，但它强的不只是 simulation kernel，还包含大量平台集成、编辑器工具、资源工作流和历史演进包袱。

Lattice 可以学习它的经验，但不能照抄它的膨胀路径。

### 1. 不要把系统族越长越多

不要为了“让业务写起来更顺手”，把 `core-runtime` 逐步扩成：

- `SystemMainThread`
- `SystemMainThreadFilter<T>`
- `SystemThreadedFilter<T>`
- `SystemArrayFilter<T>`
- `SystemArrayComponent<T>`
- 更多语义相近但心智成本更高的系统基类族

这类 API 一旦进入主干，很难再删，最终只会扩大兼容面和学习成本。

**原则：**

- `core-runtime` 只保留最小稳定 system contract
- 高层便捷基类可以做，但应优先放在更外层

### 2. 不要把 `Frame` 做成万能神对象

FrameSync 的 `Frame` 很强，但也很重。

Lattice 不能因为“以后可能会用到”，就不断往 `Frame` 塞：

- 玩家管理
- 资源入口
- 地图上下文
- 事件总线
- 编辑器元信息
- 引擎桥接态

**原则：**

- `Frame` 是单帧状态容器，不是平台入口总线
- 超出“状态 + 确定性操作”边界的东西，默认不进 `Frame`

### 3. 不要把配置驱动直接做成反射味很重的大总入口

FrameSync 的 `SystemsConfig / SystemSetup / CommandSetup` 是成熟产品化后的形态，但对当前 Lattice 来说，直接复制会带来过早复杂化。

**不要做成：**

- 一个超级资源入口
- 一个超级反射发现器
- 一个超级构造约定中心
- 一个兼容层、编辑器、运行时三合一入口

**原则：**

- 可以补 authoring / assembly 层
- 但它应是薄装配层，不应吞掉 `core-runtime` 的边界

### 4. 不要把 runner 入口演化成超大参数总线

不要把所有平台、网络、回放、资源、调试、热启动、房间态参数都逐步塞进 `SessionRunner` 或 `SessionRunnerBuilder`。

这会让 runtime host 层和 runtime kernel 层重新耦合在一起。

**原则：**

- `SessionRuntime` 负责模拟运行
- `SessionRunner` 负责最小宿主驱动
- 更重的环境装配，优先放在更外层 integration / host 层

### 5. 不要把平台能力误当成底层能力

FrameSync 的很多强能力，本质上是：

- Unity 编辑器能力
- Inspector / Asset / View 工作流
- Runtime Debugger / FrameDiffer / Graph Profiler
- 网络与会话平台集成

这些不是 simulation kernel 本身就必须承担的职责。

**原则：**

- Lattice 要补外围能力
- 但应该“核心更小，外围更厚”
- 绝不为了方便平台接入而污染 `core-runtime`

---

## `core-runtime` 准入规则

后续任何新能力，在进入 `core-runtime` 前，至少要通过下面这组问题。

只要有两到三个问题回答不稳，就默认不进入 `core-runtime`。

### 1. 这是不是稳定的 runtime kernel 职责

如果它更像：

- 编辑器工具
- 内容 authoring
- 项目装配
- 平台桥接
- 调试面板
- 玩法特化策略

那么默认不应进入 `core-runtime`。

### 2. 它是否引入新的长期兼容面

如果加进去以后：

- 很难删除
- 很难重命名
- 很难换语义
- 会被外部代码广泛依赖

那必须先把正式边界说清楚，再决定是否进入。

### 3. 它是否会扩大 `Frame` / `SessionRuntime` / `System` 的职责

如果一个新能力的代价是让这几个核心类型继续膨胀，那么默认应该先寻找外置方案。

### 4. 它是否真的被多个场景复用

只有在多个玩法、多个入口、多个模式下都会稳定复用的能力，才有资格进入 `core-runtime`。

“某个项目现在正好需要”不构成充分理由。

### 5. 它是否可以被测试和诊断

任何进入 `core-runtime` 的能力，都必须回答：

- 如何验证 determinism
- 如何验证兼容面
- 如何验证性能退化
- 出问题后如何定位

如果回答不了，说明还不适合进核心层。

---

## 接下来六个月必须补的方向

### 必须补

- 统一初始化自检与注册链路校验
- 更正式的 runtime diagnostics / trace / state diff 能力
- 项目级 system assembly / runtime assembly 层
- 更贴近真实玩法的长期负载验证
- 更明确的持久化 / replay / checkpoint 使用边界

### 必须忍住不补进 `core-runtime`

- 更多系统基类家族
- 更重的引擎桥接语义
- 内容 authoring 资源体系
- 大而全的 runner 参数模型
- 玩法特化工具和便利 API

---

## 一句话红线

**Lattice 当前最需要防守的，不是技术落后，而是成功之后的过度扩张。**

后续开发时，如果某个需求“看起来放进底层最省事”，请先默认它很可能不该进入 `core-runtime`。

正确的方向应该是：

- 核心边界更稳
- 装配层更清楚
- 工具层更强
- 项目层更自由

而不是让底层重新变成一个无限长大的总框架。
