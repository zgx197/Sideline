#nullable enable

using System;
using System.Text;
using Godot;
using Sideline.Facet.Extensions.RedDot;
using Sideline.Facet.Projection.Diagnostics;
using Sideline.Facet.Runtime;
using Sideline.Facet.Runtime.Debug;

/// <summary>
/// Facet 运行时调试页。
/// 统一承载运行时诊断、运行指标、桥接状态、红点状态与常用调试动作。
/// </summary>
public partial class RuntimeDebugPanel : PanelContainer, IUIPageLifecycle
{
    private static readonly Vector2I RecommendedDebugWindowSize = new(1320, 920);
    private static readonly Vector2I MinimumDebugWindowSize = new(960, 720);
    private const string ResetScrollOffsetsMethodName = nameof(ResetScrollOffsets);

    private ScrollContainer _scrollContainer = null!;
    private Label _summaryLabel = null!;
    private Label _validationLabel = null!;
    private Label _activeRuntimesLabel = null!;
    private Label _registeredPagesLabel = null!;
    private Label _bridgeStatusLabel = null!;
    private Label _redDotStatusLabel = null!;
    private Label _metricsLabel = null!;
    private Label _logSummaryLabel = null!;
    private Label _actionStatusLabel = null!;
    private GridContainer _actionButtonsGrid = null!;
    private Button _backButton = null!;
    private Button _refreshButton = null!;
    private Button _currentPageHotReloadButton = null!;
    private Button _dungeonHotReloadButton = null!;
    private Button _openGeneratedLayoutLabButton = null!;
    private Button _openTemplateLayoutLabButton = null!;
    private Button _toggleIdleRedDotButton = null!;
    private Button _toggleDungeonRedDotButton = null!;
    private Button _clearRedDotButton = null!;

    private UIContext? _pageContext;
    private bool _nodesBound;
    private bool _signalsBound;
    private bool _windowPresentationCaptured;
    private Vector2I _capturedWindowSize;
    private Vector2I _capturedWindowPosition;
    private Vector2I _capturedWindowMinSize;
    private bool _capturedWindowBorderless;
    private bool _capturedWindowAlwaysOnTop;

    public override void _Ready()
    {
        Visible = false;
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized)
        {
            UpdateResponsiveLayout();
            QueueResetHorizontalScroll();
        }
    }

    public void OnPageInitialize(UIContext context)
    {
        _pageContext = context;
        EnsureNodesResolved();
        EnsureSignalsBound();
        EnsureDebugWindowPresentation();
        UpdateResponsiveLayout();
        RefreshDebugView("page.initialize");
        QueueResetScrollOffsets();
    }

    public void OnPageShow(UIContext context)
    {
        _pageContext = context;
        EnsureNodesResolved();
        EnsureSignalsBound();
        EnsureDebugWindowPresentation();
        UpdateResponsiveLayout();
        RefreshDebugView("page.show");
        QueueResetScrollOffsets();
    }

    public void OnPageRefresh(UIContext context)
    {
        _pageContext = context;
        EnsureNodesResolved();
        EnsureSignalsBound();
        UpdateResponsiveLayout();
        RefreshDebugView("page.refresh");
        QueueResetHorizontalScroll();
    }

    public void OnPageHide(UIContext context)
    {
        RestoreWindowPresentation();
    }

    public void OnPageDispose(UIContext context)
    {
        RestoreWindowPresentation();
        _pageContext = null;
    }

    private void EnsureNodesResolved()
    {
        if (_nodesBound)
        {
            return;
        }

        _scrollContainer = GetNode<ScrollContainer>("ScrollContainer");
        _summaryLabel = GetNode<Label>("%SummaryLabel");
        _validationLabel = GetNode<Label>("%ValidationLabel");
        _activeRuntimesLabel = GetNode<Label>("%ActiveRuntimesLabel");
        _registeredPagesLabel = GetNode<Label>("%RegisteredPagesLabel");
        _bridgeStatusLabel = GetNode<Label>("%BridgeStatusLabel");
        _redDotStatusLabel = GetNode<Label>("%RedDotStatusLabel");
        _metricsLabel = GetNode<Label>("%MetricsLabel");
        _logSummaryLabel = GetNode<Label>("%LogSummaryLabel");
        _actionStatusLabel = GetNode<Label>("%ActionStatusLabel");
        _actionButtonsGrid = GetNode<GridContainer>("%ActionButtonsGrid");
        _backButton = GetNode<Button>("%BackButton");
        _refreshButton = GetNode<Button>("%RefreshButton");
        _currentPageHotReloadButton = GetNode<Button>("%CurrentPageHotReloadButton");
        _dungeonHotReloadButton = GetNode<Button>("%DungeonHotReloadButton");
        _openGeneratedLayoutLabButton = GetNode<Button>("%OpenGeneratedLayoutLabButton");
        _openTemplateLayoutLabButton = GetNode<Button>("%OpenTemplateLayoutLabButton");
        _toggleIdleRedDotButton = GetNode<Button>("%ToggleIdleRedDotButton");
        _toggleDungeonRedDotButton = GetNode<Button>("%ToggleDungeonRedDotButton");
        _clearRedDotButton = GetNode<Button>("%ClearRedDotButton");

        _scrollContainer.FollowFocus = false;
        ConfigureTextLabel(_summaryLabel);
        ConfigureTextLabel(_validationLabel);
        ConfigureTextLabel(_activeRuntimesLabel);
        ConfigureTextLabel(_registeredPagesLabel);
        ConfigureTextLabel(_bridgeStatusLabel);
        ConfigureTextLabel(_redDotStatusLabel);
        ConfigureTextLabel(_metricsLabel);
        ConfigureTextLabel(_logSummaryLabel);
        ConfigureTextLabel(_actionStatusLabel);
        _nodesBound = true;
    }

    private void EnsureSignalsBound()
    {
        if (_signalsBound)
        {
            return;
        }

        ConnectPressed(_backButton, OnBackPressed);
        ConnectPressed(_refreshButton, OnRefreshPressed);
        ConnectPressed(_currentPageHotReloadButton, OnCurrentPageHotReloadPressed);
        ConnectPressed(_dungeonHotReloadButton, OnDungeonHotReloadPressed);
        ConnectPressed(_openGeneratedLayoutLabButton, OnOpenGeneratedLayoutLabPressed);
        ConnectPressed(_openTemplateLayoutLabButton, OnOpenTemplateLayoutLabPressed);
        ConnectPressed(_toggleIdleRedDotButton, OnToggleIdleRedDotPressed);
        ConnectPressed(_toggleDungeonRedDotButton, OnToggleDungeonRedDotPressed);
        ConnectPressed(_clearRedDotButton, OnClearRedDotsPressed);
        _signalsBound = true;
    }

    private static void ConnectPressed(Button button, Action handler)
    {
        Callable callable = Callable.From(handler);
        if (!button.IsConnected(Button.SignalName.Pressed, callable))
        {
            button.Connect(Button.SignalName.Pressed, callable);
        }
    }

    private void RefreshDebugView(string reason)
    {
        FacetRuntimeDebugSnapshot snapshot = FacetRuntimeDebugService.Capture(_pageContext?.Services);
        _summaryLabel.Text = FormatSummary(snapshot.Diagnostics, TryGetArgument("sourcePageId"));
        _validationLabel.Text = FormatValidation(snapshot.Diagnostics);
        _activeRuntimesLabel.Text = FormatActiveRuntimes(snapshot.Diagnostics);
        _registeredPagesLabel.Text = FormatRegisteredPages(snapshot.Diagnostics);
        _bridgeStatusLabel.Text = FormatBridgeStatus(snapshot);
        _redDotStatusLabel.Text = FormatRedDotStatus(_pageContext?.Services);
        _metricsLabel.Text = FormatMetrics(snapshot.Metrics);
        _logSummaryLabel.Text = FormatRecentLogs(snapshot);

        if (string.IsNullOrWhiteSpace(_actionStatusLabel.Text))
        {
            _actionStatusLabel.Text = $"最近刷新: {DateTime.Now:HH:mm:ss} ({reason})";
        }
    }

    private void UpdateResponsiveLayout()
    {
        if (!_nodesBound || !GodotObject.IsInstanceValid(_actionButtonsGrid))
        {
            return;
        }

        float contentWidth = Size.X;
        _actionButtonsGrid.Columns = contentWidth switch
        {
            <= 720f => 1,
            <= 1120f => 2,
            _ => 3,
        };
    }

    private void QueueResetScrollOffsets()
    {
        CallDeferred(ResetScrollOffsetsMethodName, true);
    }

    private void QueueResetHorizontalScroll()
    {
        CallDeferred(ResetScrollOffsetsMethodName, false);
    }

    private void ResetScrollOffsets(bool resetVertical)
    {
        if (!_nodesBound || !GodotObject.IsInstanceValid(_scrollContainer))
        {
            return;
        }

        _scrollContainer.ScrollHorizontal = 0;
        if (resetVertical)
        {
            _scrollContainer.ScrollVertical = 0;
        }
    }

    private static void ConfigureTextLabel(Label label)
    {
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        label.ClipText = false;
    }

    private void EnsureDebugWindowPresentation()
    {
        Window window = GetWindow();
        if (!GodotObject.IsInstanceValid(window))
        {
            return;
        }

        if (!_windowPresentationCaptured)
        {
            _capturedWindowSize = window.Size;
            _capturedWindowPosition = window.Position;
            _capturedWindowMinSize = window.MinSize;
            _capturedWindowBorderless = window.Borderless;
            _capturedWindowAlwaysOnTop = window.AlwaysOnTop;
            _windowPresentationCaptured = true;
        }

        Vector2I screenSize = DisplayServer.ScreenGetSize();
        Vector2I targetMinSize = GetClampedWindowSize(screenSize, MinimumDebugWindowSize);
        Vector2I targetSize = GetClampedWindowSize(screenSize, RecommendedDebugWindowSize);
        targetSize = new Vector2I(
            Mathf.Max(targetSize.X, _capturedWindowSize.X),
            Mathf.Max(targetSize.Y, _capturedWindowSize.Y));
        targetSize = GetClampedWindowSize(screenSize, targetSize);

        window.MinSize = targetMinSize;
        if (window.Size.X < targetSize.X || window.Size.Y < targetSize.Y)
        {
            window.Size = targetSize;
            window.Position = new Vector2I(
                Mathf.Max(0, (screenSize.X - targetSize.X) / 2),
                Mathf.Max(0, (screenSize.Y - targetSize.Y) / 2));
        }
    }

    private void RestoreWindowPresentation()
    {
        if (!_windowPresentationCaptured)
        {
            return;
        }

        Window window = GetWindow();
        if (GodotObject.IsInstanceValid(window))
        {
            window.MinSize = _capturedWindowMinSize;

            bool samePresentationMode =
                window.Borderless == _capturedWindowBorderless &&
                window.AlwaysOnTop == _capturedWindowAlwaysOnTop;

            if (samePresentationMode)
            {
                window.Size = _capturedWindowSize;
                window.Position = _capturedWindowPosition;
            }
        }

        _windowPresentationCaptured = false;
        _capturedWindowSize = Vector2I.Zero;
        _capturedWindowPosition = Vector2I.Zero;
        _capturedWindowMinSize = Vector2I.Zero;
        _capturedWindowBorderless = false;
        _capturedWindowAlwaysOnTop = false;
    }

    private static Vector2I GetClampedWindowSize(Vector2I screenSize, Vector2I requestedSize)
    {
        int maxWidth = Mathf.Max(640, screenSize.X - 96);
        int maxHeight = Mathf.Max(480, screenSize.Y - 96);
        int minWidth = Mathf.Min(MinimumDebugWindowSize.X, maxWidth);
        int minHeight = Mathf.Min(MinimumDebugWindowSize.Y, maxHeight);

        return new Vector2I(
            Mathf.Clamp(requestedSize.X, minWidth, maxWidth),
            Mathf.Clamp(requestedSize.Y, minHeight, maxHeight));
    }

    private static string FormatSummary(FacetRuntimeDiagnosticsSnapshot? diagnostics, string? sourcePageId)
    {
        if (diagnostics == null)
        {
            return "运行时摘要\n当前未读取到 runtime diagnostics 快照。";
        }

        return
            "运行时摘要\n" +
            $"Session: {Shorten(diagnostics.RuntimeSessionId)}\n" +
            $"来源页面: {SafeValue(sourcePageId)}\n" +
            $"当前页面: {SafeValue(diagnostics.CurrentPageId)}\n" +
            $"返回栈深度: {diagnostics.BackStackDepth}\n" +
            $"活动运行时: {diagnostics.ActiveRuntimeCount}\n" +
            $"Projection 数量: {diagnostics.ProjectionCount}\n" +
            $"Lua 脚本数: {diagnostics.LuaRegisteredScriptCount}\n" +
            $"红点路径数: {diagnostics.RedDotRegisteredPathCount}\n" +
            $"快照时间: {SafeValue(diagnostics.UpdatedAtUtc)}";
    }

    private static string FormatValidation(FacetRuntimeDiagnosticsSnapshot? diagnostics)
    {
        if (diagnostics == null)
        {
            return "运行时校验\n当前未读取到校验结果。";
        }

        StringBuilder builder = new();
        builder.AppendLine("运行时校验");
        builder.AppendLine($"总计: {diagnostics.ValidationResultCount}");
        builder.AppendLine(
            $"Pass: {diagnostics.ValidationPassedCount}  Warning: {diagnostics.ValidationWarningCount}  Fail: {diagnostics.ValidationFailedCount}");

        int shownCount = 0;
        foreach (FacetRuntimeValidationResultSnapshot result in diagnostics.ValidationResults)
        {
            if (!string.Equals(result.Status, "Warning", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(result.Status, "Fail", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            builder.AppendLine($"- [{SafeValue(result.Status)}] {SafeValue(result.Subject)}: {SafeValue(result.Message)}");
            shownCount++;
            if (shownCount >= 4)
            {
                break;
            }
        }

        if (shownCount == 0)
        {
            builder.Append("- 当前没有 Warning / Fail。");
        }

        return builder.ToString().TrimEnd();
    }

    private static string FormatActiveRuntimes(FacetRuntimeDiagnosticsSnapshot? diagnostics)
    {
        if (diagnostics == null)
        {
            return "活动运行时\n当前没有可展示的数据。";
        }

        StringBuilder builder = new();
        builder.AppendLine("活动运行时");

        if (diagnostics.ActiveRuntimes.Count == 0)
        {
            builder.Append("当前没有活动运行时。");
            return builder.ToString();
        }

        foreach (FacetRuntimePageRuntimeSnapshot runtime in diagnostics.ActiveRuntimes)
        {
            builder.Append("- ");
            if (runtime.IsCurrentPage)
            {
                builder.Append("[Current] ");
            }

            builder.Append(runtime.PageId);
            builder.Append(" / ");
            builder.Append(SafeValue(runtime.State));
            builder.Append(" / Lua=");
            builder.Append(runtime.HasLuaController ? "On" : "Off");

            if (!string.IsNullOrWhiteSpace(runtime.BindingScope?.ScopeId))
            {
                builder.Append(" / Bindings=");
                builder.Append(runtime.BindingScope.BindingCount);
            }

            builder.AppendLine();
        }

        return builder.ToString().TrimEnd();
    }

    private static string FormatRegisteredPages(FacetRuntimeDiagnosticsSnapshot? diagnostics)
    {
        if (diagnostics == null)
        {
            return "页面注册表\n当前没有可展示的数据。";
        }

        StringBuilder builder = new();
        builder.AppendLine($"页面注册表 ({diagnostics.RegisteredPageCount})");

        foreach (FacetRuntimeRegisteredPageSnapshot page in diagnostics.RegisteredPages)
        {
            builder.AppendLine(
                $"- {SafeValue(page.PageId)} / {SafeValue(page.LayoutType)} / {SafeValue(page.CachePolicy)}");
        }

        return builder.ToString().TrimEnd();
    }

    private static string FormatMetrics(FacetRuntimeMetricListProjection? metrics)
    {
        if (metrics == null)
        {
            return "运行时指标\n当前未读取到 RuntimeMetrics Projection。";
        }

        StringBuilder builder = new();
        builder.AppendLine(SafeValue(metrics.Title, "运行时指标"));

        if (metrics.Items.Count == 0)
        {
            builder.Append("- 当前没有指标项。");
            return builder.ToString();
        }

        foreach (FacetRuntimeMetricItem item in metrics.Items)
        {
            builder.AppendLine(
                $"- {SafeValue(item.Label)}: {SafeValue(item.Value)} ({SafeValue(item.Key)})");
        }

        builder.Append($"Updated: {metrics.UpdatedAtUtc:O}");
        return builder.ToString();
    }

    private static string FormatBridgeStatus(FacetRuntimeDebugSnapshot snapshot)
    {
        StringBuilder builder = new();
        builder.AppendLine("桥接状态");
        builder.AppendLine(
            FormatBridgeLine(
                "Hot Reload",
                snapshot.HotReloadStatus?.State,
                snapshot.HotReloadStatus?.Message,
                snapshot.HotReloadStatus?.RuntimePageId));
        builder.Append(
            FormatBridgeLine(
                "Layout Lab",
                snapshot.LayoutLabStatus?.State,
                snapshot.LayoutLabStatus?.Message,
                snapshot.LayoutLabStatus?.RuntimePageId));
        return builder.ToString();
    }

    private static string FormatBridgeLine(string name, string? state, string? message, string? runtimePageId)
    {
        string normalizedMessage = NormalizeBridgeMessage(name, state, message);
        return
            $"{name}: {SafeValue(state, "unknown")} / Page={SafeValue(runtimePageId, "-")} / {normalizedMessage}";
    }

    private static string NormalizeBridgeMessage(string name, string? state, string? message)
    {
        if (!LooksLikeMojibake(message))
        {
            return SafeValue(message, "暂无状态");
        }

        if (string.Equals(name, "Layout Lab", StringComparison.Ordinal))
        {
            return state?.ToLowerInvariant() switch
            {
                "idle" => "Layout Lab 当前空闲，等待新的打开请求。",
                "requested" => "Layout Lab 请求已发出，等待运行时处理。",
                "running" => "Layout Lab 请求处理中，正在切换目标页面。",
                "completed" => "Layout Lab 请求已完成。",
                "failed" => "Layout Lab 请求执行失败，请查看运行日志。",
                "ignored" => "Layout Lab 请求未被处理，请检查命令和运行时状态。",
                _ => "Layout Lab 状态暂不可用。",
            };
        }

        if (string.Equals(name, "Hot Reload", StringComparison.Ordinal))
        {
            return "热重载状态文本已损坏，请查看运行日志。";
        }

        return "状态文本已损坏，请查看运行日志。";
    }

    private static bool LooksLikeMojibake(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return value.Contains('闂') ||
               value.Contains('妗') ||
               value.Contains('鏆') ||
               value.Contains('杩') ||
               value.Contains('閻') ||
               value.Contains('鍩') ||
               value.Contains('缁') ||
               value.Contains('鈧');
    }

    private static string FormatRedDotStatus(FacetServices? services)
    {
        return
            "运行时红点\n" +
            $"Idle Page: {FormatRedDotSnapshot(services, "client.idle")}\n" +
            $"Dungeon Page: {FormatRedDotSnapshot(services, "client.dungeon")}\n" +
            $"Idle Manual: {(FacetRuntimeDebugService.GetManualRedDotState(services, FacetRuntimeDebugPaths.IdleManualRedDotPath) ? "On" : "Off")}\n" +
            $"Dungeon Manual: {(FacetRuntimeDebugService.GetManualRedDotState(services, FacetRuntimeDebugPaths.DungeonManualRedDotPath) ? "On" : "Off")}";
    }

    private static string FormatRedDotSnapshot(FacetServices? services, string path)
    {
        if (!FacetRuntimeDebugService.TryGetRedDotSnapshot(services, path, out RedDotNodeSnapshot snapshot))
        {
            return "未注册";
        }

        return
            $"{(snapshot.HasRedDot ? "On" : "Off")} " +
            $"(self={(snapshot.SelfHasRedDot ? "On" : "Off")}, children={snapshot.ActiveChildCount}/{snapshot.DirectChildCount}, sources={snapshot.SourceCount})";
    }

    private static string FormatRecentLogs(FacetRuntimeDebugSnapshot snapshot)
    {
        if (snapshot.RecentLogs.Count == 0)
        {
            return "最近日志\n当前未读取到结构化日志。";
        }

        StringBuilder builder = new();
        builder.AppendLine("最近日志");
        foreach (FacetRuntimeDebugLogEntry log in snapshot.RecentLogs)
        {
            builder.AppendLine($"- {ShortTimestamp(log.TimestampUtc)} [{SafeValue(log.Level)}] {SafeValue(log.Category)}");
            builder.AppendLine($"  {SafeValue(log.Message)}");
        }

        return builder.ToString().TrimEnd();
    }

    private void OnBackPressed()
    {
        bool succeeded = FacetRuntimeDebugService.TryGoBack(GetNavigator(), out string statusMessage);
        _actionStatusLabel.Text = statusMessage;
        if (!succeeded)
        {
            RefreshDebugView("action.back.failed");
        }
    }

    private void OnRefreshPressed()
    {
        _actionStatusLabel.Text = $"最近刷新: {DateTime.Now:HH:mm:ss}";
        RefreshDebugView("action.refresh");
    }

    private void OnCurrentPageHotReloadPressed()
    {
        FacetRuntimeDebugService.TryRunCurrentPageHotReloadTest(
            _pageContext?.Services,
            "runtime.debug.panel.current",
            out string statusMessage);
        _actionStatusLabel.Text = statusMessage;
        RefreshDebugView("action.hot_reload.current");
    }

    private void OnDungeonHotReloadPressed()
    {
        FacetRuntimeDebugService.TryRunDungeonHotReloadTest(
            _pageContext?.Services,
            "runtime.debug.panel.dungeon",
            out string statusMessage);
        _actionStatusLabel.Text = statusMessage;
        RefreshDebugView("action.hot_reload.dungeon");
    }

    private void OnOpenGeneratedLayoutLabPressed()
    {
        FacetRuntimeDebugService.TryOpenGeneratedLayoutLab(GetNavigator(), out string statusMessage);
        _actionStatusLabel.Text = statusMessage;
    }

    private void OnOpenTemplateLayoutLabPressed()
    {
        FacetRuntimeDebugService.TryOpenTemplateLayoutLab(GetNavigator(), out string statusMessage);
        _actionStatusLabel.Text = statusMessage;
    }

    private void OnToggleIdleRedDotPressed()
    {
        FacetRuntimeDebugService.TryToggleManualRedDot(
            _pageContext?.Services,
            FacetRuntimeDebugPaths.IdleManualRedDotPath,
            "Idle Manual",
            out string statusMessage);
        _actionStatusLabel.Text = statusMessage;
        RefreshDebugView("action.red_dot.idle");
    }

    private void OnToggleDungeonRedDotPressed()
    {
        FacetRuntimeDebugService.TryToggleManualRedDot(
            _pageContext?.Services,
            FacetRuntimeDebugPaths.DungeonManualRedDotPath,
            "Dungeon Manual",
            out string statusMessage);
        _actionStatusLabel.Text = statusMessage;
        RefreshDebugView("action.red_dot.dungeon");
    }

    private void OnClearRedDotsPressed()
    {
        FacetRuntimeDebugService.TryClearManualRedDots(_pageContext?.Services, out string statusMessage);
        _actionStatusLabel.Text = statusMessage;
        RefreshDebugView("action.red_dot.clear");
    }

    private IUIPageNavigator? GetNavigator()
    {
        return _pageContext?.Services.TryGet(out IUIPageNavigator? navigator) == true
            ? navigator
            : null;
    }

    private string? TryGetArgument(string key)
    {
        if (_pageContext?.Arguments.TryGetValue(key, out object? value) == true)
        {
            return value?.ToString();
        }

        return null;
    }

    private static string SafeValue(string? value, string fallback = "未知")
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    private static string Shorten(string value)
    {
        return string.IsNullOrWhiteSpace(value) || value.Length <= 8
            ? SafeValue(value)
            : value[..8];
    }

    private static string ShortTimestamp(string timestampUtc)
    {
        return DateTimeOffset.TryParse(timestampUtc, out DateTimeOffset timestamp)
            ? timestamp.ToLocalTime().ToString("HH:mm:ss")
            : SafeValue(timestampUtc);
    }
}
