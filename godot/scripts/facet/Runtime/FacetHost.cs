#nullable enable

using System;
using System.Collections.Generic;
using Godot;
using Sideline.Facet.Application;
using Sideline.Facet.Application.Diagnostics;
using Sideline.Facet.Projection;
using Sideline.Facet.Projection.Diagnostics;

namespace Sideline.Facet.Runtime
{
    /// <summary>
    /// Facet 全局宿主入口。
    /// </summary>
    public partial class FacetHost : Node
    {
        private static readonly ProjectionKey RuntimeProbeProjectionKey = FacetProjectionKeys.RuntimeProbe;
        private static readonly ProjectionKey RuntimeMetricsProjectionKey = FacetProjectionKeys.RuntimeMetrics;

        /// <summary>
        /// 最小启动验证日志标记。
        /// 启动主场景后可直接搜索该文本，确认 Facet 宿主已接入。
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

        /// <summary>
        /// 初始化 Facet 宿主。
        /// </summary>
        public void Initialize()
        {
            if (IsInitialized)
            {
                Logger.Warning("Host", "FacetHost 重复初始化请求已忽略。");
                return;
            }

            Config = BuildConfig();
            Services = new FacetServices();
            Logger = CreateLogger(Config);
            Context = new FacetRuntimeContext(Config, Services, Logger);

            RegisterCoreServices();

            IsInitialized = true;
            LogStartupSummary();
            EmitSignal(SignalName.Initialized);
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
                });

            VerifyApplicationBoundary();
            VerifyProjectionLayer();
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
