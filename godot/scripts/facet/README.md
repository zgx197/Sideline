# Facet

Facet 是 Sideline 的客户端表现层运行时框架。
它位于 Godot 表现层与 Lattice 逻辑层之间，负责把逻辑世界整理成可观察、可交互、可热更新、可诊断的页面系统。

本文档现在同时承担两件事：

- 作为 Facet 的主设计说明，解释框架定位、边界、术语和协作关系
- 作为 Facet 的精简路线图，说明当前完成度、阶段成果和后续增强方向

更细的阶段历史、验收证据和推进记录仍保留在 [ImplementationRoadmap.md](./ImplementationRoadmap.md)。

## 一句话理解

- `Lattice` 负责“世界如何运转”
- `Facet` 负责“客户端如何观察并操作这个世界”
- `Godot` 负责“这些页面最终如何渲染、输入和挂到节点树上”

## Facet 解决什么问题

Facet 不是：

- 确定性逻辑框架
- ECS 框架
- 网络框架
- 单纯的 Godot 控件封装集合

Facet 是：

- 客户端页面运行时
- Projection 驱动的 UI 组织层
- Lua 页面控制器宿主
- 手工布局、模板布局、自动布局的统一承接层
- 红点、热重载、诊断、工具化等客户端共性能力的沉淀层

## 快速阅读指引

如果你是第一次接触 Facet，建议按这个顺序读：

1. 先看“设计目标”和“总体结构”，建立整体心智模型
2. 再看“页面运行模型”，理解页面是如何被创建、显示、刷新和销毁的
3. 再看“模块分层”，理解 Projection、Layout、Binding、Lua 各自在什么边界内工作
4. 最后看“阶段状态总览”，了解当前代码库已经走到哪里

如果你是准备改代码，建议优先定位：

- 页面调度：`Runtime/Pages/UIManager.cs`
- 页面生命周期：`Runtime/Pages/UIPageRuntime.cs`
- 宿主与服务装配：`Runtime/FacetHost.cs`
- Binding：`UI/UIBindingService.cs`
- Lua API 边界：`Lua/LuaApiBridge.cs`
- 红点树：`Extensions/RedDot/RedDotService.cs`

## 设计目标

Facet 的核心目标有五个：

1. 让页面层不直接依赖底层逻辑对象结构
2. 让页面行为可以逐步迁移到 Lua，并具备后续热更新能力
3. 让手工布局、模板布局、自动布局共享同一套运行时协议
4. 让客户端层从一开始就具备清晰的应用边界和可替换数据源
5. 让红点、诊断、工具化等高频能力沉淀为框架能力，而不是页面私有逻辑

Facet 当前明确不追求：

- 在 Facet 内实现确定性逻辑
- 让 Lua 直接接触底层系统或 CLR 反射能力
- 让页面直接依赖 Godot 节点路径和底层数据对象
- 一开始就支持任意 C# 逻辑热更或任意资源热更

## 总体结构

```text
Lattice / Domain
    -> Application Services / Gateway
    -> Projection Store / ViewModel
    -> Facet Runtime
        -> Layout Providers
        -> Binding System
        -> Lua Controllers
        -> Extensions / Tooling
    -> Godot Scene / Node / Asset / Animation
```

可以把 Facet 理解成一条自上而下的数据与行为链路：

1. 应用层产出可消费的命令、查询和 Projection
2. Facet Runtime 根据页面定义装配页面
3. Layout Provider 决定页面根节点如何构建
4. Binding 把 Projection / 状态 / 命令连接到节点
5. Lua 控制器承接页面行为
6. Extensions 和 Tooling 提供红点、热重载、诊断等通用能力

## 模块分层

### Application

职责：

- 承接页面发起的命令与查询
- 协调应用服务与 Gateway
- 输出统一结果模型

代表对象：

- `ICommandBus`
- `IQueryBus`
- `AppResult`
- `IAppService`
- `IGateway`

### Projection

职责：

- 组织页面展示数据
- 提供局部刷新基础
- 为列表、按钮可用态、显隐态和红点等 UI 行为提供统一数据源

代表对象：

- `ProjectionKey`
- `ProjectionStore`
- `ProjectionChange`
- `ProjectionRefreshCoordinator`

### Runtime

职责：

- 页面打开、返回、缓存和销毁
- 页面状态机与生命周期
- 页面上下文注入
- Lua 控制器宿主接线

代表对象：

- `FacetHost`
- `UIManager`
- `UIRouteService`
- `UIPageRuntime`
- `UIContext`

### Layout

职责：

- 屏蔽页面布局来源差异
- 统一 ExistingNode / Template / Generated 等布局类型
- 输出稳定的根节点与节点注册表

代表对象：

- `IUILayoutProvider`
- `SceneLayoutProvider`
- `TemplateLayoutProvider`
- `GeneratedLayoutProvider`
- `UILayoutResult`

### UI / Binding

职责：

- 节点注册与解析
- Binding 作用域与统一刷新
- 列表、组件、命令、红点显隐等 UI 层基础设施

代表对象：

- `UINodeRegistry`
- `UINodeResolver`
- `UIBindingService`
- `IUIBindingScope`
- `IUIComponentBindingScope`

### Lua

职责：

- 承接页面行为层
- 提供受限桥接 API
- 为热重载和控制器重建提供稳定入口

代表对象：

- `ILuaRuntimeHost`
- `LuaRuntimeHost`
- `ILuaScriptSource`
- `LuaApiBridge`
- `LuaControllerHandle`

### Extensions / Tooling

职责：

- 沉淀客户端共性能力
- 提供诊断、观察和调试工具

当前代表对象：

- `RedDotService`
- `FacetRuntimeDiagnosticsBridge`
- `LuaReloadCoordinator`
- `LuaHotReloadTestService`

## 关键术语

### Page

Facet 中的页面运行单元。
一个 Page 对应一份页面定义、一个布局来源、一个运行时壳子，以及一个控制器实例。

### Page Definition

页面元数据定义，至少描述：

- `pageId`
- `layoutType`
- `layoutPath` 或布局构建入口
- `controllerScript`
- `layer`
- `cachePolicy`

### Page Runtime

页面运行时壳子，负责持有页面根节点、维护页面状态机、驱动生命周期，并协调 C# 页面脚本与 Lua 控制器。

### Projection

面向 UI 的只读展示数据。
Projection 不等于底层逻辑对象，而是“可以直接给页面消费的结构化结果”。

### Binding

节点与数据、节点与命令、节点与组件能力之间的统一连接机制。

### Node Key

Facet 中稳定的节点标识。
它不等于 Godot 路径，但必须能映射到唯一节点。

### Layout Provider

页面布局来源的统一抽象。
它决定页面根节点如何被构建，但不决定页面行为如何执行。

## 页面运行模型

Facet 的页面生命周期与布局来源解耦，并同时兼容 C# 生命周期对象和 Lua 控制器。

统一状态：

- `Created`
- `Initialized`
- `Shown`
- `Hidden`
- `Disposed`

统一语义：

- `Create`：创建页面根节点，构建节点注册表和运行时壳子
- `Initialize`：创建 `UIContext`，注入解析器、Binding 作用域和 Lua 桥接对象
- `Show`：进入显示态并允许交互
- `Refresh`：响应 Projection 变化或外部刷新请求
- `Hide`：离开显示态但保留可复用运行时
- `Dispose`：释放 Binding、控制器和页面资源，进入终态

统一规则：

- `OnInit` 只执行一次
- `OnShow` / `OnHide` 可以重复出现
- `OnRefresh` 只在页面可用态执行
- `OnDispose` 只执行一次

## Lua 边界

Lua 在 Facet 中承担“页面行为层”的职责，但只通过受限桥接对象与宿主交互。

Lua 当前允许访问：

- 页面参数
- 受限节点解析能力
- Binding 注册与刷新能力
- Command / Query 调用
- 页面路由
- 红点查询
- 受限诊断查询

Lua 当前不允许直接访问：

- Lattice 世界对象
- 存档系统内部实现
- 网络底层实现
- CLR 反射能力
- 未经过 Facet 约束的全局服务

这条边界很重要。Facet 的一个核心设计点，不是“让 Lua 能做更多”，而是“让 Lua 在稳定边界里做对的事”。

## 布局策略

Facet 必须同时支持两类布局来源：

- 手工布局
- 模板布局 / 自动生成布局

适用场景大致如下：

- 手工布局：视觉层级复杂、动效要求高、需要精细节点控制
- 模板 / 自动布局：调试页、配置页、高重复列表页、结构稳定但数据变化大的面板

无论布局来源是什么，都必须满足四个统一约束：

- 能产出 `Control` 根节点
- 能输出稳定节点键
- 能被统一 Binding 系统消费
- 能被统一页面控制器驱动

## 当前阶段状态总览

Facet 的阶段 0 到阶段 12 已全部完成。

为了阅读方便，这里不再展开每个阶段的完整历史，而是按“能力层”压缩总结。

### 阶段 0 到阶段 4：基础骨架完成

已完成：

- 宿主入口与服务容器
- 统一日志能力
- Application 边界
- Projection 数据中心
- 页面定义与注册系统

结果：

- 页面已经不再依赖“场景里某个节点是否 visible”这种原始切换方式
- `UIManager.Open(pageId)` 已成为正式页面打开入口

### 阶段 5 到阶段 7：页面 Runtime 完成

已完成：

- 页面生命周期状态机
- 返回栈与缓存策略
- 节点注册与解析
- 页面级、组件级、复杂列表级 Binding

结果：

- 页面刷新已经开始从“手写节点赋值”收敛为“Binding 驱动刷新”
- 页面逻辑和 Godot 原始路径依赖明显下降

### 阶段 8 到阶段 10：Lua 与扩展能力完成

已完成：

- 真实 Lua 控制器宿主
- 页面级热重载协调和往返测试
- 红点树运行时、红点查询与显隐 Binding

结果：

- 页面行为已经可以由 Lua 控制器承接
- 热重载具备真实证据链，而不是只停在“理论支持”
- 红点不再是页面私有逻辑，而是正式扩展能力

### 阶段 11：高阶布局能力完成

已完成：

- `TemplateLayoutProvider`
- `GeneratedLayoutProvider`
- 阶段 11 布局实验室页面入口

结果：

- `Generated` / `Template` 两类布局已经进入统一 `PageDefinition -> Loader -> Runtime` 链路
- 手工布局和高阶布局不再是两套分裂系统

### 阶段 12：工具化与工程化完成

已完成：

- 运行时诊断快照
- 页面注册浏览
- Projection / Lua / 红点摘要查看
- 运行时校验器
- 深度观察器
- 第一版交互式调试工作台

结果：

- Facet 已经从“能跑”进入“能观察、能定位、能验证”的状态

## 当前代码库已经具备

- FacetHost 宿主入口与基础运行时依赖
- Command、Query、AppResult、Gateway 等应用边界骨架
- ProjectionStore、ProjectionRefreshCoordinator、ProjectionUpdater 刷新骨架
- UIPageDefinition、UIPageRegistry、UIPageLoader、UIManager、UIRouteService、UIPageRuntime 等正式页面运行时
- SceneLayoutProvider、UINodeRegistry、UINodeResolver 等布局提供者与节点解析基础设施
- UIBindingService 与页面级、组件级、复杂列表级 Binding 能力
- 基于 MoonSharp 的真实 Lua 控制器宿主与文件脚本源
- 页面级 Lua 生命周期、状态袋、参数读取与受限诊断查询接口
- LuaReloadCoordinator、脚本版本检测、控制器重建与页面级热重载轮询骨架
- RedDotService、RedDotAggregator 与基于 Projection 的红点 Provider
- 运行时诊断快照桥、校验器、深度观察器与 Facet 主面板工具页

## 当前仍未具备

下面这些不是 Facet 当前主链路的阻塞项，但仍属于后续可继续增强的方向：

- 页面定义外部文件化
- 更高级的 Lua 热重载恢复策略验证
- 更细粒度的权限、引导等扩展系统正式接入
- 更完整的页面定义离线校验器
- Projection / 红点树更强的可视化交互调试工具

## 实现顺序原则

Facet 的推进顺序必须坚持：

1. 先宿主与基础依赖
2. 再应用边界
3. 再 Projection
4. 再 Runtime
5. 再布局与 Binding
6. 再 Lua 宿主与热更新
7. 最后扩展系统与工具化

不要跳过 Application 和 Projection，直接在页面里堆业务逻辑。
不要先做表层 API，再回头补生命周期和运行时约束。

## 验证方式建议

建议每个阶段都保留最小验证样例，而不是一开始就接入复杂业务页面。

当前最重要的验证点如下：

- 启动主场景后能看到 `FacetHost 启动验证成功`
- 页面通过 `pageId` 打开，而不是节点显隐硬切换
- 生命周期 `Create / Initialize / Show / Refresh / Hide / Dispose` 日志完整
- Lua 控制器已创建，并能看到对应生命周期与 Binding 日志
- 脚本改动后页面可在不重启客户端的前提下恢复
- 红点路径、聚合与页面挂载关系稳定
- `Generated` / `Template` 两类布局可以被注册、加载和建树
- 运行时诊断页能看到页面注册、活动运行时、Projection、Lua 与红点摘要

## 当前结论

截至 2026-03-23，Facet 已完成阶段 0 至阶段 12 的主链路建设。

现在的 Facet 已经具备：

- 清晰的应用边界
- 正式页面运行时
- 多布局来源统一承接
- Binding 驱动刷新
- 真实 Lua 页面控制器宿主
- 页面级热重载协调
- 红点树扩展能力
- 第一版诊断、校验和调试工具

如果后续继续推进，重点不再是“补齐基本运行能力”，而是进一步提升：

- 配置化程度
- 可视化工具深度
- 离线校验能力
- 更复杂页面状态的恢复和调试体验
