#nullable enable

using System;
using System.Collections.Generic;
using System.Text;
using Godot;
using Sideline.Facet.Application;
using Sideline.Facet.Application.Diagnostics;
using Sideline.Facet.Extensions.RedDot;
using Sideline.Facet.Layout;
using Sideline.Facet.Lua;
using Sideline.Facet.Projection;
using Sideline.Facet.Projection.Diagnostics;
using Sideline.Facet.UI;

namespace Sideline.Facet.Runtime
{
    /// <summary>
    /// Facet 全局宿主入口。
    /// </summary>
    public partial class FacetHost : Node
    {
        private static readonly ProjectionKey RuntimeProbeProjectionKey = FacetProjectionKeys.RuntimeProbe;
        private static readonly ProjectionKey RuntimeMetricsProjectionKey = FacetProjectionKeys.RuntimeMetrics;
        private const string LuaHotReloadCurrentPageTestShortcutDescription = "Ctrl+Shift+F10";
        private const string LuaHotReloadDungeonTestShortcutDescription = "Ctrl+Shift+F11";
        private const double EditorHotReloadLabPollIntervalSeconds = 0.25d;

        private double _hotReloadPollTimer;
        private double _editorHotReloadLabPollTimer;
        private string? _lastHandledHotReloadLabRequestId;
        private string? _lastHandledLayoutLabRequestId;
        private string? _lastRuntimeDiagnosticsFingerprint;

        /// <summary>
        /// 最小启动验证标记。
        /// 启动主场景后可以直接搜索该文本，确认 Facet 宿主已接入。
        /// </summary>
        public const string StartupVerificationMarker = "FacetHost 启动验证成功";

        [Signal]
        public delegate void InitializedEventHandler();

        [Export]
        public bool AutoInitialize { get; set; } = true;

        [Export]
        public bool EnableDebugLogging { get; set; } = true;

        [Export]
        public bool EnableStructuredLogging { get; set; } = true;

        [Export]
        public string StructuredLogPath { get; set; } = "user://logs/facet-structured.jsonl";

        [Export(PropertyHint.Range, "32,2048,32")]
        public int StructuredLogBufferCapacity { get; set; } = 256;

        [Export(PropertyHint.Range, "1,50,1")]
        public int StructuredLogHistoryLimit { get; set; } = 10;

        [Export]
        public bool EnableConsoleMirrorLogging { get; set; } = true;

        [Export]
        public string ConsoleMirrorLogPath { get; set; } = "user://logs/facet-console.log";

        [Export(PropertyHint.Range, "1,50,1")]
        public int ConsoleMirrorLogHistoryLimit { get; set; } = 10;

        [Export]
        public bool EnableHotReload { get; set; } = true;

        [Export(PropertyHint.Range, "0.1,5,0.1")]
        public double HotReloadPollIntervalSeconds { get; set; } = 0.5d;

        [Export]
        public bool EnableLuaHotReloadTestShortcut { get; set; } = false;

        [Export]
        public bool EnablePageCacheByDefault { get; set; } = true;

        [Export(PropertyHint.Range, "1,128,1")]
        public int DefaultPageCacheCapacity { get; set; } = 8;

        /// <summary>
        /// 当前宿主实例。
        /// </summary>
        public static FacetHost? Instance { get; private set; }

        /// <summary>
        /// 是否已完成初始化。
        /// </summary>
        public bool IsInitialized { get; private set; }

        /// <summary>
        /// 当前运行会话标识。
        /// </summary>
        public string CurrentSessionId => Logger is FacetLogger facetLogger ? facetLogger.SessionId : string.Empty;

        /// <summary>
        /// Facet 当前配置。
        /// </summary>
        public FacetConfig Config { get; private set; } = FacetConfig.Default;

        /// <summary>
        /// Facet 当前服务容器。
        /// </summary>
        public FacetServices Services { get; private set; } = new();

        /// <summary>
        /// Facet 当前日志器。
        /// </summary>
        public IFacetLogger Logger { get; private set; } = CreateLogger(FacetConfig.Default);

        /// <summary>
        /// Facet 当前运行时上下文。
        /// </summary>
        public FacetRuntimeContext Context { get; private set; } = null!;

        public override void _EnterTree()
        {
            if (Instance != null && Instance != this)
            {
                GD.PushWarning("[Facet][Host] 检测到多个 FacetHost 实例，后进入的实例将覆盖前一个引用。");
            }

            Instance = this;
        }

        public override void _Ready()
        {
            FacetPlainTextLogEncoding.EnsureGodotLogUtf8Bom();

            if (!AutoInitialize)
            {
                GD.Print($"[Facet][Bootstrap] FacetHost 已加载，等待手动初始化。Path={GetPath()}");
                return;
            }

            GD.Print($"[Facet][Bootstrap] FacetHost 自动初始化开始。Path={GetPath()}");
            Initialize();
        }

        public override void _Process(double delta)
        {
            if (!IsInitialized)
            {
                return;
            }

            _editorHotReloadLabPollTimer += delta;
            if (_editorHotReloadLabPollTimer >= EditorHotReloadLabPollIntervalSeconds)
            {
                _editorHotReloadLabPollTimer = 0.0d;
                PollEditorHotReloadLabRequests();
                PollEditorLayoutLabRequests();
                PublishRuntimeDiagnosticsSnapshot();
            }

            if (!Config.EnableHotReload)
            {
                return;
            }

            _hotReloadPollTimer += delta;
            if (_hotReloadPollTimer < Config.HotReloadPollIntervalSeconds)
            {
                return;
            }

            _hotReloadPollTimer = 0;
            Services.GetRequired<LuaReloadCoordinator>().Poll("host.poll");
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (!IsInitialized || !EnableLuaHotReloadTestShortcut)
            {
                return;
            }

            if (@event is not InputEventKey keyEvent ||
                !keyEvent.Pressed ||
                keyEvent.Echo ||
                !keyEvent.CtrlPressed ||
                !keyEvent.ShiftPressed)
            {
                return;
            }

            bool handled = keyEvent.Keycode switch
            {
                Key.F10 => TryRunLuaHotReloadRoundTripTest(reason: "host.shortcut"),
                Key.F11 => TryRunDungeonLuaHotReloadRoundTripTest(reason: "host.shortcut.dungeon"),
                _ => false,
            };

            if (handled)
            {
                GetViewport()?.SetInputAsHandled();
            }
        }

        /// <summary>
        /// 初始化 Facet 宿主。
        /// </summary>
        public void Initialize()
        {
            if (IsInitialized)
            {
                Logger.Warning("Host", "FacetHost 重复初始化请求已忽略。", null);
                return;
            }

            Config = BuildConfig();
            Services = new FacetServices();
            Logger = CreateLogger(Config);
            Context = new FacetRuntimeContext(Config, Services, Logger);
            _hotReloadPollTimer = 0;
            _editorHotReloadLabPollTimer = 0;
            _lastHandledHotReloadLabRequestId = null;
            _lastHandledLayoutLabRequestId = null;
            _lastRuntimeDiagnosticsFingerprint = null;

            RegisterCoreServices();

            IsInitialized = true;
            SetProcess(true);
            SetProcessUnhandledInput(EnableLuaHotReloadTestShortcut);
            LogStartupSummary();
            CleanupConsumedLabRequests();
            PublishHotReloadLabIdleStatus();
            PublishLayoutLabIdleStatus();
            PublishRuntimeDiagnosticsSnapshot(force: true);
            EmitSignal("Initialized");
        }

        /// <summary>
        /// 重置宿主运行时状态。
        /// </summary>
        public void ResetHost()
        {
            IsInitialized = false;
            Services = new FacetServices();
            Config = FacetConfig.Default;
            Logger = CreateLogger(Config);
            Context = new FacetRuntimeContext(Config, Services, Logger);
            _hotReloadPollTimer = 0;
            _editorHotReloadLabPollTimer = 0;
            _lastHandledHotReloadLabRequestId = null;
            _lastHandledLayoutLabRequestId = null;
            _lastRuntimeDiagnosticsFingerprint = null;
            SetProcess(false);
            SetProcessUnhandledInput(false);
        }

        /// <summary>
        /// 主动执行一次 Lua 热重载往返测试。
        /// 默认优先使用当前页面运行时所绑定的 Lua 控制器脚本。
        /// </summary>
        public bool TryRunLuaHotReloadRoundTripTest(string? scriptId = null, string reason = "manual")
        {
            if (!IsInitialized || !Services.TryGet(out LuaHotReloadTestService? testService) || testService == null)
            {
                Logger.Warning(
                    "Lua.HotReload.Test",
                    "Lua 热重载测试请求被忽略，测试服务尚未就绪。",
                    new Dictionary<string, object?>
                    {
                        ["requestedScriptId"] = scriptId,
                        ["reason"] = reason,
                        ["isInitialized"] = IsInitialized,
                    });
                return false;
            }

            return testService.TryRunRoundTripTest(scriptId, reason);
        }

        public bool TryRunDungeonLuaHotReloadRoundTripTest(string reason = "manual")
        {
            return TryRunLuaHotReloadRoundTripTest(FacetLuaScriptIds.DungeonRuntimeController, reason);
        }

        /// <summary>
        /// 获取必须存在的 Facet 服务。
        /// </summary>
        public TService GetRequired<TService>() where TService : class
        {
            return Services.GetRequired<TService>();
        }

        private FacetConfig BuildConfig()
        {
            return new FacetConfig
            {
                EnableDebugLogging = EnableDebugLogging,
                EnableStructuredLogging = EnableStructuredLogging,
                StructuredLogPath = StructuredLogPath,
                StructuredLogBufferCapacity = StructuredLogBufferCapacity,
                StructuredLogHistoryLimit = StructuredLogHistoryLimit,
                EnableConsoleMirrorLogging = EnableConsoleMirrorLogging,
                ConsoleMirrorLogPath = ConsoleMirrorLogPath,
                ConsoleMirrorLogHistoryLimit = ConsoleMirrorLogHistoryLimit,
                EnableHotReload = EnableHotReload,
                HotReloadPollIntervalSeconds = HotReloadPollIntervalSeconds,
                EnablePageCacheByDefault = EnablePageCacheByDefault,
                DefaultPageCacheCapacity = DefaultPageCacheCapacity,
            };
        }

        private void RegisterCoreServices()
        {
            LocalCommandBus commandBus = new(Logger);
            LocalQueryBus queryBus = new(Logger);
            ProjectionStore projectionStore = new();
            ProjectionRefreshCoordinator projectionRefreshCoordinator = new(Logger);
            IFacetRuntimeProbeGateway runtimeProbeGateway = new InMemoryFacetRuntimeProbeGateway();
            FacetRuntimeProbeService runtimeProbeService = new(Context, runtimeProbeGateway, CurrentSessionId);
            RuntimeProbeProjectionUpdater runtimeProbeProjectionUpdater = new(Context);
            RuntimeMetricsProjectionUpdater runtimeMetricsProjectionUpdater = new(Context);
            IPageDefinitionSource pageDefinitionSource = new InMemoryPageDefinitionSource(FacetBuiltInPageDefinitions.CreateMainSceneDefinitions());
            UIPageRegistry pageRegistry = new();
            FacetDynamicNodeFactory dynamicNodeFactory = new();
            IFacetGeneratedLayoutStore generatedLayoutStore = new InMemoryFacetGeneratedLayoutStore(FacetBuiltInLayoutDefinitions.CreateGeneratedLayouts());
            IFacetTemplateLayoutStore templateLayoutStore = new InMemoryFacetTemplateLayoutStore(FacetBuiltInLayoutDefinitions.CreateTemplateLayouts());
            SceneLayoutProvider sceneLayoutProvider = new(Logger);
            GeneratedLayoutProvider generatedLayoutProvider = new(generatedLayoutStore, dynamicNodeFactory, Logger);
            TemplateLayoutProvider templateLayoutProvider = new(templateLayoutStore, dynamicNodeFactory, Logger);
            IUILayoutProvider[] layoutProviders =
            {
                sceneLayoutProvider,
                templateLayoutProvider,
                generatedLayoutProvider,
            };
            UIPageLoader pageLoader = new(layoutProviders, Logger);
            UIBindingService bindingService = new(Logger);
            UIRouteService routeService = new();
            UIManager uiManager = new(pageRegistry, pageLoader, routeService, Context, Logger);
            string projectRootPath = ProjectSettings.GlobalizePath("res://");
            ILuaScriptSource luaScriptSource = new FileSystemLuaScriptSource(projectRootPath, FacetLuaScriptIds.All);
            RedDotService redDotService = new(Logger);
            FacetRuntimeRedDotProvider runtimeRedDotProvider = new(projectionStore, Logger);
            ManualRedDotProvider manualRedDotProvider = new();
            redDotService.RegisterProvider(runtimeRedDotProvider);
            redDotService.RegisterProvider(manualRedDotProvider);
            ILuaRedDotBridge luaRedDotBridge = new FacetLuaRedDotBridge(redDotService);
            ILuaRuntimeHost luaRuntimeHost = new LuaRuntimeHost(luaScriptSource, luaRedDotBridge);
            LuaReloadCoordinator luaReloadCoordinator = new(uiManager, Logger);
            LuaHotReloadTestService luaHotReloadTestService = new(uiManager, luaReloadCoordinator, luaScriptSource, Logger);

            pageRegistry.RegisterRange(pageDefinitionSource);

            Services.RegisterSingleton(Config);
            Services.RegisterSingleton(Logger);
            Services.RegisterSingleton((FacetLogger)Logger);
            Services.RegisterSingleton(Context);
            Services.RegisterSingleton<ICommandBus>(commandBus);
            Services.RegisterSingleton(commandBus);
            Services.RegisterSingleton<IQueryBus>(queryBus);
            Services.RegisterSingleton(queryBus);
            Services.RegisterSingleton(projectionStore);
            Services.RegisterSingleton(projectionRefreshCoordinator);
            Services.RegisterSingleton(runtimeProbeGateway);
            Services.RegisterSingleton(runtimeProbeService);
            Services.RegisterSingleton(runtimeProbeProjectionUpdater);
            Services.RegisterSingleton(runtimeMetricsProjectionUpdater);
            Services.RegisterSingleton(pageDefinitionSource);
            Services.RegisterSingleton(pageRegistry);
            Services.RegisterSingleton(dynamicNodeFactory);
            Services.RegisterSingleton(generatedLayoutStore);
            Services.RegisterSingleton(templateLayoutStore);
            Services.RegisterSingleton(sceneLayoutProvider);
            Services.RegisterSingleton(generatedLayoutProvider);
            Services.RegisterSingleton(templateLayoutProvider);
            Services.RegisterSingleton(pageLoader);
            Services.RegisterSingleton(bindingService);
            Services.RegisterSingleton(routeService);
            Services.RegisterSingleton<IUIPageNavigator>(uiManager);
            Services.RegisterSingleton(uiManager);
            Services.RegisterSingleton<IRedDotService>(redDotService);
            Services.RegisterSingleton(redDotService);
            Services.RegisterSingleton(runtimeRedDotProvider);
            Services.RegisterSingleton(manualRedDotProvider);
            Services.RegisterSingleton(luaScriptSource);
            Services.RegisterSingleton(luaRedDotBridge);
            Services.RegisterSingleton(luaRuntimeHost);
            Services.RegisterSingleton(luaReloadCoordinator);
            Services.RegisterSingleton(luaHotReloadTestService);

            commandBus.RegisterHandler<RecordFacetRuntimeProbeCommand>(runtimeProbeService.RecordAsync);
            queryBus.RegisterHandler<FacetRuntimeProbeQuery, FacetRuntimeProbeSnapshot>(runtimeProbeService.QueryCurrentAsync);
            queryBus.RegisterHandler<FacetRuntimeProbeStatusQuery, FacetRuntimeProbeStatusSnapshot>(runtimeProbeService.QueryStatusAsync);

            projectionRefreshCoordinator.Register(runtimeProbeProjectionUpdater);
            projectionRefreshCoordinator.Register(runtimeMetricsProjectionUpdater);
        }

        private void LogStartupSummary()
        {
            Logger.Info(
                "Bootstrap",
                StartupVerificationMarker,
                new Dictionary<string, object?>
                {
                    ["path"] = GetPath().ToString(),
                    ["sessionId"] = CurrentSessionId,
                    ["autoInitialize"] = AutoInitialize,
                    ["hotReload"] = Config.EnableHotReload,
                    ["hotReloadPollIntervalSeconds"] = Config.HotReloadPollIntervalSeconds,
                    ["pageCache"] = Config.EnablePageCacheByDefault,
                    ["cacheCapacity"] = Config.DefaultPageCacheCapacity,
                    ["structuredLogging"] = Config.EnableStructuredLogging,
                    ["structuredLogPath"] = Config.StructuredLogPath,
                    ["structuredLogBufferCapacity"] = Config.StructuredLogBufferCapacity,
                    ["structuredLogHistoryLimit"] = Config.StructuredLogHistoryLimit,
                    ["consoleMirrorLogging"] = Config.EnableConsoleMirrorLogging,
                    ["consoleMirrorLogPath"] = Config.ConsoleMirrorLogPath,
                    ["consoleMirrorLogHistoryLimit"] = Config.ConsoleMirrorLogHistoryLimit,
                    ["commandBus"] = nameof(LocalCommandBus),
                    ["queryBus"] = nameof(LocalQueryBus),
                    ["projectionStore"] = nameof(ProjectionStore),
                    ["projectionRefreshCoordinator"] = nameof(ProjectionRefreshCoordinator),
                    ["registeredProjectionUpdaters"] = Context.ProjectionRefreshCoordinator.Count,
                    ["runtimeProbeGateway"] = nameof(InMemoryFacetRuntimeProbeGateway),
                    ["registeredPages"] = Services.GetRequired<UIPageRegistry>().Count,
                    ["pageDefinitionSource"] = nameof(InMemoryPageDefinitionSource),
                    ["layoutProviders"] = string.Join(",", nameof(SceneLayoutProvider), nameof(TemplateLayoutProvider), nameof(GeneratedLayoutProvider)),
                    ["bindingService"] = nameof(UIBindingService),
                    ["routeService"] = nameof(UIRouteService),
                    ["backStackDepth"] = Services.GetRequired<UIRouteService>().Count,
                });

            VerifyApplicationBoundary();
            VerifyProjectionLayer();
            VerifyPageDefinitions();
            VerifyLuaRuntime();
            VerifyRedDotRuntime();
            VerifyAdvancedLayouts();
            VerifyRuntimeDiagnostics();
        }

        private void PublishHotReloadLabIdleStatus()
        {
            string message = Config.EnableHotReload
                ? "运行时已就绪，等待 Hot Reload Lab 请求。"
                : "运行时已启动，但当前未开启热重载。";

            FacetHotReloadLabBridge.SaveStatus(new FacetHotReloadLabStatus
            {
                State = FacetHotReloadLabBridge.StateIdle,
                Success = null,
                Message = message,
                UpdatedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
                RuntimeSessionId = CurrentSessionId,
                RuntimePageId = Services.TryGet(out UIManager? uiManager) ? uiManager?.CurrentPageId ?? string.Empty : string.Empty,
            });
        }

        private void PublishLayoutLabIdleStatus()
        {
            FacetLayoutLabBridge.SaveStatus(new FacetLayoutLabStatus
            {
                State = FacetLayoutLabBridge.StateIdle,
                Success = null,
                Message = "运行时已就绪，等待 Layout Lab 请求。",
                UpdatedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
                RuntimeSessionId = CurrentSessionId,
                RuntimePageId = Services.TryGet(out UIManager? uiManager) ? uiManager?.CurrentPageId ?? string.Empty : string.Empty,
            });
        }

        private void PublishRuntimeDiagnosticsSnapshot(bool force = false)
        {
            try
            {
                FacetRuntimeDiagnosticsSnapshot snapshot = CreateRuntimeDiagnosticsSnapshot();
                string fingerprint = CreateRuntimeDiagnosticsFingerprint(snapshot);
                if (!force && string.Equals(_lastRuntimeDiagnosticsFingerprint, fingerprint, StringComparison.Ordinal))
                {
                    return;
                }

                FacetRuntimeDiagnosticsBridge.SaveSnapshot(snapshot);
                _lastRuntimeDiagnosticsFingerprint = fingerprint;
            }
            catch (Exception exception)
            {
                Logger.Error(
                    "Tooling.Diagnostics",
                    "运行时诊断快照写入失败。",
                    new Dictionary<string, object?>
                    {
                        ["exceptionType"] = exception.GetType().FullName,
                        ["message"] = exception.Message,
                    });
            }
        }

        private FacetRuntimeDiagnosticsSnapshot CreateRuntimeDiagnosticsSnapshot()
        {
            UIPageRegistry pageRegistry = Services.GetRequired<UIPageRegistry>();
            UIManager uiManager = Services.GetRequired<UIManager>();
            ProjectionStore projectionStore = Services.GetRequired<ProjectionStore>();
            ILuaScriptSource luaScriptSource = Services.GetRequired<ILuaScriptSource>();
            IRedDotService redDotService = Services.GetRequired<IRedDotService>();
            IFacetGeneratedLayoutStore generatedLayoutStore = Services.GetRequired<IFacetGeneratedLayoutStore>();
            IFacetTemplateLayoutStore templateLayoutStore = Services.GetRequired<IFacetTemplateLayoutStore>();

            List<FacetRuntimeRegisteredPageSnapshot> registeredPages = new();
            foreach (UIPageDefinition definition in pageRegistry.GetAll())
            {
                registeredPages.Add(new FacetRuntimeRegisteredPageSnapshot
                {
                    PageId = definition.PageId,
                    LayoutType = definition.LayoutType.ToString(),
                    LayoutPath = definition.LayoutPath,
                    Layer = definition.Layer,
                    CachePolicy = definition.CachePolicy.ToString(),
                    ControllerScript = definition.ControllerScript ?? string.Empty,
                });
            }

            registeredPages.Sort(static (left, right) => string.Compare(left.PageId, right.PageId, StringComparison.OrdinalIgnoreCase));

            List<FacetRuntimePageRuntimeSnapshot> activeRuntimes = new();
            foreach (UIPageRuntime runtime in uiManager.GetPageRuntimesSnapshot())
            {
                UIBindingDiagnosticsSnapshot? bindingDiagnostics = runtime.BindingScope?.GetDiagnosticsSnapshot();
                activeRuntimes.Add(new FacetRuntimePageRuntimeSnapshot
                {
                    PageId = runtime.Definition.PageId,
                    IsCurrentPage = string.Equals(runtime.Definition.PageId, uiManager.CurrentPageId, StringComparison.OrdinalIgnoreCase),
                    State = runtime.State.ToString(),
                    LayoutType = runtime.Definition.LayoutType.ToString(),
                    ControllerScript = runtime.LuaControllerScript ?? runtime.Definition.ControllerScript ?? string.Empty,
                    HasLuaController = runtime.HasLuaController,
                    LuaControllerVersionToken = runtime.LuaControllerVersionToken ?? string.Empty,
                    PageRootPath = runtime.PageRoot.GetPath().ToString(),
                    BindingScope = bindingDiagnostics == null
                        ? null
                        : new FacetRuntimeBindingScopeSnapshot
                        {
                            ScopeId = bindingDiagnostics.ScopeId,
                            BindingCount = bindingDiagnostics.BindingCount,
                            RefreshCount = bindingDiagnostics.RefreshCount,
                            LastRefreshReason = bindingDiagnostics.LastRefreshReason ?? string.Empty,
                        },
                });
            }

            activeRuntimes.Sort(static (left, right) =>
            {
                int currentComparison = right.IsCurrentPage.CompareTo(left.IsCurrentPage);
                return currentComparison != 0
                    ? currentComparison
                    : string.Compare(left.PageId, right.PageId, StringComparison.OrdinalIgnoreCase);
            });

            List<string> projectionKeys = new();
            foreach (ProjectionKey key in projectionStore.GetKeysSnapshot())
            {
                projectionKeys.Add(key.ToString());
            }

            List<string> luaRegisteredScripts = new(luaScriptSource.GetRegisteredScripts());
            luaRegisteredScripts.Sort(StringComparer.OrdinalIgnoreCase);

            List<string> redDotPaths = new(redDotService.GetRegisteredPaths());
            redDotPaths.Sort(StringComparer.OrdinalIgnoreCase);

            List<FacetRuntimeValidationResultSnapshot> validationResults = CreateRuntimeValidationResults(
                pageRegistry,
                uiManager,
                projectionStore,
                luaScriptSource,
                redDotService,
                generatedLayoutStore,
                templateLayoutStore,
                registeredPages,
                activeRuntimes,
                projectionKeys,
                redDotPaths);

            int validationPassedCount = CountValidationResults(validationResults, "Pass");
            int validationWarningCount = CountValidationResults(validationResults, "Warning");
            int validationFailedCount = CountValidationResults(validationResults, "Fail");

            return new FacetRuntimeDiagnosticsSnapshot
            {
                RuntimeSessionId = CurrentSessionId,
                UpdatedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
                CurrentPageId = uiManager.CurrentPageId ?? string.Empty,
                BackStackDepth = uiManager.BackStackDepth,
                RegisteredPageCount = registeredPages.Count,
                RegisteredPages = registeredPages,
                ActiveRuntimeCount = activeRuntimes.Count,
                ActiveRuntimes = activeRuntimes,
                ProjectionCount = projectionStore.Count,
                ProjectionKeys = projectionKeys,
                LuaRegisteredScriptCount = luaRegisteredScripts.Count,
                LuaRegisteredScripts = luaRegisteredScripts,
                RedDotRegisteredPathCount = redDotPaths.Count,
                RedDotPaths = redDotPaths,
                ValidationResultCount = validationResults.Count,
                ValidationPassedCount = validationPassedCount,
                ValidationWarningCount = validationWarningCount,
                ValidationFailedCount = validationFailedCount,
                ValidationResults = validationResults,
            };
        }

        private static List<FacetRuntimeValidationResultSnapshot> CreateRuntimeValidationResults(
            UIPageRegistry pageRegistry,
            UIManager uiManager,
            ProjectionStore projectionStore,
            ILuaScriptSource luaScriptSource,
            IRedDotService redDotService,
            IFacetGeneratedLayoutStore generatedLayoutStore,
            IFacetTemplateLayoutStore templateLayoutStore,
            List<FacetRuntimeRegisteredPageSnapshot> registeredPages,
            List<FacetRuntimePageRuntimeSnapshot> activeRuntimes,
            List<string> projectionKeys,
            List<string> redDotPaths)
        {
            List<FacetRuntimeValidationResultSnapshot> results = new();

            results.Add(CreateValidationResult(
                "page_registry.not_empty",
                isSuccess: registeredPages.Count > 0,
                subject: "UIPageRegistry",
                successMessage: $"已注册 {registeredPages.Count} 个页面。",
                failureMessage: "页面注册表为空。"));

            results.Add(CreateValidationResult(
                "projection.count_consistent",
                isSuccess: projectionStore.Count == projectionKeys.Count,
                subject: "ProjectionStore",
                successMessage: $"Projection 数量与键快照一致：{projectionStore.Count}。",
                failureMessage: $"Projection 数量与键快照不一致：count={projectionStore.Count}, keys={projectionKeys.Count}。"));

            results.Add(CreateValidationResult(
                "red_dot.count_consistent",
                isSuccess: redDotService.RegisteredPathCount == redDotPaths.Count,
                subject: "RedDotService",
                successMessage: $"红点路径数量一致：{redDotPaths.Count}。",
                failureMessage: $"红点路径数量不一致：registered={redDotService.RegisteredPathCount}, snapshot={redDotPaths.Count}。"));

            foreach (FacetRuntimeRegisteredPageSnapshot page in registeredPages)
            {
                bool layoutResolved = page.LayoutType switch
                {
                    nameof(UIPageLayoutType.Generated) => generatedLayoutStore.TryGet(page.LayoutPath, out _),
                    nameof(UIPageLayoutType.Template) => templateLayoutStore.TryGet(page.LayoutPath, out _),
                    _ => true,
                };

                results.Add(CreateValidationResult(
                    "page.layout_resolved",
                    isSuccess: layoutResolved,
                    subject: page.PageId,
                    successMessage: $"布局已解析：{page.LayoutPath}。",
                    failureMessage: $"布局未注册：{page.LayoutPath}。"));

                if (!string.IsNullOrWhiteSpace(page.ControllerScript))
                {
                    bool scriptRegistered = luaScriptSource.TryGetVersionToken(page.ControllerScript, out _);
                    results.Add(CreateValidationResult(
                        "page.lua_script_registered",
                        isSuccess: scriptRegistered,
                        subject: page.PageId,
                        successMessage: $"Lua 脚本已注册：{page.ControllerScript}。",
                        failureMessage: $"Lua 脚本未注册：{page.ControllerScript}。"));
                }
                else
                {
                    results.Add(CreateValidationResult(
                        "page.lua_script_optional",
                        isSuccess: true,
                        subject: page.PageId,
                        successMessage: "页面未绑定 Lua 脚本。",
                        failureMessage: string.Empty,
                        severity: "Info"));
                }
            }

            foreach (FacetRuntimePageRuntimeSnapshot runtime in activeRuntimes)
            {
                bool pageRegistered = pageRegistry.Contains(runtime.PageId);
                results.Add(CreateValidationResult(
                    "runtime.page_registered",
                    isSuccess: pageRegistered,
                    subject: runtime.PageId,
                    successMessage: "活动运行时对应页面已注册。",
                    failureMessage: "活动运行时对应页面未注册。"));

                bool bindingScopePresent = runtime.BindingScope != null;
                results.Add(CreateValidationResult(
                    "runtime.binding_scope_present",
                    status: bindingScopePresent ? "Pass" : "Warning",
                    subject: runtime.PageId,
                    successMessage: $"BindingScope 已就绪：{runtime.BindingScope?.ScopeId}。",
                    failureMessage: "活动运行时缺少 BindingScope。",
                    severity: "Warning"));
            }

            bool currentPageMatchesRuntime = string.IsNullOrWhiteSpace(uiManager.CurrentPageId) ||
                activeRuntimes.Exists(runtime => string.Equals(runtime.PageId, uiManager.CurrentPageId, StringComparison.OrdinalIgnoreCase));
            results.Add(CreateValidationResult(
                "runtime.current_page_observed",
                status: currentPageMatchesRuntime ? "Pass" : "Warning",
                subject: uiManager.CurrentPageId ?? "<empty>",
                successMessage: "当前页面已出现在活动运行时快照中。",
                failureMessage: "当前页面未出现在活动运行时快照中。",
                severity: "Warning"));

            results.Sort(static (left, right) =>
            {
                int severityComparison = CompareValidationSeverity(left, right);
                return severityComparison != 0
                    ? severityComparison
                    : string.Compare(left.Subject, right.Subject, StringComparison.OrdinalIgnoreCase);
            });

            return results;
        }

        private static FacetRuntimeValidationResultSnapshot CreateValidationResult(
            string ruleId,
            bool isSuccess,
            string subject,
            string successMessage,
            string failureMessage,
            string severity = "Error")
        {
            return CreateValidationResult(
                ruleId,
                isSuccess ? "Pass" : "Fail",
                subject,
                successMessage,
                failureMessage,
                severity);
        }

        private static FacetRuntimeValidationResultSnapshot CreateValidationResult(
            string ruleId,
            string status,
            string subject,
            string successMessage,
            string failureMessage,
            string severity = "Error")
        {
            bool isSuccess = string.Equals(status, "Pass", StringComparison.OrdinalIgnoreCase);
            return new FacetRuntimeValidationResultSnapshot
            {
                RuleId = ruleId,
                Severity = isSuccess ? "Info" : severity,
                Status = status,
                Subject = subject,
                Message = isSuccess ? successMessage : failureMessage,
            };
        }

        private static int CountValidationResults(List<FacetRuntimeValidationResultSnapshot> results, string status)
        {
            int count = 0;
            foreach (FacetRuntimeValidationResultSnapshot result in results)
            {
                if (string.Equals(result.Status, status, StringComparison.OrdinalIgnoreCase))
                {
                    count++;
                }
            }

            return count;
        }

        private static int CompareValidationSeverity(FacetRuntimeValidationResultSnapshot left, FacetRuntimeValidationResultSnapshot right)
        {
            return GetValidationSeverityRank(left).CompareTo(GetValidationSeverityRank(right));
        }

        private static int GetValidationSeverityRank(FacetRuntimeValidationResultSnapshot result)
        {
            if (string.Equals(result.Status, "Warning", StringComparison.OrdinalIgnoreCase))
            {
                return 1;
            }

            if (string.Equals(result.Status, "Fail", StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            return 2;
        }

        private static string CreateRuntimeDiagnosticsFingerprint(FacetRuntimeDiagnosticsSnapshot snapshot)
        {
            StringBuilder builder = new();
            builder.Append(snapshot.RuntimeSessionId);
            builder.Append('|');
            builder.Append(snapshot.CurrentPageId);
            builder.Append('|');
            builder.Append(snapshot.BackStackDepth);
            builder.Append('|');
            builder.Append(snapshot.RegisteredPageCount);
            builder.Append('|');
            builder.Append(snapshot.ActiveRuntimeCount);
            builder.Append('|');
            builder.Append(snapshot.ProjectionCount);
            builder.Append('|');
            builder.Append(snapshot.LuaRegisteredScriptCount);
            builder.Append('|');
            builder.Append(snapshot.RedDotRegisteredPathCount);
            builder.Append('|');
            builder.Append(snapshot.ValidationResultCount);
            builder.Append('|');
            builder.Append(snapshot.ValidationPassedCount);
            builder.Append('|');
            builder.Append(snapshot.ValidationWarningCount);
            builder.Append('|');
            builder.Append(snapshot.ValidationFailedCount);

            foreach (FacetRuntimeRegisteredPageSnapshot page in snapshot.RegisteredPages)
            {
                builder.Append("|page:");
                builder.Append(page.PageId);
                builder.Append(':');
                builder.Append(page.LayoutType);
                builder.Append(':');
                builder.Append(page.LayoutPath);
                builder.Append(':');
                builder.Append(page.Layer);
                builder.Append(':');
                builder.Append(page.CachePolicy);
                builder.Append(':');
                builder.Append(page.ControllerScript);
            }

            foreach (FacetRuntimePageRuntimeSnapshot runtime in snapshot.ActiveRuntimes)
            {
                builder.Append("|runtime:");
                builder.Append(runtime.PageId);
                builder.Append(':');
                builder.Append(runtime.IsCurrentPage);
                builder.Append(':');
                builder.Append(runtime.State);
                builder.Append(':');
                builder.Append(runtime.LayoutType);
                builder.Append(':');
                builder.Append(runtime.ControllerScript);
                builder.Append(':');
                builder.Append(runtime.HasLuaController);
                builder.Append(':');
                builder.Append(runtime.LuaControllerVersionToken);
                builder.Append(':');
                builder.Append(runtime.PageRootPath);

                if (runtime.BindingScope != null)
                {
                    builder.Append(':');
                    builder.Append(runtime.BindingScope.ScopeId);
                    builder.Append(':');
                    builder.Append(runtime.BindingScope.BindingCount);
                    builder.Append(':');
                    builder.Append(runtime.BindingScope.RefreshCount);
                    builder.Append(':');
                    builder.Append(runtime.BindingScope.LastRefreshReason);
                }
            }

            foreach (string projectionKey in snapshot.ProjectionKeys)
            {
                builder.Append("|projection:");
                builder.Append(projectionKey);
            }

            foreach (string script in snapshot.LuaRegisteredScripts)
            {
                builder.Append("|lua:");
                builder.Append(script);
            }

            foreach (string path in snapshot.RedDotPaths)
            {
                builder.Append("|red_dot:");
                builder.Append(path);
            }

            foreach (FacetRuntimeValidationResultSnapshot result in snapshot.ValidationResults)
            {
                builder.Append("|validation:");
                builder.Append(result.Status);
                builder.Append(':');
                builder.Append(result.Severity);
                builder.Append(':');
                builder.Append(result.RuleId);
                builder.Append(':');
                builder.Append(result.Subject);
                builder.Append(':');
                builder.Append(result.Message);
            }

            return builder.ToString();
        }

        private void PollEditorHotReloadLabRequests()
        {
            if (!FacetHotReloadLabBridge.TryLoadRequest(out FacetHotReloadLabRequest? request) ||
                request == null ||
                string.IsNullOrWhiteSpace(request.RequestId))
            {
                return;
            }

            FacetHotReloadLabBridge.TryLoadStatus(out FacetHotReloadLabStatus? status);
            if (!FacetHotReloadLabBridge.IsPending(request, status))
            {
                return;
            }

            if (string.Equals(_lastHandledHotReloadLabRequestId, request.RequestId, StringComparison.Ordinal))
            {
                return;
            }

            _lastHandledHotReloadLabRequestId = request.RequestId;
            HandleHotReloadLabRequest(request);
        }

        private void PollEditorLayoutLabRequests()
        {
            if (!FacetLayoutLabBridge.TryLoadRequest(out FacetLayoutLabRequest? request) ||
                request == null ||
                string.IsNullOrWhiteSpace(request.RequestId))
            {
                return;
            }

            FacetLayoutLabBridge.TryLoadStatus(out FacetLayoutLabStatus? status);
            if (!FacetLayoutLabBridge.IsPending(request, status))
            {
                return;
            }

            if (string.Equals(_lastHandledLayoutLabRequestId, request.RequestId, StringComparison.Ordinal))
            {
                return;
            }

            _lastHandledLayoutLabRequestId = request.RequestId;
            HandleLayoutLabRequest(request);
        }

        private void HandleHotReloadLabRequest(FacetHotReloadLabRequest request)
        {
            string currentPageId = Services.TryGet(out UIManager? uiManager)
                ? uiManager?.CurrentPageId ?? string.Empty
                : string.Empty;

            FacetHotReloadLabBridge.SaveStatus(new FacetHotReloadLabStatus
            {
                RequestId = request.RequestId,
                Command = request.Command,
                State = FacetHotReloadLabBridge.StateRunning,
                Success = null,
                Message = "运行时已接收请求，正在执行 Lua 热重载往返测试。",
                IssuedBy = request.IssuedBy,
                IssuedAtUtc = request.IssuedAtUtc,
                UpdatedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
                RuntimeSessionId = CurrentSessionId,
                RuntimePageId = currentPageId,
            });

            try
            {
                if (!Config.EnableHotReload)
                {
                    FacetHotReloadLabBridge.SaveStatus(new FacetHotReloadLabStatus
                    {
                        RequestId = request.RequestId,
                        Command = request.Command,
                        State = FacetHotReloadLabBridge.StateIgnored,
                        Success = false,
                        Message = "运行时未开启热重载，无法执行阶段 9 测试。",
                        IssuedBy = request.IssuedBy,
                        IssuedAtUtc = request.IssuedAtUtc,
                        UpdatedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
                        RuntimeSessionId = CurrentSessionId,
                        RuntimePageId = currentPageId,
                    });
                    return;
                }

                bool success = request.Command switch
                {
                    FacetHotReloadLabBridge.CommandCurrentPageRoundTrip => TryRunLuaHotReloadRoundTripTest(reason: "editor.lab.current"),
                    FacetHotReloadLabBridge.CommandDungeonRoundTrip => TryRunDungeonLuaHotReloadRoundTripTest(reason: "editor.lab.dungeon"),
                    _ => false,
                };

                string state = success
                    ? FacetHotReloadLabBridge.StateCompleted
                    : request.Command switch
                    {
                        FacetHotReloadLabBridge.CommandCurrentPageRoundTrip => FacetHotReloadLabBridge.StateFailed,
                        FacetHotReloadLabBridge.CommandDungeonRoundTrip => FacetHotReloadLabBridge.StateFailed,
                        _ => FacetHotReloadLabBridge.StateIgnored,
                    };

                string message = request.Command switch
                {
                    FacetHotReloadLabBridge.CommandCurrentPageRoundTrip when success => "当前页 Lua 热重载往返测试已通过。",
                    FacetHotReloadLabBridge.CommandDungeonRoundTrip when success => "地下城页 Lua 热重载往返测试已通过。",
                    FacetHotReloadLabBridge.CommandCurrentPageRoundTrip => "当前页 Lua 热重载往返测试未通过，请查看 Lua.HotReload.Test 日志。",
                    FacetHotReloadLabBridge.CommandDungeonRoundTrip => "地下城页 Lua 热重载往返测试未通过，请查看 Lua.HotReload.Test 日志。",
                    _ => "收到未知的 Hot Reload Lab 命令，已忽略。",
                };

                currentPageId = Services.TryGet(out uiManager)
                    ? uiManager?.CurrentPageId ?? string.Empty
                    : currentPageId;

                FacetHotReloadLabBridge.SaveStatus(new FacetHotReloadLabStatus
                {
                    RequestId = request.RequestId,
                    Command = request.Command,
                    State = state,
                    Success = success,
                    Message = message,
                    IssuedBy = request.IssuedBy,
                    IssuedAtUtc = request.IssuedAtUtc,
                    UpdatedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
                    RuntimeSessionId = CurrentSessionId,
                    RuntimePageId = currentPageId,
                });
            }
            finally
            {
                FacetHotReloadLabBridge.DeleteRequest();
            }
        }

        private void HandleLayoutLabRequest(FacetLayoutLabRequest request)
        {
            string currentPageId = Services.TryGet(out UIManager? uiManager)
                ? uiManager?.CurrentPageId ?? string.Empty
                : string.Empty;

            FacetLayoutLabBridge.SaveStatus(new FacetLayoutLabStatus
            {
                RequestId = request.RequestId,
                Command = request.Command,
                State = FacetLayoutLabBridge.StateRunning,
                Success = null,
                Message = "运行时已接收请求，正在打开阶段 11 布局实验页面。",
                IssuedBy = request.IssuedBy,
                IssuedAtUtc = request.IssuedAtUtc,
                UpdatedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
                RuntimeSessionId = CurrentSessionId,
                RuntimePageId = currentPageId,
            });

            try
            {
                string? targetPageId = request.Command switch
                {
                    FacetLayoutLabBridge.CommandOpenGeneratedLayoutLab => UIPageIds.GeneratedLayoutLab,
                    FacetLayoutLabBridge.CommandOpenTemplateLayoutLab => UIPageIds.TemplateLayoutLab,
                    _ => null,
                };

                if (string.IsNullOrWhiteSpace(targetPageId))
                {
                    FacetLayoutLabBridge.SaveStatus(new FacetLayoutLabStatus
                    {
                        RequestId = request.RequestId,
                        Command = request.Command,
                        State = FacetLayoutLabBridge.StateIgnored,
                        Success = false,
                        Message = "收到未知的 Layout Lab 命令，已忽略。",
                        IssuedBy = request.IssuedBy,
                        IssuedAtUtc = request.IssuedAtUtc,
                        UpdatedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
                        RuntimeSessionId = CurrentSessionId,
                        RuntimePageId = currentPageId,
                    });
                    return;
                }

                if (uiManager == null)
                {
                    FacetLayoutLabBridge.SaveStatus(new FacetLayoutLabStatus
                    {
                        RequestId = request.RequestId,
                        Command = request.Command,
                        State = FacetLayoutLabBridge.StateFailed,
                        Success = false,
                        Message = "运行时 UIManager 尚未就绪，无法打开布局实验页面。",
                        IssuedBy = request.IssuedBy,
                        IssuedAtUtc = request.IssuedAtUtc,
                        UpdatedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
                        RuntimeSessionId = CurrentSessionId,
                        RuntimePageId = currentPageId,
                    });
                    return;
                }

                UIPageRuntime runtime = uiManager.Open(targetPageId, arguments: null, pushHistory: false);
                currentPageId = uiManager.CurrentPageId ?? currentPageId;

                Logger.Info(
                    "UI.Layout",
                    "布局实验室页面打开完成。",
                    new Dictionary<string, object?>
                    {
                        ["requestId"] = request.RequestId,
                        ["command"] = request.Command,
                        ["issuedBy"] = request.IssuedBy,
                        ["pageId"] = runtime.Definition.PageId,
                        ["layoutType"] = runtime.Definition.LayoutType.ToString(),
                        ["registeredNodes"] = runtime.NodeRegistry.Count,
                        ["currentPageId"] = currentPageId,
                    });

                FacetLayoutLabBridge.SaveStatus(new FacetLayoutLabStatus
                {
                    RequestId = request.RequestId,
                    Command = request.Command,
                    State = FacetLayoutLabBridge.StateCompleted,
                    Success = true,
                    Message = $"已打开布局实验页面：{runtime.Definition.PageId}",
                    IssuedBy = request.IssuedBy,
                    IssuedAtUtc = request.IssuedAtUtc,
                    UpdatedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
                    RuntimeSessionId = CurrentSessionId,
                    RuntimePageId = currentPageId,
                });
            }
            catch (Exception exception)
            {
                Logger.Error(
                    "UI.Layout",
                    "布局实验室页面打开失败。",
                    new Dictionary<string, object?>
                    {
                        ["requestId"] = request.RequestId,
                        ["command"] = request.Command,
                        ["issuedBy"] = request.IssuedBy,
                        ["currentPageId"] = currentPageId,
                        ["exceptionType"] = exception.GetType().FullName,
                        ["message"] = exception.Message,
                    });

                FacetLayoutLabBridge.SaveStatus(new FacetLayoutLabStatus
                {
                    RequestId = request.RequestId,
                    Command = request.Command,
                    State = FacetLayoutLabBridge.StateFailed,
                    Success = false,
                    Message = $"打开布局实验页面失败：{exception.Message}",
                    IssuedBy = request.IssuedBy,
                    IssuedAtUtc = request.IssuedAtUtc,
                    UpdatedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
                    RuntimeSessionId = CurrentSessionId,
                    RuntimePageId = currentPageId,
                });
            }
            finally
            {
                FacetLayoutLabBridge.DeleteRequest();
            }
        }

        private void CleanupConsumedLabRequests()
        {
            CleanupConsumedHotReloadLabRequest();
            CleanupConsumedLayoutLabRequest();
        }

        private void CleanupConsumedHotReloadLabRequest()
        {
            if (!FacetHotReloadLabBridge.TryLoadRequest(out FacetHotReloadLabRequest? request) ||
                request == null ||
                string.IsNullOrWhiteSpace(request.RequestId))
            {
                return;
            }

            if (!FacetHotReloadLabBridge.TryLoadStatus(out FacetHotReloadLabStatus? status) ||
                status == null)
            {
                return;
            }

            if (!string.Equals(request.RequestId, status.RequestId, StringComparison.Ordinal) ||
                !FacetHotReloadLabBridge.IsTerminalState(status.State))
            {
                return;
            }

            FacetHotReloadLabBridge.DeleteRequest();
        }

        private void CleanupConsumedLayoutLabRequest()
        {
            if (!FacetLayoutLabBridge.TryLoadRequest(out FacetLayoutLabRequest? request) ||
                request == null ||
                string.IsNullOrWhiteSpace(request.RequestId))
            {
                return;
            }

            if (!FacetLayoutLabBridge.TryLoadStatus(out FacetLayoutLabStatus? status) ||
                status == null)
            {
                return;
            }

            if (!string.Equals(request.RequestId, status.RequestId, StringComparison.Ordinal) ||
                !FacetLayoutLabBridge.IsTerminalState(status.State))
            {
                return;
            }

            FacetLayoutLabBridge.DeleteRequest();
        }

        private void VerifyApplicationBoundary()
        {
            try
            {
                AppResult<FacetRuntimeProbeSnapshot> currentProbeResult = Context.QueryBus
                    .QueryAsync(new FacetRuntimeProbeQuery())
                    .GetAwaiter()
                    .GetResult();

                if (!currentProbeResult.IsSuccess || currentProbeResult.Value == null)
                {
                    Logger.Warning(
                        "Application",
                        "Facet 阶段 2 当前快照查询失败。",
                        new Dictionary<string, object?>
                        {
                            ["errorCode"] = currentProbeResult.ErrorCode,
                            ["errorMessage"] = currentProbeResult.ErrorMessage,
                        });
                    return;
                }

                FacetRuntimeProbeSnapshot snapshot = currentProbeResult.Value;
                AppResult recordResult = Context.CommandBus
                    .SendAsync(new RecordFacetRuntimeProbeCommand(snapshot))
                    .GetAwaiter()
                    .GetResult();

                if (!recordResult.IsSuccess)
                {
                    Logger.Warning(
                        "Application",
                        "Facet 阶段 2 探针记录命令失败。",
                        new Dictionary<string, object?>
                        {
                            ["errorCode"] = recordResult.ErrorCode,
                            ["errorMessage"] = recordResult.ErrorMessage,
                        });
                    return;
                }

                AppResult<FacetRuntimeProbeStatusSnapshot> statusResult = Context.QueryBus
                    .QueryAsync(new FacetRuntimeProbeStatusQuery())
                    .GetAwaiter()
                    .GetResult();

                if (!statusResult.IsSuccess || statusResult.Value == null)
                {
                    Logger.Warning(
                        "Application",
                        "Facet 阶段 2 探针状态查询失败。",
                        new Dictionary<string, object?>
                        {
                            ["errorCode"] = statusResult.ErrorCode,
                            ["errorMessage"] = statusResult.ErrorMessage,
                        });
                    return;
                }

                FacetRuntimeProbeStatusSnapshot status = statusResult.Value;
                Logger.Info(
                    "Application",
                    "Facet 阶段 2 应用边界闭环验证成功。",
                    new Dictionary<string, object?>
                    {
                        ["sessionId"] = snapshot.SessionId,
                        ["hotReload"] = snapshot.HotReloadEnabled,
                        ["pageCache"] = snapshot.PageCacheEnabled,
                        ["pageCacheCapacity"] = snapshot.PageCacheCapacity,
                        ["structuredLogging"] = snapshot.StructuredLoggingEnabled,
                        ["structuredLogPath"] = snapshot.StructuredLogPath,
                        ["commandBusRegistered"] = snapshot.CommandBusRegistered,
                        ["queryBusRegistered"] = snapshot.QueryBusRegistered,
                        ["capturedAtUtc"] = snapshot.CapturedAtUtc.ToString("O"),
                        ["probeRecorded"] = status.HasSnapshot,
                        ["probeRecordCount"] = status.RecordedCount,
                    });
            }
            catch (Exception exception)
            {
                Logger.Error(
                    "Application",
                    "Facet 阶段 2 应用边界自检抛出异常。",
                    new Dictionary<string, object?>
                    {
                        ["exceptionType"] = exception.GetType().FullName,
                        ["message"] = exception.Message,
                    });
            }
        }

        private void VerifyProjectionLayer()
        {
            try
            {
                bool probeSubscriberTriggered = false;
                bool metricsSubscriberTriggered = false;
                ProjectionChange? probeObservedChange = null;
                ProjectionChange? metricsObservedChange = null;

                using IDisposable probeSubscription = Context.ProjectionStore.Subscribe(
                    RuntimeProbeProjectionKey,
                    change =>
                    {
                        probeSubscriberTriggered = true;
                        probeObservedChange = change;
                    });

                using IDisposable metricsSubscription = Context.ProjectionStore.Subscribe(
                    RuntimeMetricsProjectionKey,
                    change =>
                    {
                        metricsSubscriberTriggered = true;
                        metricsObservedChange = change;
                    });

                int refreshedCount = Context.ProjectionRefreshCoordinator
                    .RefreshAllAsync()
                    .GetAwaiter()
                    .GetResult();

                bool probeProjectionExists = Context.ProjectionStore.TryGet(RuntimeProbeProjectionKey, out FacetRuntimeProbeProjection? storedProbeProjection);
                bool metricsProjectionExists = Context.ProjectionStore.TryGet(RuntimeMetricsProjectionKey, out FacetRuntimeMetricListProjection? storedMetricsProjection);

                Logger.Info(
                    "Projection",
                    "Facet 阶段 3 Projection 骨架验证成功。",
                    new Dictionary<string, object?>
                    {
                        ["registeredUpdaters"] = Context.ProjectionRefreshCoordinator.Count,
                        ["refreshedCount"] = refreshedCount,
                        ["projectionKey"] = RuntimeProbeProjectionKey.ToString(),
                        ["subscriberTriggered"] = probeSubscriberTriggered,
                        ["observedChangeKind"] = probeObservedChange?.Kind.ToString(),
                        ["projectionExists"] = probeProjectionExists,
                        ["projectionCount"] = Context.ProjectionStore.Count,
                        ["recordedCount"] = storedProbeProjection?.RecordedCount,
                        ["hasSnapshot"] = storedProbeProjection?.HasSnapshot,
                        ["metricsProjectionKey"] = RuntimeMetricsProjectionKey.ToString(),
                        ["metricsSubscriberTriggered"] = metricsSubscriberTriggered,
                        ["metricsObservedChangeKind"] = metricsObservedChange?.Kind.ToString(),
                        ["metricsProjectionExists"] = metricsProjectionExists,
                        ["metricsItemCount"] = storedMetricsProjection?.Items.Count,
                    });
            }
            catch (Exception exception)
            {
                Logger.Error(
                    "Projection",
                    "Facet 阶段 3 Projection 骨架验证抛出异常。",
                    new Dictionary<string, object?>
                    {
                        ["exceptionType"] = exception.GetType().FullName,
                        ["message"] = exception.Message,
                    });
            }
        }

        private void VerifyPageDefinitions()
        {
            try
            {
                UIPageRegistry pageRegistry = Services.GetRequired<UIPageRegistry>();

                Logger.Info(
                    "UI.Page",
                    "Facet 阶段 4 页面定义注册完成。",
                    new Dictionary<string, object?>
                    {
                        ["registeredPages"] = pageRegistry.Count,
                        ["containsIdlePage"] = pageRegistry.Contains(UIPageIds.Idle),
                        ["containsDungeonPage"] = pageRegistry.Contains(UIPageIds.Dungeon),
                        ["containsGeneratedLayoutLab"] = pageRegistry.Contains(UIPageIds.GeneratedLayoutLab),
                        ["containsTemplateLayoutLab"] = pageRegistry.Contains(UIPageIds.TemplateLayoutLab),
                        ["routeServiceReady"] = Services.Contains<UIRouteService>(),
                    });
            }
            catch (Exception exception)
            {
                Logger.Error(
                    "UI.Page",
                    "Facet 阶段 4 页面定义注册验证失败。",
                    new Dictionary<string, object?>
                    {
                        ["exceptionType"] = exception.GetType().FullName,
                        ["message"] = exception.Message,
                    });
            }
        }

        private void VerifyLuaRuntime()
        {
            try
            {
                ILuaScriptSource scriptSource = Services.GetRequired<ILuaScriptSource>();
                Logger.Info(
                    "Lua.Runtime",
                    "Facet 阶段 8 真实 Lua 宿主已就绪。",
                    new Dictionary<string, object?>
                    {
                        ["scriptSourceType"] = scriptSource.GetType().Name,
                        ["runtimeHostType"] = Services.GetRequired<ILuaRuntimeHost>().GetType().Name,
                        ["reloadCoordinatorReady"] = Services.Contains<LuaReloadCoordinator>(),
                        ["hotReloadEnabled"] = Config.EnableHotReload,
                        ["hotReloadPollIntervalSeconds"] = Config.HotReloadPollIntervalSeconds,
                        ["registeredScriptCount"] = scriptSource.GetRegisteredScripts().Count,
                        ["registeredScripts"] = string.Join(",", scriptSource.GetRegisteredScripts()),
                        ["routeBridgeReady"] = Services.Contains<IUIPageNavigator>(),
                        ["bindingBridgeReady"] = Services.Contains<UIBindingService>(),
                        ["redDotBridgeReady"] = Services.Contains<ILuaRedDotBridge>(),
                        ["hotReloadTestServiceReady"] = Services.Contains<LuaHotReloadTestService>(),
                        ["hotReloadTestShortcutEnabled"] = EnableLuaHotReloadTestShortcut,
                        ["hotReloadCurrentPageTestShortcut"] = EnableLuaHotReloadTestShortcut ? LuaHotReloadCurrentPageTestShortcutDescription : string.Empty,
                        ["hotReloadDungeonTestShortcut"] = EnableLuaHotReloadTestShortcut ? LuaHotReloadDungeonTestShortcutDescription : string.Empty,
                    });
            }
            catch (Exception exception)
            {
                Logger.Error(
                    "Lua.Runtime",
                    "Facet 阶段 8 Lua 宿主验证失败。",
                    new Dictionary<string, object?>
                    {
                        ["exceptionType"] = exception.GetType().FullName,
                        ["message"] = exception.Message,
                    });
            }
        }

        private void VerifyRedDotRuntime()
        {
            try
            {
                IRedDotService redDotService = Services.GetRequired<IRedDotService>();
                Logger.Info(
                    "RedDot.Runtime",
                    "Facet 阶段 10 红点树运行时已接入。",
                    new Dictionary<string, object?>
                    {
                        ["serviceType"] = redDotService.GetType().Name,
                        ["providerCount"] =
                            (Services.Contains<FacetRuntimeRedDotProvider>() ? 1 : 0) +
                            (Services.Contains<ManualRedDotProvider>() ? 1 : 0),
                        ["registeredPathCount"] = redDotService.RegisteredPathCount,
                        ["registeredPathsPreview"] = string.Join(",", redDotService.GetRegisteredPaths()),
                        ["luaBridgeAvailable"] = Services.GetRequired<ILuaRedDotBridge>().IsAvailable,
                    });
            }
            catch (Exception exception)
            {
                Logger.Error(
                    "RedDot.Runtime",
                    "Facet 阶段 10 红点树运行时验证失败。",
                    new Dictionary<string, object?>
                    {
                        ["exceptionType"] = exception.GetType().FullName,
                        ["message"] = exception.Message,
                    });
            }
        }

        private void VerifyAdvancedLayouts()
        {
            Control verificationMountRoot = new()
            {
                Name = "FacetLayoutVerificationMount",
                Visible = false,
            };
            AddChild(verificationMountRoot);

            try
            {
                UIPageLoader pageLoader = Services.GetRequired<UIPageLoader>();
                UIPageRegistry pageRegistry = Services.GetRequired<UIPageRegistry>();

                UILayoutResult generatedLayout = LoadVerificationLayout(
                    pageLoader,
                    pageRegistry.GetRequired(UIPageIds.GeneratedLayoutLab),
                    verificationMountRoot);
                VerifyRequiredNode(generatedLayout, "GeneratedTitleLabel");
                VerifyRequiredNode(generatedLayout, "GeneratedPrimaryButton");

                UILayoutResult templateLayout = LoadVerificationLayout(
                    pageLoader,
                    pageRegistry.GetRequired(UIPageIds.TemplateLayoutLab),
                    verificationMountRoot);
                VerifyRequiredNode(templateLayout, "TemplateShellTitleLabel");
                VerifyRequiredNode(templateLayout, "TemplateContentSlot");
                VerifyRequiredNode(templateLayout, "TemplateContentTitleLabel");
                VerifyRequiredNode(templateLayout, "TemplateActionPrimaryButton");

                Logger.Info(
                    "UI.Layout",
                    "Facet 阶段 11 模板布局与自动布局验证成功。",
                    new Dictionary<string, object?>
                    {
                        ["generatedPageId"] = UIPageIds.GeneratedLayoutLab,
                        ["generatedRegisteredNodes"] = generatedLayout.NodeRegistry.Count,
                        ["templatePageId"] = UIPageIds.TemplateLayoutLab,
                        ["templateRegisteredNodes"] = templateLayout.NodeRegistry.Count,
                        ["generatedOwnsRootNode"] = generatedLayout.OwnsRootNode,
                        ["templateOwnsRootNode"] = templateLayout.OwnsRootNode,
                    });

                ReleaseVerificationLayout(generatedLayout);
                ReleaseVerificationLayout(templateLayout);
            }
            catch (Exception exception)
            {
                Logger.Error(
                    "UI.Layout",
                    "Facet 阶段 11 模板布局与自动布局验证失败。",
                    new Dictionary<string, object?>
                    {
                        ["exceptionType"] = exception.GetType().FullName,
                        ["message"] = exception.Message,
                    });
            }
            finally
            {
                if (GodotObject.IsInstanceValid(verificationMountRoot))
                {
                    verificationMountRoot.QueueFree();
                }
            }
        }

        private void VerifyRuntimeDiagnostics()
        {
            try
            {
                FacetRuntimeDiagnosticsSnapshot snapshot = CreateRuntimeDiagnosticsSnapshot();
                PublishRuntimeDiagnosticsSnapshot(force: true);

                Logger.Info(
                    "Tooling.Diagnostics",
                    "Facet 阶段 12 运行时诊断快照已接入。",
                    new Dictionary<string, object?>
                    {
                        ["snapshotPath"] = FacetRuntimeDiagnosticsBridge.GetSnapshotPath(),
                        ["registeredPageCount"] = snapshot.RegisteredPageCount,
                        ["activeRuntimeCount"] = snapshot.ActiveRuntimeCount,
                        ["projectionCount"] = snapshot.ProjectionCount,
                        ["luaRegisteredScriptCount"] = snapshot.LuaRegisteredScriptCount,
                        ["redDotRegisteredPathCount"] = snapshot.RedDotRegisteredPathCount,
                    });
            }
            catch (Exception exception)
            {
                Logger.Error(
                    "Tooling.Diagnostics",
                    "Facet 阶段 12 运行时诊断快照验证失败。",
                    new Dictionary<string, object?>
                    {
                        ["exceptionType"] = exception.GetType().FullName,
                        ["message"] = exception.Message,
                    });
            }
        }

        private static UILayoutResult LoadVerificationLayout(
            UIPageLoader pageLoader,
            UIPageDefinition definition,
            Node mountRoot)
        {
            return pageLoader.Load(definition, mountRoot);
        }

        private static void VerifyRequiredNode(UILayoutResult layoutResult, string nodeKey)
        {
            layoutResult.NodeResolver.GetRequired<Control>(nodeKey);
        }

        private static void ReleaseVerificationLayout(UILayoutResult layoutResult)
        {
            if (layoutResult.OwnsRootNode && GodotObject.IsInstanceValid(layoutResult.RootNode))
            {
                layoutResult.RootNode.QueueFree();
            }
        }

        private static FacetLogger CreateLogger(FacetConfig config)
        {
            return new FacetLogger(
                config.MinimumLogLevel,
                config.EnableStructuredLogging,
                config.StructuredLogPath,
                config.StructuredLogBufferCapacity,
                config.StructuredLogHistoryLimit,
                config.EnableConsoleMirrorLogging,
                config.ConsoleMirrorLogPath,
                config.ConsoleMirrorLogHistoryLimit);
        }
    }
}
