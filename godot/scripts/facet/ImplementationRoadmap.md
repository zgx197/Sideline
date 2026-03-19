# Facet 长期实现方案

本文档从 `README.md` 中拆分，用于维护 Facet 的长期落地路线、阶段状态、验证方式和推进结论。

Facet 的总体设计、术语、生命周期、边界规范和模块职责，仍以 `README.md` 为主说明。

## 阶段状态标记规则

- `[已完成]`：该阶段目标与交付物已经完成，并已在当前文档中校准
- `[进行中]`：该阶段已经启动，但尚未达到完成标准
- `[未开始]`：该阶段尚未进入正式实现

## 实现顺序原则

Facet 的实现应坚持“先底层约束，后页面能力；先运行时骨架，后业务页面接入”的顺序。不要一开始就直接写页面框架表层 API，否则后续很容易因为生命周期、投影模型、热更新边界不稳而返工。

整体上建议分阶段推进，并按底层到上层的顺序逐步落地。

## 阶段路线

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

- `FacetHost`
  已提供最小宿主入口、初始化流程、核心服务注册与初始化信号
- `FacetConfig`
  已提供最小运行时配置对象
- `FacetServices`
  已提供最小服务注册与按类型解析能力
- `FacetRuntimeContext`
  已提供配置、服务、日志三者组成的基础运行时上下文
- `FacetLogger` / `IFacetLogger`
  已提供基于 Godot 的统一日志实现与日志接口
- 启动验证日志
  已可通过启动主场景并检索“FacetHost 启动验证成功”确认宿主接入

当前仍未完成：

- 阶段 1 的自动化验证与编译隔离仍未完成
- 针对后续模块的默认服务注册策略收敛
- `FacetHost` 之外的宿主基础依赖仍需继续补齐

### 阶段 2：应用边界与结果模型 `[未开始]`

目标：

- 把 UI 与底层逻辑之间的调用边界先抽出来
- 即使现在没有网络层，也先建立 `Command / Query / Result` 范式

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

- 页面层未来只面向 `Command / Query` 编程
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