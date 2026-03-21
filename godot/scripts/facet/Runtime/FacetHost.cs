#nullable enable

using System;
using System.Collections.Generic;
using Godot;
using Sideline.Facet.Application;
using Sideline.Facet.Application.Diagnostics;
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

            RegisterCoreServices();

            IsInitialized = true;
            SetProcess(true);
            SetProcessUnhandledInput(EnableLuaHotReloadTestShortcut);
            LogStartupSummary();
            PublishHotReloadLabIdleStatus();
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
            SceneLayoutProvider sceneLayoutProvider = new(Logger);
            IUILayoutProvider[] layoutProviders =
            {
                sceneLayoutProvider,
            };
            UIPageLoader pageLoader = new(layoutProviders, Logger);
            UIBindingService bindingService = new(Logger);
            UIRouteService routeService = new();
            UIManager uiManager = new(pageRegistry, pageLoader, routeService, Context, Logger);
            string projectRootPath = ProjectSettings.GlobalizePath("res://");
            ILuaScriptSource luaScriptSource = new FileSystemLuaScriptSource(projectRootPath, FacetLuaScriptIds.All);
            ILuaRedDotBridge luaRedDotBridge = new NullLuaRedDotBridge();
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
            Services.RegisterSingleton(sceneLayoutProvider);
            Services.RegisterSingleton(pageLoader);
            Services.RegisterSingleton(bindingService);
            Services.RegisterSingleton(routeService);
            Services.RegisterSingleton<IUIPageNavigator>(uiManager);
            Services.RegisterSingleton(uiManager);
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
                    ["layoutProvider"] = nameof(SceneLayoutProvider),
                    ["bindingService"] = nameof(UIBindingService),
                    ["routeService"] = nameof(UIRouteService),
                    ["backStackDepth"] = Services.GetRequired<UIRouteService>().Count,
                });

            VerifyApplicationBoundary();
            VerifyProjectionLayer();
            VerifyPageDefinitions();
            VerifyLuaRuntime();
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
