# Facet 长期实现方案（进度更新至 2026-03-21）

本文档用于维护 Facet 的长期落地路线、阶段状态、验证方式和当前推进结论。
主设计说明请查看 [README.md](./README.md)。

## 阶段状态标记规则

- `[已完成]`：该阶段目标与交付物已经完成，并已通过当前文档校准
- `[进行中]`：该阶段已经进入正式实现，但尚未达到完成标准
- `[未开始]`：该阶段尚未进入正式实现

## 最新进度

- 阶段 0：[已完成]
- 阶段 1：[已完成]
- 阶段 2：[已完成]
- 阶段 3：[已完成]
- 阶段 4：[已完成]
- 阶段 5：[已完成]
- 阶段 6：[已完成]
- 阶段 7：[已完成]
- 阶段 8：[已完成]
- 阶段 9：[已完成]
- 阶段 10：[未开始]
- 阶段 11：[未开始]
- 阶段 12：[未开始]

## 当前代码库已经具备

- FacetHost 宿主入口与基础运行时依赖
- Command、Query、AppResult、Gateway 等应用边界骨架
- ProjectionStore、ProjectionRefreshCoordinator、ProjectionUpdater 刷新骨架
- UIPageDefinition、UIPageRegistry、UIPageLoader、UIManager、UIRouteService、UIPageRuntime 等正式页面运行时
- SceneLayoutProvider、UINodeRegistry、UINodeResolver 等布局提供者与节点解析基础设施
- UIBindingService 与页面级、组件级、复杂列表级 Binding 最小能力
- IdlePanel / DungeonPanel 两个真实 Projection 驱动样例
- 基于 MoonSharp 的真实 Lua 控制器宿主与文件脚本源
- LuaBindingScopeBridge 支持的页面级、组件级、结构化列表级 Lua Binding 接口
- 页面级 Lua 生命周期、状态袋、参数读取与受限诊断查询接口
- LuaReloadCoordinator、脚本版本检测、控制器重建与页面级热重载轮询骨架
- Facet 编辑器工作区、结构化日志与基础诊断能力

## 当前仍未具备

- 红点树正式实现
- TemplateLayoutProvider / GeneratedLayoutProvider 正式落地
- 页面定义外部文件化与校验工具
- 更高级的 Lua 热重载恢复策略验证与调试工具
- 更细粒度的红点、权限、引导等扩展系统正式接入

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

---

## 阶段 0：实现前校准 `[已完成]`

目标：

- 明确 Facet 的定位、边界、术语和生命周期规范
- 把 README 作为 Facet 的主设计说明
- 决定早期不直接接入真实业务页面

完成结论：

- 设计术语和模块边界已统一
- 目录结构与职责划分已稳定

## 阶段 1：底层宿主与基础依赖 `[已完成]`

目标：

- 建立 `FacetHost`
- 建立 `FacetConfig`、`FacetServices`、`FacetRuntimeContext`
- 建立统一日志入口

当前已落地：

- `FacetHost`
- `FacetConfig`
- `FacetServices`
- `FacetRuntimeContext`
- `FacetLogger` / `IFacetLogger`
- 启动验证日志与结构化日志基础能力

完成标准结论：

- Facet 可以在 Godot 主场景中稳定初始化
- 运行时服务可以通过统一方式访问

## 阶段 2：应用边界与结果模型 `[已完成]`

目标：

- 把页面与底层逻辑之间的调用边界抽离出来
- 让页面未来统一面向 Command / Query / Result 编程

当前已落地：

- `ICommandBus` / `IQueryBus`
- `AppResult` / `AppResult<T>`
- `IAppService` / `IGateway`
- `LocalCommandBus` / `LocalQueryBus`
- 运行时探针服务与 Query -> Command -> Gateway -> Query 闭环样例

完成标准结论：

- 页面层已具备统一调用协议，不再依赖底层实现细节

## 阶段 3：Projection 层与数据刷新骨架 `[已完成]`

目标：

- 建立统一 Projection 数据中心
- 让页面刷新由 Projection 变化驱动

当前已落地：

- `ProjectionKey`
- `ProjectionStore`
- `ProjectionRefreshCoordinator`
- `ProjectionChange`
- `RuntimeProbeProjectionUpdater`
- `RuntimeMetricsProjectionUpdater`
- Idle / Dungeon 两个真实 Projection 驱动页面样例

完成标准结论：

- Projection 已成为页面层的正式数据来源

## 阶段 4：页面定义与注册系统 `[已完成]`

目标：

- 把页面从硬编码约定变成运行时可识别单元
- 建立页面元数据与注册表

当前已落地：

- `UIPageDefinition`
- `UIPageRegistry`
- `IPageDefinitionSource`
- `InMemoryPageDefinitionSource`
- `FacetBuiltInPageDefinitions`
- `UIPageLoader`

完成标准结论：

- `UIManager.Open(pageId)` 已经成为正式打开入口

## 阶段 5：Runtime 核心与页面生命周期 `[已完成]`

目标：

- 建立页面状态机
- 建立页面上下文与统一生命周期
- 建立返回栈和缓存策略

当前已落地：

- `UIPageState`
- `UIContext`
- `IUIPageLifecycle`
- `UIPageRuntime`
- `UIRouteService`
- `UIManager`

完成标准结论：

- 页面已经具备统一的 `Create / Initialize / Show / Refresh / Hide / Dispose` 生命周期日志

## 阶段 6：布局提供者与节点注册 `[已完成]`

目标：

- 让页面控制器不直接依赖 Godot 路径
- 统一手工布局和后续自动布局的运行时接入协议

当前已落地：

- `IUILayoutProvider`
- `SceneLayoutProvider`
- `UILayoutResult`
- `UINodeRegistry`
- `UINodeResolver`

完成标准结论：

- 页面、Binding 与控制器已经能够通过稳定节点标识工作，而不是直接依赖 Godot 路径

## 阶段 7：Binding 系统 `[已完成]`

目标：

- 把散落的节点赋值逻辑收敛成统一 Binding 机制

当前已落地：

- `IUIBindingScope`
- `IUIComponentBindingScope`
- `IUIComplexListBinding<T>`
- `UIBindingService`
- Text / Visibility / Interactable / Command / List / ComplexList Binding
- Idle / Dungeon 页面上的真实 Binding 接入
- Binding 诊断快照与结构化日志

完成标准结论：

- 页面刷新已明显从“手写节点赋值”收敛为“Binding 驱动刷新”

## 阶段 8：Lua 宿主与页面控制器 `[已完成]`

目标：

- 把页面行为从 C# 页面脚本中逐步抽离出来
- 建立受限 Lua 控制器宿主
- 为后续热重载建立稳定入口

设计对象：

- `ILuaRuntimeHost` / `LuaRuntimeHost`
- `ILuaScriptSource`
- `LuaApiBridge`
- `LuaControllerHandle`
- `ILuaRedDotBridge`

当前已落地：

- `LuaRuntimeHost`
  已按 `controllerScript` 创建控制器实例，并把受限桥接对象注入控制器
- `MoonSharpLuaPageController`
  已基于 MoonSharp `Preset_SoftSandbox` 执行真实 Lua 脚本，并按统一生命周期调用全局函数入口
- `FileSystemLuaScriptSource`
  已从项目文件系统读取 `res://scripts/facet/LuaScripts/*.lua`，并为热重载生成稳定版本标记
- `LuaApiBridge`
  已暴露受限的节点解析、参数读取、Binding 刷新、受限诊断查询、页面路由与红点占位能力
- `LuaBindingScopeBridge`
  已把页面级、组件级与结构化列表级 Binding 以白名单桥接方式暴露给 Lua
- `IUIPageNavigator`
  已把页面路由以受限接口方式暴露给 Lua 宿主
- `UIPageRuntime`
  已统一转发 `OnInit / OnShow / OnRefresh / OnHide / OnDispose` 给 Lua 控制器
- `FacetBuiltInPageDefinitions`
  已给 `client.idle` 与 `client.dungeon` 配置真实 `controllerScript`
- `idle_runtime.lua` / `dungeon_runtime.lua`
  已用 Lua 实际声明文本、显隐、交互与结构化列表 Binding，而不是只输出生命周期日志
- `FacetHost`
  已注册脚本源、红点桥与 Lua 宿主，并输出阶段 8 启动验证日志
- `Main`
  已补充阶段 8 主场景日志，方便直接确认当前页面是否已经挂到 Lua 控制器

当前阶段结论：

- 阶段 8 已完成从“样例闭环”到“真实 Lua 页面控制器宿主”的收口
- 当前代码库已经具备真实脚本执行、受限 API、页面级/组件级/列表级 Binding 桥接，以及真实页面样例的运行日志验证
- 阶段 8 的主目标已完成，后续增强应转入阶段 10 以后扩展系统与工具化能力的持续落地

阶段边界说明：

- 阶段 8 关注“Lua 宿主是否已经真实接管页面行为”，验收重点是脚本执行、受限桥接、Lua Binding 和页面生命周期闭环
- 阶段 9 关注“Lua 页面在不重启客户端时能否安全热重载并恢复”，验收重点是版本检测、控制器重建、往返测试、页面恢复和结构化证据链
- 因此，后续若再出现热重载恢复问题，不应回退阶段 8 状态，而应作为阶段 9 之后的增强或修复继续处理

## 阶段 9：热重载协调器 `[已完成]`

目标：

- 支持 Lua 页面行为热更新
- 支持页面级受控重载

计划重点：

- `LuaReloadCoordinator`
- 控制器失效检测
- 页面重绑与状态恢复
- 重载过程日志与错误定位

当前已落地：

- `LuaReloadCoordinator`
  已由 `FacetHost._Process` 轮询当前页面运行时，并在脚本版本变化时触发页面级热重载尝试
- `FileSystemLuaScriptSource`
  已基于脚本文件内容生成版本标记，为热重载失效检测提供稳定依据
- `UIPageRuntime.TryReloadLuaController`
  已支持复用页面上下文、状态袋与现有页面节点，仅重建 Lua 控制器实例
- `LuaReloadResult`
  已记录页面标识、脚本标识、版本变化、重载原因与错误信息
- 热重载相关结构化日志
  已覆盖轮询、失败、控制器释放与重建完成等关键过程
- 编辑器热重载测试台与运行时文件桥
  已支持从 Facet 主面板直接发起 `current_page_round_trip` / `dungeon_round_trip` 测试请求，并读取最近一次状态
- 热重载往返测试服务
  已支持对 `idle_runtime.lua` 与 `dungeon_runtime.lua` 执行前向重载、回滚恢复与先前页面恢复
- 结构化日志共享读写
  `FacetMainScreen` 与 `FacetLogger` 已改为共享读取、共享追加与短重试，降低热重载测试期间日志文件竞争导致的证据丢失风险

后续可优化项：

- 校准更复杂页面状态的恢复策略，尤其是列表、订阅与中间交互状态
- 补齐热重载失败后的调试面板、过滤工具与更清晰的用户侧诊断信息

### 阶段 9 严格验收清单（2026-03-21 校准）

以下条目全部满足，才可判定“阶段 9 主链路通过”：

- 桥接层：测试请求必须从 `requested` 进入 `running`，最终进入 `completed` 或 `failed`，且状态文件中必须包含 `runtimeSessionId`
- 协调器层：结构化日志中必须出现 `Lua.HotReload` 记录，且前向与回滚阶段都必须出现 `reloadedRuntimeCount > 0`
- 前向阶段：必须出现 `phase = forward` 的 `Lua.HotReload.Test` 成功日志，且 `expectedVersionToken == actualVersionToken`
- 回滚阶段：必须出现 `phase = rollback` 的 `Lua.HotReload.Test` 成功日志，且版本必须回到测试开始前的原始 token
- 汇总阶段：必须出现 `Lua 热重载往返测试已完成` 日志，且 `forwardSuccess = true`、`rollbackSuccess = true`、`success = true`
- 页面恢复层：若测试临时打开了目标页，必须出现“已恢复先前页面”或等价恢复日志，且最终当前页恢复为测试前页面
- 生命周期与 Binding 层：热重载后页面仍需继续输出正常的 `Show / Refresh`、Lua 控制器日志与 Binding 刷新日志，不能只停留在状态文件成功
- 证据稳定性：Facet 编辑器面板读取结构化日志时，不应再持续触发 `Structured log write failed` 这类由文件竞争造成的告警

### 阶段 9 最新验收证据（2026-03-21）

最新一轮日志已覆盖 Facet 面板触发和页面内按钮触发两类入口，关键证据如下：

- `editor.workspace.dungeon`
  已在 `2026-03-21T09:21:26Z` 完成地下城页往返测试，状态文件结果为 `completed + success=true`
- `Lua.HotReload.Test`
  已记录 `editor.lab.dungeon` 的开始、`forward` 成功、`rollback` 成功和汇总成功日志
- `Lua.HotReload`
  已记录前向与回滚两轮热重载轮询完成，且两次 `reloadedRuntimeCount = 1`
- 页面恢复
  已记录“Lua 热重载测试已恢复先前页面”，并在后续日志中看到 `client.idle` 的 `Show / Refresh / 页面返回完成`
- 页面内入口
  `idle.panel.button.current`、`idle.panel.button.dungeon`、`dungeon.panel.button.current`、`dungeon.panel.button.dungeon` 均已出现完整成功证据链

完成标准结论：

- 阶段 9 的主链路能力已经通过真实运行时验证
- 阶段 9 已完成页面级热重载协调、往返测试、页面恢复与证据链收口
- 后续若出现更复杂状态恢复或调试侧需求，应作为阶段 12 的工具化增强继续推进

## 阶段 10：扩展系统与红点树 `[未开始]`

目标：

- 把高频通用能力从页面中剥离出来

计划重点：

- `RedDotService`
- `RedDotProvider`
- `RedDotBinding`
- `RedDotAggregator`

## 阶段 11：模板布局与自动生成布局 `[未开始]`

目标：

- 在 Runtime、Binding、NodeRegistry 稳定后引入更高阶页面构造方式

计划重点：

- `TemplateLayoutProvider`
- `GeneratedLayoutProvider`
- 动态区域节点工厂
- 配置化布局描述对象

## 阶段 12：工具化与工程化 `[未开始]`

目标：

- 让 Facet 从“能运行”进入“能维护、能扩展、能调试”的阶段

计划重点：

- 页面定义校验工具
- 生命周期调试面板
- 页面注册浏览器
- Projection 观察器
- Lua 热重载诊断工具
- 红点树调试工具

---

## 每阶段验证方式建议

建议每个阶段都配一个最小验证样例，而不是一开始就接入复杂业务页面。

例如：

- 阶段 1：启动 `Main.tscn`，确认日志中出现 `FacetHost 启动验证成功`
- 阶段 2：确认日志中出现应用边界闭环验证成功
- 阶段 3：确认 Projection 更新与页面刷新日志正常出现
- 阶段 4：确认页面通过 `pageId` 打开，而不是节点可见性硬切换
- 阶段 5：确认生命周期 `Create / Initialize / Show / Refresh / Hide` 全部出现
- 阶段 6：确认节点通过稳定节点键解析，不再依赖 Godot 路径
- 阶段 7：确认 Binding、组件作用域和复杂列表作用域日志出现
- 阶段 8：确认页面已经创建 Lua 控制器，并能看到 `Lua 控制器已创建`、Lua Binding 刷新与各生命周期日志
- 阶段 9：确认脚本改动后页面可在不重启客户端的前提下恢复，并能看到热重载成功或失败的结构化日志
- 阶段 10：确认红点路径、聚合和页面挂载关系都能稳定工作

## 当前结论

截至 2026-03-21，阶段 0 至阶段 9 已完成。
当前 Facet 已经具备：宿主、应用边界、Projection、正式页面运行时、布局提供者、Binding 系统、真实 Lua 页面控制器宿主，以及页面级热重载协调与运行时验证链路。

后续工作的重点将放在：

- 阶段 10 以后扩展系统与工具化能力的持续落地
