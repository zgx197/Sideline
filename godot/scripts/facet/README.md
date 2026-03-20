# Facet

Facet 是 Sideline 的客户端表现层运行时框架。
它位于 Godot 表现层与 Lattice 逻辑层之间，负责把逻辑世界整理为可观察、可交互、可热更新、可扩展的客户端页面系统。

本文档是 Facet 的主设计说明，负责定义：

- Facet 的定位与边界
- 统一术语
- 生命周期规范
- 模块职责
- Lua 宿主约束
- 页面定义、布局、Projection、Binding 的协作关系

实现阶段和推进状态请查看 [ImplementationRoadmap.md](./ImplementationRoadmap.md)。

## 1. 框架定位

Facet 不是：

- 确定性逻辑框架
- ECS 框架
- 网络框架
- 只服务于静态 UI 的控件工具集

Facet 是：

- 客户端页面运行时
- Projection 驱动的 UI 组织层
- Lua 页面控制器宿主
- 手工布局与自动布局之间的统一承接层
- 红点、诊断、热更新等客户端通用能力的沉淀层

## 2. 与其他系统的关系

### 2.1 与 Lattice 的关系

- `Lattice` 负责确定性、帧同步、ECS、规则推进与逻辑状态
- `Facet` 负责把逻辑结果投影为客户端可消费的页面、组件和交互流程

简单来说：

- `Lattice` 负责“世界如何运转”
- `Facet` 负责“客户端如何观察并操作这个世界”

Facet 不直接侵入 Lattice 内部结构，页面层只依赖 Projection、Command、Query 和应用层边界。

### 2.2 与 Godot 的关系

Godot 是 Facet 的宿主，不是 Facet 的上层业务框架。
Godot 负责：

- 节点树与场景资源
- 渲染、动画、输入、窗口
- 编辑器与资源管线

Facet 在 Godot 之上建立统一的页面运行时规范，屏蔽“页面如何被创建、如何被驱动、如何被刷新”的差异。

### 2.3 与 Lua 的关系

Lua 在 Facet 中承担“页面行为层”的职责。
Lua 控制器只处理页面行为，不直接触底层系统。

Lua 可以接触：

- 页面参数
- 受限节点解析能力
- Binding 注册与刷新能力
- Command / Query 调用
- 页面路由
- 红点接口占位

Lua 不能直接接触：

- Lattice 世界对象
- 存档系统内部实现
- 网络底层实现
- 任意 CLR 反射能力
- 未经过 Facet 约束的全局服务

### 2.4 与应用层边界的关系

Facet 页面不直接区分“本地模式”和“联网模式”。
页面只面向：

- `ICommandBus`
- `IQueryBus`
- `AppResult`
- `ProjectionStore`

底层具体是本地实现、模拟实现还是远程实现，都应隐藏在应用层和 Gateway 后面。

## 3. 设计目标

Facet 的核心目标有五个：

1. 让页面层不直接依赖底层逻辑对象结构
2. 让页面行为可以逐步迁移到 Lua，并具备后续热更新能力
3. 让手工布局、模板布局、自动布局共享同一套运行时协议
4. 让客户端层从一开始就具备清晰的应用边界和可替换数据源
5. 让红点、诊断、工具化等高频能力沉淀为框架能力，而不是页面私有逻辑

## 4. 非目标

Facet 当前阶段不追求：

- 在 Facet 内实现确定性逻辑
- 用 Godot 节点脚本承担复杂页面编排逻辑
- 让 Lua 直接访问底层系统
- 一开始就支持任意 C# 逻辑热更
- 一开始就支持任意 Godot 资源热更

## 5. 核心关键词

- Projection-Driven
- Lifecycle-First
- Layout-Agnostic
- Hot-Swappable
- Service-Bounded

## 6. 统一术语

### 6.1 Page

Facet 中的页面运行单元。
一个 Page 对应一份页面定义、一个布局来源、一个运行时壳子，以及一个控制器实例。

### 6.2 Page Definition

页面元数据定义，至少描述：

- `pageId`
- `layoutType`
- `layoutPath` 或布局构建入口
- `controllerScript`
- `layer`
- `cachePolicy`

### 6.3 Page Runtime

页面运行时壳子，负责：

- 持有页面根节点
- 持有页面状态机
- 维护 `UIContext`
- 调用页面生命周期
- 协调 C# 页面脚本与 Lua 控制器

### 6.4 Projection

面向 UI 的只读展示数据。
Projection 不等于底层逻辑对象，而是“给页面直接消费的结构化结果”。

### 6.5 Binding

节点与数据、节点与命令、节点与组件能力之间的统一连接机制。

### 6.6 Node Key

Facet 中稳定的节点标识。
它不直接等于 Godot 路径，但必须能映射到唯一可定位的节点。

### 6.7 Layout Provider

页面布局来源的统一抽象。
它决定页面根节点如何被构建，但不决定页面行为如何执行。

### 6.8 Extension

沉淀在框架层的通用能力单元，例如：

- 红点树
- 权限控制
- 引导锚点
- 诊断面板

## 7. 总体架构

```text
Lattice / Domain
    -> Application Services / Gateway
    -> Projection Store / ViewModel
    -> Facet Runtime
        -> Layout Providers
        -> Lua Controllers
        -> Binding System
        -> Extensions
    -> Godot Scene / Node / Asset / Animation
```

## 8. 模块划分

### 8.1 Application

职责：

- 承接页面发起的命令与查询
- 协调应用服务与 Gateway
- 输出统一结果模型

典型对象：

- `ICommandBus`
- `IQueryBus`
- `AppResult`
- `IAppService`
- `IGateway`

### 8.2 Projection

职责：

- 组织页面展示数据
- 提供局部刷新基础
- 为红点、列表、按钮状态等 UI 行为提供统一数据源

典型对象：

- `ProjectionKey`
- `ProjectionStore`
- `ProjectionChange`
- `IProjectionUpdater`

### 8.3 Runtime

职责：

- 页面打开、隐藏、关闭、返回栈
- 页面状态机与生命周期
- 页面上下文注入
- Lua 控制器宿主接线

典型对象：

- `UIManager`
- `UIRouteService`
- `UIPageRuntime`
- `UIContext`
- `FacetHost`

### 8.4 Layout

职责：

- 屏蔽页面布局来源差异
- 统一 ExistingNode / PackedScene / 模板化布局 / 自动生成布局
- 输出稳定的根节点与节点注册表

典型对象：

- `IUILayoutProvider`
- `SceneLayoutProvider`
- `TemplateLayoutProvider`
- `GeneratedLayoutProvider`
- `UILayoutResult`

### 8.5 UI

职责：

- 节点注册与解析
- Binding 作用域与刷新
- 列表、组件、命令绑定等 UI 层基础设施

典型对象：

- `UINodeRegistry`
- `UINodeResolver`
- `UIBindingService`
- `IUIBindingScope`
- `IUIComponentBindingScope`

### 8.6 Lua

职责：

- 承接页面行为宿主
- 提供受限桥接 API
- 作为后续热更新和控制器重建的基础入口

典型对象：

- `ILuaRuntimeHost`
- `LuaRuntimeHost`
- `ILuaScriptSource`
- `LuaApiBridge`
- `LuaControllerHandle`

### 8.7 Extensions

职责：

- 沉淀高频客户端通用能力
- 避免红点、提示、权限等逻辑散落在页面内部

首批规划：

- 红点树
- 引导锚点
- 页面诊断
- 权限与可见性规则

## 9. 页面定义文件格式的作用

Facet 必须有统一的页面定义层。
页面定义的作用不是承载页面逻辑，而是把“一个页面是什么、如何被构建、如何被运行时管理”描述清楚。

如果没有这层定义，就会出现：

1. `UIManager` 不知道一个页面该如何被打开
2. 布局来源、控制器来源和缓存策略散落在代码中
3. 热更新时无法确定一个页面到底由哪些资源和脚本组成
4. 工具化无法统一读取页面注册信息

当前阶段我们先用 C# 对象表达页面定义，后续可替换为 JSON、资源文件或其他外部格式。

## 10. 页面统一生命周期

Facet 的页面生命周期必须与布局来源解耦，并同时适配 C# 页面脚本与 Lua 控制器。

### 10.1 页面状态

统一使用：

- `Created`
- `Initialized`
- `Shown`
- `Hidden`
- `Disposed`

### 10.2 生命周期语义

#### Create

- 创建页面根节点
- 构建节点注册表与解析器
- 创建页面运行时壳子

#### Initialize

- 创建 `UIContext`
- 注入节点解析器、Binding 作用域和 Lua 桥接对象
- 初始化页面脚本和控制器

#### Show

- 让页面进入显示态
- 允许交互
- 调用 `OnShow`

#### Refresh

- 更新页面参数
- 响应 Projection 变化或外部刷新请求
- 调用 `OnRefresh`

#### Hide

- 暂时离开显示态
- 停止页面级订阅或交互
- 调用 `OnHide`

#### Dispose

- 释放运行时资源
- 释放 Binding 与控制器
- 进入不可恢复终态

### 10.3 统一规则

- `OnInit` 只执行一次
- `OnShow` 与 `OnHide` 可成对重复出现
- `OnRefresh` 只在页面可用态中执行
- `OnDispose` 只执行一次，之后页面不可再用

## 11. Lua 控制器统一生命周期

Lua 控制器生命周期与页面生命周期严格对齐：

- `OnInit`
- `OnShow`
- `OnRefresh`
- `OnHide`
- `OnDispose`

约束如下：

1. Lua 控制器不自行维护页面状态机
2. Lua 控制器不决定页面是否缓存或销毁
3. Lua 控制器只通过 `UIContext` / `LuaApiBridge` 与宿主交互
4. Lua 控制器内部状态必须能由页面参数、Projection 或运行时缓存恢复

## 12. 阶段 8 当前落地

当前代码库已经完成了阶段 8 的最小闭环，而不是只停留在设计层：

- `LuaRuntimeHost`
  负责按页面定义中的 `controllerScript` 创建控制器实例
- `InMemoryLuaScriptSource`
  用内存注册表模拟 Lua 脚本来源，先把宿主边界和生命周期时序稳定下来
- `LuaApiBridge`
  当前只暴露节点解析、Binding 刷新、命令/查询、页面路由和红点占位接口
- `UIPageRuntime`
  已统一转发 `OnInit / OnShow / OnRefresh / OnHide / OnDispose` 到 Lua 控制器
- `FacetBuiltInPageDefinitions`
  已给 `client.idle` 与 `client.dungeon` 配置内置 `controllerScript`
- `FacetBuiltInLuaControllers`
  已提供两个内置控制器样例，用于验证节点访问、查询调用、路由状态和生命周期日志

这意味着 Facet 现在已经具备“Lua 页面控制器宿主入口”，后续阶段 9 主要是在这条链路上补真实 Lua VM、热重载协调和恢复策略。

## 13. 混合布局策略

Facet 必须同时支持两类布局来源：

- 手工布局
- 自动生成或模板化布局

### 13.1 手工布局

适合：

- 视觉层级复杂
- 动效要求高
- 需要精细控制节点结构

### 13.2 自动生成或模板布局

适合：

- 调试页
- 配置页
- 高重复列表页
- 结构稳定但数据变化大的面板

### 13.3 统一约束

无论布局来源是什么，都必须满足：

- 能产出 `Control` 根节点
- 能输出稳定的节点标识
- 能被统一 Binding 系统消费
- 能被统一页面控制器驱动

## 14. Projection 规范

Projection 是 Facet 的页面数据中心。
页面不直接依赖底层对象，而是消费 Projection。

原则：

1. Projection 面向 UI，而不是复用底层逻辑对象
2. 页面刷新优先由 Projection 变化驱动
3. 列表、按钮可用态、显隐态、状态摘要等统一从 Projection 计算

## 15. Binding 规范

Facet 的 Binding 是页面逻辑收敛的关键。

当前最小落地能力包括：

- `TextBinding`
- `VisibilityBinding`
- `InteractableBinding`
- `CommandBinding`
- `ListBinding`
- `ComplexListBinding`

扩展方向包括：

- 组件级 Binding 复用
- 更复杂的模板列表 Binding
- 与红点、权限、引导能力的统一挂接

## 16. Lua API 暴露原则

Lua 只应看到受限桥接对象，而不应直接获取整个宿主世界。

当前桥接能力包含：

- 节点解析
- Binding 刷新
- Command / Query
- 页面路由
- 红点占位接口
- 统一日志入口

后续即便接入真实 Lua VM，也必须沿着这条受限边界继续演进。

## 17. 日志与诊断

Facet 需要同时支持：

- 结构化日志
- 纯文本镜像日志
- 页面生命周期日志
- Binding 诊断日志
- Lua 宿主与控制器日志

目标不是只“看到文本”，而是让后续过滤、诊断、问题定位和工具面板接入都有稳定的数据基础。

## 18. 收尾结论

Facet 应被视为 Sideline 的客户端表现层基础设施。
它的核心价值在于：

1. 用 Projection 隔离逻辑世界与表现世界
2. 用 Runtime 统一页面生命周期和布局来源
3. 用 Lua 宿主承接后续可热更新的页面行为层
4. 用应用边界屏蔽本地实现与远程实现差异
5. 用扩展系统沉淀高频客户端能力

只要后续实现继续围绕这五点展开，Facet 就会保持足够稳定，也足够可演进。