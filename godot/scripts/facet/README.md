# Facet

Facet 是 Sideline 的客户端表现层运行时框架。

它位于 Godot 表现层与 Lattice 逻辑层之间，负责把逻辑世界投影为可交互、可热更新、可扩展的客户端界面体验。Facet 不是一个零散的 UI 工具集，而是一套面向 Sideline 客户端的表现层基础设施。

## 文档定位

本文档是 `Facet` 当前唯一的详细设计说明。

Facet 的架构定位、术语定义、生命周期约束、模块边界、热更新策略、混合布局约定和后续实现方向，均以本文档为准。`facet/` 子目录不再保留分散的 `README.md` 或平行设计文档。

## 框架定位

Facet 的职责是承接客户端表现层运行时，而不是承接底层逻辑规则。

Facet 以 Godot 作为渲染与资源宿主，以 Lua 作为页面行为热更新层，以 Projection / Command / Query 作为 UI 与业务之间的交互协议，并以可替换的数据源边界为未来联网能力预留扩展空间。

Facet 不是：

1. 确定性逻辑框架
2. ECS 框架
3. 网络框架
4. 仅服务于静态页面的 UI 工具库

Facet 是：

1. 客户端页面运行时
2. UI 行为热更新宿主
3. 页面布局来源统一抽象层
4. 表现层通用能力沉淀平台

## 与其他系统的关系

### 与 Lattice 的关系

- `Lattice`
  负责确定性逻辑、规则推进、同步友好型数据结构与会话逻辑
- `Facet`
  负责把逻辑世界投影为客户端界面与交互体验

两者关系不是包含与被包含，而是逻辑世界与观测切面的关系。

`Lattice` 负责“世界如何运转”，`Facet` 负责“客户端如何观察并操作这个世界”。

### 与 Godot 的关系

Godot 是 Facet 的表现层宿主，负责：

- 节点树与场景系统
- 资源加载与管理
- 动画与过渡表现
- 输入接收
- 渲染与窗口管理

Facet 不替代 Godot，而是在 Godot 之上建立稳定的页面运行时与组织约束。

### 与 Lua 的关系

Lua 在 Facet 中承担“页面行为热更新层”的职责。

Lua 负责：

- 页面生命周期逻辑
- 绑定注册
- 页面局部交互流程
- 命令发起
- 页面侧扩展声明

Lua 不负责：

- 直接操作 Lattice 世界对象
- 直接网络访问
- 存档读写
- 鉴权与会话管理
- 绕过应用层直接调底层服务

### 与应用服务层的关系

Facet 不把网络交互直接暴露给页面，而是先抽象出“应用服务层”。

页面只接触：

- `Command`
- `Query`
- `AppResult`
- `Projection`

底层可以是：

- 本地逻辑实现
- 本地存档实现
- 本地模拟网关
- 未来远程服务器实现

因此，Facet 是离线优先、联网兼容的客户端表现层运行时。

## 设计目标

1. 让 UI 不直接依赖 Lattice 内部状态结构
2. 让页面行为可以通过 Lua 热更新
3. 让手工布局、自动生成、模板混合共享同一套运行时协议
4. 让客户端在纯本地阶段就建立稳定边界，为未来联网预留演进空间
5. 让红点、权限、引导、提示等高频通用能力沉淀为框架能力

## 非目标

1. 不在 Facet 内实现确定性逻辑
2. 不让 Godot 控件脚本承担页面级业务编排职责
3. 不让 Lua 直接越过应用层访问底层系统
4. 不在第一阶段追求整个客户端任意代码热更新
5. 不在第一阶段追求任意 Godot 资源热更

## 核心关键词

- `Projection-Driven`
- `Hot-Swappable`
- `Layout-Agnostic`
- `Service-Ready`

## 总体结构

```text
Lattice / Domain
    ↓
Application Services / Gateway
    ↓
Projection Store / ViewModel
    ↓
Facet Runtime
    ├─ Layout Providers
    ├─ Lua Controllers
    ├─ UI Binding
    └─ Extensions
    ↓
Godot Nodes / Scene / Animation / Assets
```

## 目录结构

Facet 当前保留以下模块目录，用于后续承载实现代码与对应子系统：

- `Application/`
  面向 UI 的应用服务层抽象
- `Projection/`
  页面投影模型与投影存储
- `Runtime/`
  页面生命周期、路由、层级与运行时调度
- `Layout/`
  手工布局、自动生成、模板混合的统一布局来源抽象
- `UI/`
  Godot 节点访问、视图壳子与绑定能力
- `Lua/`
  Lua 页面控制器宿主与热更新支持
- `Extensions/`
  红点树等通用扩展能力

这些目录当前不要求都有实现代码，但目录职责应保持稳定。

## 模块划分

### Application

职责：

- 承接页面发起的命令与查询
- 协调用例执行
- 屏蔽本地实现与未来远程实现差异
- 输出标准化结果模型

建议包含：

- `CommandBus`
- `QueryBus`
- `AppResult<T>`
- `Gateway`
- `AppService`

### Projection

职责：

- 把逻辑状态整理为页面可消费的数据模型
- 提供页面刷新与扩展系统依赖的数据源
- 支持局部刷新与增量更新

建议包含：

- `ViewModel`
- `ProjectionStore`
- `ProjectionKey`
- `ProjectionUpdater`

### Runtime

职责：

- 页面打开、关闭、隐藏、恢复、销毁
- 页面层级、返回栈、路由、参数传递
- 控制器注入与运行时协调
- 生命周期一致性管理

建议包含：

- `UIManager`
- `UIRouteService`
- `UIPageRuntime`
- `UIContext`
- `UILayer`

### Layout

职责：

- 统一不同页面布局来源
- 屏蔽 `.tscn`、代码生成、模板混合之间的差异
- 向 Runtime 输出统一页面根节点

建议包含：

- `IUILayoutProvider`
- `SceneLayoutProvider`
- `GeneratedLayoutProvider`
- `TemplateLayoutProvider`
- `UINodeRegistry`

### UI

职责：

- Godot 节点访问与视图壳子
- 通用绑定机制
- 控件与命令之间的桥接

建议包含：

- `UIView`
- `UIWidget`
- `UINodeResolver`
- `UIBindingService`
- `UICommandBinder`

### Lua

职责：

- Lua 虚拟机宿主
- 页面控制器生命周期
- 热重载调度
- 受限 API 注入

建议包含：

- `LuaRuntimeHost`
- `LuaPageController`
- `LuaApiBridge`
- `LuaReloadCoordinator`
- `LuaScriptSource`

### Extensions

职责：

- 沉淀项目高频共性 UI 能力
- 由框架统一计算与协调，而不是页面各自实现

首期重点：

- 红点树
- 权限与解锁控制
- 引导锚点
- 提示与状态装饰

## 统一术语表

本节定义 Facet 内部使用的统一术语。后续代码、文档、注释与讨论应尽量使用这些词汇，避免一词多义。

### Page

Facet 中的页面运行单元。
一个 `Page` 对应一份页面定义、一个布局来源、一个运行时壳子，以及一个控制器实例。

### Page Definition

页面元数据定义，描述：

- 页面标识
- 布局来源
- 控制器脚本
- 所在层级
- 缓存策略
- 页面参数类型

### Page Runtime

页面运行时壳子。
负责持有页面根节点、上下文、控制器、状态与生命周期。

### View

页面或子部件在 Godot 中的可见壳子，强调表现层载体。

### Widget

可复用的页面子部件。
其生命周期比 `Page` 更轻，通常不独立参与路由。

### Layout Provider

页面布局来源的统一抽象。
它决定页面根节点如何被创建，但不决定页面行为逻辑。

### Projection

从逻辑状态整理出的、可直接驱动 UI 的展示数据模型。

### ViewModel

某个页面、面板、组件消费的具体投影对象。

### Command

由页面发起的写操作意图，例如“装备物品”“领取奖励”“打开邮件”。

### Query

由页面发起的读操作意图，例如“获取背包页数据”“读取分页内容”。

### Gateway

数据源访问抽象。
底层实现可以是本地逻辑、模拟实现或远程服务。

### Binding

把节点与数据、节点与命令、节点与扩展能力连接起来的统一机制。

### Node Key

Facet 中节点的稳定逻辑标识。
不直接等价于 Godot 路径，但必须映射到可定位节点。

### Extension

框架级通用能力单元，例如红点、权限、引导锚点等。

### Hot Reload

在不重启客户端的情况下，替换页面控制器或相关配置并恢复页面运行状态。

### Manual Layout

使用手工 `.tscn` 构建的页面布局。

### Generated Layout

通过代码或配置生成的页面布局。

### Template Layout

固定骨架加动态区域的混合布局模式。

### UI Context

页面运行时注入给控制器的统一能力集合，例如：

- 节点访问
- 绑定入口
- 页面路由
- 命令与查询入口
- 红点挂载入口

## 统一命名约定

### 页面命名

- 页面定义：`InventoryPage`
- 页面控制器：`inventory_page.lua` 或等价规范形式
- 页面场景：`InventoryPage.tscn`

### 节点命名

- 节点逻辑标识使用稳定、语义化名称
- 避免把节点路径作为外部协议
- 推荐使用 `Node Key` 进行访问与绑定

### 红点路径命名

使用层级点分路径，例如：

- `mail.system`
- `bag.equip.weapon`
- `activity.signin.daily`

### 服务命名

- `InventoryAppService`
- `MailQueryService`
- `RewardCommandHandler`

## 页面定义文件格式

Facet 需要一份统一的页面定义文件格式，用于把“页面”从若干散落的资源和代码约定，收敛为运行时可识别、可加载、可热更新、可路由的标准单元。

如果没有这层定义，后续会出现以下问题：

1. `UIManager` 不知道一个页面应如何被打开
2. 框架无法判断页面布局来自 `.tscn`、代码生成还是模板混合
3. 框架无法统一定位页面控制器脚本
4. 页面层级、缓存策略、返回栈策略只能硬编码在代码里
5. 热更新时无法明确哪些资源、配置和行为属于同一个页面
6. 页面注册、预加载、调试、工具化都缺乏统一入口

因此，页面定义文件的本质是“页面元数据描述文件”。它的作用不是承载页面逻辑，而是向 Facet 运行时声明这个页面是什么、如何被构建、如何被管理。

### 页面定义文件的职责

页面定义文件应至少回答以下问题：

- 这个页面是谁
- 这个页面的布局从哪里来
- 这个页面的控制器是谁
- 这个页面属于哪个 UI 层级
- 这个页面是否允许缓存
- 这个页面接收什么参数
- 这个页面是否启用某些扩展能力
- 这个页面是否支持预加载与热更新

### 页面定义文件的核心价值

#### 统一页面注册

页面不再依赖散落在代码中的手工注册或硬编码打开逻辑，而是通过统一元数据纳入 Facet Runtime。

#### 解耦布局与控制器

控制器不需要硬编码布局来源，Runtime 可以根据定义文件选择 `SceneLayoutProvider`、`GeneratedLayoutProvider` 或 `TemplateLayoutProvider`。

#### 支撑热更新

只有当 Runtime 知道页面由哪些资源、配置和脚本组成时，才能安全地做页面级热重载。

#### 支撑运行时路由

页面层级、缓存策略、返回栈行为、参数传递方式都需要通过统一元数据描述，而不是散在不同系统中。

#### 支撑工具化与调试

后续无论做页面浏览器、页面预览、资源校验、依赖分析还是预加载工具，都需要依赖页面定义文件作为统一入口。

### 建议包含的最小字段

Facet 第一阶段的页面定义文件建议至少包含以下字段：

- `pageId`
  页面唯一标识
- `layoutType`
  页面布局类型，例如 `scene`、`generated`、`template`
- `layoutPath` 或 `layoutBuilder`
  页面布局来源
- `controllerScript`
  页面对应的 Lua 控制器脚本
- `layer`
  页面所在层级
- `cachePolicy`
  页面缓存策略

### 后续可扩展字段

在框架能力稳定后，可以继续扩展以下信息：

- `route`
- `defaultArgs`
- `permissions`
- `preloadAssets`
- `openAnimation`
- `closeAnimation`
- `redDotBindings`
- `tags`
- `hotReloadPolicy`

### 与 Facet Runtime 的关系

Facet 运行时应以页面定义文件作为页面装载的第一入口。一个典型打开流程如下：

```text
UIManager.Open(pageId, args)
    → 读取页面定义
    → 选择布局提供者
    → 创建页面根节点
    → 创建页面运行时壳子
    → 加载 Lua 控制器
    → 注入 UIContext / Projection / Services
    → 进入 OnInit / OnShow 生命周期
```

### 当前阶段的结论

Facet 现在还不急于实现页面定义文件解析器，但必须先把“页面定义文件”作为正式概念写入设计规范。后续无论采用 JSON、TOML、自定义资源还是 C# 描述对象，都应遵守这里约定的职责边界。
## 页面统一生命周期

Facet 的页面生命周期必须与布局来源解耦，并对 Lua 控制器、绑定系统、扩展系统保持一致。

### 页面状态

建议统一使用以下页面状态：

1. `Created`
2. `Initialized`
3. `Shown`
4. `Hidden`
5. `Disposed`

### 页面主生命周期

#### Create

页面运行时被创建，完成以下动作：

- 读取页面定义
- 选择布局提供者
- 创建根节点
- 构建页面运行时对象

此阶段尚未进入业务可交互状态。

#### Initialize

页面第一次初始化，完成以下动作：

- 构建 `UIContext`
- 创建控制器实例
- 注入宿主 API
- 注册基础绑定
- 调用控制器 `OnInit`

此阶段后页面具备进入显示的前提。

#### Show

页面进入显示状态，完成以下动作：

- 附加到目标层级
- 恢复可见性
- 应用显示参数
- 调用控制器 `OnShow`

#### Refresh

页面参数或投影变化时执行刷新，完成以下动作：

- 更新页面参数
- 更新投影上下文
- 调用控制器 `OnRefresh`
- 触发相关绑定刷新

#### Hide

页面临时隐藏时执行，完成以下动作：

- 脱离交互焦点
- 暂停必要订阅
- 调用控制器 `OnHide`

此状态下页面可被缓存。

#### Dispose

页面销毁时执行，完成以下动作：

- 解绑节点与扩展
- 销毁控制器
- 释放运行时资源
- 释放页面节点
- 调用控制器 `OnDispose`

### 页面生命周期规则

1. `OnInit` 只执行一次
2. `OnShow` 可多次执行
3. `OnRefresh` 可在 `Shown` 状态多次执行
4. `OnHide` 与 `OnShow` 成对出现，但缓存页可反复切换
5. `OnDispose` 只执行一次，执行后页面不可恢复

## Lua 控制器统一生命周期

Lua 控制器生命周期应与页面生命周期严格对齐。

### 标准回调

- `OnInit`
- `OnShow`
- `OnRefresh`
- `OnHide`
- `OnDispose`

### 约束

1. Lua 控制器不自行维护页面显示状态机
2. Lua 控制器不决定页面是否缓存或销毁
3. Lua 控制器通过 `UIContext` 与宿主交互
4. Lua 控制器内部状态必须可由页面参数、投影态或运行时缓存恢复

## 热重载统一生命周期

Facet 的热重载不是“整个页面逻辑随意替换”，而是受控的页面控制器重建过程。

### 第一阶段热更新范围

支持：

- Lua 页面控制器脚本
- 页面绑定配置
- 页面元数据

暂不支持：

- 任意 C# 代码热更
- 任意底层业务逻辑热更
- 任意 Godot 资源热更

### 热重载状态

建议统一使用以下状态：

1. `Idle`
2. `ReloadPending`
3. `Reloading`
4. `Rebinding`
5. `Reloaded`
6. `ReloadFailed`

### 热重载流程

```text
检测脚本或配置变更
    → 标记页面控制器失效
    → 暂停旧控制器事件接收
    → 保存可恢复上下文
    → 销毁旧控制器
    → 创建新控制器
    → 重新注入 UIContext / Projection / Services
    → 重新注册绑定
    → 执行恢复逻辑
    → 恢复页面事件流
```

### 状态恢复原则

热重载后的页面状态优先从以下来源恢复：

1. 页面参数
2. Projection Store
3. Runtime Cache

不依赖 Lua 虚拟机内部状态持久化。

## 混合布局统一策略

Facet 必须同时支持以下三类页面：

1. 手工布局页面
2. 自动生成页面
3. 模板混合页面

### 手工布局页面

由 `.tscn` 场景构成，适合：

- 视觉要求高的页面
- 动效复杂的页面
- 需要精细节点层级的页面

约束：

- 节点必须遵守统一命名规范
- 页面节点必须能通过稳定 `Node Key` 访问

### 自动生成页面

由代码或配置生成节点树，适合：

- 调试页
- 配置页
- 数据驱动列表页
- 高重复结构页面

约束：

- 必须产出稳定 `Node Key`
- 不能因为运行时生成而失去统一绑定能力

### 模板混合页面

由固定骨架和动态区域共同构成，适合：

- 骨架稳定但内容结构变化大的页面
- 列表、网格、装备位、属性区域等

### 统一协议

无论布局来源是什么，页面都必须满足：

1. 可产出根节点 `Control`
2. 可注册稳定节点键名
3. 可被统一绑定系统消费
4. 可被统一 Lua 控制器驱动

### 推荐顺序

Facet 第一阶段建议优先支持：

1. 手工布局页面
2. 模板混合页面

自动生成整页能力应在 Runtime、Binding、NodeRegistry 稳定后逐步引入。

## 应用层边界规范

Facet 从第一天开始就需要独立的应用层边界，即使项目初期没有网络交互层。

### 原因

如果页面直接调用本地逻辑对象，未来接入服务器或异步交互流程后会产生以下问题：

1. 页面与底层实现强耦合
2. Lua 控制器难以统一处理异步结果
3. 本地模式与联网模式需要两套页面逻辑
4. 自动化测试与编辑器预览难以稳定开展

### 统一原则

1. 页面不感知数据来自本地还是远程
2. 页面不感知调用是同步还是异步
3. 页面只处理结果与投影变化
4. `Gateway` 是可替换实现，而不是页面关心点

### 页面可见协议

页面始终只接触：

- `Command`
- `Query`
- `AppResult`
- `Projection`

### 推荐接口方向

- `ICommandBus`
- `IQueryBus`
- `IAppService`
- `IGateway`
- `AppResult<T>`

## Projection 规范

Facet 以投影态驱动 UI，而不是让页面直接绑定逻辑对象。

### 原则

1. 投影对象面向 UI，不复用底层逻辑对象
2. 页面刷新依赖投影变化，而不是逻辑层主动推动节点修改
3. 红点、列表、按钮可用态等 UI 状态优先从投影层计算

### 目标

- 隔离底层结构
- 提供稳定页面数据源
- 支持页面缓存与局部刷新
- 支持未来远程同步场景

## Binding 规范

Facet 的 Binding 是页面节点与数据、命令、扩展能力之间的统一连接层。

### Binding 类型

- 数据绑定
- 命令绑定
- 显隐绑定
- 可交互绑定
- 列表绑定
- 扩展绑定

### 约束

1. 页面行为优先通过绑定系统组织，而不是节点之间直接互连
2. 控件更新优先由 Binding 驱动，而不是页面手工到处写节点赋值
3. Binding 必须与布局来源解耦

## RedDot 规范

红点系统必须是 Facet 的框架级扩展能力，而不是页面私有逻辑。

### 目标

- 提供稳定的红点路径命名体系
- 支持树状聚合
- 支持布尔红点与计数红点
- 支持脏节点增量刷新
- 让手工页面与自动布局页面共享同一挂载协议

### 核心概念

- `RedDotPath`
- `RedDotService`
- `RedDotProvider`
- `RedDotBinding`
- `RedDotAggregator`

### 约束

1. 红点值不由页面脚本直接计算
2. 页面只声明绑定关系与显示节点
3. 红点刷新以投影变化和业务事件为驱动

## Lua API 暴露原则

Lua 只能看到受限宿主 API，例如：

- 节点访问
- 绑定注册
- 命令调用
- 查询调用
- 红点绑定
- 页面路由

Lua 不能直接获得整个 CLR 世界访问权。

## 第一阶段范围

Facet 第一阶段只建立设计、命名和目录骨架，不急于实现代码。

优先级如下：

1. 定义边界
2. 统一术语
3. 收紧生命周期规范
4. 明确热更新范围
5. 明确布局来源抽象

## 后续实现顺序建议

1. Runtime
2. Projection
3. Application 边界
4. Layout Provider
5. Lua 宿主
6. Binding
7. RedDot

## 长期实现方案

阶段状态标记规则：

- [已完成]：该阶段目标与交付物已经完成，并已在当前文档中校准
- [进行中]：该阶段已经启动，但尚未达到完成标准
- [未开始]：该阶段尚未进入正式实现

Facet 的实现应坚持“先底层约束，后页面能力；先运行时骨架，后业务页面接入”的顺序。不要一开始就直接写页面框架表层 API，否则后续很容易因为生命周期、投影模型、热更新边界不稳而返工。

整体上建议分阶段推进，并按底层到上层的顺序逐步落地。

### 阶段 0：实现前校准 `[已完成]`

目标：

- 把当前 README 作为唯一设计基线
- 明确 Facet 与现有临时 UI 原型代码的边界
- 决定 Facet 第一阶段不接入真实业务页面

交付物：

- 稳定目录结构
- 统一术语
- 生命周期与模块边界说明

完成标准：

- 团队后续讨论都以 Facet 术语为准
- 不再在子目录维护平行设计文档

### 阶段 1：底层宿主与基础依赖 `[进行中]`

这是 Facet 的真正起点，优先级最高。

目标：

- 建立 Facet 在 Godot 中的宿主入口
- 定义基础依赖注入方式
- 建立日志、配置、运行时上下文等基础设施

建议实现：

- `FacetHost`
  作为 Facet 全局宿主入口，负责启动 Runtime、注册服务、持有全局配置
- `FacetConfig`
  运行时配置，例如调试开关、热重载开关、页面缓存策略默认值
- `FacetServices`
  简单服务注册器，用于组织 Application、Projection、Runtime、Lua 等模块依赖
- `FacetLogger`
  统一日志入口，便于后续调试页面生命周期与热重载

为什么先做这一层：

- 后续所有模块都需要稳定的宿主与依赖组织方式
- 如果这层不先立住，后面模块之间会很快变成互相硬引用

完成标准：

- Godot 启动后能初始化 Facet Host
- 各模块可以通过统一方式取到共享服务

当前已落地项：

- FacetHost
  已提供最小宿主入口、初始化流程、核心服务注册与初始化信号
- FacetConfig
  已提供最小运行时配置对象
- FacetServices
  已提供最小服务注册与按类型解析能力
- FacetRuntimeContext
  已提供配置、服务、日志三者组成的基础运行时上下文
- FacetLogger / IFacetLogger
  已提供基于 Godot 的统一日志实现与日志接口
- 启动验证日志
  已可通过启动主场景并检索“FacetHost 启动验证成功”确认宿主接入

当前仍未完成：

- 阶段 1 的自动化验证与编译隔离仍未完成
- 针对后续模块的默认服务注册策略收敛
- FacetHost 之外的宿主基础依赖仍需继续补齐

### 阶段 2：应用边界与结果模型 `[未开始]`

目标：

- 把 UI 与底层逻辑之间的调用边界先抽出来
- 即使现在没有网络层，也先建立 Command / Query / Result 范式

建议实现：

- `ICommandBus`
- `IQueryBus`
- `AppResult<T>`
- `IAppService`
- `IGateway`

第一阶段可以只提供本地空实现或 Mock 实现。

为什么先做这一层：

- Lua 控制器和页面运行时之后都要依赖它
- 这能避免 UI 直接调用 Lattice 或本地存档对象

完成标准：

- 页面层未来只面向 Command / Query 编程
- 本地模式和未来远程模式共享同一调用范式

### 阶段 3：Projection 层与数据刷新骨架 `[未开始]`

目标：

- 把 UI 的数据来源从底层对象解耦出来
- 建立可局部刷新的 ViewModel 组织方式

建议实现：

- `ProjectionKey`
- `ProjectionStore`
- `ViewModel`
- `ProjectionChange`
- `ProjectionUpdater`

优先实现的能力：

- 按 Key 存取投影对象
- 发布投影变更事件
- 让页面能订阅和取消订阅投影变化

为什么在 Runtime 前实现 Projection：

- 页面运行时如果没有稳定的数据模型，很容易退化成“打开页面后手写赋值”
- 红点、列表、按钮状态等后续能力都依赖 Projection

完成标准：

- Runtime 能拿到统一投影数据
- 页面刷新可以由投影变化驱动

### 阶段 4：页面定义与注册系统 `[未开始]`

目标：

- 把页面从“代码约定”变成“运行时可识别单元”
- 建立页面注册表与页面元数据系统

建议实现：

- `UIPageDefinition`
- `UIPageRegistry`
- `UIPageLoader`
- `IPageDefinitionSource`

第一阶段先不急着做复杂格式解析器，可以先用：

- C# 描述对象
- 或 JSON 配置

最小字段优先支持：

- `pageId`
- `layoutType`
- `layoutPath` / `layoutBuilder`
- `controllerScript`
- `layer`
- `cachePolicy`

完成标准：

- `UIManager.Open(pageId)` 不再依赖硬编码页面构造逻辑

### 阶段 5：Runtime 核心与页面生命周期 `[未开始]`

这是 Facet 的第一块核心运行时代码。

目标：

- 实现页面状态机
- 实现页面打开、关闭、隐藏、销毁、返回栈
- 统一管理页面上下文与层级

建议实现：

- `UIManager`
- `UIRouteService`
- `UIPageRuntime`
- `UIContext`
- `UILayerRoot`

第一阶段只支持最基本页面能力：

- 打开页面
- 关闭页面
- 隐藏页面
- 返回上一级
- 缓存页面

此阶段要严格实现文档里的生命周期：

- `Create`
- `Initialize`
- `Show`
- `Refresh`
- `Hide`
- `Dispose`

完成标准：

- 页面生命周期有统一日志
- 返回栈与页面缓存行为稳定

### 阶段 6：布局提供者与节点注册 `[未开始]`

目标：

- 让页面控制器不关心布局来源
- 让手工布局和自动生成布局走统一协议

建议实现：

- `IUILayoutProvider`
- `SceneLayoutProvider`
- `UINodeRegistry`
- `UINodeResolver`

实现顺序建议：

1. 先做 `SceneLayoutProvider`
2. 再做节点注册和稳定 `Node Key`
3. 最后再引入 `TemplateLayoutProvider`
4. `GeneratedLayoutProvider` 放在更后面

为什么不要先做自动生成布局：

- 自动生成布局依赖更稳定的 Runtime 和 Binding 协议
- 先把 `.tscn` 页面纳入统一运行时，更容易验证框架正确性

完成标准：

- Lua 控制器和 Binding 系统不直接依赖 Godot 路径

### 阶段 7：Binding 系统 `[未开始]`

目标：

- 把页面更新逻辑从“散落的节点赋值”收敛为统一绑定机制

建议实现：

- `UIBindingService`
- `TextBinding`
- `VisibilityBinding`
- `InteractableBinding`
- `CommandBinding`
- `ListBinding`

第一阶段先实现四类最关键绑定：

1. 文本绑定
2. 显隐绑定
3. 按钮命令绑定
4. 简单列表绑定

完成标准：

- 大部分页面刷新不再需要手写节点赋值逻辑

### 阶段 8：Lua 宿主与页面控制器 `[未开始]`

目标：

- 把页面行为从 C# 页面脚本中抽离出来
- 建立受限 Lua 控制器宿主

建议实现：

- `LuaRuntimeHost`
- `LuaPageController`
- `LuaApiBridge`
- `LuaScriptSource`

第一阶段建议只支持：

- `OnInit`
- `OnShow`
- `OnRefresh`
- `OnHide`
- `OnDispose`

注入给 Lua 的能力先收窄到：

- 节点访问
- 绑定注册
- 命令调用
- 查询调用
- 页面路由
- 红点绑定预留接口

完成标准：

- 页面行为可以逐步迁移到 Lua
- Lua 不直接穿透到底层逻辑与网络

### 阶段 9：热重载协调器 `[未开始]`

目标：

- 支持 Lua 页面行为热更新
- 支持页面级受控重载

建议实现：

- `LuaReloadCoordinator`
- 页面控制器失效检测
- 页面重绑流程
- 运行时状态恢复逻辑

第一阶段只支持：

- Lua 脚本变更重载
- 页面绑定重建
- 基于页面参数与 Projection 的状态恢复

暂不支持：

- 页面任意局部状态持久化
- 任意 C# 逻辑热更
- 任意场景资源热更

完成标准：

- Lua 代码变更后，已打开页面可以在不重启客户端的情况下恢复运行

### 阶段 10：扩展系统与红点树 `[未开始]`

目标：

- 把高频通用能力从页面中剥离出来

建议优先实现：

- `RedDotService`
- `RedDotProvider`
- `RedDotBinding`
- `RedDotAggregator`

后续扩展可继续补：

- 权限控制
- 引导锚点
- 推荐标记
- 锁定态装饰

为什么红点不更早做：

- 红点树本质上依赖 Projection、Binding、Runtime 和稳定节点标识
- 过早实现只会变成页面私有逻辑

完成标准：

- 页面只声明红点挂载关系，不负责红点计算

### 阶段 11：模板布局与自动生成布局 `[未开始]`

目标：

- 在 Runtime、Binding、NodeRegistry 已稳定后，引入更高阶的页面构造方式

建议实现：

- `TemplateLayoutProvider`
- `GeneratedLayoutProvider`
- 动态区域节点工厂
- 配置化布局描述对象

适合在这时接入的页面类型：

- 列表页
- 数据面板页
- 调试页
- 配置页

完成标准：

- 手工布局和自动布局共享同一控制器、绑定和扩展协议

### 阶段 12：工具化与工程化 `[未开始]`

目标：

- 让 Facet 从“能运行”进入“能维护、能扩展、能调试”的阶段

建议实现：

- 页面定义校验工具
- 生命周期调试面板
- 页面注册表浏览工具
- 热重载日志与错误定位
- Projection 观察器
- RedDot 树调试工具

完成标准：

- 新页面接入成本明显下降
- 框架问题具备可观测性

## 实施原则

在长期实现过程中，应持续遵守以下原则：

1. 不要跳过 Application 和 Projection，直接写页面逻辑
2. 不要先做页面表层 API，再补生命周期底座
3. 不要让 Lua 越过 `UIContext` 直接触底层系统
4. 不要过早追求复杂自动布局和全量热更
5. 每完成一个阶段，都要用最小示例页面验证而不是直接接业务大页面

## 每阶段的验证方式

建议每个阶段都配一个最小验证样例，而不是一上来接入真实复杂页面。

例如：

- 阶段 1：启动 `Main.tscn`，确认日志中出现“FacetHost 启动验证成功”
- 阶段 3：验证 Projection 更新是否驱动一个简单 Label 刷新
- 阶段 5：验证页面打开、隐藏、返回栈
- 阶段 8：验证 Lua 控制器是否能接管一个简单页面
- 阶段 9：验证修改 Lua 后页面是否可以安全恢复
- 阶段 10：验证红点树聚合和节点挂载

## 推荐的最小里程碑

如果要把长期路线压缩成几个可交付里程碑，我建议是：

1. `M1`
   Facet Host + Application 边界 + Projection Store
2. `M2`
   Page Definition + Runtime + SceneLayoutProvider
3. `M3`
   Binding 系统 + 基础页面接入
4. `M4`
   Lua 控制器 + 基础热重载
5. `M5`
   RedDot + 模板布局 + 工具化
## 当前阶段结论

阶段 0 已完成。阶段 1 已进入实现中状态，当前已经落下宿主、配置、服务容器、日志和运行时上下文的最小骨架，并已接入主场景。现在已经具备一个更聚焦的最小验证入口：直接启动 `Main.tscn`，确认日志中出现“FacetHost 启动验证成功”。当前阻塞点不在 Facet 新增代码本身，而在项目里既有的 lattice、测试和生成属性问题导致整项目构建噪音过大。后续推进时，应继续同步更新本节及对应阶段标题状态。

## 当前结论

Facet 应被视为 Sideline 的客户端表现层基础设施，其核心价值在于：

1. 用 Projection 隔离逻辑世界与表现世界
2. 用 Lua 承载可热更新的页面行为
3. 用 Runtime 统一页面生命周期与布局来源
4. 用 Application 边界承接当前本地逻辑与未来远程交互
5. 用 Extension 机制沉淀高频共性能力

后续实现、讨论与代码结构设计，均应围绕这五点展开。